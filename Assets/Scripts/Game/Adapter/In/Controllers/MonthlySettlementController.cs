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

        public void CloseMonth()
        {
            LastResult = SettleCurrentMonth();
            if (LastResult == null)
            {
                onSettlementFailed.Invoke();
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
