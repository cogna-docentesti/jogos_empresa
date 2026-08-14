using Game.Adapter.In.UI.Theme;
using Game.Domain.Service;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Adapter.In.UI
{
    /// <summary>
    /// Camada burra da tela "Meu Estabelecimento": so recebe um
    /// EstablishmentSummary pronto e escreve nos TextMeshPro.
    /// Nao calcula nada e nao conhece GameSessionState.
    /// </summary>
    public sealed class EstablishmentSummaryView : MonoBehaviour
    {
        [Header("Resumo da empresa")]
        [SerializeField] private TextMeshProUGUI locationValue;
        [SerializeField] private TextMeshProUGUI restaurantValue;
        [SerializeField] private TextMeshProUGUI segmentValue;
        [SerializeField] private TextMeshProUGUI menuValue;
        [SerializeField] private TextMeshProUGUI priceValue;
        [SerializeField] private TextMeshProUGUI equipmentValue;
        [SerializeField] private TextMeshProUGUI teamValue;

        [Header("Indicadores")]
        [SerializeField] private TextMeshProUGUI scoreValue;
        [SerializeField] private TextMeshProUGUI cashValue;
        [SerializeField] private TextMeshProUGUI revenueValue;
        [SerializeField] private TextMeshProUGUI monthlyResultValue;
        [SerializeField] private Image scoreBarFill;

        [Header("Contexto")]
        [SerializeField] private TextMeshProUGUI coherenceValue;
        [SerializeField] private TextMeshProUGUI roundValue;
        [SerializeField] private TextMeshProUGUI noticeText;

        public void Bind(EstablishmentSummary summary)
        {
            if (summary == null)
                return;

            if (!summary.HasSession)
            {
                ShowEmptyState();
                return;
            }

            Set(locationValue,   summary.LocationLabel);
            Set(restaurantValue, summary.RestaurantLabel);
            Set(segmentValue,    summary.SegmentLabel);
            Set(menuValue,       summary.MenuLabel);
            Set(priceValue,      summary.PriceLabel);
            Set(equipmentValue,  summary.EquipmentLabel);
            Set(teamValue,       summary.TeamLabel);

            Set(scoreValue, $"{summary.Score} pts", GamePalette.Xp);
            Set(cashValue,  EstablishmentSummaryService.Brl(summary.Cash),
                summary.Cash >= 0f ? GamePalette.Money : GamePalette.Danger);
            Set(revenueValue, EstablishmentSummaryService.Brl(summary.EstimatedRevenue), GamePalette.Ink);
            Set(monthlyResultValue, EstablishmentSummaryService.Brl(summary.MonthlyResult),
                GamePalette.ResultColor(summary.MonthlyResult));

            if (scoreBarFill != null)
                scoreBarFill.fillAmount = Mathf.Clamp01(summary.ScoreProgress);

            Set(coherenceValue, summary.CoherenceLabel);
            Set(roundValue, $"Rodada {summary.Round}");

            Set(noticeText, BuildNotice(summary), GamePalette.InkCaption);
        }

        private static string BuildNotice(EstablishmentSummary summary)
        {
            if (!summary.HasTeamCatalog && !summary.HasEquipmentCatalog)
                return "Folha salarial e capacidade nao entram no calculo: os catalogos "
                     + "RoleData e EquipmentData ainda nao estao em Resources/Roles e Resources/Equipment.";

            if (!summary.HasTeamCatalog)
                return "Folha salarial fora do calculo: RoleData ainda nao esta em Resources/Roles.";

            if (!summary.HasEquipmentCatalog)
                return "Bonus de capacidade fora do calculo: EquipmentData ainda nao esta em Resources/Equipment.";

            return "Projecao para o mes corrente com base nas escolhas atuais.";
        }

        private void ShowEmptyState()
        {
            Set(locationValue,   "Pendente");
            Set(restaurantValue, "Pendente");
            Set(segmentValue,    "Pendente");
            Set(menuValue,       "Pendente");
            Set(priceValue,      "Pendente");
            Set(equipmentValue,  "Nenhum");
            Set(teamValue,       "Ninguem");

            Set(scoreValue,          "0 pts",  GamePalette.InkCaption);
            Set(cashValue,           "R$ 0",   GamePalette.InkCaption);
            Set(revenueValue,        "R$ 0",   GamePalette.InkCaption);
            Set(monthlyResultValue,  "R$ 0",   GamePalette.InkCaption);

            if (scoreBarFill != null)
                scoreBarFill.fillAmount = 0f;

            Set(coherenceValue, "A calcular");
            Set(roundValue, "Rodada 1");
            Set(noticeText, "Nenhuma sessao ativa foi carregada.", GamePalette.Warn);
        }

        private static void Set(TextMeshProUGUI label, string value)
        {
            if (label != null)
                label.text = value;
        }

        /// <summary>
        /// A cor foi removida de proposito: a aparencia de cada label vem do
        /// TMP montado na hierarquia. A sobrecarga continua existindo so para
        /// nao mexer nas ~10 chamadas, mas o parametro e ignorado.
        /// </summary>
        private static void Set(TextMeshProUGUI label, string value, Color _)
        {
            Set(label, value);
        }
    }
}
