#if UNITY_EDITOR
using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Game.Adapter.In.UI.Navigation;

namespace Game.Adapter.In.UI
{
    /// <summary>Scrollable introduction, local to its screen and absent in hub mode.</summary>
    public static class TutorialUiFactory
    {
        public static bool IsInitial(GameState state) => GameManager.Instance != null
            && GameManager.Instance.StateMachine.CurrentState == state;

        public static void Build(Transform screen, PanelId destination, bool initial, string explanation)
        {
            var previous = screen.Find("InitialTutorial");
            if (previous != null)
            {
                previous.gameObject.SetActive(false);
                UnityEngine.Object.DestroyImmediate(previous.gameObject);
            }
            if (!initial) return;

            var overlay = Object("InitialTutorial", screen, typeof(Image));
            Stretch(overlay.GetComponent<RectTransform>());
            overlay.GetComponent<Image>().color = new Color(0, 0, 0, .85f);
            var sheet = Object("Sheet", overlay.transform, typeof(Image));
            var rect = sheet.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(.08f, .08f);
            rect.anchorMax = new Vector2(.92f, .92f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            sheet.GetComponent<Image>().color = new Color(.04f, .09f, .16f, 1);

            var identity = FindIdentity(destination);
            var heading = Text(sheet.transform, identity.name, 32);
            Position(heading.rectTransform, new Vector2(0, .82f), Vector2.one, new Vector2(90, -20), new Vector2(-24, -20));
            var icon = Object("MenuIcon", sheet.transform, typeof(Image));
            Position(icon.GetComponent<RectTransform>(), new Vector2(0, .82f), new Vector2(0, 1), new Vector2(20, 0), new Vector2(76, -20));
            icon.GetComponent<Image>().sprite = identity.icon;
            icon.GetComponent<Image>().preserveAspect = true;
            icon.SetActive(identity.icon != null);

            var content = Scroll(sheet.transform);
            Text(content, explanation + "\n\nVocê poderá acessar esta tela a qualquer momento pelo menu: " + identity.name + ".", 24);
            var dismiss = Button(sheet.transform, "Entendi — vamos começar", () => { }, .02f, .13f);
            var view = overlay.AddComponent<InitialTutorialView>();
            UnityEditor.Events.UnityEventTools.AddPersistentListener(dismiss.onClick, view.Dismiss);
            overlay.SetActive(false);
        }

        private static (string name, Sprite icon) FindIdentity(PanelId id)
        {
            foreach (var node in UnityEngine.Object.FindObjectsByType<PanelNavButton>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (node.Target != id || node.GetComponent<MapMenuNode>() == null) continue;
                var label = node.transform.Find("Title") ?? node.transform.Find("MeTitle");
                var image = node.transform.Find("IconBadge/Icon")?.GetComponent<Image>();
                return (label?.GetComponent<TMP_Text>()?.text ?? Name(id), image?.sprite);
            }
            return (Name(id), null);
        }

        private static string Name(PanelId id) => id == PanelId.Financial ? "Banco"
            : id == PanelId.EquipmentStore ? "Loja de Equipamentos" : "RH";

        public static GameObject Object(string name, Transform parent, params Type[] components)
        {
            var go = new GameObject(name, new[] { typeof(RectTransform) }.Concat(components).ToArray());
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            return go;
        }

        public static TextMeshProUGUI Text(Transform parent, string value, float size = 24)
        {
            var go = Object("Text", parent, typeof(TextMeshProUGUI), typeof(LayoutElement));
            var text = go.GetComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = size;
            text.color = Color.white;
            text.raycastTarget = false;
            var font = parent.GetComponentsInParent<Canvas>(true).FirstOrDefault()
                ?.GetComponentsInChildren<TextMeshProUGUI>(true).FirstOrDefault(t => t != text && t.font != null)?.font;
            if (font != null) text.font = font;
            return text;
        }

        public static RectTransform Scroll(Transform parent)
        {
            var go = Object("Scroll", parent, typeof(Image), typeof(ScrollRect));
            Position(go.GetComponent<RectTransform>(), new Vector2(0, .17f), new Vector2(1, .8f), new Vector2(24, 0), new Vector2(-24, 0));
            go.GetComponent<Image>().color = new Color(1, 1, 1, .01f);
            var viewport = Object("Viewport", go.transform, typeof(RectMask2D));
            Stretch(viewport.GetComponent<RectTransform>());
            var content = Object("Content", viewport.transform, typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            var rect = content.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(.5f, 1);
            rect.sizeDelta = Vector2.zero;
            var layout = content.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 18;
            layout.padding = new RectOffset(8, 8, 8, 8);
            layout.childControlHeight = layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = go.GetComponent<ScrollRect>();
            scroll.viewport = viewport.GetComponent<RectTransform>();
            scroll.content = rect;
            scroll.horizontal = false;
            scroll.scrollSensitivity = 35;
            return rect;
        }

        public static UnityEngine.UI.Button Button(Transform parent, string label, UnityEngine.Events.UnityAction action, float bottom = 0, float top = 0)
        {
            var go = Object(label, parent, typeof(Image), typeof(UnityEngine.UI.Button), typeof(LayoutElement));
            go.GetComponent<Image>().color = new Color(.1f, .35f, .6f, 1);
            if (top > bottom)
                Position(go.GetComponent<RectTransform>(), new Vector2(0, bottom), new Vector2(1, top), new Vector2(24, 0), new Vector2(-24, 0));
            go.GetComponent<LayoutElement>().preferredHeight = 56;
            var text = Text(go.transform, label);
            Stretch(text.rectTransform);
            text.alignment = TextAlignmentOptions.Center;
            var button = go.GetComponent<UnityEngine.UI.Button>();
            button.targetGraphic = go.GetComponent<Image>();
            button.onClick.AddListener(action);
            return button;
        }

        public static void Stretch(RectTransform rect) => Position(rect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        public static void Position(RectTransform rect, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }
    }
}

#endif