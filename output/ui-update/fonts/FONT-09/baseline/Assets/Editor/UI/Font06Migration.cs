using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Adapter.In.UI;
using Game.Adapter.In.UI.Theme;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.EditorTools
{
    /// <summary>Typography-only edits to frozen consumers; saves a disk copy without saving the user's dirty scene.</summary>
    public static class Font06Migration
    {
        public const string Output = "output/ui-update/fonts/FONT-06";
        const string Production = "Assets/Scenes/GameScene.unity";
        const string Copy = "Assets/Scenes/Font06TypographyCopy.unity";
        [InitializeOnLoadMethod] static void ResumeAuthorizedRequest()
        {
            // Local one-shot entry point for this migration in an already open Editor without Pipeline.
            // Requests and results stay in the task's evidence directory; no second Editor is started.
            EditorApplication.update -= ProcessRequest;
            EditorApplication.update += ProcessRequest;
        }
        static double nextRequestCheck;
        static void ProcessRequest()
        {
                if (EditorApplication.timeSinceStartup < nextRequestCheck || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || SessionState.GetBool("Cogna.Font06.Restoring", false)) return;
                nextRequestCheck = EditorApplication.timeSinceStartup + 1;
            {
                const string request = Output + "/editor-request.txt";
                if (!File.Exists(request)) return;
                string action = File.ReadAllText(request).Trim(); File.Delete(request);
                try
                {
                    if (action == "migrate") Run();
                    else if (action == "validate") Font06Validation.Run();
                    else if (action == "commit-check") Font06Validation.ValidateCommitScene();
                    else throw new InvalidOperationException("Unknown FONT-06 request");
                }
                catch (Exception error) { File.WriteAllText(Output + "/editor-request-error.txt", error.ToString()); Debug.LogException(error); }
            }
        }
        static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;
        public static IEnumerable<JToken> Consumers => JObject.Parse(File.ReadAllText("docs/ui-update/font-coverage.json"))["consumers"]
            .Where(c => (string)c["kind"] == "serialized" && (string)c["owner"] == "FONT-06" && (string)c["scope"] != "scene-outside-build");
        public static void ApplyText(TMP_Text text, TypographyRole role)
        {
            GameTypography.KeepSize(text, role);
            if (role == TypographyRole.Input) text.fontStyle &= ~FontStyles.Italic;
            else if (text.overflowMode == TextOverflowModes.Truncate || text.overflowMode == TextOverflowModes.Ellipsis)
                text.overflowMode = TextOverflowModes.Overflow;
            EditorUtility.SetDirty(text); EditorUtility.SetDirty(text.GetComponent<TypographyRoleBinding>());
            PrefabUtility.RecordPrefabInstancePropertyModifications(text);
        }
        public static List<object> Apply(GameObject[] roots, string asset)
        {
            var rows = new List<object>();
            var texts = roots.SelectMany(r => r.GetComponentsInChildren<TMP_Text>(true)).ToArray();
            // hierarchy_key is the frozen inventory identity, not a naming heuristic.
            var map = Consumers.Where(c => (string)c["asset"] == asset).ToDictionary(c => (string)c["hierarchy_key"]);
            foreach (var text in texts)
            {
                if (!map.TryGetValue(PathOf(text.transform), out var c)) continue;
                var role = FontRoleMigration.ParseRole((string)c["target"]["role"]);
                ApplyText(text, role);
                if (c["font06_layout"] is JObject layout)
                {
                    if (layout["size"] != null) text.fontSize=(float)layout["size"];
                    var r=text.rectTransform;
                    if (layout["height"] != null) r.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,(float)layout["height"]);
                    if (layout["top"] != null) r.anchoredPosition=new Vector2(r.anchoredPosition.x,-(float)layout["top"]);
                    if ((bool?)layout["native_flow_height"] == true && text.TryGetComponent<UnityEngine.UI.LayoutElement>(out var flow))
                    {
                        flow.preferredHeight = -1;
                        EditorUtility.SetDirty(flow); PrefabUtility.RecordPrefabInstancePropertyModifications(flow);
                    }
                    EditorUtility.SetDirty(r); PrefabUtility.RecordPrefabInstancePropertyModifications(r);
                }
                rows.Add(new { id = (string)c["id"], asset, hierarchy = PathOf(text.transform), role = role.ToString(), font = text.font.name, size = text.fontSize,
                    active = text.gameObject.activeInHierarchy, material = text.fontSharedMaterial.name, text.enableAutoSizing, overflow = text.overflowMode.ToString() });
            }
            foreach (var input in roots.SelectMany(r => r.GetComponentsInChildren<TMP_InputField>(true)))
            {
                if (input.textComponent == null || !map.ContainsKey(PathOf(input.textComponent.transform))) continue;
                input.fontAsset = GameTypography.Resolve(TypographyRole.Input).Font;
                input.pointSize = input.textComponent.fontSize;
                EditorUtility.SetDirty(input); PrefabUtility.RecordPrefabInstancePropertyModifications(input);
            }
            return rows;
        }
        [MenuItem("Tools/Jogos de Empresa/FONT-06/Integrate serialized typography %&F9")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Use Edit mode.");
            var live = SceneManager.GetSceneByPath(Production);
            if (!live.IsValid() || !live.isLoaded) throw new InvalidOperationException("Keep the existing GameScene open.");
            Directory.CreateDirectory(Output);
            var backup = Output + "/live-session-before.unity";
            if (!File.Exists(backup)) EditorSceneManager.SaveScene(live, backup, true);
            var rows = new List<object>();
            foreach (var group in Consumers.Where(c => ((string)c["asset"]).EndsWith(".prefab")).GroupBy(c => (string)c["asset"]))
            {
                var root = PrefabUtility.LoadPrefabContents(group.Key);
                try { rows.AddRange(Apply(new[] { root }, group.Key)); PrefabUtility.SaveAsPrefabAsset(root, group.Key); }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            // A separate on-disk copy contains no unsaved session delta. Its local file IDs remain stable.
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(Copy) != null) AssetDatabase.DeleteAsset(Copy);
            File.Copy(Production, Copy); AssetDatabase.ImportAsset(Copy, ImportAssetOptions.ForceSynchronousImport);
            var isolated = EditorSceneManager.OpenScene(Copy, OpenSceneMode.Additive);
            try
            {
                rows.AddRange(Apply(isolated.GetRootGameObjects(), Production));
                EditorSceneManager.MarkSceneDirty(isolated); EditorSceneManager.SaveScene(isolated);
            }
            finally { EditorSceneManager.CloseScene(isolated, true); SceneManager.SetActiveScene(live); }
            // Truncating a loaded scene is forbidden on Windows. Atomic replacement preserves its mapped version and dirty in-memory hierarchy.
            File.Replace(Copy, Production, Output + "/disk-before-atomic.unity");
            AssetDatabase.DeleteAsset(Copy);
            if (!File.ReadAllText(Production).Contains("  role: ")) throw new InvalidOperationException("Serialized roles were not saved.");
            // Apply only authorized typography in memory; never save or replace the user's loaded hierarchy.
            Apply(live.GetRootGameObjects(), Production); EditorSceneManager.MarkSceneDirty(live);
            File.WriteAllText(Output + "/serialized-consumers.json", JsonConvert.SerializeObject(new { task = "FONT-06", consumers = rows, count = rows.Count,
                saved = Production, live.isDirty, unsavedRestaurant = live.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<TMP_Text>(true)).Count(t => t.name == "Text (TMP)" && t.text == "Restaurante") }, Formatting.Indented));
            Debug.Log("FONT-06 serialized typography saved; original dirty session retained.");
        }
    }
}
