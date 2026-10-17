using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using Game.Adapter.In.UI.Theme;

namespace Game.Adapter.In.UI
{
    public enum FeedbackKind { Success, Warning, Error }
    /// <summary>Nonmodal, dismissible feedback. Its surface never intercepts clicks outside its close button.</summary>
    public sealed class FeedbackToast : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private TMP_Text message;
        [SerializeField] private UnityEngine.UI.Image surface;
        [SerializeField] private UnityEngine.UI.Button close;
        [SerializeField] private Sprite success, warning, error;
        private float remaining;
        public static FeedbackToast Current { get; private set; }
        public bool IsVisible => panel != null && panel.activeSelf;
        public FeedbackKind Kind { get; private set; }
        private void Awake() { Current = this; close?.onClick.AddListener(Hide); Hide(); }
        public static void Show(string text, FeedbackKind kind) { if (Current != null) Current.Display(text, kind); }
        public void Display(string text, FeedbackKind kind)
        {
            if (SharedModalFocus.Active != null) return;
            if (string.IsNullOrWhiteSpace(text)) { Hide(); return; }
            Kind = kind; message.text = (kind == FeedbackKind.Success ? "Sucesso • " : kind == FeedbackKind.Warning ? "Atenção • " : "Não foi possível • ") + text;
            surface.sprite = kind == FeedbackKind.Success ? success : kind == FeedbackKind.Warning ? warning : error;
            // Toast artwork has a pale message surface. Use the body color at a readable
            // size; severity already has both a text prefix and its own surface sprite.
            message.color = GamePalette.Ink;
            message.enableVertexGradient = false;
            message.fontSize = GameTypography.Body;
            surface.color = Color.white;
            var textRect = message.rectTransform;
            float height = Mathf.Max(72, message.GetPreferredValues(message.text, textRect.rect.width, 0).y + 24);
            ((RectTransform)panel.transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            textRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height - 20);
            remaining = Mathf.Max(6, message.text.Length / 14f); panel.SetActive(true);
        }
        private void Update()
        {
            if (!IsVisible) return;
            // Keyboard focus keeps feedback available until the reader leaves it or closes it.
            if (EventSystem.current?.currentSelectedGameObject == close.gameObject) return;
            remaining -= Time.unscaledDeltaTime; if (remaining <= 0) Hide();
        }
        public void Hide() { panel?.SetActive(false); }
        private void OnDisable() => Hide();
        private void OnDestroy() { if (Current == this) Current = null; }
    }
}
