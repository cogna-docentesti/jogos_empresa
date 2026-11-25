using System;
using System.Collections;
using System.Linq;
using Game.Adapter.In.UI;
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
        private bool busy, reloadRequired;

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
                plan = calendar.Read(sessionId, month) ?? calendar.Open(sessionId, month, catalog);
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

        private bool CanAct => isActiveAndEnabled && !busy && SharedModalFocus.Active == null
            && GameManager.Instance?.StateMachine?.CurrentState == GameState.Round_Event
            && GameSessionState.Current is { } session && session.sessionId == sessionId && session.currentRound == month;

        private void Advance()
        {
            if (!CanAct) return;
            if (reloadRequired) { Load(); return; }
            if (plan == null || plan.Pending != null || plan.Moving) return;
            if (plan.ReadyToClose)
            {
                busy = true;
                UpdateControls();
                if (!settlement.CompleteCalendar())
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
                events.OpenDecision(data, month, occurrence.Day, () => Open(settlement, events), required: true);
            }
            catch (Exception error) { Unavailable(error); }
        }

        private void ReturnToHub()
        {
            if (CanAct && (plan == null || (!plan.Moving && plan.Pending == null)))
                GameManager.Instance.StateMachine.TryChangeState(GameState.Management_Hub);
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
