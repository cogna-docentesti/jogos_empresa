using System;
using System.Globalization;
using System.Text.RegularExpressions;
using Game.Adapter.In.UI.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.Adapter.In.UI
{
    public sealed class ProductPricingItemView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI productNameText;
        [SerializeField] private UnityEngine.UI.Image productImage;
        [SerializeField] private TextMeshProUGUI selectedPriceText;
        [SerializeField] private TextMeshProUGUI minPriceText;
        [SerializeField] private TextMeshProUGUI maxPriceText;
        [SerializeField] private TMP_InputField priceInput;
        [SerializeField] private UnityEngine.UI.Button decreaseButton;
        [SerializeField] private UnityEngine.UI.Button decreaseHalfButton;
        [SerializeField] private UnityEngine.UI.Button increaseHalfButton;
        [SerializeField] private UnityEngine.UI.Button increaseButton;
        [SerializeField] private TextMeshProUGUI errorText;
        [SerializeField] private UnityEngine.UI.Image editingOutline;
        [SerializeField] private TextMeshProUGUI editingText;
        private static readonly CultureInfo Brazilian = CultureInfo.GetCultureInfo("pt-BR");
        private ProductData _product;
        private float _price;
        private bool _readOnly;
        public ProductData Product => _product;
        public float SelectedPrice => _price;
        public bool IsPriceValid { get; private set; }
        public string DraftInput => priceInput != null ? priceInput.text : Number(_price);
        public event Action<ProductPricingItemView> PriceChanged;

        private void OnEnable() => ApplyTypography();
        private void ApplyTypography()
        {
            GameTypography.KeepSize(productNameText,TypographyRole.SectionTitle);
            GameTypography.KeepSize(selectedPriceText,TypographyRole.Table);
            GameTypography.KeepSize(minPriceText,TypographyRole.Table);
            GameTypography.KeepSize(maxPriceText,TypographyRole.Table);
            GameTypography.KeepSize(errorText,TypographyRole.Body);
            GameTypography.KeepSize(editingText,TypographyRole.Caption);
            if (priceInput != null) { GameTypography.KeepSize(priceInput.textComponent,TypographyRole.Input); GameTypography.KeepSize(priceInput.placeholder as TMP_Text,TypographyRole.Input); }
        }
        public void Setup(ProductData product, float initialPrice)
        {
            ApplyTypography();
            _product = product;
            _price = product.ClampPrice(initialPrice);
            IsPriceValid = product.IsPriceInRange(_price) && !float.IsNaN(_price) && !float.IsInfinity(_price);
            if (productNameText != null) productNameText.text = product.displayName;
            if (productImage != null)
            {
                productImage.sprite = product.productSprite;
                productImage.type = UnityEngine.UI.Image.Type.Simple;
                productImage.preserveAspect = true;
                productImage.color = Color.white;
                productImage.raycastTarget = false;
                productImage.enabled = product.productSprite != null;
            }
            if (minPriceText != null) minPriceText.text = "Mín. " + Money(product.minPrice);
            if (maxPriceText != null) maxPriceText.text = "Máx. " + Money(product.maxPrice);
            if (priceInput != null)
            {
                priceInput.onValueChanged.RemoveListener(OnInputChanged);
                priceInput.onEndEdit.RemoveListener(OnInputEndEdit);
                priceInput.SetTextWithoutNotify(Number(_price));
                priceInput.onValueChanged.AddListener(OnInputChanged);
                priceInput.onEndEdit.AddListener(OnInputEndEdit);
            }
            Bind(decreaseButton, () => AdjustPrice(-1m));
            Bind(decreaseHalfButton, () => AdjustPrice(-0.50m));
            Bind(increaseHalfButton, () => AdjustPrice(0.50m));
            Bind(increaseButton, () => AdjustPrice(1m));
            FitProductName();
            Refresh();
        }

        private void FitProductName()
        {
            if (productNameText == null) return;
            productNameText.ForceMeshUpdate();
            float height = Mathf.Max(116, productNameText.preferredHeight);
            productNameText.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            float statusTop = Mathf.Max(176, 28 + height + 16);
            if (editingText != null) editingText.rectTransform.anchoredPosition = new Vector2(280, -statusTop);
            var layout = GetComponent<UnityEngine.UI.LayoutElement>();
            if (layout != null) layout.preferredHeight = Mathf.Max(layout.minHeight, statusTop + 52);
        }

        // Grouping, exponent, signs and extra precision are ambiguous in a price editor.
        public static bool TryParsePrice(string text, ProductData product, out float price)
        {
            price = 0;
            string value = text?.Trim();
            return product != null && value != null && value.Length <= 16
                && Regex.IsMatch(value, @"^\d+([,.]\d{1,2})?$")
                && float.TryParse(value.Replace(',', '.'), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out price)
                && !float.IsNaN(price) && !float.IsInfinity(price) && product.IsPriceInRange(price);
        }
        public static string Money(float price) => "R$ " + ((double)price).ToString("N2", Brazilian);
        private static string Number(float price) => ((double)price).ToString("F2", Brazilian);
        private static void Bind(UnityEngine.UI.Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null) return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }
        public void SetReadOnly(bool value)
        {
            _readOnly = value;
            if (priceInput != null) { priceInput.readOnly = value; priceInput.interactable = !value; }
            Refresh();
        }
        public void RestoreDraftInput(string value)
        {
            if (priceInput == null || _readOnly) return;
            priceInput.SetTextWithoutNotify(value);
            OnInputChanged(value);
        }
        private void OnInputChanged(string value)
        {
            if (_readOnly) return;
            IsPriceValid = TryParsePrice(value, _product, out float parsed);
            if (IsPriceValid) _price = parsed;
            Refresh();
            PriceChanged?.Invoke(this);
        }
        private void OnInputEndEdit(string value)
        {
            if (IsPriceValid && priceInput != null) priceInput.SetTextWithoutNotify(Number(_price));
        }
        private bool TryGetAdjustedPrice(decimal delta, out float price)
        {
            price = _price;
            if (_readOnly || _product == null || !decimal.TryParse(_price.ToString("R", CultureInfo.InvariantCulture),
                    NumberStyles.Float, CultureInfo.InvariantCulture, out var current))
                return false;

            // Add the delta to the saved amount, including any cents outside the half-real grid.
            price = (float)(current + delta);
            return price != _price && _product.IsPriceInRange(price);
        }

        private void AdjustPrice(decimal delta)
        {
            if (!TryGetAdjustedPrice(delta, out var price)) return;

            _price = price;
            IsPriceValid = true;
            priceInput?.SetTextWithoutNotify(Number(_price));
            Refresh();
            PriceChanged?.Invoke(this);
        }
        private void Refresh()
        {
            if (selectedPriceText != null) selectedPriceText.text = Money(_price);
            SetAdjustmentEnabled(decreaseButton, -1m);
            SetAdjustmentEnabled(decreaseHalfButton, -0.50m);
            SetAdjustmentEnabled(increaseHalfButton, 0.50m);
            SetAdjustmentEnabled(increaseButton, 1m);
            if (errorText != null)
            {
                errorText.text = IsPriceValid ? "" : "Use a faixa permitida e até 2 decimais.";
                errorText.color = GamePalette.ErrorText;
            }
            if (priceInput != null && priceInput.TryGetComponent<SharedControlAppearance>(out var appearance))
            { appearance.IsInvalid = !IsPriceValid; appearance.Refresh(); }
        }

        private void SetAdjustmentEnabled(UnityEngine.UI.Button button, decimal delta)
        {
            if (button != null) button.interactable = TryGetAdjustedPrice(delta, out _);
        }

        private void LateUpdate()
        {
            var current = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            bool editing = !_readOnly && current != null && current.transform.IsChildOf(transform);
            if (editingOutline != null) editingOutline.enabled = editing;
            if (editingText != null) editingText.text = _readOnly ? "Preço salvo" : editing ? "Em edição" : string.Empty;
        }
    }
}
