using System;
using UnityEngine;
using UnityEngine.Events;

namespace Game.Adapter.In.Controllers
{
    /// <summary>Optional bridge for a UI Button's OnClick event.</summary>
    public sealed class MonthlySettlementController : MonoBehaviour
    {
        [SerializeField] private UnityEvent onMonthSettled = new UnityEvent();
        [SerializeField] private UnityEvent onSettlementFailed = new UnityEvent();
        public RoundResultEntity LastResult { get; private set; }
        public event Action<RoundResultEntity> MonthSettled;
        private bool awaitingEvents;
        private bool resumePending;
        private GameStateMachine subscribedMachine;
        private void Start()
        {
            subscribedMachine = GameManager.Instance?.StateMachine;
            if (subscribedMachine == null) return;
            subscribedMachine.OnStateChanged += Resume;
            Resume(subscribedMachine.CurrentState);
        }
        private void OnDestroy() { if (subscribedMachine != null) subscribedMachine.OnStateChanged -= Resume; }
        private void Resume(GameState state)
        {
            if (state == GameState.MainMenu || state == GameState.Config_Identification)
            {
                awaitingEvents = false;
                foreach (var screen in FindObjectsByType<EventScreenController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    screen.ReleasePresentation();
            }
            resumePending = state == GameState.Round_Event && !awaitingEvents;
        }
        private void Update()
        {
            if (!resumePending) return;
            if (Game.Adapter.In.UI.SharedModalFocus.Active != null) return;
            resumePending = false;
            if (subscribedMachine?.CurrentState == GameState.Round_Event) CloseMonth();
        }

        public void CloseMonth()
        {
            if (awaitingEvents || !GameSessionState.HasActiveSession || !GameSessionState.IsPersisted) return;
            var state = GameManager.Instance?.StateMachine;
            if (Game.Adapter.In.UI.SharedModalFocus.Active != null || state == null || (state.CurrentState != GameState.Management_Hub
                && state.CurrentState != GameState.Round_Sales && state.CurrentState != GameState.Round_Event)) return;
            var events = FindObjectsByType<EventScreenController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (events.Length != 1) { onSettlementFailed.Invoke(); return; }
            awaitingEvents = true;
            var service = new GameSessionService(GameSessionState.Current.userId, GameSessionState.Current.professorId);
            if (state.CurrentState == GameState.Management_Hub)
                service.StartRound();
            if (!service.BeginEventDecisions()) { awaitingEvents = false; return; }
            events[0].OpenMonth(CompleteSettlement);
        }

        private void CompleteSettlement()
        {
            LastResult = SettleCurrentMonth();
            awaitingEvents = false;
            if (LastResult == null)
            {
                onSettlementFailed.Invoke();
                var events = FindObjectsByType<EventScreenController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                if (events.Length == 1) events[0].ShowSettlementFailure(CompleteSettlement);
                return;
            }
            MonthSettled?.Invoke(LastResult);
            onMonthSettled.Invoke();
        }

        public static RoundResultEntity SettleCurrentMonth()
        {
            if (!UnityEngine.Application.isPlaying || !GameSessionState.HasActiveSession
                || !GameSessionState.IsPersisted || GameManager.Instance?.StateMachine == null)
                return null;
            foreach (var screen in FindObjectsByType<EventScreenController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (screen.IsAwaitingChoice) return null;
            var session = GameSessionState.Current;
            var state = GameManager.Instance.StateMachine.CurrentState;
            if (state != GameState.Management_Hub && state != GameState.Round_Sales && state != GameState.Round_Event)
                return null;
            var service = new GameSessionService(session.userId, session.professorId);
            if (state == GameState.Management_Hub) service.StartRound();
            return service.ProcessCurrentRound();
        }
    }
}
