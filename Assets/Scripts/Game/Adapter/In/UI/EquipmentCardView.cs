using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Adapter.In.UI
{
    public sealed class EquipmentCardView : MonoBehaviour
    {
        [Header("Content")]
        [SerializeField] private Image equipmentImage;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI priceText;
        [SerializeField] private GameObject premiumIndicator;

        [Header("Selection")]
        [SerializeField] private GameObject selectionBorder;
        [SerializeField] private Button buyButton;
        [SerializeField] private TextMeshProUGUI buyButtonText;
        [SerializeField] private GameObject ownedIndicator;

        public EquipmentData Equipment { get; private set; }
        public bool IsOwned { get; private set; }
        public event Action<EquipmentCardView> SelectionRequested;

        private void OnEnable()
        {
            if (buyButton == null) return;
            buyButton.onClick.RemoveListener(OnBuyClicked);
            buyButton.onClick.AddListener(OnBuyClicked);
        }

        private void OnDisable()
        {
            if (buyButton != null) buyButton.onClick.RemoveListener(OnBuyClicked);
        }

        public void Setup(EquipmentData equipment, bool owned = false)
        {
            Equipment = equipment;

            if (equipmentImage != null)
            {
                equipmentImage.sprite = equipment != null ? equipment.icon : null;
                equipmentImage.preserveAspect = true;
                equipmentImage.gameObject.SetActive(equipmentImage.sprite != null);
            }

            if (nameText != null) nameText.text = equipment != null ? equipment.displayName : string.Empty;
            if (priceText != null) priceText.text = equipment != null ? $"R$ {equipment.cost:0,0.00}" : string.Empty;
            if (premiumIndicator != null)
            {
                bool isPremium = equipment != null
                    && equipment.category == EquipmentCategory.SPECIFIC;

                premiumIndicator.SetActive(isPremium);
            }

            SetOwned(owned);
        }

        public void SetOwned(bool owned)
        {
            IsOwned = owned;
            if (selectionBorder != null) selectionBorder.SetActive(owned);
            if (buyButton != null) buyButton.gameObject.SetActive(!owned);
            if (ownedIndicator != null) ownedIndicator.SetActive(owned);
            if (buyButtonText != null) buyButtonText.text = "Comprar";
        }

        public void SetPurchaseAvailable(bool available)
        {
            if (buyButton != null && !IsOwned)
                buyButton.interactable = available;
        }

        private void OnBuyClicked() => SelectionRequested?.Invoke(this);
    }
}
