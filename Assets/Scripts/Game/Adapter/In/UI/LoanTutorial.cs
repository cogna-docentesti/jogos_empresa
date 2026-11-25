using System;
using Game.Adapter.In.UI.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Adapter.In.UI
{
    /// <summary>Explains optional borrowing, month-based repayment, and the contract lock.</summary>
    public sealed class LoanTutorial : MonoBehaviour
    {
        private static readonly string[] Titles =
        {
            "O empréstimo é opcional",
            "O prazo depende do mês",
            "Confira antes de contratar"
        };

        private static readonly string[] Messages =
        {
            "Voc\u00EA pode escolher \"Sem empr\u00E9stimo\" e come\u00E7ar usando somente o capital dispon\u00EDvel. Se mudar de ideia, poder\u00E1 contratar uma linha de cr\u00E9dito pelo Banco no menu de gest\u00E3o durante o 1\u00BA ou o 2\u00BA m\u00EAs.",
            "Quanto mais tarde você contratar, menos tempo terá para pagar. No 3º mês, não é possível contratar um novo empréstimo.",
            "Confira o valor, os juros e as parcelas antes de confirmar. Depois de contratar, não será possível trocar ou cancelar o empréstimo."
        };
        private RectTransform card;
        private TextMeshProUGUI progress, heading, message, nextLabel;
        private Button next;
        private int current;
        private string preferenceKey;

        public bool IsVisible => gameObject.activeSelf;

        public static bool WasCompleted(string sessionId) =>
            !string.IsNullOrEmpty(sessionId) && PlayerPrefs.GetInt(Key(sessionId), 0) == 1;

        private static string Key(string sessionId) => "loan_tutorial_completed:" + sessionId;

        public static LoanTutorial Create(Transform parent, string sessionId)
        {
            var root = new GameObject("LoanTutorial", typeof(RectTransform));
            root.transform.SetParent(parent, false);
            root.SetActive(false);
            var rect = (RectTransform)root.transform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;

            var shade = root.AddComponent<Image>();
            shade.color = new Color(.08f, .05f, .12f, .72f);
            shade.raycastTarget = true;
            var canvas = root.AddComponent<Canvas>();
            canvas.overrideSorting = true; canvas.sortingOrder = 2000;
            root.AddComponent<GraphicRaycaster>();

            var tutorial = root.AddComponent<LoanTutorial>();
            tutorial.preferenceKey = Key(sessionId);
            tutorial.Build();
            root.AddComponent<SharedModalFocus>().Configure(tutorial.next, null, false);
            return tutorial;
        }

        private static RectTransform Node(Transform parent, string name)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            return rect;
        }

        private static TextMeshProUGUI Text(Transform parent, string name, TypographyToken token, bool bold = false)
        {
            var text = Node(parent, name).gameObject.AddComponent<TextMeshProUGUI>();
            GameTypography.Apply(text, token, bold);
            text.color = GamePalette.Ink;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.raycastTarget = false;
            return text;
        }

        private Button MakeButton(Transform parent, string name, string label, SharedControlKind kind, Action action)
        {
            var rect = Node(parent, name);
            var layout = rect.gameObject.AddComponent<LayoutElement>();
            layout.flexibleWidth = 1; layout.minHeight = 68;
            var button = rect.gameObject.AddComponent<Button>();
            SharedUiStyles.Bind(button, kind);
            var text = Text(rect, "Label", GameTypography.Tokens.Button, true);
            text.text = label; text.alignment = TextAlignmentOptions.Center;
            text.rectTransform.anchorMin = Vector2.zero; text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = new Vector2(16, 8); text.rectTransform.offsetMax = new Vector2(-16, -8);
            button.onClick.AddListener(() => action());
            return button;
        }

        private void Build()
        {
            card = Node(transform, "TutorialCard");
            card.anchorMin = card.anchorMax = new Vector2(.5f, .5f);
            card.pivot = new Vector2(.5f, .5f);
            SharedUiStyles.Surface(card.gameObject, "UI_MODAL_SURFACE", true);
            var layout = card.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(28, 28, 24, 24); layout.spacing = 14;
            layout.childControlHeight = layout.childControlWidth = true;
            layout.childForceExpandHeight = false; layout.childForceExpandWidth = true;
            card.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // contador "BANCO · 1 DE 3": face Medium e cor de texto (Caption regular em cinza ficava apagado)
            progress = Text(card, "Progress", new TypographyToken(TypographyRole.Label, GameTypography.Label));
            progress.color = GamePalette.Ink;
            heading = Text(card, "Heading", GameTypography.Tokens.SectionTitle, true);
            message = Text(card, "Message", GameTypography.Tokens.Body);
            var actions = Node(card, "Actions");
            actions.gameObject.AddComponent<LayoutElement>().preferredHeight = 68;
            var row = actions.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.spacing = 14; row.childControlHeight = row.childControlWidth = true;
            row.childForceExpandWidth = true;
            next = MakeButton(actions, "Next", "Continuar", SharedControlKind.Primary, Next);
            nextLabel = next.GetComponentInChildren<TextMeshProUGUI>();
        }

        public void Show()
        {
            current = 0;
            Render();
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            var canvas = GetComponent<Canvas>(); canvas.overrideSorting = true; canvas.sortingOrder = 2000;
            Resize();
        }

        private void Render()
        {
            progress.text = $"BANCO  \u00B7  {current + 1} DE {Titles.Length}";
            heading.text = Titles[current];
            message.text = Messages[current];
            nextLabel.text = current == Titles.Length - 1 ? "Entendi, escolher op\u00E7\u00E3o" : "Continuar";
            LayoutRebuilder.ForceRebuildLayoutImmediate(card);
            Resize();
        }

        private void Next()
        {
            if (current < Titles.Length - 1) { current++; Render(); }
            else Complete();
        }

        private void Complete()
        {
            if (!string.IsNullOrEmpty(preferenceKey))
            {
                PlayerPrefs.SetInt(preferenceKey, 1);
                PlayerPrefs.Save();
            }
            gameObject.SetActive(false);
        }

        private void Resize()
        {
            if (card == null) return;
            var root = (RectTransform)transform;
            card.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Min(1040, Mathf.Max(300, root.rect.width - 48)));
            LayoutRebuilder.ForceRebuildLayoutImmediate(card);
            card.anchoredPosition = Vector2.zero;
        }

        private void LateUpdate() => Resize();
        public void Hide() { if (gameObject != null) gameObject.SetActive(false); }
    }
}
