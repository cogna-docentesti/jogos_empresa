using System;
using System.Collections.Generic;
using Game.Adapter.In.UI.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Adapter.In.UI
{
    /// <summary>Short business explanations, interleaved with real catalog and checkout actions.</summary>
    public sealed class EquipmentTutorial : MonoBehaviour
    {
        public const int Version = 2;
        public const int ReviewStep = 2;
        public const int CheckoutStep = 4;
        public const int PurchasedStep = 5;
        public const int CompletedStep = 6;
        public static bool RequiresAction(int step) => step == 1 || step == ReviewStep || step == CheckoutStep;
        public static int ResumeStep(int saved, bool ownsBasic, bool kitInCart = false, bool cartOpen = false) => saved >= CompletedStep
            ? CompletedStep : ownsBasic ? PurchasedStep : !kitInCart ? Mathf.Clamp(saved, 0, 1)
            : Mathf.Clamp(saved, 0, cartOpen ? CheckoutStep : ReviewStep);

        private static readonly string[] Titles = {
            "Sua cozinha tem um limite", "Primeiro, o essencial", "Confira antes de comprar",
            "Você pode voltar depois", "Hora de concluir a compra", "Explore a loja"
        };
        private static readonly string[] Messages = {
            "Os equipamentos ajudam a ampliar a capacidade de atendimento e a qualidade do restaurante. Para transformar isso em vendas, você também precisa de equipe e de clientes interessados.",
            "O kit básico reúne a estrutura necessária para começar e é obrigatório nesta etapa. Use o botão de adicionar no cartão destacado para colocá-lo no carrinho. O dinheiro só sai do caixa ao concluir a compra.",
            "O kit está no carrinho, mas ainda não foi comprado. Clique em Revisar carrinho, no botão destacado, para conferir os equipamentos, o total e quanto vai sobrar no caixa.",
            "Você não precisa comprar tudo agora. Entre as rodadas, pode voltar à loja pelo menu e investir conforme a necessidade do restaurante. Por enquanto, vamos garantir o kit básico.",
            "Confira os itens e o valor da compra. Quando estiver pronto, use o botão destacado para concluir a compra do kit básico. O total será descontado do seu caixa.",
            "O kit já é seu! Os equipamentos básicos servem a todos os restaurantes; os específicos são voltados ao seu tipo de restaurante. Explore os cartões e compare os bônus. Quando terminar, use Continuar para equipe."
        };

        public event Action NextRequested;
        public event Action BackRequested;
        private TextMeshProUGUI progress, heading, message, actionLabel;
        private Button back, next;
        private RectTransform card;
        private int current;
        private RectTransform target, ring;
        private IReadOnlyList<RectTransform> inlineTargets;
        private readonly List<RectTransform> inlineRings = new();
        private readonly RectTransform[] shades = new RectTransform[4];
        private Image backdrop;
        private RectTransform dock;
        private Vector2 dockSize;
        private float dockHeight;
        private RectTransform dockRoot;
        private Vector2 dockRootSize;
        private float dockRootHeight;
        public bool IsVisible => gameObject.activeSelf;
        public RectTransform ActionTarget => target;

        public static EquipmentTutorial Create(Transform parent, string name = "EquipmentTutorial")
        {
            var root = new GameObject(name, typeof(RectTransform));
            root.SetActive(false);
            root.transform.SetParent(parent, false);
            var rect = (RectTransform)root.transform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var shade = root.AddComponent<Image>();
            shade.color = new Color(.14f, .07f, .24f, .48f);
            var canvas = root.AddComponent<Canvas>(); canvas.overrideSorting = true; canvas.sortingOrder = 1001;
            root.AddComponent<GraphicRaycaster>();
            var tutorial = root.AddComponent<EquipmentTutorial>();
            tutorial.backdrop = shade;
            tutorial.Build();
            root.AddComponent<SharedModalFocus>().Configure(tutorial.next, null, false);
            return tutorial;
        }

        private static RectTransform Node(Transform parent, string name)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false); return rect;
        }
        private static TextMeshProUGUI Text(Transform parent, string name, TypographyToken token, bool bold = false)
        {
            var text = Node(parent, name).gameObject.AddComponent<TextMeshProUGUI>();
            GameTypography.Apply(text, token, bold); text.color = GamePalette.Ink;
            text.textWrappingMode = TextWrappingModes.Normal; text.raycastTarget = false;
            return text;
        }
        private Button Button(Transform parent, string name, string label, SharedControlKind kind, Action clicked)
        {
            var rect = Node(parent, name);
            var element = rect.gameObject.AddComponent<LayoutElement>(); element.flexibleWidth = 1; element.minHeight = 68;
            var button = rect.gameObject.AddComponent<Button>();
            var text = Text(rect, "Label", GameTypography.Tokens.Button, true);
            text.text = label; text.alignment = TextAlignmentOptions.Center;
            text.rectTransform.anchorMin = Vector2.zero; text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = new Vector2(16, 8); text.rectTransform.offsetMax = new Vector2(-16, -8);
            SharedUiStyles.Bind(button, kind);
            button.onClick.AddListener(() => clicked()); return button;
        }
        private void Build()
        {
            for (int i = 0; i < shades.Length; i++)
            {
                shades[i] = Node(transform, "Shade" + i);
                var image = shades[i].gameObject.AddComponent<Image>();
                // Keep unrelated actions blocked without dimming the shop during hands-on steps.
                image.color = Color.clear;
                shades[i].gameObject.SetActive(false);
            }
            ring = Node(transform, "ActionHighlight");
            SharedUiStyles.Surface(ring.gameObject, "UI_CARD_SELECTED_OUTLINE").raycastTarget = false;
            ring.gameObject.SetActive(false);
            card = Node(transform, "Card"); card.anchorMin = card.anchorMax = new Vector2(.5f, .5f);
            SharedUiStyles.Surface(card.gameObject, "UI_MODAL_SURFACE", true);
            var layout = card.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(24, 24, 16, 16); layout.spacing = 10;
            layout.childControlHeight = layout.childControlWidth = true;
            layout.childForceExpandHeight = false; layout.childForceExpandWidth = true;
            card.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            progress = Text(card, "Progress", GameTypography.Tokens.Caption, true);
            progress.color = GamePalette.Muted;
            heading = Text(card, "Heading", GameTypography.Tokens.SectionTitle, true);
            message = Text(card, "Message", GameTypography.Tokens.Body);
            var actions = Node(card, "Actions");
            actions.gameObject.AddComponent<LayoutElement>().preferredHeight = 68;
            var row = actions.gameObject.AddComponent<HorizontalLayoutGroup>(); row.spacing = 16;
            row.childControlHeight = row.childControlWidth = true; row.childForceExpandWidth = true;
            back = Button(actions, "Back", "Anterior", SharedControlKind.Secondary, () => BackRequested?.Invoke());
            next = Button(actions, "Next", "Continuar", SharedControlKind.Primary, () => NextRequested?.Invoke());
            actionLabel = next.GetComponentInChildren<TextMeshProUGUI>();
        }
        public void Show(int step, RectTransform actionTarget = null)
        {
            if (step >= CompletedStep) { Hide(); return; }
            current = Mathf.Clamp(step, 0, PurchasedStep);
            target = RequiresAction(current) ? actionTarget : null;
            inlineTargets = null;
            ShowCard($"EQUIPAMENTOS  ·  {current + 1} DE {CompletedStep}", Titles[current], Messages[current],
                !RequiresAction(current), current == PurchasedStep ? "Explorar loja" : "Continuar");
        }
        public void ShowInline(string counter, string title, string body, bool showNext, IReadOnlyList<RectTransform> highlights)
        {
            target = null;
            inlineTargets = highlights;
            ShowCard(counter, title, body, showNext, "Continuar");
        }
        private void ShowCard(string counter, string title, string body, bool showNext, string buttonLabel)
        {
            heading.text = title; message.text = body;
            progress.text = counter; actionLabel.text = buttonLabel;
            back.gameObject.SetActive(false);
            next.gameObject.SetActive(showNext);
            next.transform.parent.gameObject.SetActive(showNext);
            var focus = GetComponent<SharedModalFocus>();
            if (focus != null) focus.enabled = inlineTargets == null && showNext;
            Resize(); gameObject.SetActive(true);
            transform.SetAsLastSibling();
            // Nested canvases may reset overrideSorting while inactive; apply after activation, as the cart does.
            var overlay = GetComponent<Canvas>(); overlay.overrideSorting = true; overlay.sortingOrder = 2000;
            if (target != null) target.GetComponent<Selectable>()?.Select();
            else if (next.gameObject.activeSelf) next.Select();
            UpdateSpotlight();
        }
        private void Resize()
        {
            var root = (RectTransform)transform;
            card.anchorMin = card.anchorMax = new Vector2(.5f, .5f);
            card.pivot = new Vector2(.5f, 0);
            if (dock == null)
            {
                card.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Min(1000, Mathf.Max(280, root.rect.width - 48)));
                card.anchoredPosition = new Vector2(0, root.rect.yMin + 24);
                return;
            }
            // Reserve a dedicated band beneath the products instead of covering their viewport.
            if (dockRoot != null) dockRoot.sizeDelta = dockRootSize;
            dock.sizeDelta = dockSize;
            var corners = new Vector3[4]; dock.GetWorldCorners(corners);
            var min = root.InverseTransformPoint(corners[0]);
            var max = root.InverseTransformPoint(corners[2]);
            float width = Mathf.Max(280, max.x - min.x);
            card.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            LayoutRebuilder.ForceRebuildLayoutImmediate(card);
            card.anchoredPosition = new Vector2(min.x + width / 2, min.y);
            float scale = Mathf.Abs(root.InverseTransformVector(dock.TransformVector(Vector3.up)).y);
            float reserve = (card.rect.height + 16) / Mathf.Max(.001f, scale);
            dock.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(160, dockHeight - reserve));
            if (dockRoot != null)
                dockRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(160, dockRootHeight - reserve));
        }
        public void SetDock(ScrollRect scroll)
        {
            var rect = scroll != null ? scroll.viewport : null;
            if (dock == rect) return;
            ReleaseDock(); dock = rect;
            if (dock != null)
            {
                dockSize = dock.sizeDelta; dockHeight = dock.rect.height;
                dockRoot = (RectTransform)scroll.transform;
                dockRootSize = dockRoot.sizeDelta; dockRootHeight = dockRoot.rect.height;
            }
        }
        private void ReleaseDock()
        {
            if (dock != null) dock.sizeDelta = dockSize;
            if (dockRoot != null) dockRoot.sizeDelta = dockRootSize;
            dock = null; dockRoot = null;
        }
        private void UpdateSpotlight()
        {
            UpdateInlineHighlights();
            bool action = target != null && target.gameObject.activeInHierarchy;
            backdrop.enabled = inlineTargets == null && !RequiresAction(current);
            ring.gameObject.SetActive(action);
            // During checkout, leave the cart available for scrolling while highlighting the purchase button.
            bool blockOtherActions = action && current != CheckoutStep;
            foreach (var shade in shades) shade.gameObject.SetActive(blockOtherActions);
            if (!action) return;
            var hole = LocalBounds(target, 8);
            var bounds = ((RectTransform)transform).rect;
            Place(ring, hole.xMin, hole.yMin, hole.xMax, hole.yMax);
            if (!blockOtherActions) return;
            Place(shades[0], bounds.xMin, bounds.yMin, bounds.xMax, hole.yMin);
            Place(shades[1], bounds.xMin, hole.yMax, bounds.xMax, bounds.yMax);
            Place(shades[2], bounds.xMin, hole.yMin, hole.xMin, hole.yMax);
            Place(shades[3], hole.xMax, hole.yMin, bounds.xMax, hole.yMax);
        }
        private void UpdateInlineHighlights()
        {
            int count = inlineTargets?.Count ?? 0;
            while (inlineRings.Count < count)
            {
                var outline = Node(transform, "InlineHighlight");
                outline.SetSiblingIndex(card.GetSiblingIndex());
                SharedUiStyles.Surface(outline.gameObject, "UI_CARD_SELECTED_OUTLINE").raycastTarget = false;
                inlineRings.Add(outline);
            }
            for (int i = 0; i < inlineRings.Count; i++)
            {
                var highlight = i < count ? inlineTargets[i] : null;
                var outline = inlineRings[i];
                bool visible = highlight != null && highlight.gameObject.activeInHierarchy;
                if (visible)
                {
                    var bounds = LocalBounds(highlight, 6);
                    if (dock != null && highlight.IsChildOf(dock))
                    {
                        var viewport = LocalBounds(dock, 0);
                        visible = viewport.Contains(bounds.min) && viewport.Contains(bounds.max);
                    }
                    if (visible) Place(outline, bounds.xMin, bounds.yMin, bounds.xMax, bounds.yMax);
                }
                outline.gameObject.SetActive(visible);
            }
        }
        private readonly Vector3[] highlightCorners = new Vector3[4];
        private Rect LocalBounds(RectTransform rect, float padding)
        {
            var root = (RectTransform)transform;
            rect.GetWorldCorners(highlightCorners);
            var min = root.InverseTransformPoint(highlightCorners[0]);
            var max = root.InverseTransformPoint(highlightCorners[2]);
            return Rect.MinMaxRect(min.x - padding, min.y - padding, max.x + padding, max.y + padding);
        }
        private static void Place(RectTransform rect, float x0, float y0, float x1, float y1)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.anchoredPosition = new Vector2((x0 + x1) / 2, (y0 + y1) / 2);
            rect.sizeDelta = new Vector2(Mathf.Max(0, x1 - x0), Mathf.Max(0, y1 - y0));
        }
        public void RefreshLayout() { Resize(); UpdateSpotlight(); }
        private void LateUpdate() => RefreshLayout();
        private void OnDisable() => ReleaseDock();
        public void Hide() { ReleaseDock(); gameObject.SetActive(false); }
    }
}
