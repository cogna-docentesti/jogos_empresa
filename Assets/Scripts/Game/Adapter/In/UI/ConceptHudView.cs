using Game.Domain.Service;
using Game.Adapter.In.UI.Theme;
using TMPro;
using UnityEngine;

namespace Game.Adapter.In.UI
{
    /// <summary>Read-only shared HUD and hub preview; uses the same snapshot as establishment.</summary>
    public sealed class ConceptHudView : MonoBehaviour
    {
        [SerializeField] TMP_Text month, cash, score, revenue, result, reputation;
        [SerializeField] TMP_Text context;
        [SerializeField] string setupContext;
        [SerializeField] UnityEngine.UI.Image facade;
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
            if(context!=null)
            {
                var state=GameManager.Instance?.StateMachine?.CurrentState;
                bool setup=state==GameState.Config_Identification||state==GameState.Config_Location||state==GameState.Config_Restaurant||state==GameState.Config_TargetSegment||state==GameState.Initial_Capital||state==GameState.Initial_Equipment||state==GameState.Initial_Team;
                context.text=setup&&!string.IsNullOrEmpty(setupContext)?setupContext:"GESTÃO DE RESTAURANTES";
            }
            if(month != null) month.text = s.HasSession ? $"Mês {s.Round} de 3" : "Abertura";
            if(cash != null) { cash.text = s.HasSession ? EstablishmentSummaryService.Brl(s.Cash) : "A confirmar"; cash.color=s.Cash<0?Theme.GamePalette.Parse(Theme.GamePalette.HexNegativeOnDark):Theme.GamePalette.OnDark; }
            if(score != null) score.text = s.HasScore ? s.Score.ToString() : "—";
            if(revenue != null) revenue.text = s.HasMonthlyEstimate ? EstablishmentSummaryService.Brl(s.EstimatedRevenue) : "A calcular";
            if(result != null) { result.text = s.HasMonthlyEstimate ? EstablishmentSummaryService.Brl(s.MonthlyResult) : "A calcular"; result.color=!s.HasMonthlyEstimate?Theme.GamePalette.Muted:s.MonthlyResult<0?Theme.GamePalette.ErrorText:Theme.GamePalette.Money; }
            if(reputation != null) reputation.text = s.HasSession ? $"{s.Reputation} / 100" : "—";
            if(facade != null) { facade.sprite = s.Facade; facade.enabled = s.Facade != null; }
        }
    }
}
