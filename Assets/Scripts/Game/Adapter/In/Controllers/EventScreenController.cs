using System;
using System.Linq;
using Game.Adapter.In.UI;
using Game.Adapter.In.UI.Navigation;
using UnityEngine;

namespace Game.Adapter.In.Controllers
{
    /// <summary>Explicit open/wait/confirm/close contract for the monthly orchestrator.</summary>
    public sealed class EventScreenController : MonoBehaviour
    {
        [SerializeField] private EventScreenView view;
        private EventData current;
        private string sessionId;
        private int month, day, selected = -1;
        private bool confirmed, recording, historyVisible, failed, mandatory;
        private Action completed;
        public bool IsAwaitingChoice { get; private set; }
        public EventData CurrentEvent => current;
        public int SelectedOption => selected;
        public event Action<SessionEventHistoryEntity> ChoiceConfirmed;
        public event Action Closed;
        private MonthlyEventDecisionService Service => new(DatabaseInitializer.DatabaseService?.Connection);
        private SessionEventHistoryRepository History => new(DatabaseInitializer.DatabaseService.Connection);
        private void Awake() { view ??= GetComponent<EventScreenView>(); Bind(); }
        private void Bind() => view?.BindActions(Confirm, ShowHistory, Return);
        public void ReleasePresentation()
        {
            completed = null; IsAwaitingChoice = false; current = null; sessionId = null;
            mandatory = confirmed = recording = historyVisible = failed = false;
            selected = -1;
        }
        private void OnEnable()
        {
            view ??= GetComponent<EventScreenView>(); Bind();
            if (MenuNavigator.HubAvailable && !IsAwaitingChoice) ShowHistory();
        }
        public void OpenDecision(EventData data, int eventMonth, int eventDay, Action onCompleted = null, bool required = false)
        {
            if (IsAwaitingChoice)
            {
                if (mandatory && current == data && month == eventMonth && day == eventDay
                    && sessionId == GameSessionState.Current?.sessionId)
                {
                    UIStateListener.PresentScreen(gameObject);
                    if (!failed) Render();
                }
                return;
            }
            mandatory = required;
            completed = onCompleted;
            sessionId = GameSessionState.Current?.sessionId;
            Present(data, eventMonth, eventDay);
        }
        private void Present(EventData data, int eventMonth, int eventDay)
        {
            current = data; month = eventMonth; day = eventDay; selected = -1; confirmed = recording = failed = historyVisible = false;
            IsAwaitingChoice = true; UIStateListener.PresentScreen(gameObject);
            try
            {
                if (!view.Ready || current?.options == null || current.options.Length == 0 || view.Illustration(current.id) == null
                    || current.options.Any(o => o == null || o.effects == null) || !GameSessionState.IsPersisted || GameSessionState.Current?.sessionId != sessionId)
                    throw new InvalidOperationException();
                var existing = History.GetBySessionAndRound(sessionId, month).SingleOrDefault(h => h.eventId == current.id && h.day == day);
                if (existing != null) { selected = Array.FindIndex(current.options, o => o.id == existing.selectedChoiceId); confirmed = true; IsAwaitingChoice = false; }
                else if (!GameSessionState.HasActiveSession || GameSessionState.Current.currentRound != month)
                    throw new InvalidOperationException();
                if (confirmed && mandatory) { Finish(); return; }
                Render();
            }
            catch (Exception)
            { failed = true; IsAwaitingChoice = mandatory; view.Error("Evento ou sessão indisponível. Nenhuma escolha pode ser confirmada no momento.", mandatory); }
        }
        private void Render()
        {
            historyVisible = false;
            view.Decision(current, month, day, 0, 0, selected, !confirmed && !failed,
                confirmed ? "Esta escolha já foi registrada. Os efeitos pertencem ao fechamento mensal." : "Leia as opções e selecione uma para confirmar.", Select);
        }
        public void Select(int choice)
        {
            if (!IsAwaitingChoice || confirmed || failed || historyVisible || current?.options == null || choice < 0 || choice >= current.options.Length) return;
            selected = choice; view.SelectOption(selected);
        }
        public void Confirm()
        {
            if (!IsAwaitingChoice || confirmed || recording || failed || historyVisible || selected < 0 || current == null) return;
            recording = true;
            try
            {
                if (GameSessionState.Current?.sessionId != sessionId || !GameSessionState.IsPersisted) throw new InvalidOperationException();
                var record = Service.Confirm(sessionId, month, day, current, current.options[selected]);
                confirmed = true; view.ConfirmButton.interactable = false; ChoiceConfirmed?.Invoke(record);
                Finish();
            }
            catch (Exception)
            { failed = true; view.Error("Não foi possível registrar a escolha. Tente novamente; uma confirmação anterior será preservada.", true); }
            finally { recording = false; }
        }
        public void ShowHistory()
        {
            historyVisible = true;
            try
            {
                if (GameSessionState.Current == null || !GameSessionState.IsPersisted || DatabaseInitializer.DatabaseService?.Connection == null)
                    throw new InvalidOperationException();
                var session = GameSessionState.Current;
                view.History(History.GetBySession(session.sessionId), session.currentRound, IsAwaitingChoice);
            }
            catch (Exception) { view.Error("Histórico indisponível. Não há dados de sessão para consultar.", false); }
        }
        public void Return()
        {
            if (IsAwaitingChoice)
            {
                if (failed) Present(current, month, day);
                else Render();
                return;
            }
            if (MenuNavigator.HubAvailable) MenuNavigator.Instance.Back();
            else { UIStateListener.DismissScreen(gameObject); Closed?.Invoke(); }
        }
        public bool Close()
        {
            if (IsAwaitingChoice) return false;
            Return(); return true;
        }
        private void Finish()
        {
            IsAwaitingChoice = false; UIStateListener.DismissScreen(gameObject);
            var callback = completed; completed = null; Closed?.Invoke(); callback?.Invoke();
        }
    }
}
