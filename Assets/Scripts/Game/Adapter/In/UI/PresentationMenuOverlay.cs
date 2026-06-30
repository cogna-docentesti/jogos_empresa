using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.Adapter.In.UI
{
    /// <summary>
    /// Menu temporario para demonstracoes, criado depois que uma linha de credito e confirmada.
    /// </summary>
    public sealed class PresentationMenuOverlay : MonoBehaviour
    {
        private readonly struct ScreenEntry
        {
            public ScreenEntry(string panelName, string label)
            {
                PanelName = panelName;
                Label = label;
            }

            public string PanelName { get; }
            public string Label { get; }
        }

        private static readonly ScreenEntry[] Screens =
        {
            new("Panel_Location", "Localizacao"),
            new("Panel_Restaurant", "Restaurante"),
            new("Panel_MenuPricing", "Cardapio e precos"),
            new("Panel_Financial", "Linha de credito")
        };

        private static PresentationMenuOverlay _instance;

        private GameObject _dialog;
        private GameObject _openButton;

        public static void Show()
        {
            EnsureEventSystem();

            if (_instance == null)
                _instance = Create();

            _instance.Open();
        }

        private static PresentationMenuOverlay Create()
        {
            var root = new GameObject("PresentationMenuOverlay", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));

            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 5000;

            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var overlay = root.AddComponent<PresentationMenuOverlay>();
            overlay.BuildInterface();
            return overlay;
        }

        private void BuildInterface()
        {
            _dialog = CreateImage("Dialog", transform, new Color(0.02f, 0.05f, 0.1f, 0.96f));
            Stretch(_dialog.GetComponent<RectTransform>());

            var card = CreateImage("Card", _dialog.transform, new Color(0.06f, 0.12f, 0.2f, 1f));
            var cardRect = card.GetComponent<RectTransform>();
            cardRect.anchorMin = cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.sizeDelta = new Vector2(760f, 760f);

            var layout = card.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(60, 60, 48, 48);
            layout.spacing = 18f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;

            CreateText("Modo de apresentacao", card.transform, 42f, FontStyles.Bold, 76f);
            CreateText("Escolha a tela que deseja exibir", card.transform, 25f, FontStyles.Normal, 54f);

            foreach (var screen in Screens)
            {
                var entry = screen;
                CreateButton(card.transform, entry.Label, () => NavigateTo(entry.PanelName), 72f);
            }

            CreateButton(card.transform, "Fechar", Close, 62f, new Color(0.23f, 0.29f, 0.36f, 1f));

            _openButton = CreateButton(transform, "Telas", Open, 64f, new Color(0.05f, 0.45f, 0.75f, 1f));
            var openRect = _openButton.GetComponent<RectTransform>();
            openRect.anchorMin = openRect.anchorMax = new Vector2(1f, 1f);
            openRect.pivot = new Vector2(1f, 1f);
            openRect.anchoredPosition = new Vector2(-28f, -28f);
            openRect.sizeDelta = new Vector2(180f, 64f);
            _openButton.SetActive(false);
        }

        private void NavigateTo(string panelName)
        {
            var panels = FindScreenPanels();
            var target = panels.FirstOrDefault(panel => panel.name == panelName);

            if (target == null)
            {
                Debug.LogWarning($"[PresentationMenu] Tela nao encontrada: {panelName}");
                return;
            }

            foreach (var panel in panels)
                panel.SetActive(panel == target);

            Close();
        }

        private static List<GameObject> FindScreenPanels()
        {
            var names = new HashSet<string>(Screens.Select(screen => screen.PanelName));
            Scene activeScene = SceneManager.GetActiveScene();

            return Resources.FindObjectsOfTypeAll<GameObject>()
                .Where(item => item.scene == activeScene && names.Contains(item.name))
                .ToList();
        }

        private void Open()
        {
            _dialog.SetActive(true);
            _openButton.SetActive(false);
        }

        private void Close()
        {
            _dialog.SetActive(false);
            _openButton.SetActive(true);
        }

        private static GameObject CreateButton(Transform parent, string label, Action action, float height,
            Color? color = null)
        {
            var buttonObject = CreateImage($"Button_{label}", parent,
                color ?? new Color(0.05f, 0.45f, 0.75f, 1f));
            var button = buttonObject.AddComponent<Button>();
            button.targetGraphic = buttonObject.GetComponent<Image>();
            button.onClick.AddListener(() => action());

            var layout = buttonObject.AddComponent<LayoutElement>();
            layout.preferredHeight = height;

            var text = CreateText(label, buttonObject.transform, 27f, FontStyles.Bold, height);
            Stretch(text.rectTransform);
            text.alignment = TextAlignmentOptions.Center;

            return buttonObject;
        }

        private static TextMeshProUGUI CreateText(string value, Transform parent, float size,
            FontStyles style, float height)
        {
            var textObject = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);

            var text = textObject.GetComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center;

            var layout = textObject.AddComponent<LayoutElement>();
            layout.preferredHeight = height;
            return text;
        }

        private static GameObject CreateImage(string name, Transform parent, Color color)
        {
            var item = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            item.transform.SetParent(parent, false);
            item.GetComponent<Image>().color = color;
            return item;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null)
                return;

            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }
    }
}