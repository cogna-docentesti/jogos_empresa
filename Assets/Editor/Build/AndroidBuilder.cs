#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Gera o APK de Android do jogo, tanto pelo menu quanto por linha de
    /// comando (-executeMethod Game.EditorTools.AndroidBuilder.Build).
    ///
    /// ------------------------------------------------------------------
    ///  POR QUE ESTE SCRIPT PRECISA CONSERTAR COISAS ANTES DE BUILDAR
    /// ------------------------------------------------------------------
    ///
    /// 1. CENA DE ENTRADA.
    ///    O Build Settings do projeto lista a SampleScene como indice 0, e ela
    ///    tem apenas uma camera e uma luz. A Bootstrap - que carrega o
    ///    GameManager e o DatabaseInitializer, e e quem chama
    ///    SceneManager.LoadScene("GameScene") - esta DESABILITADA.
    ///    Buildar assim entrega um app que abre numa tela vazia e nunca sai
    ///    dela. Vale para Android e para Windows.
    ///
    ///    Aqui a lista de cenas e passada EXPLICITAMENTE para o BuildPipeline,
    ///    entao o EditorBuildSettings do projeto nao e alterado. Se voce quiser
    ///    consertar o projeto de vez, use o item de menu "Corrigir cenas do
    ///    Build Settings".
    ///
    /// 2. PACKAGE NAME.
    ///    applicationIdentifier so tem entrada para Standalone. Sem um id de
    ///    Android o build falha com "Application Identifier has not been set up
    ///    correctly".
    ///
    /// 3. ARQUITETURA.
    ///    O projeto esta em ARM64 apenas. Nenhum emulador x86_64 consegue rodar
    ///    esse APK. Para o build de teste entra tambem x86_64.
    ///
    /// 4. ORIENTACAO.
    ///    A UI e desenhada em paisagem 16:9. O projeto esta em AutoRotation com
    ///    retrato liberado, o que espremeria a tela num celular. Aqui o retrato
    ///    e desligado e as duas paisagens ficam liberadas.
    /// </summary>
    public static class AndroidBuilder
    {
        private const string OutputDir  = "Builds/Android";
        private const string ApkName    = "JogosDeEmpresa.apk";
        private const string PackageId  = "com.defaultcompany.jogosdeempresa";

        /// <summary>
        /// A ordem importa: o indice 0 e a cena que o app abre.
        /// Bootstrap monta o GameManager e o banco, e so entao carrega a
        /// GameScene pelo nome - por isso as duas precisam estar na lista.
        /// </summary>
        private static readonly string[] Scenes =
        {
            "Assets/Scenes/Bootstrap.unity",
            "Assets/Scenes/GameScene.unity"
        };

        // =====================================================
        //  ENTRADAS
        // =====================================================

        [MenuItem("Tools/Jogo/Build/Android APK (ARM64 + x86_64)", false, 300)]
        public static void BuildFromMenu()
        {
            Run(includeX86: true, exitWhenDone: false);
        }

        [MenuItem("Tools/Jogo/Build/Android APK (so ARM64, para celular)", false, 301)]
        public static void BuildArmOnlyFromMenu()
        {
            Run(includeX86: false, exitWhenDone: false);
        }

        /// <summary>Chamado por linha de comando via -executeMethod.</summary>
        public static void Build()
        {
            Run(includeX86: true, exitWhenDone: true);
        }

        /// <summary>Chamado por linha de comando. Build so ARM64.</summary>
        public static void BuildArmOnly()
        {
            Run(includeX86: false, exitWhenDone: true);
        }

        // =====================================================
        //  EXECUCAO
        // =====================================================

        private static void Run(bool includeX86, bool exitWhenDone)
        {
            var log = new StringBuilder();
            log.AppendLine("[AndroidBuilder] ==== iniciando ====");

            foreach (var scene in Scenes)
            {
                if (File.Exists(scene))
                    continue;

                Debug.LogError("[AndroidBuilder] Cena nao encontrada: " + scene);

                if (exitWhenDone)
                    EditorApplication.Exit(2);

                return;
            }

            ApplyPlayerSettings(includeX86, log);

            Directory.CreateDirectory(OutputDir);

            string apkPath = Path.Combine(OutputDir, ApkName);

            var options = new BuildPlayerOptions
            {
                scenes           = Scenes,
                locationPathName = apkPath,
                target           = BuildTarget.Android,
                targetGroup      = BuildTargetGroup.Android,
                options          = BuildOptions.None
            };

            log.AppendLine("[AndroidBuilder] cenas: " + string.Join(", ", Scenes));
            log.AppendLine("[AndroidBuilder] saida: " + apkPath);
            Debug.Log(log.ToString());

            BuildReport report  = BuildPipeline.BuildPlayer(options);
            BuildSummary resumo = report.summary;

            if (resumo.result == BuildResult.Succeeded)
            {
                double mb = resumo.totalSize / (1024.0 * 1024.0);

                Debug.Log(string.Format(
                    "[AndroidBuilder] SUCESSO. APK em {0} | {1:F1} MB | {2:F0}s",
                    apkPath, mb, resumo.totalTime.TotalSeconds));

                if (exitWhenDone)
                    EditorApplication.Exit(0);

                return;
            }

            Debug.LogError(string.Format(
                "[AndroidBuilder] FALHOU: {0} | {1} erro(s)",
                resumo.result, resumo.totalErrors));

            // As mensagens de erro de verdade ficam nos steps, nao no summary.
            foreach (var step in report.steps)
            {
                foreach (var message in step.messages)
                {
                    if (message.type == LogType.Error || message.type == LogType.Exception)
                        Debug.LogError("[AndroidBuilder] " + step.name + ": " + message.content);
                }
            }

            if (exitWhenDone)
                EditorApplication.Exit(1);
        }

        private static void ApplyPlayerSettings(bool includeX86, StringBuilder log)
        {
            var android = NamedBuildTarget.Android;

            // ---- package name ----
            string atual = PlayerSettings.GetApplicationIdentifier(android);

            if (string.IsNullOrEmpty(atual) || atual.Contains("DefaultCompany.2D-URP"))
            {
                PlayerSettings.SetApplicationIdentifier(android, PackageId);
                log.AppendLine("[AndroidBuilder] applicationIdentifier definido: " + PackageId);
            }

            // ---- arquitetura ----
            // IL2CPP e obrigatorio: o backend Mono nao suporta ARM64 no Android.
            PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);

            PlayerSettings.Android.targetArchitectures = includeX86
                ? AndroidArchitecture.ARM64 | AndroidArchitecture.X86_64
                : AndroidArchitecture.ARM64;

            log.AppendLine("[AndroidBuilder] arquiteturas: "
                         + PlayerSettings.Android.targetArchitectures);

            // ---- sdk ----
            PlayerSettings.Android.minSdkVersion    = AndroidSdkVersions.AndroidApiLevel25;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;

            // ---- orientacao ----
            // A tela e desenhada em paisagem. Liberar retrato num celular
            // espremeria todo o layout.
            PlayerSettings.defaultInterfaceOrientation           = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait           = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft      = true;
            PlayerSettings.allowedAutorotateToLandscapeRight     = true;

            // ---- assinatura ----
            // Sem keystore proprio o Unity assina com a debug key, que e o
            // suficiente para instalar e testar.
            PlayerSettings.Android.useCustomKeystore = false;

            // APK, nao AAB - AAB nao se instala direto no aparelho.
            EditorUserBuildSettings.buildAppBundle = false;

            AssetDatabase.SaveAssets();
        }

        // =====================================================
        //  CONSERTO OPCIONAL DO PROJETO
        // =====================================================

        [MenuItem("Tools/Jogo/Build/Corrigir cenas do Build Settings", false, 320)]
        public static void FixBuildSettingsScenes()
        {
            if (!EditorUtility.DisplayDialog(
                    "Corrigir a lista de cenas?",
                    "Hoje o Build Settings abre na SampleScene, que so tem uma camera e uma luz, "
                    + "e a Bootstrap - que carrega o GameManager e o banco - esta desabilitada. "
                    + "Qualquer build (Android ou Windows) abre numa tela vazia.\n\n"
                    + "Isto deixa a lista assim:\n"
                    + "  0 - Bootstrap\n"
                    + "  1 - GameScene\n"
                    + "  2 - SampleScene (desabilitada)",
                    "Corrigir", "Cancelar"))
                return;

            var novas = new EditorBuildSettingsScene[]
            {
                new EditorBuildSettingsScene("Assets/Scenes/Bootstrap.unity", true),
                new EditorBuildSettingsScene("Assets/Scenes/GameScene.unity", true),
                new EditorBuildSettingsScene("Assets/Scenes/SampleScene.unity", false)
            };

            EditorBuildSettings.scenes = novas;

            Debug.Log("[AndroidBuilder] Build Settings corrigido: Bootstrap agora e a cena 0.");
        }
    }
}
#endif
