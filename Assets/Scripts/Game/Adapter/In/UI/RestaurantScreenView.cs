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
        [SerializeField] private TextMeshProUGUI coh2Tip;

        [Header("Action Buttons")]
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button backButton;

        private Segment? selectedSegment;

        private const string SegmentTextChildName = "Text (TMP)";
        private const string SelectedIconChildName = "selectedIcon";

        private static readonly Color CardNormal = new Color(0.08f, 0.12f, 0.20f, 1f);
        private static readonly Color CardSelected = new Color(0.45f, 1.00f, 0.10f, 0.18f);

        private static readonly Color BorderNormal = new Color(0.16f, 0.24f, 0.36f, 1f);
        private static readonly Color BorderSelected = new Color(0.70f, 1.00f, 0.00f, 1f);

        // Segmento clicável
        private static readonly Color SegBtnNormal = HexColor(0x00, 0x68, 0xA4);      // #0068A4
        private static readonly Color SegBorderNormal = Color.white;                 // #FFFFFF

        // Segmento selecionado
        private static readonly Color SegBtnSelected = new Color(0.18f, 0.62f, 0.18f, 1f);
        private static readonly Color SegBorderSelected = new Color(0.70f, 1.00f, 0.00f, 1f);

        // Segmento desabilitado
        private static readonly Color SegBtnDisabled = HexColor(0xAE, 0xAB, 0xAB);    // #AEABAB
        private static readonly Color SegBorderDisabled = Color.white;                // #FFFFFF

        private static readonly Color SegTextNormal = Color.white;
        private static readonly Color SegTextSelected = Color.white;
        private static readonly Color SegTextDisabled = HexColor(0x93, 0x93, 0x93);   // #939393

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
            Button button = GetSegmentButton(segment);

            SetChildText(button, SegmentTextChildName, NormalizeSegmentLabel(displayName));
            ApplySegmentState(button, selectedSegment == segment);
        }

        public void SelectRestaurantCard(RestaurantType? selected)
        {
            ApplyCardState(cardPodrao, selected == RestaurantType.PODRAO);
            ApplyCardState(cardJapones, selected == RestaurantType.JAPONES);
            ApplyCardState(cardFrances, selected == RestaurantType.FRANCES);
        }

        public void SelectSegmentButton(Segment? selected)
        {
            if (selected.HasValue && !GetSegmentButton(selected.Value).interactable)
                selectedSegment = null;
            else
                selectedSegment = selected;

            RefreshSegmentButtons();
        }

        public void SetSegmentAvailability(Segment segment, bool available)
        {
            Button button = GetSegmentButton(segment);
            button.interactable = available;

            if (!available && selectedSegment == segment)
                selectedSegment = null;

            RefreshSegmentButtons();
        }

        public void SetConfirmEnabled(bool v)
        {
            confirmButton.interactable = v;
        }

        public void UpdateCoherence(float coh1, string tip1, float coh2, string tip2)
        {
            ApplyCoherenceBar(coh1BarFill, coh1);

            if (coh1Tip != null)
                coh1Tip.text = tip1;

            if (coh2Tip != null)
                coh2Tip.text = tip2;
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

            if (img != null)
                img.color = isSelected ? CardSelected : CardNormal;

            card.colors = BuildButtonColors(isSelected ? CardSelected : CardNormal);

            var outline = card.GetComponent<Outline>();

            if (outline == null)
                outline = card.gameObject.AddComponent<Outline>();

            outline.effectColor = isSelected ? BorderSelected : BorderNormal;
            outline.effectDistance = isSelected ? new Vector2(4f, -4f) : new Vector2(1f, -1f);
            outline.enabled = isSelected;
        }

        private void RefreshSegmentButtons()
        {
            ApplySegmentState(btnLow, selectedSegment == Segment.LOW);
            ApplySegmentState(btnMedium, selectedSegment == Segment.MEDIUM);
            ApplySegmentState(btnHigh, selectedSegment == Segment.HIGH);
        }

        private void ApplySegmentState(Button btn, bool isSelected)
        {
            bool isDisabled = !btn.interactable;
            bool showSelected = isSelected && !isDisabled;

            Color bgColor;
            Color textColor;
            Color borderColor;
            Vector2 borderDistance;

            if (isDisabled)
            {
                bgColor = SegBtnDisabled;
                textColor = SegTextDisabled;
                borderColor = SegBorderDisabled;
                borderDistance = new Vector2(3f, -3f);
            }
            else if (showSelected)
            {
                bgColor = SegBtnSelected;
                textColor = SegTextSelected;
                borderColor = SegBorderSelected;
                borderDistance = new Vector2(3f, -3f);
            }
            else
            {
                bgColor = SegBtnNormal;
                textColor = SegTextNormal;
                borderColor = SegBorderNormal;
                borderDistance = new Vector2(3f, -3f);
            }

            var img = btn.GetComponent<Image>();

            if (img != null)
                img.color = bgColor;

            btn.colors = BuildSegmentButtonColors(bgColor);

            var txt = btn.GetComponentInChildren<TextMeshProUGUI>(true);

            if (txt != null)
            {
                string label = NormalizeSegmentLabel(txt.text);

                txt.text = showSelected ? $"  {label}" : label;
                txt.color = textColor;
                txt.fontStyle = FontStyles.Bold;
                txt.alignment = TextAlignmentOptions.Center;
                txt.characterSpacing = 2f;
            }

            SetSelectedIconVisible(btn, showSelected);

            var outline = btn.GetComponent<Outline>();

            if (outline == null)
                outline = btn.gameObject.AddComponent<Outline>();

            outline.effectColor = borderColor;
            outline.effectDistance = borderDistance;
            outline.enabled = true;
        }

        private static void SetSelectedIconVisible(Button button, bool visible)
        {
            Transform icon = FindChildRecursive(button.transform, SelectedIconChildName);

            if (icon != null)
                icon.gameObject.SetActive(visible);
        }

        private static Transform FindChildRecursive(Transform parent, string childName)
        {
            foreach (Transform child in parent)
            {
                if (child.name == childName)
                    return child;

                Transform found = FindChildRecursive(child, childName);

                if (found != null)
                    return found;
            }

            return null;
        }

        private static void ApplyCoherenceBar(Image barFill, float value)
        {
            if (barFill == null)
                return;

            float normalizedValue = Mathf.Clamp01(value);

            barFill.color = CoherenceColor(normalizedValue);

            RectTransform rt = barFill.rectTransform;

            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(normalizedValue, 1f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            barFill.fillAmount = 1f;
            barFill.enabled = normalizedValue > 0.001f;
        }

        private static ColorBlock BuildButtonColors(Color baseColor)
        {
            return new ColorBlock
            {
                normalColor = baseColor,
                highlightedColor = Color.Lerp(baseColor, Color.white, 0.08f),
                pressedColor = Color.Lerp(baseColor, Color.black, 0.18f),
                selectedColor = baseColor,
                disabledColor = new Color(0.28f, 0.32f, 0.38f, 0.85f),
                colorMultiplier = 1f,
                fadeDuration = 0.08f
            };
        }

        private static ColorBlock BuildSegmentButtonColors(Color baseColor)
        {
            return new ColorBlock
            {
                normalColor = baseColor,
                highlightedColor = Color.Lerp(baseColor, Color.white, 0.18f),
                pressedColor = Color.Lerp(baseColor, Color.black, 0.25f),
                selectedColor = baseColor,
                disabledColor = SegBtnDisabled,
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

        private static string NormalizeSegmentLabel(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            return value
                .Replace("✓", "")
                .Trim()
                .ToUpperInvariant();
        }

        private static Color CoherenceColor(float value)
        {
            if (value >= 0.7f)
                return new Color(0.086f, 0.639f, 0.29f, 1f);

            if (value >= 0.4f)
                return new Color(0.851f, 0.604f, 0.043f, 1f);

            return new Color(0.882f, 0.114f, 0.282f, 1f);
        }

        private static string FormatCoherenceMessage(string label, float value, string tip)
        {
            string status = value switch
            {
                >= 0.7f => "Alta",
                >= 0.4f => "Média",
                > 0f => "Baixa",
                _ => "-"
            };

            return $"{label}: {status} ({Mathf.RoundToInt(value * 100f)}%)\n{tip}";
        }

        private static Color HexColor(byte r, byte g, byte b, byte a = 255)
        {
            return new Color32(r, g, b, a);
        }
    }
}