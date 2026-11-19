using System.Collections.Generic;
using Game.Domain.Simulation;
using UnityEngine;

/// <summary>
/// Configuracao do simulador sistemico, editavel no Inspector pelo professor ou pela equipe.
///
/// Local esperado: Assets/Resources/Simulation/SimulationConfig.asset
/// (o adaptador carrega com Resources.Load("Simulation/SimulationConfig")).
/// Se o asset nao existir, o jogo usa os valores padrao do DefaultSimulationConfig e avisa no Console.
///
/// Os ScriptableObjects que ja existem (LocationData, RestaurantData, ProductData, RoleData,
/// EquipmentData, CreditLineData, EventData) continuam sendo a fonte de nomes, precos, salarios,
/// custos e capacidades. Este asset guarda so o que eles nao tem: perfil de publico por regiao,
/// curvas de aceitacao de preco, confiabilidade de equipamentos, politicas de gestao, eventos
/// do motor diario e parametros financeiros.
/// </summary>
[CreateAssetMenu(fileName = "SimulationConfig", menuName = "Game Data/Simulation Config")]
public sealed class SimulationConfigAsset : ScriptableObject
{
    public const string ResourcesPath = "Simulation/SimulationConfig";

    [Header("Fontes de dados")]
    [Tooltip("Acrescenta ao motor diario os EventData de Resources/Events que nao tem definicao propria abaixo.")]
    public bool includeLegacyEvents = true;

    [Tooltip("Usa titulo, descricao e textos das opcoes dos EventData quando o id coincide. " +
             "Deixe desligado enquanto os assets tiverem textos de teste.")]
    public bool useEventAssetTexts;

    [Header("Simulacao")]
    [Tooltip("Rodadas da previsao de crescimento/estagnacao/encerramento ao fechar o mes. 0 desliga.")]
    [Min(0)] public int outlookRuns = 40;

    [Tooltip("Parametros do simulador. 'Restaurar padroes' no menu de contexto (tres pontos) recria os valores de referencia.")]
    public SimulationConfig config = new SimulationConfig();

    /// <summary>Copia profunda: a partida nunca altera o asset (que e compartilhado no editor).</summary>
    public SimulationConfig CloneConfig()
    {
        if (config == null || config.segments == null || config.segments.Count == 0)
            return DefaultSimulationConfig.Create();
        return JsonUtility.FromJson<SimulationConfig>(JsonUtility.ToJson(config));
    }

    public List<string> Validate() => DefaultSimulationConfig.CreateCatalog(CloneConfig()).Validate();

    [ContextMenu("Restaurar padroes")]
    public void ResetToDefaults()
    {
        config = DefaultSimulationConfig.Create();
        includeLegacyEvents = true;
        useEventAssetTexts = false;
        outlookRuns = 40;
    }

    private void Reset() => ResetToDefaults();

    public static SimulationConfigAsset LoadFromResources() => Resources.Load<SimulationConfigAsset>(ResourcesPath);
}
