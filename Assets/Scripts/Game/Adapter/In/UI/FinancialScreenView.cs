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
        [SerializeField] private float cardsSpacing = 16f;
        [SerializeField] private float scrollViewportHeight = 520f;

        [Header("Actions")]
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button backButton;

        private ScrollRect cardsScrollRect;

        private void Awake()
        {
            EnsureVerticalScrollView();
        }

        public bool HasRequiredReferences()
        {
            EnsureVerticalScrollView();

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

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(bankCardsContainer as RectTransform);

            if (cardsScrollRect != null)
                cardsScrollRect.verticalNormalizedPosition = 1f;

            return card;
        }

        public void ClearBankCards()
        {
            if (bankCardsContainer == null)
                return;

            for (int i = bankCardsContainer.childCount - 1; i >= 0; i--)
                Destroy(bankCardsContainer.GetChild(i).gameObject);

            if (cardsScrollRect != null)
                cardsScrollRect.verticalNormalizedPosition = 1f;
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

        private void EnsureVerticalScrollView()
        {
            if (cardsScrollRect != null || bankCardsContainer == null)
                return;

            RectTransform original = bankCardsContainer as RectTransform;
            if (original == null || original.parent == null)
                return;

            Transform parent = original.parent;
            int siblingIndex = original.GetSiblingIndex();

            var scrollObject = new GameObject("LoanCardsScrollView", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Image), typeof(ScrollRect));
            scrollObject.layer = original.gameObject.layer;
            scrollObject.transform.SetParent(parent, false);
            scrollObject.transform.SetSiblingIndex(siblingIndex);

            RectTransform scrollTransform = scrollObject.GetComponent<RectTransform>();
            CopyRectTransform(original, scrollTransform);
            scrollTransform.sizeDelta = new Vector2(
                scrollTransform.sizeDelta.x,
                Mathf.Max(scrollTransform.sizeDelta.y, scrollViewportHeight)
            );
            scrollObject.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.001f);

            var viewportObject = new GameObject("Viewport", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Image), typeof(RectMask2D));
            viewportObject.layer = original.gameObject.layer;
            viewportObject.transform.SetParent(scrollObject.transform, false);

            RectTransform viewport = viewportObject.GetComponent<RectTransform>();
            Stretch(viewport);
            viewportObject.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.001f);

            var contentObject = new GameObject("LoanCardsContent", typeof(RectTransform),
                typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentObject.layer = original.gameObject.layer;
            contentObject.transform.SetParent(viewport, false);

            RectTransform content = contentObject.GetComponent<RectTransform>();
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;

            VerticalLayoutGroup layout = contentObject.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 12, 12);
            layout.spacing = cardsSpacing;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            ContentSizeFitter fitter = contentObject.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            cardsScrollRect = scrollObject.GetComponent<ScrollRect>();
            cardsScrollRect.viewport = viewport;
            cardsScrollRect.content = content;
            cardsScrollRect.horizontal = false;
            cardsScrollRect.vertical = true;
            cardsScrollRect.movementType = ScrollRect.MovementType.Elastic;
            cardsScrollRect.inertia = true;
            cardsScrollRect.scrollSensitivity = 35f;
            cardsScrollRect.verticalScrollbar = CreateVerticalScrollbar(scrollObject.transform, viewport);
            cardsScrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
            cardsScrollRect.verticalScrollbarSpacing = 8f;

            bankCardsContainer = content;
            Destroy(original.gameObject);
        }

        private void ConfigureCardLayout(BankCardView card)
        {
            if (card == null)
                return;

            var layoutElement = card.GetComponent<LayoutElement>();
            float prefabHeight = 0f;

            if (layoutElement != null && layoutElement.preferredHeight > 0f)
                prefabHeight = layoutElement.preferredHeight;

            if (prefabHeight <= 0f && card.transform is RectTransform cardRect)
                prefabHeight = Mathf.Abs(cardRect.sizeDelta.y);

            if (prefabHeight <= 0f)
                prefabHeight = 180f;

            if (layoutElement == null)
                layoutElement = card.gameObject.AddComponent<LayoutElement>();

            layoutElement.preferredHeight = prefabHeight;
            layoutElement.minHeight = prefabHeight;
            layoutElement.flexibleHeight = 0f;
            layoutElement.flexibleWidth = 1f;
        }

        private static Scrollbar CreateVerticalScrollbar(Transform parent, RectTransform viewport)
        {
            viewport.offsetMax = new Vector2(-30f, viewport.offsetMax.y);

            var scrollbarObject = new GameObject(
                "VerticalScrollbar",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Scrollbar)
            );
            scrollbarObject.transform.SetParent(parent, false);

            RectTransform scrollbarRect = scrollbarObject.GetComponent<RectTransform>();
            scrollbarRect.anchorMin = new Vector2(1f, 0f);
            scrollbarRect.anchorMax = new Vector2(1f, 1f);
            scrollbarRect.pivot = new Vector2(1f, 0.5f);
            scrollbarRect.anchoredPosition = new Vector2(-4f, 0f);
            scrollbarRect.sizeDelta = new Vector2(18f, -8f);

            Image track = scrollbarObject.GetComponent<Image>();
            track.color = new Color(0.02f, 0.08f, 0.14f, 0.9f);

            var handleObject = new GameObject(
                "Handle",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image)
            );
            handleObject.transform.SetParent(scrollbarObject.transform, false);

            RectTransform handleRect = handleObject.GetComponent<RectTransform>();
            Stretch(handleRect);
            handleRect.offsetMin = new Vector2(3f, 3f);
            handleRect.offsetMax = new Vector2(-3f, -3f);

            Image handleImage = handleObject.GetComponent<Image>();
            handleImage.color = new Color(0.08f, 0.55f, 0.9f, 1f);

            Scrollbar scrollbar = scrollbarObject.GetComponent<Scrollbar>();
            scrollbar.targetGraphic = handleImage;
            scrollbar.handleRect = handleRect;
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            return scrollbar;
        }
        private static void CopyRectTransform(RectTransform source, RectTransform target)
        {
            target.anchorMin = source.anchorMin;
            target.anchorMax = source.anchorMax;
            target.pivot = source.pivot;
            target.anchoredPosition = source.anchoredPosition;
            target.sizeDelta = source.sizeDelta;
            target.localRotation = source.localRotation;
            target.localScale = source.localScale;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}