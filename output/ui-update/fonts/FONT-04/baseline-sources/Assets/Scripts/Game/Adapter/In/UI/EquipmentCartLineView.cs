using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace Game.Adapter.In.UI
{
    public sealed class EquipmentCartLineView : MonoBehaviour
    {
        [SerializeField] private UnityEngine.UI.Image artwork;
        [SerializeField] private TMP_Text nameText, priceText;
        [SerializeField] private UnityEngine.UI.Button removeButton;
        public EquipmentData Equipment { get; private set; }
        public void Setup(EquipmentData equipment, UnityAction remove)
        {
            Equipment = equipment; artwork.sprite = equipment.icon; artwork.color = Color.white;
            artwork.type = UnityEngine.UI.Image.Type.Simple; artwork.preserveAspect = true;
            nameText.text = equipment.displayName; priceText.text = FinancialScreenView.Money(equipment.cost);
            removeButton.onClick.RemoveAllListeners(); removeButton.onClick.AddListener(remove);
            float height = Mathf.Max(56, nameText.GetPreferredValues(nameText.text, nameText.rectTransform.rect.width, float.PositiveInfinity).y);
            nameText.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            priceText.rectTransform.anchoredPosition = new Vector2(140, -20 - height - 8);
            GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = Mathf.Max(144, height + 84);
        }
    }
}
