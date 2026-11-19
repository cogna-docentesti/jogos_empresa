// Assets/Scripts/Game/Adapter/In/Controllers/SystemicRoundFlow.cs
// Raiz de composição e ligação com a GameStateMachine do projeto.
// 1. Cria as peças (provedor de catálogo -> gateway SQLite -> serviço) uma vez, no Awake.
// 2. Planejamento confirmado -> grava políticas -> Management_Hub -> Round_Start -> Round_Sales -> começa o mês.
// 3. Mês fechado -> Round_Costs -> Round_Event -> Round_Summary (o UIStateListener mostra o resumo).
// 4. Botão Continuar do resumo -> Management_Hub, FinalReport ou GameOver_Bankruptcy.
// Não muda a GameStateMachine: usa as transições que já existem.
using System.Collections.Generic;
using Game.Adapter.Out.Simulation;
using Game.Application.UseCases.Simulation;
using Game.Domain.Simulation;
using UnityEngine;

namespace Game.Adapter.In.Controllers
{
    public sealed class SystemicRoundFlow : MonoBehaviour
    {
        [Tooltip("Vazio = carrega Resources/Simulation/SimulationConfig")]
        [SerializeField] private SimulationConfigAsset simulationConfig;
        [SerializeField] private MonthSimulationController monthController;
        [SerializeField] private ManagementPoliciesView planningView;

        private SystemicMonthService _service;

        private static GameStateMachine Machine => GameManager.Instance != null ? GameManager.Instance.StateMachine : null;
        private static string SessionId => GameSessionState.Current?.sessionId;

        public SystemicMonthService Service => _service;
        public MonthReport LastReport { get; private set; }

        /// Verdadeiro enquanto o mês roda e ainda não foi gravado (para o diálogo Sair avisar).
        public static bool MonthInProgress =>
            Machine != null && (Machine.CurrentState == GameState.Round_Start || Machine.CurrentState == GameState.Round_Sales);

        private void Awake()
        {
            if (!DatabaseInitializer.IsReady || DatabaseInitializer.DatabaseService?.Connection == null)
            {
                Debug.LogError("[Simulação] Banco indisponível: o simulador não foi iniciado. " + DatabaseInitializer.FailureReason);
                enabled = false;
                return;
            }
            var config = simulationConfig != null ? simulationConfig
                       : Resources.Load<SimulationConfigAsset>(UnitySimulationCatalogProvider.ConfigResourcePath);
            if (config == null)
            {
                Debug.LogError("[Simulação] SimulationConfig.asset não encontrado. Use o menu Game > Simulação > Criar ou atualizar SimulationConfig.");
                enabled = false;
                return;
            }

            var provider = new UnitySimulationCatalogProvider(config);
            var gateway = new SqliteSimulationGateway(DatabaseInitializer.DatabaseService.Connection, provider);
            _service = new SystemicMonthService(gateway);

            monthController.Initialize(_service);
            monthController.MonthClosed += OnMonthClosed;
            if (planningView != null)
            {
                planningView.Opened += OnPlanningOpened;
                planningView.SaveClicked += OnSavePolicies;
                planningView.ConfirmAndStartClicked += OnConfirmAndStart;
                if (planningView.isActiveAndEnabled) OnPlanningOpened();      // já estava aberto antes do Awake
            }
        }

        private void OnDestroy()
        {
            if (monthController != null) monthController.MonthClosed -= OnMonthClosed;
            if (planningView != null)
            {
                planningView.Opened -= OnPlanningOpened;
                planningView.SaveClicked -= OnSavePolicies;
                planningView.ConfirmAndStartClicked -= OnConfirmAndStart;
            }
        }

        // ------------------------------------------------------------ planejamento (Panel_Planning)
        private void OnPlanningOpened() => planningView.Bind(SessionId != null ? _service.Planning(SessionId) : null);

        private void OnSavePolicies(MonthPolicies policies)
        {
            var result = _service.SavePolicies(SessionId, policies);
            planningView.ShowSaveResult(result.Ok, result.Message);
        }

        private void OnConfirmAndStart(MonthPolicies policies)
        {
            var planning = _service.Planning(SessionId);
            if (planning == null || !planning.CanStart)
            {
                planningView.ShowSaveResult(false, planning == null ? "Nenhuma partida encontrada." : string.Join(" ", planning.Errors));
                return;
            }
            var result = _service.SavePolicies(SessionId, policies);
            if (!result.Ok) { planningView.ShowSaveResult(false, result.Message); return; }
            if (!EnterMonth()) { planningView.ShowSaveResult(false, "O mês só pode começar a partir do hub."); return; }
            monthController.StartMonth(SessionId);
        }

        private static bool EnterMonth()
        {
            var machine = Machine;
            if (machine == null) return false;
            if (machine.CurrentState == GameState.Round_Sales) return true;
            if (machine.CurrentState != GameState.Management_Hub) return false;
            Game.Adapter.In.UI.Navigation.MenuNavigator.Instance?.CloseAll();   // fecha telas do hub abertas
            return machine.TryChangeState(GameState.Round_Start) && machine.TryChangeState(GameState.Round_Sales);
        }

        // ------------------------------------------------------------ fechamento
        private void OnMonthClosed(MonthReport report)
        {
            LastReport = report;
            var machine = Machine;
            if (machine == null || machine.CurrentState != GameState.Round_Sales) return;
            machine.TryChangeState(GameState.Round_Costs);
            machine.TryChangeState(GameState.Round_Event);
            machine.TryChangeState(GameState.Round_Summary);                    // o painel do resumo lê LastReport
        }

        /// Meses fechados da partida (relatório final e falência).
        public IReadOnlyList<MonthReport> MatchReports() => _service != null && SessionId != null ? _service.Reports(SessionId) : null;

        /// Botão Continuar do resumo do mês.
        public void LeaveSummary()
        {
            var machine = Machine;
            if (machine == null || machine.CurrentState != GameState.Round_Summary || LastReport == null) return;
            if (LastReport.Bankrupt) machine.TryChangeState(GameState.GameOver_Bankruptcy);
            else if (LastReport.Month >= _service.MonthsPerMatch) machine.TryChangeState(GameState.FinalReport);
            else machine.TryChangeState(GameState.Management_Hub);
        }

        /// Sair no meio do mês: nada foi gravado; ao voltar, o mês recomeça do dia 1 com os mesmos sorteios.
        /// A máquina não tem a transição Round_Sales -> Management_Hub, por isso ForceState.
        public void AbandonMonth()
        {
            var machine = Machine;
            if (machine == null || !MonthInProgress) return;
            monthController.Stop();
            _service.Abandon();
            Debug.Log("[Simulação] Mês abandonado antes do fechamento. Nada foi gravado.");
            machine.ForceState(GameState.Management_Hub);
        }
    }
}
