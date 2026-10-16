using System;
using System.Collections.Generic;
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
        private int month, day, selected = -1, index;
        private bool confirmed, recording, historyVisible, failed, monthFlow;
        private Action completed;
        private readonly List<MonthlyEventDecisionService.Entry> entries = new();
        private EventData[] catalog;
        public bool IsAwaitingChoice { get; private set; }
        public EventData CurrentEvent => current;
        public int SelectedOption => selected;
        public event Action<SessionEventHistoryEntity> ChoiceConfirmed;
        public event Action Closed;
        private MonthlyEventDecisionService Service => new(DatabaseInitializer.DatabaseService?.Connection);
        private SessionEventHistoryRepository History => new(DatabaseInitializer.DatabaseService.Connection);
        private void Awake() { view ??= GetComponent<EventScreenView>(); Bind(); }
        private void Bind() => view?.BindActions(Confirm, ShowHistory, Return);
        private void OnEnable()
        {
            view ??= GetComponent<EventScreenView>(); Bind();
            if (MenuNavigator.HubAvailable && !IsAwaitingChoice) ShowHistory();
        }
        public void OpenMonth(Action onCompleted)
        {
            if (IsAwaitingChoice && !failed) { gameObject.SetActive(true); Render(); return; }
            completed = onCompleted; monthFlow = true; failed = false;
            try
            {
                var session = GameSessionState.Current;
                if (session == null || !GameSessionState.IsPersisted || view == null || !view.Ready)
                    throw new InvalidOperationException("Session/presentation unavailable.");
                sessionId = session.sessionId; month = session.currentRound;
                catalog = Resources.LoadAll<EventData>("Events");
                var context = MonthlySimulationCatalog.Load(session);
                var plan = Service.OpenMonth(session, catalog, context.Restaurant, context.Equipment, context.Settings.operatingDays);
                var history = History.GetBySessionAndRound(sessionId, month);
                entries.Clear(); entries.AddRange(plan.Where(e => !history.Any(h => h.eventId == e.eventId && h.day == e.day)));
                index = 0;
                if (entries.Count == 0) { Finish(); return; }
                OpenNext();
            }
            catch (Exception)
            { failed = true; IsAwaitingChoice = true; gameObject.SetActive(true); view.Error("Não foi possível carregar os eventos. Tente novamente para continuar o fechamento.", true); }
        }
        private void OpenNext()
        {
            var entry = entries[index];
            Present(catalog.Single(e => e.id == entry.eventId), month, entry.day);
        }
        public void OpenDecision(EventData data, int eventMonth, int eventDay, Action onCompleted = null)
        {
            if (IsAwaitingChoice) return;
            monthFlow = false; entries.Clear(); index = 0; completed = onCompleted;
            sessionId = GameSessionState.Current?.sessionId;
            Present(data, eventMonth, eventDay);
        }
        private void Present(EventData data, int eventMonth, int eventDay)
        {
            current = data; month = eventMonth; day = eventDay; selected = -1; confirmed = recording = failed = historyVisible = false;
            IsAwaitingChoice = true; gameObject.SetActive(true);
            try
            {
                if (!view.Ready || current?.options == null || current.options.Length == 0 || view.Illustration(current.id) == null
                    || current.options.Any(o => o == null || o.effects == null) || !GameSessionState.IsPersisted || GameSessionState.Current?.sessionId != sessionId)
                    throw new InvalidOperationException();
                var existing = History.GetBySessionAndRound(sessionId, month).SingleOrDefault(h => h.eventId == current.id && h.day == day);
                if (existing != null) { selected = Array.FindIndex(current.options, o => o.id == existing.selectedChoiceId); confirmed = true; IsAwaitingChoice = false; }
                else if (!GameSessionState.HasActiveSession || GameSessionState.Current.currentRound != month)
                    throw new InvalidOperationException();
                Render();
            }
            catch (Exception)
            { failed = true; IsAwaitingChoice = monthFlow; view.Error("Evento ou sessão indisponível. Nenhuma escolha pode ser confirmada no momento.", monthFlow); }
        }
        private void Render()
        {
            historyVisible = false;
            view.Decision(current, month, day, index, entries.Count, selected, !confirmed && !failed,
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
                if (monthFlow && ++index < entries.Count) OpenNext();
                else Finish();
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
                if (failed)
                {
                    if (monthFlow) OpenMonth(completed);
                    else Present(current, month, day);
                }
                else Render();
                return;
            }
            if (MenuNavigator.HubAvailable) MenuNavigator.Instance.Back();
            else { gameObject.SetActive(false); Closed?.Invoke(); }
        }
        public bool Close()
        {
            if (IsAwaitingChoice) return false;
            Return(); return true;
        }
        private void Finish()
        {
            IsAwaitingChoice = false; gameObject.SetActive(false);
            var callback = completed; completed = null; Closed?.Invoke(); callback?.Invoke();
        }
        public void ShowSettlementFailure(Action retry)
        {
            IsAwaitingChoice = true; failed = true; monthFlow = true; completed = retry;
            gameObject.SetActive(true);
            view.Error("Não foi possível fechar o mês. As escolhas registradas foram preservadas. Tente novamente.", true);
        }
    }
}
