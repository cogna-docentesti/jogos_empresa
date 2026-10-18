using System;
using TMPro;
using UnityEngine;

namespace Game.Adapter.In.UI
{
    public sealed class EventOptionCardView : MonoBehaviour
    {
        [SerializeField] private TMP_Text titleText, descriptionText, effectsText;
        [SerializeField] private UnityEngine.UI.Button button;
        private UnityEngine.UI.LayoutElement element;
        public UnityEngine.UI.Button Button => button;
        public void Bind(string title, string description, string effects, Action choose, bool enabledChoice, bool selected, bool history = false)
        {
            titleText.text = title; descriptionText.text = description; effectsText.text = effects;
            button.onClick.RemoveAllListeners();
            if (choose != null) button.onClick.AddListener(() => { if (button.interactable) choose(); });
            button.interactable = enabledChoice;
            var appearance = button.GetComponent<SharedControlAppearance>();
            appearance.SetSelected(selected);
            if (history)
            {
                appearance.enabled = false; button.enabled = false;
                foreach (var name in new[] { "SharedFocus", "SharedSelection", "SharedDisabled" })
                    if (transform.Find(name) is var child && child != null) child.gameObject.SetActive(false);
                foreach (var text in new[] { titleText, descriptionText, effectsText }) text.color = Theme.GamePalette.Ink;
            }
            Reflow();
        }
        private void LateUpdate() => Reflow();
        private void Reflow()
        {
            element ??= GetComponent<UnityEngine.UI.LayoutElement>();
            float width = Mathf.Max(100, ((RectTransform)transform).rect.width - 48), y = 20;
            foreach (var text in new[] { titleText, descriptionText, effectsText })
            {
                var r = text.rectTransform; float height = Mathf.Max(30, text.GetPreferredValues(text.text, width, Mathf.Infinity).y);
                r.anchoredPosition = new Vector2(24, -y); r.sizeDelta = new Vector2(width, height); y += height + 12;
            }
            element.preferredHeight = Mathf.Max(172, y + 8);
        }
    }
}
