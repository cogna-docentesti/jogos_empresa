using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Toolbars;
using UnityEngine;

[InitializeOnLoad]
internal static class SampleScenePlayButton
{
    private const string SampleScenePath = "Assets/Scenes/SampleScene.unity";
    private const string ToolbarElementPath = "Jogo/Play SampleScene";

    private static bool waitingForPlayModeToStop;

    static SampleScenePlayButton()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    [MainToolbarElement(
        ToolbarElementPath,
        defaultDockPosition = MainToolbarDockPosition.Middle,
        defaultDockIndex = 1)]
    private static MainToolbarElement CreateToolbarButton()
    {
        return new MainToolbarButton(
            new MainToolbarContent("▶ Sample", "Abrir a SampleScene e iniciar o jogo"),
            StartFromSampleScene)
        {
            displayed = true,
            enabled = true
        };
    }

    private static void StartFromSampleScene()
    {
        if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            if (EditorApplication.isPlaying)
            {
                waitingForPlayModeToStop = true;
                EditorApplication.isPlaying = false;
            }

            return;
        }

        OpenAndPlaySampleScene();
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (!waitingForPlayModeToStop || state != PlayModeStateChange.EnteredEditMode)
            return;

        waitingForPlayModeToStop = false;
        EditorApplication.delayCall += OpenAndPlaySampleScene;
    }

    private static void OpenAndPlaySampleScene()
    {
        if (EditorApplication.isCompiling)
            return;

        SceneAsset sampleScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(SampleScenePath);
        if (sampleScene == null)
        {
            EditorUtility.DisplayDialog(
                "SampleScene não encontrada",
                $"Não foi possível encontrar a cena em:\n{SampleScenePath}",
                "OK"
            );
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Single);
        EditorApplication.isPlaying = true;
    }
}
