using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Adapter.In.UI
{
    public sealed class BankCardView : MonoBehaviour
    {
        [Header("Texts")]
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI maxAmountText;
        [SerializeField] private TextMeshProUGUI interestRateText;
        [SerializeField] private TextMeshProUGUI termText;

        [Header("Action")]
        [SerializeField] private Button selectButton;

        [Header("Visual")]
        [SerializeField] private Image cardBackground;

        [SerializeField] private GameObject selectionBorder;

        [Header("Coins")]
        [SerializeField] private Image coinPrefab;

        [SerializeField] private RectTransform coinContainer;

        [SerializeField] private float coinSpacing = 26f;

        [SerializeField, Range(1, 3)]
        private int defaultCoinCount = 1;

        public CreditLineData CreditLine { get; private set; }

        public event Action<BankCardView> Selected;

        private readonly List<Image> coinInstances = new();

        private bool isSelected;
        private bool hasOriginalCoinPosition;
        private Vector2 originalCoinAnchoredPosition;

        private static readonly Color CardNormal = HexColor(0x00, 0x68, 0xA4);       // #0068A4
        private static readonly Color CardSelected = HexColor(0xE6, 0xC8, 0x57);     // verde selecionado
        private static readonly Color CardHighlighted = HexColor(0xE6, 0xC8, 0x57);  // verde selecionado
        private static readonly Color CardPressed = HexColor(0x00, 0x3F, 0x70);

        private static readonly Color BorderSelected = HexColor(0x25, 0x63, 0xEB);   // azulzinho
        private static readonly Color BorderNormal = Color.white;

        private void Awake()
        {
            if (cardBackground == null)
                cardBackground = GetComponent<Image>();

            if (selectButton == null)
                selectButton = GetComponent<Button>();

            CacheOriginalCoinPosition();
            ConfigureSelectionBorder(false);
            ApplyVisualState();
        }

        private void OnEnable()
        {
            if (selectButton != null)
            {
                selectButton.onClick.RemoveListener(OnSelected);
                selectButton.onClick.AddListener(OnSelected);
            }
        }

        private void OnDisable()
        {
            if (selectButton != null)
                selectButton.onClick.RemoveListener(OnSelected);
        }

        public void Setup(CreditLineData creditLine)
        {
            Setup(creditLine, defaultCoinCount);
        }

        public void Setup(CreditLineData creditLine, int coinCount)
        {
            CreditLine = creditLine;

            if (nameText != null)
                nameText.text = creditLine.displayName;

            if (maxAmountText != null)
                maxAmountText.text = FormatCurrency(creditLine.maxAmount);

            if (interestRateText != null)
                interestRateText.text = FormatInterestRate(creditLine.monthlyInterestRate);

            if (termText != null)
                termText.text = $"{creditLine.termRounds} rodadas";

            SetCoinCount(coinCount);

            SetSelected(false);

            if (selectButton != null)
            {
                selectButton.onClick.RemoveListener(OnSelected);
                selectButton.onClick.AddListener(OnSelected);
            }
        }

        public void SetSelected(bool selected)
        {
            isSelected = selected;
            ApplyVisualState();
        }

        public void SetCoinCount(int count)
        {
            defaultCoinCount = Mathf.Clamp(count, 1, 3);

            EnsureCoinInstances();
            ApplyCoinCount(defaultCoinCount);
        }

       private void OnSelected()
       {
           Selected?.Invoke(this);
       }

        private void ApplyVisualState()
        {
            Color baseColor = isSelected ? CardSelected : CardNormal;

            if (cardBackground != null)
                cardBackground.color = baseColor;

            if (selectButton != null)
                selectButton.colors = BuildButtonColors(baseColor);

            ConfigureSelectionBorder(isSelected);
            ConfigureOutline(isSelected);
        }

        private void ConfigureSelectionBorder(bool visible)
        {
            if (selectionBorder == null)
                return;

            selectionBorder.SetActive(visible);

            var borderImage = selectionBorder.GetComponent<Image>();

            if (borderImage != null)
            {
                borderImage.color = BorderSelected;
                borderImage.raycastTarget = false;
            }
        }

        private void ConfigureOutline(bool visible)
        {
            var outline = GetComponent<Outline>();

            if (outline == null)
                outline = gameObject.AddComponent<Outline>();

            outline.enabled = visible;
            outline.effectColor = visible ? BorderSelected : BorderNormal;
            outline.effectDistance = new Vector2(3f, -3f);
        }

        private void EnsureCoinInstances()
        {
            if (coinPrefab == null)
                return;

            CacheOriginalCoinPosition();

            if (coinContainer == null)
                coinContainer = coinPrefab.transform.parent as RectTransform;

            if (!coinInstances.Contains(coinPrefab))
                coinInstances.Insert(0, coinPrefab);

            while (coinInstances.Count < 3)
            {
                Image clone = Instantiate(coinPrefab, coinContainer);
                clone.name = $"CoinIcon_{coinInstances.Count + 1}";
                clone.raycastTarget = false;

                coinInstances.Add(clone);
            }
        }

        private void ApplyCoinCount(int count)
        {
            if (coinPrefab == null)
                return;

            EnsureCoinInstances();

            int visibleCount = Mathf.Clamp(count, 1, 3);
            float totalWidth = (visibleCount - 1) * coinSpacing;

            for (int i = 0; i < coinInstances.Count; i++)
            {
                Image coin = coinInstances[i];

                if (coin == null)
                    continue;

                bool visible = i < visibleCount;
                coin.gameObject.SetActive(visible);

                if (!visible)
                    continue;

                RectTransform rt = coin.rectTransform;

                float xOffset = (i * coinSpacing) - (totalWidth / 2f);
                rt.anchoredPosition = originalCoinAnchoredPosition + new Vector2(xOffset, 0f);

                rt.SetAsLastSibling();
            }
        }

        private void CacheOriginalCoinPosition()
        {
            if (coinPrefab == null || hasOriginalCoinPosition)
                return;

            originalCoinAnchoredPosition = coinPrefab.rectTransform.anchoredPosition;
            hasOriginalCoinPosition = true;
        }

        private static ColorBlock BuildButtonColors(Color baseColor)
        {
            return new ColorBlock
            {
                normalColor = baseColor,
                highlightedColor = CardHighlighted,
                pressedColor = CardPressed,
                selectedColor = baseColor,
                disabledColor = new Color(0.35f, 0.35f, 0.35f, 0.85f),
                colorMultiplier = 1f,
                fadeDuration = 0.08f
            };
        }

        private static string FormatCurrency(float value)
        {
            return $"R$ {value:0,0.00}";
        }

        private static string FormatInterestRate(float value)
        {
            return $"{value * 100f:0.##}% ao mes";
        }

        private static Color HexColor(byte r, byte g, byte b, byte a = 255)
        {
            return new Color32(r, g, b, a);
        }
    }
}