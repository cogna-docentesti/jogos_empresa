using System;
using System.IO;
using System.Linq;
using Game.Adapter.In.UI.Theme;
using Game.Adapter.Out.Simulation;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>Keep the event artwork registry current without rewriting scenes or saved campaigns.</summary>
    public sealed class EventArtCoverageMigration : AssetPostprocessor, IPreprocessBuildWithReport
    {
        private static bool scheduled;
        public int callbackOrder => 0;

        [InitializeOnLoadMethod]
        private static void Schedule()
        {
            if (scheduled) return;
            scheduled = true;
            EditorApplication.update += WhenReady;
        }

        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (imported.Any(p => p.StartsWith("Assets/Art/new/events/", StringComparison.Ordinal))) Schedule();
        }

        private static void WhenReady()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            scheduled = false;
            EditorApplication.update -= WhenReady;
            Sync();
        }

        [MenuItem("Tools/Jogos de Empresa/Events/Sync and audit illustrations")]
        private static void SyncMenu() => Sync();

        public static string[] Sync()
        {
            var skin = AssetDatabase.LoadAssetAtPath<SharedUiSkin>("Assets/Resources/SharedUiSkin.asset");
            if (skin == null) throw new InvalidOperationException("SharedUiSkin.asset indisponível.");
            var entries = skin.entries.ToList();
            bool changed = false;
            foreach (var rule in SharedUiImportRules.Read().Where(r => r.kind == "evento"))
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/new/" + rule.path);
                if (sprite == null) continue;
                int index = entries.FindIndex(e => e.id == rule.id);
                if (index >= 0 && entries[index].sprite == sprite) continue;
                var entry = new SharedUiSkin.Entry { id = rule.id, sprite = sprite };
                if (index >= 0) entries[index] = entry; else entries.Add(entry);
                changed = true;
            }
            if (changed)
            {
                skin.entries = entries.ToArray();
                EditorUtility.SetDirty(skin);
                AssetDatabase.SaveAssetIfDirty(skin);
            }

            var config = Resources.Load<SimulationConfigAsset>(UnitySimulationCatalogProvider.ConfigResourcePath);
            var ids = Resources.LoadAll<EventData>("Events").Select(e => e.id)
                .Concat(config == null ? Array.Empty<string>() : config.events.Select(e => e.id))
                .Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal).ToArray();
            var missing = ids.Where(id => skin.Get("EVENT_" + id.ToUpperInvariant()) == null).ToArray();
            Directory.CreateDirectory("Temp/EventArt");
            File.WriteAllText("Temp/EventArt/coverage.json", Newtonsoft.Json.JsonConvert.SerializeObject(new
            {
                checkedAt = DateTime.UtcNow, total = ids.Length, missing,
                events = ids.Select(id => new { id, image = AssetDatabase.GetAssetPath(skin.Get("EVENT_" + id.ToUpperInvariant())) })
            }, Newtonsoft.Json.Formatting.Indented));
            return missing;
        }

        public void OnPreprocessBuild(BuildReport report)
        {
            var missing = Sync();
            if (missing.Length > 0) throw new BuildFailedException("Eventos sem imagem: " + string.Join(", ", missing));
        }
    }
}
