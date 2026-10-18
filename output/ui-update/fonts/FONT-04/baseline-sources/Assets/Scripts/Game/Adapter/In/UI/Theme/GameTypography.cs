using TMPro;
using UnityEngine;

namespace Game.Adapter.In.UI.Theme
{
    /// <summary>Shared Inter Tight roles in 1920x1080 design units. See visual-contract.json.</summary>
    public static class GameTypography
    {
        public const string FontAssetPath = "Assets/TextMesh Pro/Fonts/InterTight-Variable.asset";
        public const float ScreenTitle = 44f;
        public const float ScreenHint = 24f;
        public const float SectionTitle = 32f;
        public const float DisplayValue = 42f;
        public const float Body = 24f;
        public const float Button = 24f;
        public const float Input = 24f;
        public const float Label = 22f;
        public const float Caption = 20f;
        public const float Table = 22f;
        public const float BrandTitle = 36f;
        public const float MapBarTitle = SectionTitle;
        public const float MapBarSubtitle = Body;
        public const float PillCaption = Caption;
        public const float PillValue = Body;
        public const float NodeTitle = Body;
        public const float NodeSubtitle = Caption;
        public const float LineSpacing = 0f; // Native Inter Tight line height: 1.21 em.
        public const float CharacterSpacing = 0f;
        public const float ShadowOffsetX = 0f;
        public const float ShadowOffsetY = 0f;
        public const float ShadowAlpha = 0f;

        public static void Apply(TMP_Text text, TMP_FontAsset font, float size, bool bold = false)
        {
            text.font = font; text.fontSharedMaterial = font.material;
            text.fontSize = size; text.enableAutoSizing = false;
            text.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            text.fontWeight = bold ? FontWeight.Bold : FontWeight.Regular;
            text.characterSpacing = CharacterSpacing; text.lineSpacing = LineSpacing;
            text.wordSpacing = 0; text.paragraphSpacing = 0; text.margin = Vector4.zero;
            text.enableVertexGradient = false; text.raycastTarget = false;
        }
    }
}
