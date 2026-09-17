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
        [SerializeField] private TextMeshProUGUI descriptionText;
        [SerializeField] private TextMeshProUGUI maxAmountText;
        [SerializeField] private TextMeshProUGUI interestRateText;
        [SerializeField] private TextMeshProUGUI termText;

        [Header("Action")]
        [SerializeField] private Button selectButton;

        [Header("Visual")]
        [SerializeField] private Image creditLineIcon;

        [SerializeField] private GameObject selectionBorder;

        [Header("Coins")]
        [SerializeField] private Image coinPrefab;

        [SerializeField] private RectTransform coinContainer;

        [SerializeField] private float coinSpacing = 26f;

        [SerializeField, Min(0)]
        private int defaultCoinCount = 1;

        public CreditLineData CreditLine { get; private set; }

        public event Action<BankCardView> Selected;

        private readonly List<Image> coinInstances = new();

        private bool isSelected;
        private bool hasOriginalCoinPosition;
        private Vector2 originalCoinAnchoredPosition;
        private float originalCoinAlpha = 1f;

        private void Awake()
        {
            if (selectButton == null)
                selectButton = GetComponent<Button>();

            CacheOriginalCoinPosition();
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

            ConfigureCreditLineIcon(creditLine.icon);

            if (nameText != null)
                nameText.text = creditLine.displayName;

            if (descriptionText == null)
            {
                Transform descriptionTransform = transform.Find("creditLineDescription");
                if (descriptionTransform != null)
                    descriptionText = descriptionTransform.GetComponent<TextMeshProUGUI>();
            }

            if (descriptionText != null)
                descriptionText.text = creditLine.cardDescription;

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
            defaultCoinCount = Mathf.Max(0, count);

            EnsureCoinInstances(defaultCoinCount);
            ApplyCoinCount(defaultCoinCount);
        }

       private void OnSelected()
       {
           Selected?.Invoke(this);
       }

        private void ConfigureCreditLineIcon(Sprite icon)
        {
            if (creditLineIcon == null)
            {
                var existing = transform.Find("CreditLineIcon");
                if (existing != null)
                    creditLineIcon = existing.GetComponent<Image>();
            }

            if (creditLineIcon == null)
            {
                var iconObject = new GameObject("CreditLineIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                iconObject.transform.SetParent(transform, false);
                creditLineIcon = iconObject.GetComponent<Image>();

                RectTransform iconRect = creditLineIcon.rectTransform;
                iconRect.anchorMin = new Vector2(0f, 1f);
                iconRect.anchorMax = new Vector2(0f, 1f);
                iconRect.pivot = new Vector2(0f, 1f);
                iconRect.anchoredPosition = new Vector2(24f, -24f);
                iconRect.sizeDelta = new Vector2(96f, 96f);
            }

            creditLineIcon.sprite = icon;
            creditLineIcon.preserveAspect = true;
            creditLineIcon.raycastTarget = false;
            creditLineIcon.gameObject.SetActive(icon != null);
            creditLineIcon.transform.SetAsLastSibling();
        }

        private void ApplyVisualState()
        {
            if (selectionBorder == null)
                return;

            selectionBorder.SetActive(isSelected);
        }

        private void EnsureCoinInstances(int requiredCount)
        {
            if (coinPrefab == null)
                return;

            CacheOriginalCoinPosition();

            if (coinContainer == null)
                coinContainer = coinPrefab.transform.parent as RectTransform;

            if (!coinInstances.Contains(coinPrefab))
                coinInstances.Insert(0, coinPrefab);

            while (coinInstances.Count < requiredCount)
            {
                Image clone = Instantiate(coinPrefab, coinContainer);
                clone.name = $"CoinIcon_{coinInstances.Count + 1}";
                clone.raycastTarget = false;

                coinInstances.Add(clone);
            }
        }

        private void ApplyCoinCount(int count)
        {
            int visibleCount = Mathf.Max(0, count);
            bool representsZeroCoins = visibleCount == 0;
            int displayedCoinCount = representsZeroCoins ? 1 : visibleCount;

            if (coinPrefab == null)
                return;

            EnsureCoinInstances(displayedCoinCount);
            float totalWidth = (displayedCoinCount - 1) * coinSpacing;

            for (int i = 0; i < coinInstances.Count; i++)
            {
                Image coin = coinInstances[i];

                if (coin == null)
                    continue;

                bool visible = i < displayedCoinCount;
                coin.gameObject.SetActive(visible);

                if (!visible)
                    continue;

                Color coinColor = coin.color;
                coinColor.a = representsZeroCoins ? 0.4f : originalCoinAlpha;
                coin.color = coinColor;

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
            originalCoinAlpha = coinPrefab.color.a;
            hasOriginalCoinPosition = true;
        }

        private static string FormatCurrency(float value)
        {
            return $"R$ {value:0,0.00}";
        }

        private static string FormatInterestRate(float value)
        {
            return $"{value * 100f:0.##}% ao mes";
        }

    }
}
