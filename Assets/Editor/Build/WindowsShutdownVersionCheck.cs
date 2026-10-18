using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>Prevent shipping Windows Players with the known UI Automation shutdown crash.</summary>
public sealed class WindowsShutdownVersionCheck : IPreprocessBuildWithReport
{
    public int callbackOrder => -1000;

    public void OnPreprocessBuild(BuildReport report)
    {
        if (report.summary.platform != BuildTarget.StandaloneWindows
            && report.summary.platform != BuildTarget.StandaloneWindows64) return;

        const string prefix = "6000.3.";
        string version = Application.unityVersion;
        if (!version.StartsWith(prefix, System.StringComparison.Ordinal)) return;
        string patch = version.Substring(prefix.Length).Split('a', 'b', 'f', 'p')[0];
        if (int.TryParse(patch, out int number) && number < 21)
            throw new BuildFailedException(
                "Windows builds require Unity 6000.3.21f1 or a later patched release. "
                + "Earlier 6000.3 versions crash at shutdown after Windows UI Automation interaction "
                + "(UUM-146676). Reopen this project with its pinned Editor version. "
                + "https://unity.com/releases/editor/whats-new/6000.3.21f1");
    }
}
