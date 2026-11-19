using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;

/// <summary>Imports and inspects FONT-02 source files in the open Editor; never saves scenes or creates TMP assets.</summary>
public static class Font02SourceValidation
{
    const string Output = "output/ui-update/fonts/FONT-02";
    static string Hash(string value)
    {
        using var sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", "").ToLowerInvariant();
    }

    static object SceneState()
    {
        var scenes = Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt).ToArray();
        var texts = scenes.SelectMany(s => s.GetRootGameObjects()).SelectMany(r => r.GetComponentsInChildren<TMP_Text>(true))
            .OrderBy(t => t.GetInstanceID()).ToArray();
        return new {
            scenes = scenes.Select(s => new {s.path, s.isDirty, s.isLoaded, roots = s.rootCount}).ToArray(),
            texts = texts.Length,
            fingerprint = Hash(string.Join("\n", texts.Select(t => JsonUtility.ToJson(t)))),
            unsavedRestaurant = texts.Count(t => t.name == "Text (TMP)" && t.text == "Restaurante")
        };
    }

    [MenuItem("Tools/Jogos de Empresa/FONT-02/Import and validate source faces %&F2")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Run FONT-02 in Edit mode.");
        Directory.CreateDirectory(Output);
        var before = SceneState();
        var failures = new List<string>();
        var logs = new List<string>();
        var faces = new List<object>();
        void Capture(string message, string trace, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) logs.Add(message); }
        Application.logMessageReceived += Capture;
        try
        {
            var manifest = JObject.Parse(File.ReadAllText("docs/ui-update/font-sources.json"));
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            FontEngine.InitializeFontEngine();
            foreach (var row in manifest["static_faces"])
            {
                var path = (string)row["path"];
                string guidBeforeReimport = AssetDatabase.AssetPathToGUID(path);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                var font = AssetDatabase.LoadAssetAtPath<Font>(path);
                var importer = AssetImporter.GetAtPath(path) as TrueTypeFontImporter;
                var status = font != null ? FontEngine.LoadFontFace(font, 90) : (FontEngineError)(-1);
                if (font == null || importer == null || status != FontEngineError.Success)
                { failures.Add(path + ": import/load failed " + status); continue; }
                var info = FontEngine.GetFaceInfo();
                if (info.familyName != (string)row["names"]["1"] || info.styleName != (string)row["names"]["2"])
                    failures.Add(path + ": wrong FreeType family/style: " + info.familyName + "/" + info.styleName);
                var samples = "AHMãoç0123".Select(c => {
                    bool found = FontEngine.TryGetGlyphWithUnicodeValue(c, GlyphLoadFlags.LOAD_NO_HINTING, out var glyph);
                    if (!found) failures.Add(path + ": missing sample " + c);
                    return new { character = c.ToString(), found, index = glyph?.index, metrics = glyph?.metrics };
                }).ToArray();
                var sourceGuid = AssetDatabase.AssetPathToGUID(path);
                bool emptyFallbacks = File.ReadAllText(path + ".meta").Contains("fallbackFontReferences: []");
                if (string.IsNullOrEmpty(sourceGuid) || sourceGuid != guidBeforeReimport || !importer.includeFontData || !emptyFallbacks)
                    failures.Add(path + ": unstable GUID or font data/fallback settings");
                faces.Add(new { path, guid = sourceGuid, importer = importer.GetType().Name,
                    info.familyName, info.styleName, info.pointSize, info.scale,
                    includeFontData = importer.includeFontData, emptyFallbacks, guidStableOnReimport = sourceGuid == guidBeforeReimport,
                    status = status.ToString(), samples });
            }
            if (faces.Count != 6) failures.Add("Expected six imported static faces.");
        }
        catch (Exception e) { failures.Add(e.ToString()); }
        finally { Application.logMessageReceived -= Capture; }
        var after = SceneState();
        bool preserved = JsonConvert.SerializeObject(before) == JsonConvert.SerializeObject(after);
        if (!preserved) failures.Add("Open scene changed during source validation.");
        var logEntries = typeof(Editor).Assembly.GetType("UnityEditor.LogEntries");
        object[] counts = {0, 0, 0};
        logEntries.GetMethod("GetCountsByType", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, counts);
        if ((int)counts[0] != 0) failures.Add("Console contains errors: " + counts[0]);
        var report = new { task = "FONT-02", unity = Application.unityVersion,
            utc = DateTime.UtcNow.ToString("o"), passed = failures.Count == 0 && logs.Count == 0,
            faces, failures, importErrors = logs, consoleErrors = counts[0], consoleWarnings = counts[1],
            before, after, scenePreserved = preserved, sceneSaved = false, tmpAssetsCreated = false };
        File.WriteAllText(Output + "/unity-import.json", JsonConvert.SerializeObject(report, Formatting.Indented) + "\n");
        Debug.Log("FONT-02 source validation: " + (report.passed ? "PASS" : "FAIL") + "; report: " + Output + "/unity-import.json");
    }
}
