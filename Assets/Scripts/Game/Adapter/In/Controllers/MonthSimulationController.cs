// Assets/Scripts/Game/Adapter/In/Controllers/MonthSimulationController.cs
// Roda no Unity. Controla o tempo (II, 1x, 2x, 4x, Pular), chama o serviço um dia por vez,
// abre o popup quando o motor pausa e avisa o fluxo quando o mês fecha.
// Não tem regra de negócio: só traduz "tempo passou" e "botão clicado" em chamadas ao serviço.
using System;
using System.Linq;
using Game.Application.UseCases.Simulation;
using Game.Domain.Simulation;
using UnityEngine;

namespace Game.Adapter.In.Controllers
{
    public sealed class MonthSimulationController : MonoBehaviour
    {
        [Header("Telas (só desenham)")]
        [SerializeField] private MonthSimulationView monthView;
        [SerializeField] private EventDecisionPopupView popupView;

        [Header("Tempo")]
        [Tooltip("Segundos de tela por dia simulado na velocidade 1x")]
        [SerializeField] private float secondsPerDayAt1x = 1.0f;

        private SystemicMonthService _service;
        private float _speed;          // 0 = pausado (II), 1, 2 ou 4
        private float _timer;
        private bool _waitingDecision;
        private bool _closing;

        /// Avisado quando o fechamento foi gravado. Quem escuta é o SystemicRoundFlow.
        public event Action<MonthReport> MonthClosed;

        public bool IsRunning => _service?.Current != null && !_closing && !_service.Current.IsFinished;

        /// Chamado pelo SystemicRoundFlow (raiz de composição), que cria o serviço com o gateway SQLite.
        public void Initialize(SystemicMonthService service) => _service = service;

        // ------------------------------------------------------------ ciclo do mês
        public void StartMonth(string sessionId)
        {
            var month = _service.Begin(sessionId);       // recomeça do dia 1 se o aluno saiu no meio
            monthView.ShowMonthStart(month.Month, month.State.Cash);
            _waitingDecision = false;
            _closing = false;
            _timer = 0;
            SetSpeed(1);
        }

        /// Para o tempo e fecha o popup (usado ao abandonar o mês; nada é gravado).
        public void Stop()
        {
            SetSpeed(0);
            _waitingDecision = false;
            popupView.Hide();
            monthView.HideAbandonConfirm();
        }

        /// Botão ABANDONAR MÊS: pausa o tempo e pede confirmação. Cancelar volta à velocidade de antes.
        public void RequestAbandon(Action abandon)
        {
            if (!IsRunning) return;
            float before = _speed;
            SetSpeed(0);
            monthView.ShowAbandonConfirm(abandon, () => SetSpeed(before));
        }

        private void Update()
        {
            if (_service?.Current == null || _speed <= 0 || _waitingDecision || _closing) return;
            _timer += Time.deltaTime * _speed;
            if (_timer < secondsPerDayAt1x) return;
            _timer = 0;
            StepOnce();
        }

        private void StepOnce()
        {
            var step = _service.Step();
            if (step.Kind == StepKind.AwaitingDecision)
            {
                _waitingDecision = true;                  // o tempo para até o aluno escolher
                ShowNextDecision();
                return;
            }
            int day = step.DayResult.Day;
            monthView.RenderDay(step.DayResult, _service.Current.Occurrences.Where(o => o.Day == day));
            if (_service.Current.IsFinished) FinishMonth();
        }

        // ------------------------------------------------------------ botões de velocidade
        public void SetSpeed(float speed)                // ligado aos botões II (0), 1x, 2x e 4x
        {
            _speed = speed;
            monthView.ShowSpeed(speed);
        }

        public void SkipToEnd()                           // botão Pular: corre até o próximo popup ou o fim
        {
            while (!_waitingDecision && !_closing && _service.Current != null && !_service.Current.IsFinished)
                StepOnce();
        }

        // ------------------------------------------------------------ popup de evento
        private void ShowNextDecision()
        {
            var occurrence = _service.Current.PendingDecisions[0];
            monthView.ShowPending(occurrence);
            popupView.Show(occurrence, _service.ReactionsFor(occurrence), OnReactionChosen);
        }

        private void OnReactionChosen(EventOccurrence occurrence, string reactionId)
        {
            _service.Choose(occurrence.OccurrenceId, reactionId);
            popupView.Hide();
            if (_service.Current.IsAwaitingDecision) { ShowNextDecision(); return; }
            _waitingDecision = false;                     // próximo Step roda a operação do mesmo dia
        }

        // ------------------------------------------------------------ fechamento
        private void FinishMonth()
        {
            _closing = true;
            var (result, report) = _service.Finish();
            if (result.Ok) { MonthClosed?.Invoke(report); return; }
            monthView.ShowCommitError(result.Message, RetryCommit);
        }

        private void RetryCommit()                        // botão "Tentar de novo" da mensagem de erro
        {
            var result = _service.RetryCommit();          // mesmo pacote, sem recalcular o mês
            if (!result.Ok) { monthView.ShowCommitError(result.Message, RetryCommit); return; }
            var reports = _service.Reports(_service.Current.Setup.SessionId);
            MonthClosed?.Invoke(reports[reports.Count - 1]);
        }
    }
}
