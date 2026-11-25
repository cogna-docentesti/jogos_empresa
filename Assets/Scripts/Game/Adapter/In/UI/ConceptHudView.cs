using Game.Domain.Service;
using Game.Adapter.In.UI.Theme;
using TMPro;
using UnityEngine;

namespace Game.Adapter.In.UI
{
    /// <summary>Read-only shared HUD and hub preview; uses the same snapshot as establishment.</summary>
    public sealed class ConceptHudView : MonoBehaviour
    {
        public const int ReputationStarCount = 5;
        private const int MaximumReputation = 100;

        [SerializeField] TMP_Text month, cash, score, revenue, result, reputation;
        [SerializeField] TMP_Text context;
        [SerializeField] UnityEngine.UI.Image facade;
        [SerializeField] RectTransform reputationStars;
        [SerializeField] UnityEngine.UI.Image[] reputationFills = System.Array.Empty<UnityEngine.UI.Image>();
        float next;
        void OnEnable() { ApplyTypography(); next = 0; Refresh(); }
        public void ApplyTypography()
        {
            foreach (var text in new[] { month, cash, score, revenue, result, reputation })
                GameTypography.KeepSize(text, TypographyRole.Kpi);
            GameTypography.KeepSize(context, TypographyRole.Caption);
        }
        void Update() { if(Time.unscaledTime >= next) { next = Time.unscaledTime + .5f; Refresh(); } }
        void Refresh()
        {
            var s = EstablishmentSummaryService.Build();
            if(month != null) month.text = s.HasSession ? $"Mês {s.Round} de 3" : "Abertura";
            if(cash != null) { cash.text = s.HasSession ? EstablishmentSummaryService.Brl(s.Cash) : "A confirmar"; cash.color=s.Cash<0?Theme.GamePalette.Parse(Theme.GamePalette.HexNegativeOnDark):Theme.GamePalette.OnDark; }
            if(score != null) score.text = s.HasScore ? s.Score.ToString() : "—";
            if(revenue != null) revenue.text = s.HasMonthlyEstimate ? EstablishmentSummaryService.Brl(s.EstimatedRevenue) : "A calcular";
            if(result != null) { result.text = s.HasMonthlyEstimate ? EstablishmentSummaryService.Brl(s.MonthlyResult) : "A calcular"; result.color=!s.HasMonthlyEstimate?Theme.GamePalette.Muted:s.MonthlyResult<0?Theme.GamePalette.ErrorText:Theme.GamePalette.Money; }
            ApplyReputation(s.HasSession ? s.Reputation : (int?)null);
            if(facade != null) { facade.sprite = s.Facade; facade.enabled = s.Facade != null; }
        }

        private void ApplyReputation(int? value)
        {
            if (reputation != null)
                reputation.text = value.HasValue ? $"{value.Value} / {MaximumReputation}" : "—";
            if (reputationStars == null) return;

            reputationStars.gameObject.SetActive(value.HasValue);
            // Each full star represents 20 reputation points, with fractional fill between them.
            float filledStars = Mathf.Clamp01(value.GetValueOrDefault() / (float)MaximumReputation) * ReputationStarCount;
            for (int i = 0; i < reputationFills.Length; i++)
                reputationFills[i].fillAmount = Mathf.Clamp01(filledStars - i);
        }
    }
}
