using System;
using Game.Adapter.In.UI.Theme;
using Game.Domain.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Adapter.In.UI.Simulation
{
    /// <summary>
    /// Um botao de reacao no popup do evento (prefab ReactionOption). Mostra titulo, custo,
    /// descricao e, quando indisponivel, o motivo (ex.: "Caixa insuficiente").
    /// </summary>
    public sealed class ReactionOptionView : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Image background;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text costText;
        [SerializeField] private TMP_Text descriptionText;

        public void Bind(ReactionOption option, Action<string> onChoose)
        {
            if (titleText != null) titleText.text = option.Title;
            if (costText != null)
            {
                costText.text = option.Cost > 0.004d ? SimFormat.Money(-option.Cost) : "Sem custo imediato";
                costText.color = option.Cost > 0.004d ? GamePalette.Danger : GamePalette.Muted;
            }
            if (descriptionText != null)
            {
                string text = option.Description ?? string.Empty;
                if (option.UsesOverdraft) text += (text.Length > 0 ? "\n" : string.Empty) + "<color=" + GamePalette.HexWarn + ">Usa o cheque especial (juros).</color>";
                if (!option.Enabled && !string.IsNullOrEmpty(option.DisabledReason))
                    text += (text.Length > 0 ? "\n" : string.Empty) + "<color=" + GamePalette.HexDanger + ">" + option.DisabledReason + "</color>";
                descriptionText.text = text;
            }
            if (background != null) background.color = option.Enabled ? GamePalette.SurfaceAlt : GamePalette.WithAlpha(GamePalette.Parse(GamePalette.HexDisabledCard), GamePalette.DisabledAlpha);
            if (button == null) return;
            button.interactable = option.Enabled;
            button.onClick.RemoveAllListeners();
            string id = option.ReactionId;
            button.onClick.AddListener(() => onChoose?.Invoke(id));
        }
    }
}
