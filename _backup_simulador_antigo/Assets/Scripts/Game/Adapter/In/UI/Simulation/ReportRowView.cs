using Game.Adapter.In.UI.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Adapter.In.UI.Simulation
{
    /// <summary>Linha "rotulo ..... valor" dos relatorios (prefab ReportRow).</summary>
    public sealed class ReportRowView : MonoBehaviour
    {
        [SerializeField] private TMP_Text labelText;
        [SerializeField] private TMP_Text valueText;
        [SerializeField] private Image background;
        [Tooltip("Recuo em pixels por nivel (linhas de detalhe da DRE).")]
        [SerializeField] private float indentPerLevel = 32f;

        public void Set(string label, string value, Color? valueColor = null, bool emphasis = false, int indent = 0)
        {
            if (labelText != null)
            {
                labelText.text = emphasis ? "<b>" + label + "</b>" : label;
                labelText.color = indent > 0 ? GamePalette.Muted : GamePalette.Ink;
                labelText.margin = new Vector4(indent * indentPerLevel, 0f, 0f, 0f);
            }
            if (valueText != null)
            {
                valueText.text = emphasis ? "<b>" + value + "</b>" : value;
                valueText.color = valueColor ?? GamePalette.Ink;
            }
            if (background != null) background.color = emphasis ? GamePalette.SurfaceAlt : GamePalette.WithAlpha(GamePalette.Surface, 0f);
        }

        /// <summary>Linha de texto corrido (feedback, eventos): ocupa a largura toda.</summary>
        public void SetParagraph(string text, Color color)
        {
            if (labelText != null)
            {
                labelText.text = text;
                labelText.color = color;
                labelText.margin = Vector4.zero;
            }
            if (valueText != null) valueText.text = string.Empty;
            if (background != null) background.color = GamePalette.WithAlpha(GamePalette.Surface, 0f);
        }
    }
}
