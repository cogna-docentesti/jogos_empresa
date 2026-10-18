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
        [SerializeField] private UnityEngine.UI.Slider priceSlider;
        [SerializeField] private TextMeshProUGUI selectedPriceText;
        [SerializeField] private TextMeshProUGUI minPriceText;
        [SerializeField] private TextMeshProUGUI maxPriceText;
        [SerializeField] private TMP_InputField priceInput;
        [SerializeField] private UnityEngine.UI.Button decreaseButton;
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

        public void Setup(ProductData product, float initialPrice)
        {
            GameTypography.KeepSize(productNameText,TypographyRole.SectionTitle);
            GameTypography.KeepSize(selectedPriceText,TypographyRole.TableTotal);
            GameTypography.KeepSize(minPriceText,TypographyRole.Caption);
            GameTypography.KeepSize(maxPriceText,TypographyRole.Caption);
            GameTypography.KeepSize(errorText,TypographyRole.Body);
            GameTypography.KeepSize(editingText,TypographyRole.Caption);
            if (priceInput != null) { GameTypography.KeepSize(priceInput.textComponent,TypographyRole.Input); GameTypography.KeepSize(priceInput.placeholder as TMP_Text,TypographyRole.Input); }
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
            if (priceSlider != null)
            {
                priceSlider.onValueChanged.RemoveListener(OnSliderChanged);
                priceSlider.minValue = product.minPrice;
                priceSlider.maxValue = product.maxPrice;
                priceSlider.wholeNumbers = false;
                priceSlider.SetValueWithoutNotify(_price);
                priceSlider.onValueChanged.AddListener(OnSliderChanged);
            }
            if (priceInput != null)
            {
                priceInput.onValueChanged.RemoveListener(OnInputChanged);
                priceInput.onEndEdit.RemoveListener(OnInputEndEdit);
                priceInput.SetTextWithoutNotify(Number(_price));
                priceInput.onValueChanged.AddListener(OnInputChanged);
                priceInput.onEndEdit.AddListener(OnInputEndEdit);
            }
            Bind(decreaseButton, Decrease);
            Bind(increaseButton, Increase);
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
            if (priceSlider != null) priceSlider.interactable = !value;
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
            if (IsPriceValid)
            {
                _price = parsed;
                priceSlider?.SetValueWithoutNotify(_price);
            }
            Refresh();
            PriceChanged?.Invoke(this);
        }
        private void OnInputEndEdit(string value)
        {
            if (IsPriceValid && priceInput != null) priceInput.SetTextWithoutNotify(Number(_price));
        }
        private void OnSliderChanged(float value)
        {
            if (_readOnly) { priceSlider.SetValueWithoutNotify(_price); return; }
            SetPrice(value);
        }
        private void Decrease() { if (!_readOnly) SetPrice(_price - 1f); }
        private void Increase() { if (!_readOnly) SetPrice(_price + 1f); }
        private void SetPrice(float value)
        {
            _price = _product.ClampPrice((float)Math.Round(value, 2, MidpointRounding.AwayFromZero));
            IsPriceValid = _product.IsPriceInRange(_price);
            priceSlider?.SetValueWithoutNotify(_price);
            priceInput?.SetTextWithoutNotify(Number(_price));
            Refresh();
            PriceChanged?.Invoke(this);
        }
        private void Refresh()
        {
            if (selectedPriceText != null) selectedPriceText.text = Money(_price);
            if (decreaseButton != null) decreaseButton.interactable = !_readOnly && (!IsPriceValid || _price > _product.minPrice);
            if (increaseButton != null) increaseButton.interactable = !_readOnly && (!IsPriceValid || _price < _product.maxPrice);
            if (errorText != null)
            {
                errorText.text = IsPriceValid ? "" : "Use a faixa permitida e até 2 decimais.";
                errorText.color = GamePalette.ErrorText;
            }
            if (priceInput != null && priceInput.TryGetComponent<SharedControlAppearance>(out var appearance))
            { appearance.IsInvalid = !IsPriceValid; appearance.Refresh(); }
        }
        private void LateUpdate()
        {
            var current = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            bool editing = !_readOnly && current != null && current.transform.IsChildOf(transform);
            if (editingOutline != null) editingOutline.enabled = editing;
            if (editingText != null) editingText.text = _readOnly ? "Preço salvo" : editing ? "Em edição" : "Incluído no cardápio";
        }
    }
}
