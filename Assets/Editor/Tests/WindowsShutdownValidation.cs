using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.Build.Profile;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>Build the existing Windows profile for the native shutdown regression.</summary>
public static class WindowsShutdownValidation
{
    public static void BuildQa() => Build(true, false);
    public static void BuildDevelopmentQa() => Build(true, true);
    public static void BuildRelease() => Build(false, false);

    private static void Build(bool qa, bool development)
    {
        string output = Environment.GetEnvironmentVariable("COGNA_SHUTDOWN_BUILD_OUTPUT");
        if (string.IsNullOrWhiteSpace(output)) throw new InvalidOperationException("Build output directory required.");
        Directory.CreateDirectory(output);
        var profile = AssetDatabase.LoadAssetAtPath<BuildProfile>("Assets/Settings/Build Profiles/New Windows Profile.asset");
        if (profile == null) throw new InvalidOperationException("Existing Windows profile missing.");
        var previousProfile = BuildProfile.GetActiveBuildProfile();
        var defines = profile.scriptingDefines;
        var state = new SerializedObject(profile);
        bool hadDefines = state.FindProperty("m_HasScriptingDefines").boolValue;
        try
        {
            if (qa)
            {
                profile.scriptingDefines = defines.Concat(new[] { "FONT10_QA_PLAYER" }).Distinct().ToArray();
                state.Update();
                state.FindProperty("m_HasScriptingDefines").boolValue = true;
                state.ApplyModifiedPropertiesWithoutUndo();
            }
            BuildProfile.SetActiveBuildProfile(profile);
            string path = Path.Combine(Path.GetFullPath(output), "JogosDeEmpresa.exe");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerWithProfileOptions
            {
                buildProfile = profile,
                locationPathName = path,
                options = development ? BuildOptions.Development : BuildOptions.None
            });
            File.WriteAllText(Path.Combine(output, "build.json"), JsonConvert.SerializeObject(new
            {
                passed = report.summary.result == BuildResult.Succeeded,
                unity = Application.unityVersion,
                result = report.summary.result.ToString(),
                output = path, qa, development,
                errors = report.summary.totalErrors,
                warnings = report.summary.totalWarnings,
                seconds = report.summary.totalTime.TotalSeconds,
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path),
                steps = report.steps.Select(s => new { s.name, messages = s.messages.Select(m => new { type = m.type.ToString(), m.content }) })
            }, Formatting.Indented));
            if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Windows build failed.");
        }
        finally
        {
            profile.scriptingDefines = defines;
            state.Update();
            state.FindProperty("m_HasScriptingDefines").boolValue = hadDefines;
            state.ApplyModifiedPropertiesWithoutUndo();
            BuildProfile.SetActiveBuildProfile(previousProfile);
        }
    }
}
