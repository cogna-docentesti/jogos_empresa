using System;
using Game.Adapter.In.UI.Navigation;
using Game.Adapter.In.UI.Theme;
using Game.Domain.Service;
using TMPro;
using UnityEngine;

namespace Game.Adapter.In.UI
{
    /// <summary>Independent ART-27 layers. All visible values come from the bound snapshot.</summary>
    public sealed class EstablishmentReviewContent : MonoBehaviour
    {
        [Serializable] public sealed class DecisionVisual
        {
            public UnityEngine.UI.Button button;
            public UnityEngine.UI.Image check;
            public UnityEngine.UI.Image tick;
            public TMP_Text title, detail, state;
            public PanelId destination;
        }
        [Serializable] public sealed class MemberVisual
        {
            public GameObject root;
            public UnityEngine.UI.Image portrait, environment;
            public TMP_Text label;
        }
        [Serializable] public sealed class RoleArtwork { public string id; public Sprite portrait, environment; }
        [SerializeField] private SelectedEstablishmentView selected;
        [SerializeField] private TMP_Text context, round, status, notice, cash, revenue, result, score, coherence;
        [SerializeField] private TMP_Text revenueTitle, resultTitle, menuDetails, equipmentDetails, teamDetails, teamEmpty;
        [SerializeField] private RectTransform scoreFill;
        [SerializeField] private DecisionVisual[] decisions = Array.Empty<DecisionVisual>();
        [SerializeField] private MemberVisual[] members = Array.Empty<MemberVisual>();
        [SerializeField] private RoleArtwork[] roleArtwork = Array.Empty<RoleArtwork>();
        [SerializeField] private RectTransform teamGrid;
        [SerializeField] private UnityEngine.UI.Button reviewButton;

        public void Bind(EstablishmentSummary s)
        {
            selected?.Bind(s);
            Set(context, s.ContextLabel);
            Set(round, s.HasSession ? $"Mês {s.Round} de 3" : "Indicadores após a confirmação");
            Set(status, s.IsReady ? "Requisitos atendidos" : s.HasChoices ? "Escolhas pendentes" : "Ainda sem escolhas", s.IsReady ? GamePalette.Money : GamePalette.Ink);
            Set(notice, s.Notice);
            Set(cash, s.HasSession ? EstablishmentSummaryService.Brl(s.Cash) : "A confirmar", s.HasSession ? AmountColor(s.Cash) : GamePalette.Muted);
            Set(revenue, s.HasMonthlyEstimate ? EstablishmentSummaryService.Brl(s.EstimatedRevenue) : "Indisponível", s.HasMonthlyEstimate ? GamePalette.Ink : GamePalette.Muted);
            Set(result, s.HasMonthlyEstimate ? EstablishmentSummaryService.Brl(s.MonthlyResult) : "Indisponível", s.HasMonthlyEstimate ? AmountColor(s.MonthlyResult) : GamePalette.Muted);
            Set(score, s.HasScore ? $"{s.Score} / {s.ScoreMax}" : "A calcular");
            Set(coherence, s.HasScore ? s.CoherenceLabel : "A calcular");
            Set(revenueTitle, s.IsLastResult ? "Receita registrada" : "Receita prevista");
            Set(resultTitle, s.IsLastResult ? "Resultado registrado" : "Resultado previsto");
            if (scoreFill != null)
            {
                scoreFill.anchorMax = new Vector2(s.HasScore ? Mathf.Clamp01(s.ScoreProgress) : 0, 1);
                scoreFill.offsetMin = scoreFill.offsetMax = Vector2.zero;
                scoreFill.GetComponent<UnityEngine.UI.Image>().color = GamePalette.ScoreColor(s.ScoreProgress);
            }
            Set(menuDetails, s.MenuDetails);
            Set(equipmentDetails, s.EquipmentDetails);
            Set(teamDetails, s.TeamDetails);
            for (int i = 0; i < decisions.Length; i++)
            {
                var v = decisions[i];
                if (i >= s.Decisions.Length) { v.button.gameObject.SetActive(false); continue; }
                var d = s.Decisions[i]; v.button.gameObject.SetActive(true);
                Set(v.title, d.Label); Set(v.detail, d.Detail);
                Set(v.state, d.Ready ? "Pronto" : "Pendente", d.Ready ? GamePalette.Money : GamePalette.Muted);
                v.check.sprite = SharedUiSkin.Current.Get(d.Ready ? "UI_CHECKBOX_ON" : "UI_CHECKBOX_OFF");
                if (v.tick != null) v.tick.enabled = d.Ready;
                v.button.onClick.RemoveAllListeners();
                var destination = v.destination;
                v.button.onClick.AddListener(() => MenuNavigator.Instance?.Open(destination));
                v.button.interactable = MenuNavigator.HubAvailable;
            }
            int visible = Math.Min(members.Length, s.Members.Length);
            for (int i = 0; i < members.Length; i++)
            {
                var v = members[i]; v.root.SetActive(i < visible);
                if (i >= visible) continue;
                var member = s.Members[i];
                var art = Array.Find(roleArtwork, a => a.id == member.RoleId);
                v.portrait.sprite = art?.portrait; v.portrait.enabled = art?.portrait != null;
                v.environment.sprite = art?.environment; v.environment.enabled = art?.environment != null;
                Set(v.label, $"{member.Name}\n{member.Quantity} " + (member.Quantity == 1 ? "contratado" : "contratados"));
            }
            if (teamEmpty != null) teamEmpty.gameObject.SetActive(visible == 0);
            if (teamGrid != null)
            {
                teamGrid.gameObject.SetActive(visible > 0);
                var height = Mathf.Ceil(visible / 3f) * 252;
                var layout = teamGrid.GetComponent<UnityEngine.UI.LayoutElement>();
                layout.minHeight = layout.preferredHeight = height;
            }
            if (reviewButton != null)
            {
                reviewButton.interactable = MenuNavigator.HubAvailable;
                reviewButton.onClick.RemoveAllListeners();
                reviewButton.onClick.AddListener(() => MenuNavigator.Instance?.Open(PanelId.Review));
            }
            // Native TMP preferred heights reflow after the viewport width is resolved.
            foreach (var scroll in GetComponentsInChildren<UnityEngine.UI.ScrollRect>())
                UnityEngine.UI.LayoutRebuilder.MarkLayoutForRebuild(scroll.content);
        }

        private static Color AmountColor(float value) => value >= 0 ? GamePalette.Money : GamePalette.ErrorText;
        private static void Set(TMP_Text t, string value, Color? color = null)
        {
            if (t == null) return;
            t.text = value ?? "";
            if (color.HasValue) t.color = color.Value;
        }
    }
}
