using UnityEngine;

/// <summary>
/// Ordem das decisoes obrigatorias de abertura, num lugar so.
///
///   D1 Localizacao  ->  D2 Restaurante  ->  D3 Cardapio  ->  (grava no banco)  ->  tutoriais
///
/// O jogo usa uma cena unica (GameScene) e cada tela e um painel ligado pelo
/// UIStateListener conforme o GameState. Por isso, o equivalente aos "nomes
/// de cena" do roteiro aqui sao os estados: os controllers pedem
/// InitialDecisionFlow.Restaurant em vez de escrever GameState.Config_Restaurant
/// solto. Se a ordem mudar de novo, muda so este arquivo e as transicoes
/// do GameStateMachine.
/// </summary>
public static class InitialDecisionFlow
{
    /// <summary>D1. Painel Panel_Location.</summary>
    public const GameState Location = GameState.Config_Location;

    /// <summary>D2. Painel Panel_Restaurant (tipo de restaurante + classe social).</summary>
    public const GameState Restaurant = GameState.Config_Restaurant;

    /// <summary>
    /// D3. Painel Panel_MenuPricing.
    /// O nome do estado e historico (de quando a D3 era o segmento-alvo).
    /// Nao foi renomeado para nao gerar conflito com as branches da Thaysla e do Renan.
    /// </summary>
    public const GameState Menu = GameState.Config_TargetSegment;

    /// <summary>Estado de passagem entre o confirm da D3 e o primeiro tutorial.</summary>
    public const GameState Review = GameState.Config_Review;

    /// <summary>
    /// Para onde o jogo vai depois que as tres decisoes foram gravadas.
    /// Hoje: tela do Banco (D4), que abre a sequencia Banco -> Equipamentos -> RH
    /// que a Thaysla esta transformando em tutorial. Quando o hub virar o destino,
    /// troque so esta linha (e a transicao Config_Review no GameStateMachine).
    /// </summary>
    public const GameState AfterCommit = GameState.Initial_Capital;

    /// <summary>
    /// Pede a troca de estado ao GameManager. Devolve false se nao houver
    /// GameManager na cena ou se a transicao nao for permitida.
    /// </summary>
    public static bool GoTo(GameState next)
    {
        var manager = GameManager.Instance;

        if (manager == null || manager.StateMachine == null)
        {
            Debug.LogError("[InitialDecisionFlow] GameManager nao encontrado. "
                         + "Rode o jogo a partir da cena 0_Identification ou da GameScene.");
            return false;
        }

        return manager.StateMachine.TryChangeState(next);
    }
}
