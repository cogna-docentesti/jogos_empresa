using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using Game.Adapter.In.UI.Theme;

namespace Game.Adapter.In.UI
{
    public enum EquipmentFilter { All, Basic, Specific, Owned }
    public sealed class EquipmentScreenView : MonoBehaviour
    {
        [SerializeField] private EquipmentCardView cardPrefab;
        [SerializeField] private RectTransform cardContainer;
        [SerializeField] private UnityEngine.UI.ScrollRect catalogScroll;
        [SerializeField] private TextMeshProUGUI hintText, availableCashText, cartTotalValueText, remainingCashValueText;
        [SerializeField] private TextMeshProUGUI stepText, contextText, resultText, confirmLabel, footerText, summaryItemsText;
        [SerializeField] private UnityEngine.UI.Button confirmButton, cartButton, bankBackButton;
        [SerializeField] private UnityEngine.UI.Button[] filterButtons = Array.Empty<UnityEngine.UI.Button>();
        [SerializeField] private GameObject catalogEmpty, summaryEmpty;
        [SerializeField] private GameObject cartModal, cartEmpty;
        [SerializeField] private RectTransform cartLines;
        [SerializeField] private EquipmentCartLineView cartLinePrefab;
        [SerializeField] private TextMeshProUGUI cartTotalText, cartStatusText, cartCashText, cartRemainingText;
        [SerializeField] private UnityEngine.UI.Button checkoutButton, closeCartButton;
        [SerializeField] private UnityEngine.UI.Button retryCatalogButton;
        private readonly List<EquipmentCardView> cards = new();
        private readonly List<EquipmentCartLineView> lines = new();
        private UnityAction checkoutAction;
        public event Action<EquipmentFilter> FilterRequested;
        public event Action<string> CartRemovalRequested;
        public bool CartIsOpen => cartModal != null && cartModal.activeSelf;
        private void Awake()
        {
            for (int i = 0; i < filterButtons.Length; i++)
            {
                var filter = (EquipmentFilter)i;
                if (filterButtons[i] != null) filterButtons[i].onClick.AddListener(() => FilterRequested?.Invoke(filter));
            }
            if (checkoutButton != null) checkoutButton.onClick.AddListener(() => checkoutAction?.Invoke());
            if (closeCartButton != null) closeCartButton.onClick.AddListener(HideCart);
        }
        public bool HasRequiredReferences() => cardPrefab != null && cardContainer != null && confirmButton != null
            && cartModal != null && cartLinePrefab != null && cartLines != null;
        public EquipmentCardView CreateCard()
        {
            var card = Instantiate(cardPrefab, cardContainer); card.gameObject.SetActive(true); cards.Add(card); return card;
        }
        public void ClearCards()
        {
            foreach (var card in cards) if (card != null) { card.gameObject.SetActive(false); Destroy(card.gameObject); }
            cards.Clear();
        }
        public void SetContext(bool setup, string restaurant)
        {
            if (stepText != null) stepText.text = setup ? "PASSO 6 DE 7  •  EQUIPAMENTOS" : "LOJA DE EQUIPAMENTOS";
            if (contextText != null) contextText.text = GameSessionState.HasSession ? restaurant + "  •  Uma unidade por equipamento" : "Sessão indisponível";
            if (bankBackButton != null) bankBackButton.gameObject.SetActive(setup);
            if (confirmLabel != null) confirmLabel.text = setup ? "Continuar para equipe" : "Concluir visita";
            if (footerText != null)
            {
                footerText.gameObject.SetActive(!setup);
                footerText.text = "Compras para o seu restaurante.";
            }
        }
        public void SetCartSummary(float availableCash, IReadOnlyCollection<EquipmentData> items, bool available)
        {
            float total = items.Sum(e => Mathf.Max(0, e.cost));
            if (availableCashText != null) availableCashText.text = available ? FinancialScreenView.Money(availableCash) : "Indisponível";
            if (cartTotalValueText != null) cartTotalValueText.text = available ? FinancialScreenView.Money(total) : "Indisponível";
            if (remainingCashValueText != null)
            {
                remainingCashValueText.text = available ? FinancialScreenView.Money(availableCash - total) : "Indisponível";
                remainingCashValueText.color = total > availableCash ? GamePalette.ErrorText : GamePalette.Ink;
            }
            if (summaryEmpty != null) summaryEmpty.SetActive(available && items.Count == 0);
            if (summaryItemsText != null)
            {
                summaryItemsText.gameObject.SetActive(items.Count > 0);
                summaryItemsText.text = string.Join("\n\n", items.Select(e => $"1 × {e.displayName}\n{FinancialScreenView.Money(e.cost)}"));
            }
            if (cartButton != null)
            {
                cartButton.interactable = available;
                cartButton.GetComponentInChildren<TMP_Text>().text = $"Revisar carrinho ({items.Count})";
            }
        }
        public void ApplyFilter(EquipmentFilter filter)
        {
            int count = 0;
            foreach (var card in cards)
            {
                bool show = filter == EquipmentFilter.All || filter == EquipmentFilter.Basic && card.Equipment.category == EquipmentCategory.BASIC
                    || filter == EquipmentFilter.Specific && card.Equipment.category == EquipmentCategory.SPECIFIC || filter == EquipmentFilter.Owned && card.IsOwned;
                card.gameObject.SetActive(show); if (show) count++;
            }
            for (int i = 0; i < filterButtons.Length; i++) filterButtons[i]?.GetComponent<SharedControlAppearance>()?.SetSelected(i == (int)filter);
            if (catalogEmpty != null) catalogEmpty.SetActive(count == 0);
            if (resultText != null) resultText.text = $"{count} equipamento(s)";
            if (catalogScroll != null) { catalogScroll.StopMovement(); catalogScroll.verticalNormalizedPosition = 1; }
        }
        private void LateUpdate()
        {
            var grid = cardContainer != null ? cardContainer.GetComponent<UnityEngine.UI.GridLayoutGroup>() : null;
            if (grid == null) return;
            float minimum = grid.constraintCount >= 3 ? 704 : 360;
            float height = Mathf.Max(minimum, cards.Where(c => c != null && c.gameObject.activeSelf).Select(c => c.RequiredHeight).DefaultIfEmpty(minimum).Max());
            if (!Mathf.Approximately(grid.cellSize.y, height)) grid.cellSize = new Vector2(grid.cellSize.x, height);
        }
        public void SetHint(string message) { if (hintText != null) hintText.text = message; }
        public void BindRetry(UnityAction action) => Bind(retryCatalogButton, action);
        public void SetCatalogLoading(bool loading)
        {
            if (retryCatalogButton != null) retryCatalogButton.gameObject.SetActive(false);
            if (catalogEmpty != null) catalogEmpty.SetActive(false);
            if (resultText != null) resultText.text = loading ? "Carregando catálogo…" : "";
            if (loading)
            {
                SetCartSummary(0, Array.Empty<EquipmentData>(), false);
                SetHint("Carregando o catálogo de equipamentos…");
            }
        }
        public void ShowCatalogFailure(string message)
        {
            if (catalogEmpty != null) { catalogEmpty.SetActive(true); catalogEmpty.GetComponent<TMP_Text>().text = message; }
            if (retryCatalogButton != null) { retryCatalogButton.gameObject.SetActive(true); retryCatalogButton.Select(); }
            if (resultText != null) resultText.text = "Catálogo indisponível";
            SetHint(message);
        }
        public void ResetCatalogMessage()
        { if (catalogEmpty != null) catalogEmpty.GetComponent<TMP_Text>().text = "Nenhum equipamento neste filtro.\nEscolha outra categoria para continuar."; }
        public void SetConfirmEnabled(bool enabled) { if (confirmButton != null) confirmButton.interactable = enabled; }
        public void BindBankBack(UnityAction action) => Bind(bankBackButton, action);
        public void BindCart(UnityAction action) => Bind(cartButton, action);
        public void BindConfirm(UnityAction action) => Bind(confirmButton, action);
        public void ShowCart(IReadOnlyCollection<EquipmentData> items, float availableCash, UnityAction onCheckout)
        {
            if (!isActiveAndEnabled || !HasRequiredReferences()) return;
            checkoutAction = onCheckout;
            foreach (var line in lines) if (line != null) { line.gameObject.SetActive(false); Destroy(line.gameObject); }
            lines.Clear();
            foreach (var item in items)
            {
                var line = Instantiate(cartLinePrefab, cartLines); line.gameObject.SetActive(true);
                line.Setup(item, () => CartRemovalRequested?.Invoke(item.id)); lines.Add(line);
            }
            float total = items.Sum(e => Mathf.Max(0, e.cost));
            cartTotalText.text = FinancialScreenView.Money(total);
            cartCashText.text = FinancialScreenView.Money(availableCash);
            cartRemainingText.text = FinancialScreenView.Money(availableCash - total);
            cartRemainingText.color = total > availableCash ? GamePalette.ErrorText : GamePalette.Ink;
            cartStatusText.text = items.Count == 0 ? "Adicione equipamentos no catálogo." : total > availableCash
                ? "Saldo insuficiente. Remova itens para concluir." : $"{items.Count} unidade(s). Confira os itens e o total.";
            cartStatusText.color = total > availableCash ? GamePalette.ErrorText : GamePalette.Muted;
            cartEmpty.SetActive(items.Count == 0);
            checkoutButton.interactable = items.Count > 0 && total <= availableCash && GameSessionState.HasActiveSession;
            bool opening = !cartModal.activeSelf;
            cartModal.SetActive(true); cartModal.transform.SetAsLastSibling();
            // A prefab's root Canvas cannot serialize overrideSorting until nested under the scene Canvas.
            var modalCanvas = cartModal.GetComponent<Canvas>();
            if (modalCanvas != null) modalCanvas.overrideSorting = true;
            if (opening) cartLines.GetComponentInParent<UnityEngine.UI.ScrollRect>().verticalNormalizedPosition = 1;
        }
        public void HideCart() { if (cartModal != null) cartModal.SetActive(false); }
        private static void Bind(UnityEngine.UI.Button button, UnityAction action)
        { if (button == null) return; button.onClick.RemoveAllListeners(); if (action != null) button.onClick.AddListener(action); }
    }
}
