using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Adapter.In.UI
{
    public sealed class ProductPricingItemView : MonoBehaviour
    {
        [Header("Product")]
        [SerializeField] private TextMeshProUGUI productNameText;
        [SerializeField] private Image productImage;

        [Header("Price")]
        [SerializeField] private Slider priceSlider;
        [SerializeField] private TextMeshProUGUI selectedPriceText;
        [SerializeField] private TextMeshProUGUI minPriceText;
        [SerializeField] private TextMeshProUGUI maxPriceText;

        private ProductData _product;

        public ProductData Product => _product;
        public float SelectedPrice => priceSlider != null ? priceSlider.value : 0f;

        public event Action<ProductPricingItemView> PriceChanged;

        public void Setup(ProductData product, float initialPrice)
        {
            _product = product;

            if (productNameText != null)
                productNameText.text = product.displayName;

            if (productImage != null)
            {
                productImage.sprite = product.productSprite;
                productImage.enabled = product.productSprite != null;
            }

            if (minPriceText != null)
                minPriceText.text = FormatPrice(product.minPrice);

            if (maxPriceText != null)
                maxPriceText.text = FormatPrice(product.maxPrice);

            if (priceSlider != null)
            {
                priceSlider.onValueChanged.RemoveListener(OnSliderChanged);
                priceSlider.minValue = product.minPrice;
                priceSlider.maxValue = product.maxPrice;
                priceSlider.wholeNumbers = false;
                priceSlider.value = product.ClampPrice(initialPrice);
                priceSlider.onValueChanged.AddListener(OnSliderChanged);
            }

            UpdateSelectedPriceText();
        }

        private void OnSliderChanged(float _)
        {
            UpdateSelectedPriceText();
            PriceChanged?.Invoke(this);
        }

        private void UpdateSelectedPriceText()
        {
            if (selectedPriceText != null)
                selectedPriceText.text = FormatPrice(SelectedPrice);
        }

        private static string FormatPrice(float value)
        {
            return $"R$ {value:0.00}";
        }
    }
}
