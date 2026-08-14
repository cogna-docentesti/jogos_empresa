using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Game.Adapter.In.UI
{
    public sealed class EquipmentScreenView : MonoBehaviour
    {
        [Header("Cards")]
        [SerializeField] private EquipmentCardView cardPrefab;
        [SerializeField] private RectTransform cardContainer;

        [Header("Summary (optional)")]
        [SerializeField] private TextMeshProUGUI hintText;
        [SerializeField] private TextMeshProUGUI availableCashText;
        [SerializeField] private TextMeshProUGUI cartTotalValueText;
        [SerializeField] private TextMeshProUGUI remainingCashValueText;

        [Header("Actions")]
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button backButton;
        [SerializeField] private Button cartButton;

        private readonly List<EquipmentCardView> cards = new();
        private TextMeshProUGUI cartButtonText;
        private GameObject cartModal;
        private TextMeshProUGUI cartItemsText;
        private TextMeshProUGUI cartTotalText;
        private TextMeshProUGUI cartStatusText;
        private Button checkoutButton;
        private UnityAction checkoutAction;

        private void Awake()
        {
            EnsureCartUi();
        }

        private void OnDestroy()
        {
            if (checkoutButton != null)
                checkoutButton.onClick.RemoveAllListeners();
        }

        public bool HasRequiredReferences() => cardPrefab != null && cardContainer != null && confirmButton != null;

        public EquipmentCardView CreateCard()
        {
            EquipmentCardView card = Instantiate(cardPrefab, cardContainer);
            card.gameObject.SetActive(true);
            cards.Add(card);
            return card;
        }

        public void ClearCards()
        {
            foreach (EquipmentCardView card in cards)
                if (card != null) Destroy(card.gameObject);
            cards.Clear();
        }

        public void SetCartSummary(float availableCash, float cartTotal)
        {
            if (availableCashText != null)
                availableCashText.text = $"R$ {availableCash:0,0.00}";

            if (cartTotalValueText != null)
                cartTotalValueText.text = $"Total do carrinho: R$ {cartTotal:0,0.00}";

            if (remainingCashValueText != null)
                remainingCashValueText.text = $"Após a compra: R$ {availableCash - cartTotal:0,0.00}";
        }

        public void SetHint(string message)
        {
            if (hintText != null) hintText.text = message;
        }

        public void SetConfirmEnabled(bool enabled)
        {
            if (confirmButton != null) confirmButton.interactable = enabled;
        }

        public void SetCartButtonCount(int itemCount)
        {
            EnsureCartUi();
            if (cartButtonText != null)
                cartButtonText.text = $"Carrinho ({itemCount})";
        }

        public void BindCart(UnityAction action)
        {
            EnsureCartUi();
            Bind(cartButton, action);
        }

        public void ShowCart(
            IReadOnlyCollection<EquipmentData> equipments,
            float availableCash,
            UnityAction onCheckout)
        {
            EnsureCartUi();
            checkoutAction = onCheckout;

            EquipmentData[] items = equipments?.Where(item => item != null).ToArray()
                ?? Array.Empty<EquipmentData>();
            float total = items.Sum(item => Mathf.Max(0, item.cost));
            bool hasItems = items.Length > 0;
            bool hasEnoughCash = total <= availableCash;

            if (cartItemsText != null)
                cartItemsText.text = hasItems
                    ? string.Join("\n", items.Select(item =>
                        $"• {item.displayName} — R$ {Mathf.Max(0, item.cost):0,0.00}"))
                    : "Nenhum item foi adicionado ao carrinho.";

            if (cartTotalText != null)
                cartTotalText.text = $"Total: R$ {total:0,0.00}";

            if (cartStatusText != null)
            {
                cartStatusText.gameObject.SetActive(hasItems && !hasEnoughCash);
                cartStatusText.text = "Saldo insuficiente para concluir esta compra.";
            }

            if (checkoutButton != null)
            {
                checkoutButton.gameObject.SetActive(hasItems);
                checkoutButton.interactable = hasItems && hasEnoughCash;
            }

            if (cartModal != null)
            {
                cartModal.SetActive(true);
                cartModal.transform.SetAsLastSibling();
            }
        }

        public void HideCart()
        {
            if (cartModal != null) cartModal.SetActive(false);
        }

        public void BindConfirm(UnityAction action) => Bind(confirmButton, action);
        public void BindBack(UnityAction action) => Bind(backButton, action);

        private void EnsureCartUi()
        {
            if (cartModal != null)
                return;

            if (cartButton != null)
                cartButtonText = cartButton.GetComponentInChildren<TextMeshProUGUI>(true);

            CreateCartModal();
        }

        private void CreateCartModal()
        {
            if (cartModal != null)
                return;

            cartModal = CreateUiObject("CartModal", transform, typeof(Image));
            RectTransform overlayRect = cartModal.GetComponent<RectTransform>();
            overlayRect.anchorMin = new Vector2(0.5f, 0.5f);
            overlayRect.anchorMax = new Vector2(0.5f, 0.5f);
            overlayRect.pivot = new Vector2(0.5f, 0.5f);
            overlayRect.sizeDelta = new Vector2(4000f, 2400f);
            cartModal.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.72f);

            GameObject panel = CreateUiObject("CartPanel", cartModal.transform, typeof(Image));
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(760f, 650f);
            panel.GetComponent<Image>().color = new Color(0.96f, 0.97f, 0.98f, 1f);

            CreateText("Title", panel.transform, "Seu carrinho", 38f, FontStyles.Bold,
                new Vector2(40f, -30f), new Vector2(-40f, -95f));

            GameObject viewport = CreateUiObject("ItemsViewport", panel.transform, typeof(Image), typeof(Mask));
            RectTransform viewportRect = viewport.GetComponent<RectTransform>();
            viewportRect.anchorMin = new Vector2(0f, 1f);
            viewportRect.anchorMax = new Vector2(1f, 1f);
            viewportRect.offsetMin = new Vector2(45f, -400f);
            viewportRect.offsetMax = new Vector2(-45f, -120f);
            viewport.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.04f);
            viewport.GetComponent<Mask>().showMaskGraphic = false;

            cartItemsText = CreateText("Items", viewport.transform, string.Empty, 25f, FontStyles.Normal,
                new Vector2(10f, -5f), new Vector2(-10f, -5f));
            cartItemsText.alignment = TextAlignmentOptions.TopLeft;
            cartItemsText.overflowMode = TextOverflowModes.Overflow;
            ContentSizeFitter fitter = cartItemsText.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect scroll = viewport.AddComponent<ScrollRect>();
            scroll.viewport = viewportRect;
            scroll.content = cartItemsText.rectTransform;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;

            cartTotalText = CreateText("Total", panel.transform, string.Empty, 32f, FontStyles.Bold,
                new Vector2(55f, -420f), new Vector2(-55f, -475f));
            cartTotalText.alignment = TextAlignmentOptions.MidlineRight;

            cartStatusText = CreateText("Status", panel.transform, string.Empty, 24f, FontStyles.Bold,
                new Vector2(55f, -485f), new Vector2(-55f, -530f));
            cartStatusText.color = new Color(0.75f, 0.12f, 0.12f, 1f);
            cartStatusText.alignment = TextAlignmentOptions.Center;

            checkoutButton = CreateButton("CheckoutButton", panel.transform, "Concluir compra",
                new Vector2(0.5f, 0f), new Vector2(230f, 70f), new Vector2(135f, 35f));
            checkoutButton.onClick.AddListener(() => checkoutAction?.Invoke());

            Button closeButton = CreateButton("CloseButton", panel.transform, "Fechar",
                new Vector2(0.5f, 0f), new Vector2(180f, 70f), new Vector2(-105f, 35f));
            closeButton.onClick.AddListener(HideCart);

            cartModal.SetActive(false);
        }

        private TextMeshProUGUI CreateText(string name, Transform parent, string value, float size,
            FontStyles style, Vector2 offsetMin, Vector2 offsetMax)
        {
            GameObject textObject = CreateUiObject(name, parent, typeof(TextMeshProUGUI));
            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(offsetMin.x, offsetMax.y);
            rect.offsetMax = new Vector2(offsetMax.x, offsetMin.y);
            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = new Color(0.08f, 0.18f, 0.24f, 1f);
            text.textWrappingMode = TextWrappingModes.Normal;
            if (hintText != null) text.font = hintText.font;
            return text;
        }

        private Button CreateButton(string name, Transform parent, string label,
            Vector2 anchor, Vector2 size, Vector2 position)
        {
            GameObject buttonObject = CreateUiObject(name, parent, typeof(Image), typeof(Button));
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            buttonObject.GetComponent<Image>().color = new Color(0.08f, 0.42f, 0.6f, 1f);

            TextMeshProUGUI labelText = CreateText("Text", buttonObject.transform, label, 25f,
                FontStyles.Bold, Vector2.zero, Vector2.zero);
            Stretch(labelText.rectTransform);
            labelText.alignment = TextAlignmentOptions.Center;
            labelText.color = Color.white;
            return buttonObject.GetComponent<Button>();
        }

        private static GameObject CreateUiObject(string name, Transform parent, params Type[] components)
        {
            var types = new List<Type> { typeof(RectTransform), typeof(CanvasRenderer) };
            types.AddRange(components);
            var instance = new GameObject(name, types.Distinct().ToArray());
            instance.layer = parent.gameObject.layer;
            instance.transform.SetParent(parent, false);
            return instance;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void Bind(Button button, UnityAction action)
        {
            if (button == null) return;
            button.onClick.RemoveAllListeners();
            if (action != null) button.onClick.AddListener(action);
        }
    }
}
