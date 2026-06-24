using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Adapter.In.UI
{
    public sealed class RestaurantScreenView : MonoBehaviour
    {
        [Header("Header")]
        [SerializeField] private TextMeshProUGUI hintText;

        [Header("Restaurant Cards")]
        [SerializeField] private Button cardPodrao;
        [SerializeField] private Button cardJapones;
        [SerializeField] private Button cardFrances;

        [Header("Segment Buttons")]
        [SerializeField] private Button btnLow;
        [SerializeField] private Button btnMedium;
        [SerializeField] private Button btnHigh;

        [Header("Coherence Panel")]
        [SerializeField] private Image coh1BarFill;
        [SerializeField] private TextMeshProUGUI coh1Tip;
        [SerializeField] private Image coh2BarFill;
        [SerializeField] private TextMeshProUGUI coh2Tip;

        [Header("Action Buttons")]
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button backButton;

        private static readonly Color CardNormal = new Color(0.1f, 0.165f, 0.243f);
        private static readonly Color CardSelected = new Color(0.09f, 0.18f, 0.36f);
        private static readonly Color BorderNormal = new Color(0.118f, 0.227f, 0.373f);
        private static readonly Color BorderSelected = new Color(0.145f, 0.565f, 0.922f);
        private static readonly Color SegBtnNormal = new Color(0.051f, 0.106f, 0.176f);
        private static readonly Color SegBtnSelected = new Color(0.09f, 0.145f, 0.329f);
        private static readonly Color SegTextNormal = new Color(0.478f, 0.612f, 0.753f);
        private static readonly Color SegTextSelected = new Color(0.576f, 0.773f, 0.992f);
        private static readonly Color SegTextDisabled = new Color(0.36f, 0.42f, 0.49f);

        public void SetHint(string v) => hintText.text = v;

        public void BindCardPodrao(UnityEngine.Events.UnityAction a) => Bind(cardPodrao, a);
        public void BindCardJapones(UnityEngine.Events.UnityAction a) => Bind(cardJapones, a);
        public void BindCardFrances(UnityEngine.Events.UnityAction a) => Bind(cardFrances, a);
        public void BindBtnLow(UnityEngine.Events.UnityAction a) => Bind(btnLow, a);
        public void BindBtnMedium(UnityEngine.Events.UnityAction a) => Bind(btnMedium, a);
        public void BindBtnHigh(UnityEngine.Events.UnityAction a) => Bind(btnHigh, a);
        public void BindConfirm(UnityEngine.Events.UnityAction a) => Bind(confirmButton, a);
        public void BindBack(UnityEngine.Events.UnityAction a) => Bind(backButton, a);

        public void SetRestaurantCardData(RestaurantType type, string displayName, string description)
        {
            Button card = GetRestaurantCard(type);
            SetChildText(card, "RestaurantName", displayName);
            SetChildText(card, "RestaurantDesc", description);
        }

        public void SetSegmentButtonData(Segment segment, string displayName)
        {
            SetChildText(GetSegmentButton(segment), "Text (TMP)", displayName);
        }

        public void SelectRestaurantCard(RestaurantType? selected)
        {
            ApplyCardState(cardPodrao, selected == RestaurantType.PODRAO);
            ApplyCardState(cardJapones, selected == RestaurantType.JAPONES);
            ApplyCardState(cardFrances, selected == RestaurantType.FRANCES);
        }

        public void SelectSegmentButton(Segment? selected)
        {
            ApplySegmentState(btnLow, selected == Segment.LOW);
            ApplySegmentState(btnMedium, selected == Segment.MEDIUM);
            ApplySegmentState(btnHigh, selected == Segment.HIGH);
        }

        public void SetSegmentAvailability(Segment segment, bool available)
        {
            Button button = GetSegmentButton(segment);
            button.interactable = available;

            var txt = button.GetComponentInChildren<TextMeshProUGUI>();
            if (txt != null && !available)
                txt.color = SegTextDisabled;
        }

        public void SetConfirmEnabled(bool v) => confirmButton.interactable = v;

        public void UpdateCoherence(float coh1, string tip1, float coh2, string tip2)
        {
            if (coh1BarFill != null) coh1BarFill.fillAmount = coh1;
            if (coh1Tip != null) coh1Tip.text = tip1;
            if (coh2BarFill != null) coh2BarFill.fillAmount = coh2;
            if (coh2Tip != null) coh2Tip.text = tip2;

            if (coh1BarFill != null) coh1BarFill.color = CoherenceColor(coh1);
            if (coh2BarFill != null) coh2BarFill.color = CoherenceColor(coh2);
        }

        private static void Bind(Button button, UnityEngine.Events.UnityAction action)
        {
            button.onClick.RemoveAllListeners();
            if (action != null)
                button.onClick.AddListener(action);
        }

        private void ApplyCardState(Button card, bool isSelected)
        {
            var img = card.GetComponent<Image>();
            img.color = isSelected ? CardSelected : CardNormal;
            card.colors = BuildButtonColors(isSelected ? CardSelected : CardNormal);

            var outline = card.GetComponent<Outline>();
            if (outline == null)
                outline = card.gameObject.AddComponent<Outline>();

            outline.effectColor = isSelected ? BorderSelected : BorderNormal;
            outline.effectDistance = isSelected ? new Vector2(4f, -4f) : new Vector2(1f, -1f);
            outline.enabled = isSelected;
        }

        private void ApplySegmentState(Button btn, bool isSelected)
        {
            var img = btn.GetComponent<Image>();
            img.color = isSelected ? SegBtnSelected : SegBtnNormal;
            btn.colors = BuildButtonColors(isSelected ? SegBtnSelected : SegBtnNormal);

            var txt = btn.GetComponentInChildren<TextMeshProUGUI>();
            if (txt != null)
                txt.color = isSelected ? SegTextSelected : SegTextNormal;

            var outline = btn.GetComponent<Outline>();
            if (outline == null)
                outline = btn.gameObject.AddComponent<Outline>();

            outline.effectColor = isSelected ? BorderSelected : BorderNormal;
            outline.effectDistance = isSelected ? new Vector2(3f, -3f) : new Vector2(1f, -1f);
            outline.enabled = isSelected;
        }

        private static ColorBlock BuildButtonColors(Color baseColor)
        {
            return new ColorBlock
            {
                normalColor = baseColor,
                highlightedColor = Color.Lerp(baseColor, Color.white, 0.08f),
                pressedColor = Color.Lerp(baseColor, Color.black, 0.18f),
                selectedColor = baseColor,
                disabledColor = new Color(0.22f, 0.26f, 0.32f, 0.55f),
                colorMultiplier = 1f,
                fadeDuration = 0.08f
            };
        }

        private Button GetRestaurantCard(RestaurantType type) => type switch
        {
            RestaurantType.PODRAO => cardPodrao,
            RestaurantType.JAPONES => cardJapones,
            RestaurantType.FRANCES => cardFrances,
            _ => cardPodrao
        };

        private Button GetSegmentButton(Segment segment) => segment switch
        {
            Segment.LOW => btnLow,
            Segment.MEDIUM => btnMedium,
            Segment.HIGH => btnHigh,
            _ => btnLow
        };

        private static void SetChildText(Button button, string childName, string value)
        {
            var texts = button.GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (var text in texts)
            {
                if (text.name == childName)
                {
                    text.text = value;
                    return;
                }
            }
        }

        private static Color CoherenceColor(float value)
        {
            if (value >= 0.7f) return new Color(0.086f, 0.639f, 0.29f);
            if (value >= 0.4f) return new Color(0.851f, 0.604f, 0.043f);
            return new Color(0.882f, 0.114f, 0.282f);
        }
    }
}
