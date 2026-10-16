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

        public void CloseMonth()
        {
            if (awaitingEvents || !GameSessionState.HasActiveSession || !GameSessionState.IsPersisted) return;
            var state = GameManager.Instance?.StateMachine;
            if (state == null || (state.CurrentState != GameState.Management_Hub && state.CurrentState != GameState.Round_Sales)) return;
            var events = FindObjectsByType<EventScreenController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (events.Length != 1) { onSettlementFailed.Invoke(); return; }
            awaitingEvents = true;
            if (state.CurrentState == GameState.Management_Hub)
                new GameSessionService(GameSessionState.Current.userId, GameSessionState.Current.professorId).StartRound();
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
            if (state != GameState.Management_Hub && state != GameState.Round_Sales)
                return null;
            var service = new GameSessionService(session.userId, session.professorId);
            if (state == GameState.Management_Hub) service.StartRound();
            return service.ProcessCurrentRound();
        }
    }
}
