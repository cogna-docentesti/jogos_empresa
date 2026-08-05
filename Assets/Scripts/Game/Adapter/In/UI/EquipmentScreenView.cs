using System.Collections.Generic;
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

        [Header("Actions")]
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button backButton;

        private readonly List<EquipmentCardView> cards = new();

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

        public void SetAvailableCash(float availableCash)
        {
            if (availableCashText != null)
                availableCashText.text = $"Crédito disponível:\nR$ {availableCash:0,0.00}";
        }

        public void SetHint(string message)
        {
            if (hintText != null) hintText.text = message;
        }

        public void SetConfirmEnabled(bool enabled)
        {
            if (confirmButton != null) confirmButton.interactable = enabled;
        }

        public void BindConfirm(UnityAction action) => Bind(confirmButton, action);
        public void BindBack(UnityAction action) => Bind(backButton, action);

        private static void Bind(Button button, UnityAction action)
        {
            if (button == null) return;
            button.onClick.RemoveAllListeners();
            if (action != null) button.onClick.AddListener(action);
        }
    }
}
