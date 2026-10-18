#if DEVELOPMENT_BUILD || FONT10_QA_PLAYER
using System;
using System.Collections;
using System.IO;
using Game.Adapter.In.UI;
using Newtonsoft.Json;
using UnityEngine;

/// <summary>Opt-in native shutdown regression probe; requires disposable QA storage.</summary>
public sealed class WindowsShutdownPlayerValidation : MonoBehaviour
{
    private string output;
    private string mode;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        var args = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(args, "--windows-shutdown-qa");
        if (Application.isEditor || index < 0) return;
        if (index + 1 >= args.Length) throw new ArgumentException("Shutdown QA mode required.");
        var root = new GameObject("Windows shutdown regression validation");
        DontDestroyOnLoad(root);
        var probe = root.AddComponent<WindowsShutdownPlayerValidation>();
        probe.mode = args[index + 1];
        probe.output = Environment.GetEnvironmentVariable("COGNA_SHUTDOWN_QA_OUTPUT");
        Application.runInBackground = true;
        probe.StartCoroutine(probe.Run());
    }

    private void Write(string name, object value) => File.WriteAllText(
        Path.Combine(output, name + ".json"), JsonConvert.SerializeObject(value, Formatting.Indented));

    private IEnumerator Run()
    {
        while (Time.frameCount < 20 || GameManager.Instance == null || DatabaseInitializer.DatabaseService?.Connection == null)
            yield return null;

        string path = Environment.GetEnvironmentVariable("COGNA_TEST_DATABASE_PATH");
        string key = Environment.GetEnvironmentVariable("COGNA_TEST_IDENTIFICATION_KEY");
        if (string.IsNullOrWhiteSpace(output) || string.IsNullOrWhiteSpace(path)
            || string.IsNullOrWhiteSpace(key) || !key.StartsWith("Cogna.Shutdown.QA.", StringComparison.Ordinal)
            || Path.GetFullPath(path) != Path.GetFullPath(Path.Combine(output, "qa.db"))
            || Path.GetFullPath(path) == Path.GetFullPath(Path.Combine(Application.persistentDataPath, "game.db"))
            || DatabaseInitializer.DatabaseService.Connection.DatabasePath != path)
            throw new InvalidOperationException("Shutdown validation requires isolated database and preferences.");

        if (mode != "window-close" && mode != "application-quit" && mode != "save" && mode != "discard")
            throw new ArgumentException("Unknown shutdown QA mode: " + mode);
        // GameManager has already created the initial in-memory draft at startup.
        var session = GameSessionState.Current
            ?? new GameSessionService("windows-shutdown-qa", "qa").CreateNewSession();
        session.menuPricingJson = "{\"items\":[{\"productId\":\"TEST\",\"selectedPrice\":25}]}";
        session.currentCash = 1000;
        if (!GameSessionState.Persist()) throw new InvalidOperationException("Could not seed isolated save.");
        GameSessionState.AddCash(129, false);
        Write("ready", new { session.sessionId, initialCash = 1000, pendingCash = 1129, mode, unity = Application.unityVersion, development = Debug.isDebugBuild, frame = Time.frameCount });

        // The external driver first exercises the Windows UI Automation provider.
        if (mode == "window-close") yield break;
        while (!File.Exists(Path.Combine(output, "continue.txt"))) yield return null;
        if (mode == "application-quit") Application.Quit();
        else
        {
            var dialog = FindFirstObjectByType<ExitDialogController>(FindObjectsInactive.Include);
            if (dialog == null) throw new InvalidOperationException("Missing real exit dialog.");
            dialog.Open();
            if (!dialog.IsOpen || dialog.CurrentMode != ExitDialogController.ExitMode.UnsavedChanges)
                throw new InvalidOperationException("The exit dialog did not show the pending save.");
            yield return null;
            dialog.Quit(mode == "save");
        }
    }

    private void OnApplicationQuit()
    {
        if (!string.IsNullOrEmpty(output)) Write("quit-callback", new { mode, frame = Time.frameCount });
    }
}
#endif
