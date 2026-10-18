using TMPro;
using UnityEngine;

namespace Game.Adapter.In.UI.Theme
{
    /// <summary>Player-safe references to the real FONT-03 faces. Role resolution belongs to the theme.</summary>
    public sealed class ConceptFontSet : ScriptableObject
    {
        public TMP_FontAsset robotoRegular;
        public TMP_FontAsset robotoBold;
        public TMP_FontAsset condensedRegular;
        public TMP_FontAsset condensedMedium;
        public TMP_FontAsset condensedSemiBold;
        public TMP_FontAsset condensedBold;
    }
}
