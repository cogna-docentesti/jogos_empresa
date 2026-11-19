using UnityEngine;

namespace Game.Adapter.In.Controllers
{
    /// <summary>
    /// Transicoes da state machine para o mes simulado dia a dia. Usa os estados que ja existem,
    /// sem mudar o GameStateMachine:
    ///
    ///   Management_Hub -> Round_Start -> Round_Sales      (tela do mes rodando)
    ///   Round_Sales -> Round_Costs -> Round_Event -> Round_Summary   (depois do COMMIT)
    ///   Round_Summary -> Management_Hub | FinalReport | GameOver_Bankruptcy
    ///
    /// Os paineis desses estados sao ligados no UIStateListener (Inspector). Ver tutorial,
    /// secao "Ligar os paineis no UIStateListener".
    /// </summary>
    public static class SystemicRoundFlow
    {
        private static GameStateMachine Machine => GameManager.Instance != null ? GameManager.Instance.StateMachine : null;

        /// <summary>
        /// Verdadeiro enquanto o mes esta rodando e ainda nao foi gravado. O dialogo Sair pode usar
        /// para avisar: "o mes em andamento recomeca do dia 1 na proxima vez".
        /// </summary>
        public static bool MonthInProgress =>
            Machine != null && (Machine.CurrentState == GameState.Round_Start || Machine.CurrentState == GameState.Round_Sales);

        public static bool CanStartMonth =>
            Machine != null && Machine.CurrentState == GameState.Management_Hub
            && GameSessionState.HasActiveSession && GameSessionState.IsPersisted;

        public static bool EnterMonth()
        {
            var machine = Machine;
            if (machine == null) return false;
            if (machine.CurrentState == GameState.Round_Sales) return true;
            if (machine.CurrentState != GameState.Management_Hub) return false;
            // Fecha telas do hub que o MenuNavigator abriu (planejamento, banco, RH...).
            Game.Adapter.In.UI.Navigation.MenuNavigator.Instance?.CloseAll();
            return machine.TryChangeState(GameState.Round_Start) && machine.TryChangeState(GameState.Round_Sales);
        }

        /// <summary>Chamado so depois do COMMIT do mes.</summary>
        public static bool ShowSummary()
        {
            var machine = Machine;
            if (machine == null) return false;
            if (machine.CurrentState == GameState.Round_Summary) return true;
            if (machine.CurrentState != GameState.Round_Sales) return false;
            return machine.TryChangeState(GameState.Round_Costs)
                   && machine.TryChangeState(GameState.Round_Event)
                   && machine.TryChangeState(GameState.Round_Summary);
        }

        /// <summary>Sai do resumo para o hub, o relatorio final ou a tela de falencia, conforme a sessao.</summary>
        public static bool LeaveSummary()
        {
            var machine = Machine;
            var session = GameSessionState.Current;
            if (machine == null || session == null || machine.CurrentState != GameState.Round_Summary) return false;
            switch (session.status)
            {
                case GameSessionStatus.BANKRUPT: return machine.TryChangeState(GameState.GameOver_Bankruptcy);
                case GameSessionStatus.COMPLETED: return machine.TryChangeState(GameState.FinalReport);
                default: return machine.TryChangeState(GameState.Management_Hub);
            }
        }

        /// <summary>
        /// Abandona o mes em andamento (nada foi gravado) e volta ao hub. A state machine nao tem
        /// a transicao Round_Sales -> Management_Hub, por isso ForceState. Como a semente e fixa,
        /// ao recomecar o mes os mesmos sorteios voltam: sair nao serve para "sortear de novo".
        /// </summary>
        public static void AbandonMonth()
        {
            var machine = Machine;
            if (machine == null) return;
            if (machine.CurrentState == GameState.Round_Start || machine.CurrentState == GameState.Round_Sales)
            {
                Debug.Log("[Simulacao] Mes abandonado antes do fechamento. Nada foi gravado.");
                machine.ForceState(GameState.Management_Hub);
            }
        }
    }
}
