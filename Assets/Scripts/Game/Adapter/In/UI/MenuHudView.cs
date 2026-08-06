using Game.Adapter.In.UI.Theme;
using Game.Domain.Service;
using TMPro;
using UnityEngine;

namespace Game.Adapter.In.UI
{
    /// <summary>
    /// Pills de Score e Caixa da barra superior do Menu do Jogo.
    /// Atualiza sozinho toda vez que o mapa volta a ficar visivel, entao
    /// reflete a ultima escolha feita no Banco, no RH ou na Loja.
    /// </summary>
    public sealed class MenuHudView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI scoreValue;
        [SerializeField] private TextMeshProUGUI cashValue;
        [SerializeField] private TextMeshProUGUI roundValue;

        private void OnEnable()
        {
            Refresh();
        }

        public void Refresh()
        {
            var summary = EstablishmentSummaryService.Build();

            if (scoreValue != null)
            {
                scoreValue.text  = summary.HasSession ? summary.Score.ToString() : "0";
                scoreValue.color = GamePalette.Xp;
            }

            if (cashValue != null)
            {
                cashValue.text  = EstablishmentSummaryService.Brl(summary.Cash);
                cashValue.color = summary.Cash >= 0f ? GamePalette.Money : GamePalette.Danger;
            }

            if (roundValue != null)
                roundValue.text = $"Rodada {summary.Round}";
        }
    }
}
