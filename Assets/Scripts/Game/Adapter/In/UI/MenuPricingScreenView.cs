using System.Collections.Generic;
using System.Linq;
using TMPro;
using Game.Adapter.In.UI.Theme;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Game.Adapter.In.UI
{
    [DefaultExecutionOrder(100)]
    public sealed class MenuPricingScreenView : MonoBehaviour
    {
        [Header("Screen")]
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI hintText;
        [SerializeField] private TextMeshProUGUI stepText;
        [SerializeField] private TextMeshProUGUI restaurantText;
        [SerializeField] private TextMeshProUGUI footerText;
        [SerializeField] private TextMeshProUGUI strategyHelpText;
        [SerializeField] private TextMeshProUGUI inputHelpText;
        [SerializeField] private TextMeshProUGUI[] summaryNames;
        [SerializeField] private TextMeshProUGUI[] summaryPrices;

        [Header("Products")]
        [SerializeField] private Transform productItemsContainer;
        [SerializeField] private ProductPricingItemView productItemPrefab;
        [SerializeField] private ScrollRect productsScrollRect;
        [SerializeField] private float productItemHeight = 400f;
        [SerializeField] private float productItemSpacing = 32f;

        [Header("Coherence Panel")]
        [SerializeField] private Image priceCoherenceBarFill;
        [SerializeField] private TextMeshProUGUI priceCoherenceText;

        [Header("Actions")]
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button backButton;
        private InputAction tabAction;
        private GameObject tabOrigin;
        private bool tabPending, tabBackwards;

        private void OnEnable()
        {
            ApplyTypography();
            tabAction = new InputAction(type: InputActionType.Button, binding: "<Keyboard>/tab");
            tabAction.performed += OnTab;
            tabAction.Enable();
        }

        public void ApplyTypography()
        {
            foreach (var binding in GetComponentsInChildren<TypographyRoleBinding>(true)) binding.Reapply();
            GameTypography.KeepSize(titleText, TypographyRole.ScreenTitle);
            foreach (var t in new[] { hintText, restaurantText, strategyHelpText, priceCoherenceText }) GameTypography.KeepSize(t, TypographyRole.Body);
            GameTypography.KeepSize(stepText, TypographyRole.Caption);
            GameTypography.KeepSize(footerText, TypographyRole.Label);
            GameTypography.KeepSize(inputHelpText, TypographyRole.Caption);
            if (summaryNames != null) foreach (var t in summaryNames) GameTypography.KeepSize(t, TypographyRole.Body);
            if (summaryPrices != null) foreach (var t in summaryPrices) GameTypography.KeepSize(t, TypographyRole.Table);
        }

        private void OnDisable()
        {
            tabAction?.Dispose();
            tabAction = null;
            tabPending = false;
        }

        private void OnTab(InputAction.CallbackContext context)
        {
            if (FindFirstObjectByType<SharedModalFocus>() != null) return;
            tabOrigin = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            tabBackwards = Keyboard.current != null && Keyboard.current.shiftKey.isPressed;
            tabPending = true;
        }

        // Commit focus after TMP consumes its text event, using the original selection so the
        // shared Canvas traversal and a focused input cannot advance twice or reclaim focus.
        private void LateUpdate()
        {
            if (!tabPending || EventSystem.current == null) return;
            tabPending = false;
            var scope = transform.parent != null ? transform.parent : transform;
            var controls = scope.GetComponentsInChildren<Selectable>()
                .Where(s => s.IsActive() && s.IsInteractable() && s.navigation.mode != UnityEngine.UI.Navigation.Mode.None).ToArray();
            if (controls.Length == 0) return;
            int current = System.Array.FindIndex(controls, s => s.gameObject == tabOrigin);
            int next = current < 0 ? (tabBackwards ? controls.Length - 1 : 0)
                : (current + (tabBackwards ? -1 : 1) + controls.Length) % controls.Length;
            controls[next].Select();
        }

        public bool HasRequiredReferences()
        {
            ResolveOptionalReferences();

            if (productItemsContainer == null)
            {
                Debug.LogError("[MenuPricingScreenView] Product Items Container nao foi configurado.");
                SetConfirmEnabled(false);
                return false;
            }

            if (productItemPrefab == null)
            {
                Debug.LogError("[MenuPricingScreenView] Product Item Prefab nao foi configurado.");
                SetConfirmEnabled(false);
                return false;
            }

            PrepareProductListLayout();
            return true;
        }

        public ProductPricingItemView CreateProductItem()
        {
            var item = Instantiate(productItemPrefab, productItemsContainer);
            ConfigureProductItemLayout(item);
            return item;
        }

        public void ClearProductItems()
        {
            if (productItemsContainer == null)
                return;

            for (int i = productItemsContainer.childCount - 1; i >= 0; i--)
            {
                var item = productItemsContainer.GetChild(i);
                item.gameObject.SetActive(false);
                Destroy(item.gameObject);
            }
        }

        public void SetTitle(string message)
        {
            if (titleText != null)
                titleText.text = message;
        }

        public void SetHint(string message, bool error = false)
        {
            if (hintText != null)
            {
                hintText.text = message;
                hintText.color = error ? GamePalette.ErrorText : GamePalette.Ink;
            }
        }

        public void SetReviewMode(bool review)
        {
            ApplyTypography();
            if (stepText != null)
            {
                stepText.text = review ? "GESTÃO · REVISÃO DO CARDÁPIO" : "PASSO 4 DE 7 · CARDÁPIO";
                stepText.rectTransform.anchoredPosition = new Vector2(review ? 680 : 1160, -24);
                stepText.rectTransform.sizeDelta = new Vector2(review ? 750 : 736, 40);
            }
            if (footerText != null) footerText.text = review ? "Preços confirmados" : "Decisões em rascunho";
            if (strategyHelpText != null) strategyHelpText.text = review
                ? "Os três produtos e valores abaixo foram confirmados na abertura da sua partida."
                : "Os três produtos fazem parte do cardápio. Ajuste cada preço dentro da faixa permitida.";
            if (inputHelpText != null) inputHelpText.text = review
                ? "Consulte os preços salvos. Voltar retorna à tela anterior; Menu abre o mapa de gestão."
                : "Digite com vírgula ou ponto. Os botões ajustam R$ 1,00 por vez; o controle deslizante permite ajustes finos.";
            if (confirmButton != null) confirmButton.gameObject.SetActive(!review);
            if (backButton != null)
            {
                backButton.gameObject.SetActive(!review);
                var label = backButton.transform.Find("Label")?.GetComponent<TMP_Text>();
                if (label != null) label.text = review ? "Voltar" : "Voltar ao restaurante";
            }
        }

        public void SetRestaurant(string restaurant, bool review)
        {
            if (restaurantText != null) restaurantText.text = restaurant + (review ? " · preços confirmados" : " · defina o preço de cada produto");
        }

        public void SetPriceSummary(IReadOnlyList<ProductPricingItemView> items)
        {
            for (int i = 0; summaryNames != null && i < summaryNames.Length; i++)
            {
                bool hasItem = i < items.Count;
                summaryNames[i].text = hasItem ? items[i].Product.displayName : "";
                if (summaryPrices != null && i < summaryPrices.Length)
                {
                    summaryPrices[i].text = !hasItem ? "" : items[i].IsPriceValid ? ProductPricingItemView.Money(items[i].SelectedPrice) : "Revisar campo";
                    summaryPrices[i].color = hasItem && !items[i].IsPriceValid ? GamePalette.ErrorText : GamePalette.Ink;
                }
            }
        }

        public void SetConfirmEnabled(bool enabled)
        {
            if (confirmButton != null)
                confirmButton.interactable = enabled;
        }

        public void SetFooterVisible(bool visible)
        {
            SetActionFooterVisible(confirmButton, backButton, visible);
        }

        public void UpdatePriceCoherence(float value, string message)
        {
            if (priceCoherenceBarFill != null)
            {
                priceCoherenceBarFill.color = CoherenceColor(value);
                // A solid quad needs geometry sizing; Image.fillAmount only clips a sprite.
                var fill = priceCoherenceBarFill.rectTransform;
                fill.anchorMin = Vector2.zero;
                fill.anchorMax = new Vector2(Mathf.Clamp01(value), 1);
                fill.offsetMin = fill.offsetMax = Vector2.zero;
            }

            if (priceCoherenceText != null)
            {
                priceCoherenceText.gameObject.SetActive(true);
                priceCoherenceText.text = FormatCoherenceMessage("Coerência", value, message);
            }
        }

        public void BindConfirm(UnityEngine.Events.UnityAction action)
        {
            Bind(confirmButton, action);
        }

        public void BindBack(UnityEngine.Events.UnityAction action)
        {
            Bind(backButton, action);
        }

        private static void Bind(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null)
                return;

            button.onClick.RemoveAllListeners();

            if (action != null)
                button.onClick.AddListener(action);
        }

        private static void SetActionFooterVisible(Button primary, Button secondary, bool visible)
        {
            Transform current = primary != null ? primary.transform : secondary != null ? secondary.transform : null;

            while (current != null && current.name != "Footer")
                current = current.parent;

            if (current != null)
            {
                current.gameObject.SetActive(visible);
                return;
            }

            if (primary != null) primary.gameObject.SetActive(visible);
            if (secondary != null) secondary.gameObject.SetActive(visible);
        }

        private void PrepareProductListLayout()
        {
            var containerRect = productItemsContainer as RectTransform;
            if (containerRect == null)
                return;

            containerRect.anchorMin = new Vector2(0f, 1f);
            containerRect.anchorMax = new Vector2(1f, 1f);
            containerRect.pivot = new Vector2(0.5f, 1f);
            containerRect.anchoredPosition = Vector2.zero;
            containerRect.sizeDelta = new Vector2(0f, containerRect.sizeDelta.y);

            var layout = productItemsContainer.GetComponent<VerticalLayoutGroup>();
            if (layout == null)
                layout = productItemsContainer.gameObject.AddComponent<VerticalLayoutGroup>();

            layout.spacing = productItemSpacing;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var fitter = productItemsContainer.GetComponent<ContentSizeFitter>();
            if (fitter == null)
                fitter = productItemsContainer.gameObject.AddComponent<ContentSizeFitter>();

            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            if (productsScrollRect == null)
                productsScrollRect = GetComponentInChildren<ScrollRect>(true);

            if (productsScrollRect != null)
            {
                productsScrollRect.content = containerRect;
                productsScrollRect.horizontal = false;
                productsScrollRect.vertical = true;
                productsScrollRect.movementType = ScrollRect.MovementType.Clamped;
            }
        }

        private void ConfigureProductItemLayout(ProductPricingItemView item)
        {
            if (item == null)
                return;

            var rect = item.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = new Vector2(0f, productItemHeight);
            }

            var layoutElement = item.GetComponent<LayoutElement>();
            if (layoutElement == null)
                layoutElement = item.gameObject.AddComponent<LayoutElement>();

            layoutElement.preferredHeight = productItemHeight;
            layoutElement.minHeight = productItemHeight;
            layoutElement.flexibleHeight = 0f;
            layoutElement.flexibleWidth = 1f;
        }

        private void ResolveOptionalReferences()
        {
            if (priceCoherenceText == null)
                priceCoherenceText = FindTextByName("CoherenceText");
        }

        private TextMeshProUGUI FindTextByName(string objectName)
        {
            foreach (var text in GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (text.name == objectName)
                    return text;
            }

            return null;
        }

        private static string FormatCoherenceMessage(string label, float value, string tip)
        {
            string status = value switch
            {
                >= 0.7f => "Alta",
                >= 0.4f => "Média",
                > 0f => "Baixa",
                _ => "-"
            };

            return $"{label}: {status} ({Mathf.RoundToInt(value * 100f)}%)\n{tip}";
        }

        private static Color CoherenceColor(float value)
        {
            if (value >= 0.7f) return GamePalette.Money;
            if (value >= 0.4f) return GamePalette.Warn;
            return GamePalette.ErrorText;
        }
    }
}
