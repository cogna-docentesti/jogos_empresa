using System;
using System.Collections.Generic;
using Game.Adapter.In.UI.Simulation;
using Game.Adapter.Out.Simulation;
using Game.Application.Ports.Out;
using Game.Application.UseCases.Simulation;
using Game.Domain.Simulation;
using UnityEngine;

namespace Game.Adapter.In.Controllers
{
    /// <summary>
    /// Orquestra o mes simulado dia a dia: tempo (1x, 2x, 4x, pausa), pausas para decisao,
    /// fechamento e navegacao. Nao calcula nada: quem calcula e o SystemicMonthService (aplicacao)
    /// com o MonthSimulation (dominio). Nao grava nada: quem grava e o SqliteSimulationGateway.
    ///
    /// Fica num GameObject SEMPRE ATIVO (ex.: Canvas/SimulationSystems), nao dentro de um painel:
    /// o UIStateListener desliga e religa paineis a cada troca de estado e isso pararia o relogio.
    /// O relogio usa Update com Time.unscaledDeltaTime (nao usa corrotina).
    /// </summary>
    public sealed class MonthSimulationController : MonoBehaviour
    {
        [Header("Telas")]
        [SerializeField] private MonthSimulationView monthView;
        [SerializeField] private EventDecisionPopupView decisionPopup;
        [SerializeField] private MonthReportView reportView;
        [SerializeField] private ManagementPoliciesView planningView;
        [SerializeField] private FinalReportView finalReportView;

        [Header("Ritmo")]
        [Tooltip("Segundos reais por dia de jogo na velocidade 1x. 2x e 4x dividem este valor.")]
        [Min(0.05f)] [SerializeField] private float secondsPerDayAt1x = 1.0f;

        [Tooltip("Comecar o mes pausado (o aluno aperta Play).")]
        [SerializeField] private bool startPaused;

        private SystemicMonthService _service;
        private SqliteSimulationGateway _gateway;
        private float _timer;
        private int _speed = 1;
        private bool _paused;
        private bool _skipToEnd;

        public SystemicMonthService Service => _service ?? (_service = CreateService());
        public int Speed => _speed;
        public bool Paused => _paused;

        public event Action<DayResult> DayCompleted;
        public event Action<PendingDecision> DecisionRequired;
        public event Action<MonthlyReport, MonthCommitResult> MonthClosed;

        private SystemicMonthService CreateService()
        {
            _gateway = new SqliteSimulationGateway();
            return new SystemicMonthService(_gateway);
        }

        private void OnEnable()
        {
            if (planningView != null) planningView.Bind(this);
            if (monthView != null) monthView.Bind(this);
            if (reportView != null) reportView.Bind(this);
            if (finalReportView != null) finalReportView.Bind(this);
        }

        // =============================================================
        //  PLANEJAMENTO (hub)
        // =============================================================

        /// <summary>Abre o painel de planejamento do mes (politicas de gestao).</summary>
        public void OpenPlanning()
        {
            if (planningView != null) planningView.Show();
        }

        public void ClosePlanning()
        {
            if (planningView != null) planningView.Hide();
        }

        // =============================================================
        //  MES
        // =============================================================

        /// <summary>Botao "Iniciar mes" do planejamento.</summary>
        public void StartMonth()
        {
            if (Service.IsRunning)
            {
                SystemicRoundFlow.EnterMonth();
                if (monthView != null) monthView.Show();
                return;
            }
            if (!SystemicRoundFlow.CanStartMonth)
            {
                if (planningView != null) planningView.ShowError("O mês só pode começar pelo hub, com a partida já gravada.");
                return;
            }
            if (!Service.Begin())
            {
                if (planningView != null) planningView.ShowError(Service.LastError);
                Debug.LogWarning("[Simulacao] " + Service.LastError);
                return;
            }
            if (_gateway != null) Service.OutlookRuns = _gateway.OutlookRuns;
            _paused = startPaused;
            _skipToEnd = false;
            _timer = 0f;
            if (planningView != null) planningView.Hide();
            SystemicRoundFlow.EnterMonth();
            if (monthView != null)
            {
                monthView.Show();
                monthView.RenderAll();
            }
        }

        public void SetSpeed(int multiplier)
        {
            _speed = multiplier == 4 ? 4 : multiplier == 2 ? 2 : 1;
            _paused = false;
            if (monthView != null) monthView.RenderControls();
        }

        public void TogglePause()
        {
            _paused = !_paused;
            if (monthView != null) monthView.RenderControls();
        }

        /// <summary>Roda sem animacao ate a proxima decisao ou o fim do mes.</summary>
        public void SkipToNextStop()
        {
            _skipToEnd = true;
            _paused = false;
        }

        /// <summary>Botao "Sair do mes": descarta o mes em memoria e volta ao hub.</summary>
        public void AbandonMonth()
        {
            if (_service == null) return;
            _service.Abandon();
            if (decisionPopup != null) decisionPopup.Hide();
            if (monthView != null) monthView.Hide();
            SystemicRoundFlow.AbandonMonth();
        }

        private void Update()
        {
            var service = _service;
            if (service == null || service.Simulation == null || service.LastOutcome != null) return;
            if (service.IsAwaitingDecision || _paused) return;

            if (service.IsFinished)
            {
                CloseMonth();
                return;
            }

            if (_skipToEnd)
            {
                // Limite por quadro para nao travar a tela em aparelhos lentos.
                for (int i = 0; i < 31 && service.IsRunning && !service.IsAwaitingDecision; i++) StepOnce();
                if (service.IsAwaitingDecision || service.IsFinished) _skipToEnd = false;
                return;
            }

            _timer += Time.unscaledDeltaTime;
            float interval = secondsPerDayAt1x / _speed;
            if (_timer < interval) return;
            _timer = 0f;
            StepOnce();
        }

        private void StepOnce()
        {
            DayResult day;
            try
            {
                day = _service.Step();
            }
            catch (Exception exception)
            {
                Debug.LogError("[Simulacao] Erro no dia simulado: " + exception);
                _paused = true;
                if (monthView != null) monthView.ShowError("Erro na simulação: " + exception.Message);
                return;
            }

            if (day != null)
            {
                if (monthView != null) monthView.AppendDay(day);
                DayCompleted?.Invoke(day);
            }

            if (_service.IsAwaitingDecision)
            {
                var pending = _service.Simulation.Pending;
                if (decisionPopup != null) decisionPopup.Show(pending, Choose);
                DecisionRequired?.Invoke(pending);
            }
        }

        /// <summary>Chamado pelo popup quando o aluno escolhe uma reacao.</summary>
        public void Choose(string occurrenceId, string reactionId)
        {
            try
            {
                _service.Choose(occurrenceId, reactionId);
            }
            catch (InvalidOperationException exception)
            {
                if (decisionPopup != null) decisionPopup.ShowError(exception.Message);
                return;
            }
            if (monthView != null) monthView.RenderAll();
            if (decisionPopup == null) return;
            if (_service.IsAwaitingDecision) decisionPopup.Show(_service.Simulation.Pending, Choose);
            else decisionPopup.Hide();
        }

        private void CloseMonth()
        {
            MonthlyReport report;
            try
            {
                report = _service.Finish();
            }
            catch (Exception exception)
            {
                Debug.LogError("[Simulacao] Falha ao fechar o mes: " + exception);
                if (monthView != null) monthView.ShowError("Não foi possível fechar o mês: " + exception.Message);
                _paused = true;
                return;
            }
            var commit = _service.LastCommit;
            MonthClosed?.Invoke(report, commit);
            if (commit == null || !commit.Success)
            {
                if (monthView != null) monthView.ShowCommitFailure(commit?.Error ?? "erro desconhecido");
                return;
            }
            OpenSummary(report);
        }

        /// <summary>Botao "Tentar gravar de novo" quando o banco recusou o fechamento.</summary>
        public void RetryCommit()
        {
            if (_service?.LastOutcome == null) return;
            var commit = _service.RetryCommit();
            if (!commit.Success)
            {
                if (monthView != null) monthView.ShowCommitFailure(commit.Error);
                return;
            }
            OpenSummary(_service.LastOutcome.Report);
        }

        private void OpenSummary(MonthlyReport report)
        {
            if (monthView != null) monthView.Hide();
            SystemicRoundFlow.ShowSummary();
            if (reportView != null) reportView.Show(report, _service.LastCommit);
        }

        /// <summary>Botao "Continuar" do resumo do mes.</summary>
        public void LeaveSummary()
        {
            if (reportView != null) reportView.Hide();
            _service?.Abandon();
            SystemicRoundFlow.LeaveSummary();
            var session = GameSessionState.Current;
            if (finalReportView != null && session != null && session.status != GameSessionStatus.IN_PROGRESS)
                finalReportView.Show(session.sessionId, session.status == GameSessionStatus.BANKRUPT);
        }

        /// <summary>Botao "Ver DRE detalhada" durante o mes: mostra o parcial ate o dia atual.</summary>
        public void OpenPartialReport()
        {
            if (_service?.Simulation == null || reportView == null) return;
            _paused = true;
            if (monthView != null) monthView.RenderControls();
            reportView.ShowPartial(_service.Simulation);
        }

        public IReadOnlyList<MonthlyReport> SavedReports()
        {
            var session = GameSessionState.Current;
            return session == null ? new List<MonthlyReport>() : Service.Reports(session.sessionId);
        }
    }
}
