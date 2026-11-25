// Assets/Editor/Tests/SetupBackNavigationTests.cs
// Requisito do projeto: toda tela da configuração inicial deixa o jogador voltar para a anterior e mudar a escolha.
using NUnit.Framework;

public class SetupBackNavigationTests
{
    [TestCase(GameState.Config_Location, GameState.Config_Identification)]
    [TestCase(GameState.Config_Restaurant, GameState.Config_Location)]
    [TestCase(GameState.Config_TargetSegment, GameState.Config_Restaurant)]
    [TestCase(GameState.Initial_Equipment, GameState.Initial_Capital)]
    [TestCase(GameState.Initial_Team, GameState.Initial_Equipment)]
    public void CadaPassoDaConfiguracao_PodeVoltarParaOAnterior(GameState from, GameState back)
    {
        var machine = new GameStateMachine();
        machine.ForceState(from);
        Assert.IsTrue(machine.CanTransitionTo(back), $"{from} não volta para {back}");
    }
}
