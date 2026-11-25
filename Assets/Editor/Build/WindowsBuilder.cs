#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Gera o executável de Windows (64 bits) do jogo, pelo menu ou por linha de comando
    /// (-executeMethod Game.EditorTools.WindowsBuilder.Build). Mesma cena de entrada do AndroidBuilder.
    /// Saída: Builds/Windows/JogosDeEmpresa.exe (a pasta inteira é o jogo; copie a pasta, não só o .exe).
    /// </summary>
    public static class WindowsBuilder
    {
        private const string OutputDir = "Builds/Windows";
        private const string ExeName = "JogosDeEmpresa.exe";
        private static readonly string[] Scenes = { "Assets/Scenes/GameScene.unity" };

        [MenuItem("Tools/Jogo/Build/Windows (.exe, 64 bits)", false, 310)]
        public static void BuildFromMenu() => Run(false);

        /// <summary>Chamado por linha de comando via -executeMethod.</summary>
        public static void Build() => Run(true);

        private static void Run(bool exitWhenDone)
        {
            foreach (var scene in Scenes)
                if (!File.Exists(scene))
                {
                    Debug.LogError("[WindowsBuilder] Cena não encontrada: " + scene);
                    if (exitWhenDone) EditorApplication.Exit(2);
                    return;
                }

            Directory.CreateDirectory(OutputDir);
            string exePath = Path.Combine(OutputDir, ExeName);
            var options = new BuildPlayerOptions
            {
                scenes = Scenes,
                locationPathName = exePath,
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None
            };
            Debug.Log("[WindowsBuilder] ==== iniciando ==== saída: " + exePath);

            BuildReport report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log(string.Format("[WindowsBuilder] SUCESSO. Executável em {0} | {1:F1} MB | {2:F0}s",
                    Path.GetFullPath(exePath), summary.totalSize / (1024.0 * 1024.0), summary.totalTime.TotalSeconds));
                if (exitWhenDone) EditorApplication.Exit(0);
                return;
            }

            Debug.LogError(string.Format("[WindowsBuilder] FALHOU: {0} | {1} erro(s)", summary.result, summary.totalErrors));
            foreach (var step in report.steps)
                foreach (var message in step.messages)
                    if (message.type == LogType.Error || message.type == LogType.Exception)
                        Debug.LogError("[WindowsBuilder] " + step.name + ": " + message.content);
            if (exitWhenDone) EditorApplication.Exit(1);
        }
    }
}
#endif
