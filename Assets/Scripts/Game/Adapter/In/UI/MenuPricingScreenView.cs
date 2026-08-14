using Game.Adapter.In.UI;
using Game.Adapter.In.UI.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Adapter.In.UI
{
    public sealed class MenuPricingScreenView : MonoBehaviour
    {
        [Header("Screen")]
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI hintText;

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
                Destroy(productItemsContainer.GetChild(i).gameObject);
        }

        public void SetTitle(string message)
        {
            if (titleText != null)
                titleText.text = message;
        }

        public void SetHint(string message)
        {
            if (hintText != null)
                hintText.text = message;
        }

        public void SetConfirmEnabled(bool enabled)
        {
            if (confirmButton != null)
                confirmButton.interactable = enabled;
        }

        public void UpdatePriceCoherence(float value, string message)
        {
            // So o preenchimento, que e dado. A cor da barra e a do Image na cena.
            if (priceCoherenceBarFill != null)
                priceCoherenceBarFill.fillAmount = value;

            if (priceCoherenceText != null)
            {
                priceCoherenceText.gameObject.SetActive(true);
                priceCoherenceText.text = FormatCoherenceMessage("Preco", value, message);
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
                >= 0.4f => "Media",
                > 0f => "Baixa",
                _ => "-"
            };

            return $"{label}: {status} ({Mathf.RoundToInt(value * 100f)}%)\n{tip}";
        }
    }
}

