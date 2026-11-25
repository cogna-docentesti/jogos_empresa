using System.IO;
using Game.Adapter.Out.Simulation;
using Game.Domain.Simulation;
using UnityEditor;
using UnityEngine;

/// <summary>Menu Game > Simulacao: cria, valida, exporta e importa o SimulationConfig.asset.</summary>
public static class SimulationConfigMenu
{
    private const string AssetFolder = "Assets/Resources/Simulation";
    private const string AssetPath = AssetFolder + "/SimulationConfig.asset";

    [MenuItem("Game/Simulacao/Criar configuracao", priority = 10)]
    public static void Create()
    {
        var existing = AssetDatabase.LoadAssetAtPath<SimulationConfigAsset>(AssetPath);
        if (existing != null)
        {
            Selection.activeObject = existing;
            EditorGUIUtility.PingObject(existing);
            Debug.Log("[Simulacao] A configuracao ja existe em " + AssetPath + ". Nada foi sobrescrito.");
            return;
        }
        if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder(AssetFolder)) AssetDatabase.CreateFolder("Assets/Resources", "Simulation");
        var asset = ScriptableObject.CreateInstance<SimulationConfigAsset>();
        asset.ResetToDefaults();
        AssetDatabase.CreateAsset(asset, AssetPath);
        AssetDatabase.SaveAssets();
        Selection.activeObject = asset;
        EditorGUIUtility.PingObject(asset);
        Debug.Log("[Simulacao] Configuracao criada em " + AssetPath + " com os valores padrao.");
    }

    [MenuItem("Game/Simulacao/Validar configuracao", priority = 11)]
    public static void Validate()
    {
        var provider = new UnitySimulationCatalogProvider();
        var catalog = provider.Load();
        var problems = catalog.Validate();
        foreach (var warning in provider.Warnings) Debug.LogWarning("[Simulacao] " + warning);
        if (problems.Count == 0)
            Debug.Log("[Simulacao] Configuracao valida: " + catalog.Locations.Count + " localizacoes, " + catalog.Restaurants.Count + " restaurantes, "
                      + catalog.Equipment.Count + " equipamentos, " + catalog.Config.events.Count + " eventos, " + catalog.Config.policies.Count + " politicas.");
        else
            foreach (var problem in problems) Debug.LogError("[Simulacao] " + problem);
    }

    [MenuItem("Game/Simulacao/Exportar configuracao (JSON)", priority = 30)]
    public static void Export()
    {
        var asset = SimulationConfigAsset.LoadFromResources();
        var config = asset != null ? asset.CloneConfig() : DefaultSimulationConfig.Create();
        string path = EditorUtility.SaveFilePanel("Exportar configuracao do simulador", Application.dataPath, "simulation-config", "json");
        if (string.IsNullOrEmpty(path)) return;
        File.WriteAllText(path, JsonUtility.ToJson(config, true));
        Debug.Log("[Simulacao] Configuracao exportada para " + path);
    }

    [MenuItem("Game/Simulacao/Importar configuracao (JSON)", priority = 31)]
    public static void Import()
    {
        var asset = SimulationConfigAsset.LoadFromResources();
        if (asset == null)
        {
            EditorUtility.DisplayDialog("Simulacao", "Crie a configuracao primeiro (Game > Simulacao > Criar configuracao).", "OK");
            return;
        }
        string path = EditorUtility.OpenFilePanel("Importar configuracao do simulador", Application.dataPath, "json");
        if (string.IsNullOrEmpty(path)) return;
        var imported = JsonUtility.FromJson<SimulationConfig>(File.ReadAllText(path));
        if (imported == null || imported.segments == null || imported.segments.Count == 0)
        {
            EditorUtility.DisplayDialog("Simulacao", "O arquivo nao parece uma configuracao do simulador.", "OK");
            return;
        }
        Undo.RecordObject(asset, "Importar configuracao do simulador");
        asset.config = imported;
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();
        Validate();
    }
}
