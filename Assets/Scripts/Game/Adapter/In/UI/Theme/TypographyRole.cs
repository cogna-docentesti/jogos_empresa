using TMPro;
using UnityEngine;

namespace Game.Adapter.In.UI.Theme
{
    public enum TypographyRole { Body, Brand, ScreenTitle, SectionTitle, Input, Table, TableTotal, Kpi, Button, Navigation, Label, Caption, MapMarker }

    public readonly struct TypographyStyle
    {
        public readonly TMP_FontAsset Font;
        public Material Material => Font.material;
        public readonly float Size;
        public readonly string Family;
        public readonly int Weight;
        public TypographyStyle(TMP_FontAsset font, float size, string family, int weight)
        { Font = font; Size = size; Family = family; Weight = weight; }
    }

    public readonly struct TypographyToken
    {
        public readonly TypographyRole? Role;
        public readonly float Size;
        public TypographyToken(TypographyRole role, float size) { Role = role; Size = size; }
        private TypographyToken(float size) { Role = null; Size = size; }
        public static implicit operator TypographyToken(float size) => new TypographyToken(size);
        public static implicit operator float(TypographyToken token) => token.Size;
    }
}
