using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Adapter.In.UI.Theme;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEditor;

namespace Game.EditorTools
{
    /// <summary>Frozen FONT-01 identities, never guesses semantic roles from GameObject names.</summary>
    public static class FontRoleMigration
    {
        [MenuItem("Tools/Jogos de Empresa/FONT-04/Set Player default font")]
        public static void ConfigureDefault()
        {
            var settings = AssetDatabase.LoadAssetAtPath<TMP_Settings>("Assets/TextMesh Pro/Resources/TMP Settings.asset");
            var serialized = new SerializedObject(settings);
            serialized.FindProperty("m_defaultFontAsset").objectReferenceValue = GameTypography.Resolve(TypographyRole.Body).Font;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssetIfDirty(settings);
        }
        static Dictionary<string, TypographyRole> roles;
        public static TypographyRole ParseRole(string role) => (TypographyRole)Enum.Parse(typeof(TypographyRole), role.Replace("_", ""), true);
        public static bool TryApply(TMP_Text text)
        {
            var binding = text.GetComponent<TypographyRoleBinding>();
            if (binding != null) { GameTypography.KeepSize(text, binding.Role); return true; }
            if (roles == null)
                roles = JObject.Parse(File.ReadAllText("docs/ui-update/font-coverage.json"))["consumers"]
                    .Where(c => (string)c["kind"] == "serialized" && !string.IsNullOrEmpty((string)c["global_id"]))
                    .ToDictionary(c => (string)c["global_id"], c => ParseRole((string)c["target"]["role"]));
            if (!roles.TryGetValue(GlobalObjectId.GetGlobalObjectIdSlow(text).ToString(), out var role)) return false;
            GameTypography.KeepSize(text, role); return true;
        }
    }
}
