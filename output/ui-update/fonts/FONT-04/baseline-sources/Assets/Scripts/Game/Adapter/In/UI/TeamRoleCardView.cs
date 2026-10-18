using System;
using TMPro;
using UnityEngine;

namespace Game.Adapter.In.UI
{
    public sealed class TeamRoleCardView : MonoBehaviour
    {
        [SerializeField] private UnityEngine.UI.Image portrait, environment, interactionOutline;
        [SerializeField] private TextMeshProUGUI nameText, salaryText, capacityText, requirementText, quantityText;
        [SerializeField] private UnityEngine.UI.Button addButton, removeButton;
        public RoleData Role { get; private set; }
        public int Quantity { get; private set; }
        public float RequiredHeight { get; private set; } = 360;
        public event Action<TeamRoleCardView, int> QuantityRequested;
        private bool available;
        private int required;
        private void OnEnable() { addButton?.onClick.AddListener(Add); removeButton?.onClick.AddListener(Remove); }
        private void OnDisable() { addButton?.onClick.RemoveListener(Add); removeButton?.onClick.RemoveListener(Remove); }
        private void Add() { if (available && Quantity < int.MaxValue) QuantityRequested?.Invoke(this, Quantity + 1); }
        private void Remove() { if (available && Quantity > 0) QuantityRequested?.Invoke(this, Quantity - 1); }
        public void Setup(RoleData role, Sprite face, Sprite setting, int minimum)
        {
            Role = role; required = minimum;
            foreach (var pair in new[] { (portrait, face), (environment, setting) })
            { pair.Item1.sprite = pair.Item2; pair.Item1.color = Color.white; pair.Item1.preserveAspect = true; pair.Item1.type = UnityEngine.UI.Image.Type.Simple; pair.Item1.raycastTarget = false; }
            nameText.text = Label(role);
            salaryText.text = FinancialScreenView.Money(role.salary) + " / mês";
            capacityText.text = $"{role.maxClientsSupported:N0} clientes/mês por pessoa\nQualidade +{role.qualityContribution:P0} por pessoa";
        }
        public static string Label(RoleData role) => role == null ? "Função indisponível" : role.id switch
        { "attendant" => "Atendente", "grill_cook" => "Chapeiro", "head_chef" => "Chef de cozinha", "manager" => "Gerente", "sushi_chef" => "Profissional de sushi", _ => role.displayName };
        public void SetQuantity(int quantity, bool enabled, bool known = true)
        {
            Quantity = quantity; available = enabled;
            quantityText.text = known ? quantity.ToString() : "—";
            addButton.interactable = enabled && quantity < int.MaxValue; removeButton.interactable = enabled && quantity > 0;
            GetComponent<UnityEngine.UI.Button>().interactable = enabled;
            GetComponent<SharedControlAppearance>()?.SetSelected(quantity > 0);
            requirementText.text = !known ? "Quantidade indisponível." : required > 0 ? $"Mínimo do restaurante: {required}  •  Atual: {quantity}" : "Função adicional";
        }
        private void LateUpdate()
        {
            var state = GetComponent<SharedControlAppearance>();
            if (interactionOutline != null && state != null)
            { interactionOutline.enabled = state.CurrentState == "hover" || state.CurrentState == "pressed"; interactionOutline.color = Theme.GamePalette.WithAlpha(Theme.GamePalette.Primary, state.CurrentState == "pressed" ? .85f : .4f); }
            if (nameText == null || addButton == null) return;
            if (((RectTransform)transform).rect.width < 500)
            {
                float nameH = Mathf.Max(48, nameText.GetPreferredValues(nameText.text, nameText.rectTransform.rect.width, float.PositiveInfinity).y);
                nameText.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, nameH);
                float artY = 20 + nameH + 12;
                environment.rectTransform.anchoredPosition = new Vector2(20, -artY);
                portrait.rectTransform.anchoredPosition = new Vector2(20, -artY);
                float portraitY = artY + 342;
                salaryText.rectTransform.anchoredPosition = new Vector2(20, -portraitY);
                capacityText.rectTransform.anchoredPosition = new Vector2(20, -portraitY - 44);
                requirementText.rectTransform.anchoredPosition = new Vector2(20, -portraitY - 112);
                portraitY += 178;
                removeButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(20, -portraitY);
                quantityText.rectTransform.anchoredPosition = new Vector2(96, -portraitY);
                addButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(322, -portraitY);
                RequiredHeight = portraitY + 88;
                return;
            }
            float height = Mathf.Max(64, nameText.GetPreferredValues(nameText.text, nameText.rectTransform.rect.width, float.PositiveInfinity).y);
            nameText.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            float y = 24 + height + 12;
            salaryText.rectTransform.anchoredPosition = new Vector2(286, -y);
            capacityText.rectTransform.anchoredPosition = new Vector2(286, -y - 44);
            requirementText.rectTransform.anchoredPosition = new Vector2(286, -y - 116);
            y += 176;
            RequiredHeight = Mathf.Max(360, y + 68 + 24);
            removeButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(286, -y);
            quantityText.rectTransform.anchoredPosition = new Vector2(366, -y);
            addButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(556, -y);
        }
    }
}
