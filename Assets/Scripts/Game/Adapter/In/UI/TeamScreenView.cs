using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using Game.Adapter.In.UI.Theme;

namespace Game.Adapter.In.UI
{
    public sealed class TeamScreenView : MonoBehaviour
    {
        [Serializable] public sealed class RoleVisual
        {
            public string roleId;
            public Sprite portrait, environment;
        }
        [SerializeField] private RoleVisual[] roleVisuals = Array.Empty<RoleVisual>();
        [SerializeField] private TeamRoleCardView cardPrefab;
        [SerializeField] private RectTransform cardContainer;
        [SerializeField] private UnityEngine.UI.ScrollRect catalogScroll;
        [SerializeField] private UnityEngine.UI.Button confirmButton;
        [SerializeField] private TextMeshProUGUI stepText, contextText, hintText, confirmLabel;
        [SerializeField] private TextMeshProUGUI countText, payrollText, capacityText, alignmentText, requirementsText;
        [SerializeField] private GameObject emptyTeam, catalogError;
        private readonly List<TeamRoleCardView> cards = new();
        private TextMeshProUGUI footerText;
        public UnityEngine.UI.ScrollRect TutorialScroll => catalogScroll;
        public RectTransform PayrollHighlight => payrollText.rectTransform;
        public RectTransform RequirementsHighlight => requirementsText.rectTransform;
        public RectTransform ConfirmHighlight => (RectTransform)confirmButton.transform;

        public bool HasRequiredReferences() => cardPrefab != null && cardContainer != null && catalogScroll != null
            && confirmButton != null && roleVisuals.Length == 5 && roleVisuals.All(v => v != null
                && !string.IsNullOrWhiteSpace(v.roleId) && v.portrait != null && v.environment != null)
            && roleVisuals.Select(v => v.roleId).Distinct().Count() == 5;
        public bool HasVisual(string id) => roleVisuals.Any(v => v != null && v.roleId == id && v.portrait != null && v.environment != null);
        public TeamRoleCardView CreateCard(RoleData role, int required)
        {
            var card = Instantiate(cardPrefab, cardContainer); card.gameObject.SetActive(true); cards.Add(card);
            var visual = roleVisuals.Single(v => v.roleId == role.id);
            card.Setup(role, visual.portrait, visual.environment, required); return card;
        }
        public void ClearCards()
        {
            foreach (var card in cards) if (card != null) { card.gameObject.SetActive(false); Destroy(card.gameObject); }
            cards.Clear();
        }
        public void SetContext(bool setup, string restaurant)
        {
            stepText.text = setup ? "PASSO 7 DE 7  •  EQUIPE" : "GESTÃO DE EQUIPE";
            contextText.text = restaurant;
            confirmLabel.text = setup ? "Confirmar equipe e abrir hub" : "Concluir visita";
            if (catalogScroll != null) { catalogScroll.StopMovement(); catalogScroll.verticalNormalizedPosition = 1; }
        }
        public void SetSummary(int count, double payroll, long capacity, AlignmentResult alignment, string requirements, bool available)
        {
            if (footerText == null) footerText = transform.Find("BottomBar/Note")?.GetComponent<TextMeshProUGUI>();
            if (footerText != null) footerText.text = available
                ? "Use + e − para ajustar as contratações." : "Contratações indisponíveis no momento.";
            countText.text = available ? $"{count} pessoa(s)" : "Indisponível";
            payrollText.text = available ? Game.Domain.Service.EstablishmentSummaryService.Brl((float)payroll) : "Indisponível";
            capacityText.text = available ? $"{capacity:N0} clientes/mês" : "Indisponível";
            alignmentText.text = available && alignment != null ? $"{alignment.totalScore}/80  •  Equipe {alignment.restaurantTeamScore}/20" : "Indisponível";
            requirementsText.text = requirements;
            if (emptyTeam != null) emptyTeam.SetActive(available && count == 0);
            if (catalogError != null) catalogError.SetActive(!available && cards.Count == 0);
        }
        public void SetHint(string value, bool warning = false) { if (hintText != null) { hintText.text = value; hintText.color = warning ? GamePalette.ErrorText : GamePalette.Ink; } }
        public void SetConfirmEnabled(bool value) { if (confirmButton != null) confirmButton.interactable = value; }

        // Botão "Voltar à loja" do passo 7: criado em tempo de execução a partir do Confirm, no lugar da nota do rodapé.
        private UnityEngine.UI.Button setupBackButton;
        private Vector2? noteDefaultPosition, noteDefaultSize;
        public void BindSetupBack(UnityAction action)
        {
            var note = transform.Find("BottomBar/Note") as RectTransform;
            if (note != null && noteDefaultPosition == null) { noteDefaultPosition = note.anchoredPosition; noteDefaultSize = note.sizeDelta; }
            if (action == null || confirmButton == null)
            {
                if (setupBackButton != null) { setupBackButton.onClick.RemoveAllListeners(); setupBackButton.gameObject.SetActive(false); }
                if (note != null && noteDefaultPosition != null) { note.anchoredPosition = noteDefaultPosition.Value; note.sizeDelta = noteDefaultSize.Value; }
                return;
            }
            if (setupBackButton == null)
            {
                var copy = Instantiate(confirmButton.gameObject, confirmButton.transform.parent);
                copy.name = "SetupBack";
                setupBackButton = copy.GetComponent<UnityEngine.UI.Button>();
                setupBackButton.onClick.RemoveAllListeners();
                var rect = (RectTransform)copy.transform;
                rect.anchoredPosition = new Vector2(304, ((RectTransform)confirmButton.transform).anchoredPosition.y);
                rect.sizeDelta = new Vector2(360, ((RectTransform)confirmButton.transform).sizeDelta.y);
                foreach (var label in copy.GetComponentsInChildren<TextMeshProUGUI>(true)) label.text = "Voltar à loja";
                SharedUiStyles.Bind(setupBackButton, SharedControlKind.Secondary);
            }
            setupBackButton.gameObject.SetActive(true);
            setupBackButton.interactable = true;
            setupBackButton.onClick.RemoveAllListeners();
            setupBackButton.onClick.AddListener(action);
            if (note != null && noteDefaultPosition != null)
            {
                note.anchoredPosition = new Vector2(304 + 360 + 32, noteDefaultPosition.Value.y);
                note.sizeDelta = new Vector2(Mathf.Max(200, noteDefaultSize.Value.x - 392), noteDefaultSize.Value.y);
            }
        }
        public void BindConfirm(UnityAction action)
        { if (confirmButton == null) return; confirmButton.onClick.RemoveAllListeners(); if (action != null) confirmButton.onClick.AddListener(action); }
        private void LateUpdate()
        {
            var grid = cardContainer != null ? cardContainer.GetComponent<UnityEngine.UI.GridLayoutGroup>() : null;
            if (grid == null) return;
            float height = cards.Where(c => c != null).Select(c => c.RequiredHeight).DefaultIfEmpty(360).Max();
            if (!Mathf.Approximately(grid.cellSize.y, height)) grid.cellSize = new Vector2(grid.cellSize.x, height);
        }
    }
}
