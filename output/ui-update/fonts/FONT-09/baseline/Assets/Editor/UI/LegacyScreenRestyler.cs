#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace Game.EditorTools
{
    /// <summary>Compatibility entry points. UI-02 owns the final style and never multiplies sizes.</summary>
    public static class LegacyScreenRestyler
    {
        [MenuItem("Tools/Jogo/Telas antigas/Apply shared theme")]
        public static void Apply() => GlobalThemeMigration.Apply();
        public static void DryRun() => Debug.Log("Shared theme: GamePalette / GameTypography; no legacy color remapping.");
        public static void DryRunFonts() => DryRun();
        public static void ApplyFonts() => Debug.LogWarning("The legacy +25% font operation is retired. Use the UI-02 shared roles.");
    }
}
#endif
