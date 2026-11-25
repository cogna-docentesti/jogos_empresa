using System;
using Game.Adapter.In.UI;
using UnityEngine;
using UnityEngine.Events;

namespace Game.Adapter.In.Controllers
{
    public sealed class MonthlySettlementController : MonoBehaviour
    {
        [SerializeField] private UnityEvent onMonthSettled = new UnityEvent();
        [SerializeField] private UnityEvent onSettlementFailed = new UnityEvent();
        [SerializeField] private MonthlyCalendarController calendar;
        public RoundResultEntity LastResult { get; private set; }
        public event Action<RoundResultEntity> MonthSettled;
        private bool settling, resumePending;
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
                LastResult = null;
                foreach (var screen in FindObjectsByType<EventScreenController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    screen.ReleasePresentation();
            }
            resumePending = state == GameState.Round_Event;
        }

        private void Update()
        {
            if (!resumePending || SharedModalFocus.Active != null) return;
            resumePending = false;
            OpenCalendar();
        }

        private void OpenCalendar()
        {
            var events = FindObjectsByType<EventScreenController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (calendar != null && events.Length == 1) calendar.Open(this, events[0]);
            else onSettlementFailed.Invoke();
        }

        // Preserve callers serialized before the hub button was renamed.
        public void CloseMonth() => ShowCalendar();

        public void ShowCalendar()
        {
            if (!GameSessionState.HasActiveSession || !GameSessionState.IsPersisted || SharedModalFocus.Active != null)
                return;
            var state = GameManager.Instance?.StateMachine;
            if (state?.CurrentState != GameState.Management_Hub && state?.CurrentState != GameState.Round_Sales)
                return;
            var session = GameSessionState.Current;
            if (!string.IsNullOrEmpty(session.pendingSummaryResultId)) return;
            LastResult = null;
            if (session.calendarMonthLocked == 0) OpenCalendar();
            else EnterCalendar();
        }

        internal void EnterCalendar()
        {
            var state = GameManager.Instance.StateMachine;
            var session = GameSessionState.Current;
            var service = new GameSessionService(session.userId, session.professorId);
            if (state.CurrentState == GameState.Management_Hub) service.StartRound();
            if (!service.BeginEventDecisions()) onSettlementFailed.Invoke();
        }

        internal bool CompleteCalendar(string sessionId, int month)
        {
            var session = GameSessionState.Current;
            if (settling || session == null || session.sessionId != sessionId || session.currentRound != month) return false;
            settling = true;
            try
            {
                LastResult = SettleCurrentMonth(month);
                if (LastResult == null) { onSettlementFailed.Invoke(); return false; }
                MonthSettled?.Invoke(LastResult);
                onMonthSettled.Invoke();
                return true;
            }
            finally { settling = false; }
        }

        internal bool CompleteSystemicCalendar(SystemicCalendarService service)
        {
            if (settling || GameManager.Instance?.StateMachine?.CurrentState != GameState.Round_Event) return false;
            settling = true;
            try
            {
                LastResult = service.Finish();
                if (LastResult == null || !GameManager.Instance.StateMachine.TryChangeState(GameState.Round_Summary)) return false;
                MonthSettled?.Invoke(LastResult); onMonthSettled.Invoke(); return true;
            }
            finally { settling = false; }
        }

        public static RoundResultEntity SettleCurrentMonth() =>
            SettleCurrentMonth(GameSessionState.Current?.currentRound ?? 0);

        private static RoundResultEntity SettleCurrentMonth(int month)
        {
            if (!UnityEngine.Application.isPlaying || !GameSessionState.HasSession
                || !GameSessionState.IsPersisted || GameManager.Instance?.StateMachine == null)
                return null;
            foreach (var screen in FindObjectsByType<EventScreenController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (screen.IsAwaitingChoice) return null;
            var session = GameSessionState.Current;
            var state = GameManager.Instance.StateMachine.CurrentState;
            if (state != GameState.Round_Event)
                return null;
            return new GameSessionService(session.userId, session.professorId).ProcessCurrentRound(month);
        }
    }
}
