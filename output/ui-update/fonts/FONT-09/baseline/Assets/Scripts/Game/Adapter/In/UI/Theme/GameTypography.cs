using TMPro;
using UnityEngine;

namespace Game.Adapter.In.UI.Theme
{
    /// <summary>Shared typography roles in 1920x1080 design units. See visual-contract.json.</summary>
    public static class GameTypography
    {
        public const string FontAssetPath = "Assets/TextMesh Pro/Fonts/Concept/SDF/Roboto-Regular.asset";
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
        public const float LineSpacing = 0f; // Native face metrics; per-screen sizing is validated separately.
        public const float CharacterSpacing = 0f;
        public const float ShadowOffsetX = 0f;
        public const float ShadowOffsetY = 0f;
        public const float ShadowAlpha = 0f;

        private static ConceptFontSet fonts;
        public static ConceptFontSet Fonts => fonts != null ? fonts : fonts = Resources.Load<ConceptFontSet>("UI/ConceptFontSet");

        public static TypographyStyle Resolve(TypographyRole role)
        {
            var set = Fonts;
            if (set == null) throw new System.InvalidOperationException("Missing UI/ConceptFontSet resource.");
            switch (role)
            {
                case TypographyRole.Brand: return new TypographyStyle(set.robotoBold, BrandTitle, "Roboto", 700);
                case TypographyRole.ScreenTitle: return new TypographyStyle(set.robotoBold, ScreenTitle, "Roboto", 700);
                case TypographyRole.SectionTitle: return new TypographyStyle(set.condensedBold, SectionTitle, "Roboto Condensed", 700);
                case TypographyRole.Kpi: return new TypographyStyle(set.robotoBold, DisplayValue, "Roboto", 700);
                case TypographyRole.TableTotal: return new TypographyStyle(set.robotoBold, Table, "Roboto", 700);
                case TypographyRole.Button: return new TypographyStyle(set.condensedSemiBold, Button, "Roboto Condensed", 600);
                case TypographyRole.MapMarker: return new TypographyStyle(set.condensedSemiBold, NodeTitle, "Roboto Condensed", 600);
                case TypographyRole.Navigation: case TypographyRole.Label: return new TypographyStyle(set.condensedMedium, Label, "Roboto Condensed", 500);
                case TypographyRole.Caption: return new TypographyStyle(set.condensedRegular, Caption, "Roboto Condensed", 400);
                case TypographyRole.Input: return new TypographyStyle(set.robotoRegular, Input, "Roboto", 400);
                case TypographyRole.Table: return new TypographyStyle(set.robotoRegular, Table, "Roboto", 400);
                default: return new TypographyStyle(set.robotoRegular, Body, "Roboto", 400);
            }
        }

        // Explicit tokens keep the old dimensions while carrying semantic roles through factory helpers.
        public static class Tokens
        {
            public static TypographyToken BrandTitle => new TypographyToken(TypographyRole.Brand, GameTypography.BrandTitle);
            public static TypographyToken ScreenTitle => new TypographyToken(TypographyRole.ScreenTitle, GameTypography.ScreenTitle);
            public static TypographyToken ScreenHint => Body;
            public static TypographyToken SectionTitle => new TypographyToken(TypographyRole.SectionTitle, GameTypography.SectionTitle);
            public static TypographyToken DisplayValue => new TypographyToken(TypographyRole.Kpi, GameTypography.DisplayValue);
            public static TypographyToken Body => new TypographyToken(TypographyRole.Body, GameTypography.Body);
            public static TypographyToken BodyStrong => new TypographyToken(TypographyRole.TableTotal, GameTypography.Body);
            public static TypographyToken Button => new TypographyToken(TypographyRole.Button, GameTypography.Button);
            public static TypographyToken Input => new TypographyToken(TypographyRole.Input, GameTypography.Input);
            public static TypographyToken Label => new TypographyToken(TypographyRole.Label, GameTypography.Label);
            public static TypographyToken Caption => new TypographyToken(TypographyRole.Caption, GameTypography.Caption);
            public static TypographyToken Table => new TypographyToken(TypographyRole.Table, GameTypography.Table);
            public static TypographyToken TableTotal => new TypographyToken(TypographyRole.TableTotal, GameTypography.Table);
            public static TypographyToken MapBarTitle => SectionTitle;
            public static TypographyToken MapBarSubtitle => Body;
            public static TypographyToken PillCaption => Caption;
            public static TypographyToken PillValue => new TypographyToken(TypographyRole.Kpi, GameTypography.PillValue);
            public static TypographyToken NodeTitle => new TypographyToken(TypographyRole.MapMarker, GameTypography.NodeTitle);
            public static TypographyToken NodeSubtitle => Caption;
        }

        public static void Apply(TMP_Text text, TypographyRole role, float? size = null)
        {
            if (text == null) return;
            var binding = text.GetComponent<TypographyRoleBinding>() ?? text.gameObject.AddComponent<TypographyRoleBinding>();
            binding.SetRole(role);
            ApplyFace(text, role);
            text.fontSize = size ?? Resolve(role).Size;
        }
        public static void KeepSize(TMP_Text text, TypographyRole role)
        { if (text != null) Apply(text, role, text.fontSize); }

        internal static void ApplyFace(TMP_Text text, TypographyRole role)
        {
            var style = Resolve(role);
            text.font = style.Font; text.fontSharedMaterial = style.Material;
            // Real face encodes weight. TMP weight tables still handle valid inline <b>/<font-weight>.
            text.fontStyle &= ~FontStyles.Bold;
            text.fontWeight = FontWeight.Regular;
            text.enableAutoSizing = false;
        }

        public static void Apply(TMP_Text text, TypographyToken token, bool bold = false)
        {
            if (text == null) return;
            var binding = text.GetComponent<TypographyRoleBinding>();
            var role = token.Role ?? (binding != null ? binding.Role : bold ? TypographyRole.TableTotal : TypographyRole.Body);
            Apply(text, role, token.Size);
            text.characterSpacing = CharacterSpacing; text.lineSpacing = LineSpacing;
            text.wordSpacing = 0; text.paragraphSpacing = 0; text.margin = Vector4.zero;
            text.enableVertexGradient = false; text.raycastTarget = false;
        }

        // Compatibility for callers not yet assigning a role. A saved binding takes precedence over legacy font/bold.
        public static void Apply(TMP_Text text, TMP_FontAsset font, float size, bool bold = false)
            => Apply(text, (TypographyToken)size, bold);
    }
}
