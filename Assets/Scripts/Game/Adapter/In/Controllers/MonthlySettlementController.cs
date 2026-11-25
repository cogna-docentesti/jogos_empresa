using System;
using Game.Adapter.In.UI;
using Game.Adapter.In.UI.Navigation;
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
            var events = FindObjectsByType<EventScreenController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (calendar != null && events.Length == 1) calendar.Open(this, events[0]);
            else onSettlementFailed.Invoke();
        }

        // The existing hub action now enters the calendar. Settlement is requested at day 30.
        public void CloseMonth()
        {
            if (!GameSessionState.HasActiveSession || !GameSessionState.IsPersisted || SharedModalFocus.Active != null)
                return;
            var state = GameManager.Instance?.StateMachine;
            if (state?.CurrentState != GameState.Management_Hub && state?.CurrentState != GameState.Round_Sales)
                return;
            var session = GameSessionState.Current;
            if (session.calendarMonthLocked == 0 && !GameSessionService.HasMinimumTeam(session))
            {
                if (state.CurrentState == GameState.Management_Hub)
                    MenuNavigator.Instance?.Open(PanelId.Team);
                FeedbackToast.Show("Complete os mínimos da equipe para iniciar o mês.", FeedbackKind.Warning);
                return;
            }
            LastResult = null;
            var service = new GameSessionService(session.userId, session.professorId);
            if (state.CurrentState == GameState.Management_Hub) service.StartRound();
            service.BeginEventDecisions();
        }

        internal bool CompleteCalendar()
        {
            if (settling) return false;
            settling = true;
            try
            {
                LastResult = SettleCurrentMonth();
                if (LastResult == null) { onSettlementFailed.Invoke(); return false; }
                MonthSettled?.Invoke(LastResult);
                onMonthSettled.Invoke();
                return true;
            }
            finally { settling = false; }
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
            if (state != GameState.Round_Sales && state != GameState.Round_Event)
                return null;
            return new GameSessionService(session.userId, session.professorId).ProcessCurrentRound();
        }
    }
}
