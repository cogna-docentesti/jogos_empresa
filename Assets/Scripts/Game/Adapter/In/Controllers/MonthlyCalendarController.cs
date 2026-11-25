using System;
using System.Collections;
using System.Linq;
using Game.Adapter.In.UI;
using Game.Adapter.In.UI.Navigation;
using UnityEngine;

namespace Game.Adapter.In.Controllers
{
    [RequireComponent(typeof(MonthlyCalendarView))]
    public sealed class MonthlyCalendarController : MonoBehaviour
    {
        private const float StepSeconds = .085f;
        private MonthlyCalendarView view;
        private MonthlySettlementController settlement;
        private EventScreenController events;
        private MonthlyCalendarService calendar;
        private MonthlyCalendarService.View plan;
        private EventData[] catalog;
        private string sessionId;
        private int month;
        private bool busy, reloadRequired, opening;

        private void Awake()
        {
            view = GetComponent<MonthlyCalendarView>();
            view.Bind(Advance, ReturnToHub);
        }

        public void Open(MonthlySettlementController owner, EventScreenController eventScreen)
        {
            settlement = owner;
            events = eventScreen;
            UIStateListener.PresentScreen(gameObject);
            Load();
        }

        private void Load()
        {
            try
            {
                busy = reloadRequired = false;
                var session = GameSessionState.Current;
                sessionId = session.sessionId;
                month = session.currentRound;
                catalog ??= Resources.LoadAll<EventData>("Events");
                calendar = new MonthlyCalendarService(DatabaseInitializer.DatabaseService?.Connection);
                plan = calendar.Read(sessionId, month);
                if (plan == null)
                {
                    view.RenderPreparation(session);
                    view.ShowReview(session, ConfirmOpening, CancelReview);
                    return;
                }
                view.Render(plan, catalog);
                if (plan.Moving) StartCoroutine(Move());
                else if (plan.Pending != null) PresentDecision();
                else
                {
                    UpdateControls();
                    view.FocusAdvance();
                }
            }
            catch (Exception error) { Unavailable(error); }
        }

        private bool IsCurrentMonth => GameSessionState.Current is { } session
            && session.sessionId == sessionId && session.currentRound == month;

        private void ConfirmOpening()
        {
            if (!view.Reviewing || opening || !IsCurrentMonth
                || !GameSessionService.HasMinimumTeam(GameSessionState.Current)) return;

            opening = true;
            try
            {
                calendar.Open(sessionId, month, catalog);
                view.CloseModal();
                if (GameManager.Instance.StateMachine.CurrentState == GameState.Round_Event) Load();
                else settlement.EnterCalendar();
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                view.ReviewError("Não foi possível abrir o mês. Tente novamente.");
            }
            finally { opening = false; }
        }

        private void CancelReview()
        {
            if (!view.Reviewing || opening) return;
            view.CloseModal();
            ReturnToHub();
        }

        private bool CanAct => isActiveAndEnabled && !busy && SharedModalFocus.Active == null
            && (GameManager.Instance?.StateMachine?.CurrentState == GameState.Round_Event || reloadRequired)
            && IsCurrentMonth;

        private void Advance()
        {
            if (!CanAct) return;
            if (reloadRequired) { Load(); return; }
            if (plan == null || plan.Pending != null || plan.Moving) return;
            if (plan.ReadyToClose)
            {
                busy = true;
                UpdateControls();
                if (!settlement.CompleteCalendar(sessionId, month))
                {
                    busy = false;
                    view.Status("Não foi possível fechar o mês. Tente novamente.");
                    UpdateControls();
                }
                return;
            }

            try
            {
                plan = calendar.BeginAdvance(sessionId, month, plan.Revision);
                if (plan.Moving) StartCoroutine(Move());
                else Load();
            }
            catch (Exception error) { Unavailable(error); }
        }

        private IEnumerator Move()
        {
            busy = true;
            UpdateControls();
            view.Status("Avançando pelo calendário…");
            view.ShowPosition(plan.Day);
            int revision = plan.Revision;
            int destination = plan.DestinationDay;
            for (int day = plan.Day + 1; day <= destination; day++)
            {
                float remaining = StepSeconds;
                while (remaining > 0 || SharedModalFocus.Active != null)
                {
                    yield return null;
                    if (SharedModalFocus.Active == null) remaining -= Time.unscaledDeltaTime;
                }
                try { plan = calendar.Step(sessionId, month, revision, day); }
                catch (Exception error) { Unavailable(error); yield break; }
                view.ShowPosition(day);
                if (!plan.Moving || plan.Revision != revision) break;
            }

            view.Render(plan, catalog);
            if (plan.Pending != null)
            {
                float remaining = .30f;
                while (remaining > 0 || SharedModalFocus.Active != null)
                {
                    yield return null;
                    if (SharedModalFocus.Active == null) remaining -= Time.unscaledDeltaTime;
                }
                PresentDecision();
                yield break;
            }
            busy = false;
            UpdateControls();
            view.FocusAdvance();
        }

        private void PresentDecision()
        {
            busy = true;
            UpdateControls();
            try
            {
                var occurrence = plan.Pending;
                var data = catalog.Single(e => e.id == occurrence.EventId);
                var modal = view.ShowEventModal(month, occurrence.Day, out var surface);
                events.OpenCalendarDecision(data, month, occurrence.Day, modal, surface, () =>
                {
                    view.CloseModal();
                    Load();
                });
            }
            catch (Exception error)
            {
                events.ReleasePresentation();
                view.CloseModal();
                Unavailable(error);
            }
        }

        private void ReturnToHub()
        {
            if (!isActiveAndEnabled || busy || SharedModalFocus.Active != null || !IsCurrentMonth
                || (plan != null && (plan.Moving || plan.Pending != null))) return;
            var state = GameManager.Instance.StateMachine;
            if (state.CurrentState == GameState.Management_Hub)
                MenuNavigator.Instance.OpenRoot();
            else state.TryChangeState(GameState.Management_Hub);
        }

        private void UpdateControls()
        {
            bool available = !busy && (plan == null || (!plan.Moving && plan.Pending == null));
            view.SetControls(!busy && (reloadRequired || available && plan != null), available,
                reloadRequired ? "Tentar novamente" : plan?.ReadyToClose == true ? "Fechar mês" : "Avançar");
        }

        private void Unavailable(Exception error)
        {
            Debug.LogException(error);
            busy = false;
            reloadRequired = true;
            view.Status("Não foi possível atualizar o calendário. Tente novamente.");
            UpdateControls();
        }
    }
}
