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
            GameTypography.KeepSize(priceText,TypographyRole.Kpi);
            GameTypography.KeepSize(categoryText,TypographyRole.Label);
            GameTypography.KeepSize(descriptionText,TypographyRole.Body);
            GameTypography.KeepSize(statusText,TypographyRole.Caption);
            GameTypography.KeepSize(quantityText,TypographyRole.TableTotal);
            GameTypography.KeepSize(buyButtonText,TypographyRole.Button);
        }
        [SerializeField] private UnityEngine.UI.Image equipmentImage;
        [SerializeField] private TextMeshProUGUI nameText, priceText, categoryText, descriptionText, statusText, quantityText;
        [SerializeField] private GameObject premiumIndicator, selectionBorder, ownedIndicator;
        [SerializeField] private UnityEngine.UI.Button buyButton, removeButton;
        [SerializeField] private TextMeshProUGUI buyButtonText;
        [SerializeField] private UnityEngine.UI.Image interactionOutline;
        public EquipmentData Equipment { get; private set; }
        public bool IsOwned { get; private set; }
        public bool IsInCart { get; private set; }
        private bool available;
        public event Action<EquipmentCardView> SelectionRequested;
        public float RequiredHeight { get; private set; } = 360;
        private void OnEnable()
        {
            ApplyTypography();
            buyButton?.onClick.AddListener(Add); removeButton?.onClick.AddListener(Remove);
            GetComponent<UnityEngine.UI.Button>()?.onClick.AddListener(Toggle);
        }
        private void OnDisable()
        {
            buyButton?.onClick.RemoveListener(Add); removeButton?.onClick.RemoveListener(Remove);
            GetComponent<UnityEngine.UI.Button>()?.onClick.RemoveListener(Toggle);
        }
        private void Add() { if (!IsInCart) Toggle(); }
        private void Remove() { if (IsInCart) Toggle(); }
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
            SetOwned(owned); SetInCart(false);
        }
        public void SetOwned(bool owned) { IsOwned = owned; Refresh(); }
        public void SetInCart(bool inCart) { IsInCart = !IsOwned && inCart; Refresh(); }
        public void SetCartAvailable(bool value) { available = value; Refresh(); }
        private void Refresh()
        {
            GetComponent<SharedControlAppearance>()?.SetSelected(IsInCart);
            var root = GetComponent<UnityEngine.UI.Button>(); if (root != null) root.interactable = available && !IsOwned;
            if (selectionBorder != null) selectionBorder.SetActive(false);
            if (ownedIndicator != null) ownedIndicator.SetActive(false);
            if (buyButton != null) { buyButton.gameObject.SetActive(true); buyButton.interactable = available && !IsOwned && !IsInCart; }
            if (removeButton != null) removeButton.interactable = available && !IsOwned && IsInCart;
            if (buyButtonText != null) buyButtonText.text = "+";
            if (quantityText != null) quantityText.text = IsInCart || IsOwned ? "1" : "0";
            if (statusText != null) statusText.text = IsOwned ? "Adquirido" : !GameSessionState.HasActiveSession ? "Sessão indisponível"
                : !available ? "Requer os 8 básicos" : IsInCart ? "No carrinho" : "Disponível";
        }
        private void LateUpdate()
        {
            var state = GetComponent<SharedControlAppearance>();
            if (interactionOutline != null && state != null)
            {
                interactionOutline.enabled = state.CurrentState == "hover" || state.CurrentState == "pressed";
                interactionOutline.color = Theme.GamePalette.WithAlpha(Theme.GamePalette.Primary, state.CurrentState == "pressed" ? .85f : .4f);
            }
            if (nameText == null || descriptionText == null || quantityText == null) return;
            if (((RectTransform)transform).rect.width < 500)
            {
                float nameH = Mathf.Max(64, nameText.GetPreferredValues(nameText.text, nameText.rectTransform.rect.width, float.PositiveInfinity).y);
                nameText.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, nameH);
                float artY = 24 + nameH + 48;
                equipmentImage.rectTransform.anchoredPosition = new Vector2(24, -artY);
                float portraitY = artY + 274;
                priceText.rectTransform.anchoredPosition = new Vector2(24, -portraitY);
                descriptionText.rectTransform.anchoredPosition = new Vector2(24, -portraitY - 48);
                float descH = Mathf.Max(54, descriptionText.GetPreferredValues(descriptionText.text, descriptionText.rectTransform.rect.width, float.PositiveInfinity).y);
                descriptionText.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, descH);
                statusText.rectTransform.anchoredPosition = new Vector2(24, -portraitY - 54 - descH);
                portraitY += 104 + descH;
                removeButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(24, -portraitY);
                quantityText.rectTransform.anchoredPosition = new Vector2(100, -portraitY);
                buyButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(326, -portraitY);
                RequiredHeight = portraitY + 92;
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
            buyButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(556, -y);
            removeButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(286, -y);
            quantityText.rectTransform.anchoredPosition = new Vector2(366, -y);
        }
    }
}
