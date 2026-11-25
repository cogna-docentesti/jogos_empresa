using Game.Adapter.In.UI.Theme;
using System;
using TMPro;
using UnityEngine;

namespace Game.Adapter.In.UI
{
    public sealed class EquipmentCardView : MonoBehaviour
    {
        private void ApplyTypography()
        {
            GameTypography.KeepSize(nameText,TypographyRole.SectionTitle);
            GameTypography.KeepSize(priceText,TypographyRole.Table);
            GameTypography.KeepSize(categoryText,TypographyRole.Label);
            GameTypography.KeepSize(descriptionText,TypographyRole.Body);
            GameTypography.KeepSize(statusText,TypographyRole.Caption);
            GameTypography.KeepSize(cartActionLabel,TypographyRole.Button);
        }
        [SerializeField] private UnityEngine.UI.Image equipmentImage;
        [SerializeField] private TextMeshProUGUI nameText, priceText, categoryText, descriptionText, statusText;
        [SerializeField] private GameObject premiumIndicator, selectionBorder, ownedIndicator;
        [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("buyButton")]
        private UnityEngine.UI.Button cartActionButton;
        [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("buyButtonText")]
        private TextMeshProUGUI cartActionLabel;
        public EquipmentData Equipment { get; private set; }
        public bool IsOwned { get; private set; }
        public bool IsInCart { get; private set; }
        public RectTransform AddAction => cartActionButton != null ? (RectTransform)cartActionButton.transform : null;
        private bool available;
        public const float PortraitActionBottomInset = 48;
        public event Action<EquipmentCardView> SelectionRequested;
        public float RequiredHeight { get; private set; } = 360;
        private void OnEnable()
        {
            ApplyTypography();
            cartActionButton?.onClick.AddListener(Toggle);
            GetComponent<UnityEngine.UI.Button>()?.onClick.AddListener(Toggle);
        }
        private void OnDisable()
        {
            cartActionButton?.onClick.RemoveListener(Toggle);
            GetComponent<UnityEngine.UI.Button>()?.onClick.RemoveListener(Toggle);
        }
        private void Toggle() { if (available && !IsOwned) SelectionRequested?.Invoke(this); }
        public void Setup(EquipmentData equipment, bool owned = false)
        {
            ApplyTypography();
            Equipment = equipment;
            equipmentImage.sprite = equipment != null ? equipment.icon : null;
            equipmentImage.preserveAspect = true; equipmentImage.type = UnityEngine.UI.Image.Type.Simple;
            equipmentImage.color = Color.white; equipmentImage.raycastTarget = false;
            nameText.text = equipment != null ? equipment.displayName : string.Empty;
            priceText.text = equipment != null ? FinancialScreenView.Money(equipment.cost) : string.Empty;
            if (premiumIndicator != null) premiumIndicator.SetActive(false);
            if (categoryText != null) categoryText.text = equipment != null && equipment.category == EquipmentCategory.SPECIFIC ? "ESPECÍFICO" : "BÁSICO";
            if (descriptionText != null && equipment != null)
            {
                descriptionText.text = equipment.category == EquipmentCategory.BASIC ? "Para todos os restaurantes" : "Para o seu tipo de restaurante";
                if (equipment.qualityBonus != 0 || equipment.capacityBonus != 0)
                    descriptionText.text += $"\nQualidade +{equipment.qualityBonus:P0} • Capacidade +{equipment.capacityBonus}";
            }
            SetCartState(owned, false, false);
        }
        public void SetCartState(bool owned, bool inCart, bool canUseCart)
        {
            IsOwned = owned;
            IsInCart = !owned && inCart;
            available = canUseCart;
            var root = GetComponent<UnityEngine.UI.Button>(); if (root != null) root.interactable = available && !IsOwned;
            GetComponent<SharedControlAppearance>()?.SetSelected(IsInCart);
            if (selectionBorder != null) selectionBorder.SetActive(false);
            if (ownedIndicator != null) ownedIndicator.SetActive(false);
            if (cartActionButton != null)
            {
                cartActionButton.interactable = available && !IsOwned;
                cartActionButton.gameObject.SetActive(!IsOwned);
            }
            if (cartActionLabel != null) cartActionLabel.text = IsInCart ? "Remover do carrinho" : "Adicionar ao carrinho";
            if (statusText != null) statusText.text = IsOwned ? "Adquirido" : !GameSessionState.HasActiveSession ? "Sessão indisponível"
                : !available ? "Requer os 8 básicos" : IsInCart ? "No carrinho" : "Disponível";
        }
        private void LateUpdate() => RefreshLayout();
        public void RefreshLayout()
        {
            if (nameText == null || descriptionText == null || cartActionButton == null) return;
            if (((RectTransform)transform).rect.width < 500)
            {
                float nameH = Mathf.Max(64, nameText.GetPreferredValues(nameText.text, nameText.rectTransform.rect.width, float.PositiveInfinity).y);
                nameText.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, nameH);
                float artY = 24 + nameH + 48;
                var artwork = equipmentImage.rectTransform;
                artwork.anchoredPosition = new Vector2(0, -artY - artwork.rect.height * .5f);
                float portraitY = artY + artwork.rect.height + 14;
                priceText.rectTransform.anchoredPosition = new Vector2(24, -portraitY);
                descriptionText.rectTransform.anchoredPosition = new Vector2(24, -portraitY - 48);
                float descH = Mathf.Max(54, descriptionText.GetPreferredValues(descriptionText.text, descriptionText.rectTransform.rect.width, float.PositiveInfinity).y);
                descriptionText.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, descH);
                statusText.rectTransform.anchoredPosition = new Vector2(24, -portraitY - 54 - descH);
                float actionTop = portraitY + 104 + descH;
                RequiredHeight = actionTop + ((RectTransform)cartActionButton.transform).rect.height + PortraitActionBottomInset;
                return;
            }
            float nameHeight = Mathf.Max(72, nameText.GetPreferredValues(nameText.text, nameText.rectTransform.rect.width, float.PositiveInfinity).y);
            float descriptionHeight = Mathf.Max(56, descriptionText.GetPreferredValues(descriptionText.text, descriptionText.rectTransform.rect.width, float.PositiveInfinity).y);
            nameText.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, nameHeight);
            float y = 24 + nameHeight + 12;
            priceText.rectTransform.anchoredPosition = new Vector2(286, -y);
            descriptionText.rectTransform.anchoredPosition = new Vector2(286, -y - 48);
            descriptionText.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, descriptionHeight);
            y += 48 + descriptionHeight + 16;
            RequiredHeight = Mathf.Max(360, y + 68 + 24);
            cartActionButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(286, -y);
        }
    }
}
