using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Adapter.In.UI
{
    public sealed class FinancialScreenView : MonoBehaviour
    {
        [Header("Header")]
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI hintText;

        [Header("Credit Cards")]
        [SerializeField] private Transform bankCardsContainer;
        [SerializeField] private BankCardView bankCardPrefab;
        [SerializeField] private float cardHeight = 180f;

        [Header("Actions")]
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button backButton;

        public bool HasRequiredReferences()
        {
            if (bankCardsContainer == null)
            {
                Debug.LogError("[FinancialScreenView] Bank Cards Container nao foi configurado.");
                SetConfirmEnabled(false);
                return false;
            }

            if (bankCardPrefab == null)
            {
                Debug.LogError("[FinancialScreenView] Bank Card Prefab nao foi configurado.");
                SetConfirmEnabled(false);
                return false;
            }
            return true;
        }

        public BankCardView CreateBankCard()
        {
            var card = Instantiate(bankCardPrefab, bankCardsContainer);
            ConfigureCardLayout(card);
            return card;
        }

        public void ClearBankCards()
        {
            if (bankCardsContainer == null)
                return;

            for (int i = bankCardsContainer.childCount - 1; i >= 0; i--)
                Destroy(bankCardsContainer.GetChild(i).gameObject);
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

        private void ConfigureCardLayout(BankCardView card)
        {
            if (card == null)
                return;

            var layoutElement = card.GetComponent<LayoutElement>();
            if (layoutElement == null)
                layoutElement = card.gameObject.AddComponent<LayoutElement>();

            layoutElement.preferredHeight = cardHeight;
            layoutElement.minHeight = cardHeight;
            layoutElement.flexibleHeight = 0f;
            layoutElement.flexibleWidth = 1f;
        }
    }
}
