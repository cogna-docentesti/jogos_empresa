using System;
using System.Linq;
using Game.Adapter.In.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.EditorTools
{
    /// <summary>UI-01 migration. Reuses the original form and its serialized references.</summary>
    public static class IdentificationPanelBuilder
    {
        private const string Art = "Assets/Art/new/";
        private static TMP_FontAsset font;
        private static Color Ink => Hex("#30243C");
        private static Color Ivory => Hex("#F5F0E8");

        [MenuItem("Tools/Jogos de Empresa/UI-01/Migrate identification")]
        public static void Build()
        {
            if (UnityEngine.Application.isPlaying || UnityEngine.Application.dataPath.Replace('\\', '/') != "D:/Cogna/jogos_empresa/Assets")
                throw new InvalidOperationException("Run in the Cogna Editor outside Play Mode.");
            var scene = SceneManager.GetActiveScene();
            if (scene.path != "Assets/Scenes/GameScene.unity")
                throw new InvalidOperationException("Open GameScene before migrating.");
            var canvas = scene.GetRootGameObjects().Single(g => g.name == "Canvas");
            if (canvas.GetComponent<UnityEngine.UI.CanvasScaler>().referenceResolution != new Vector2(1920, 1080))
                throw new InvalidOperationException("Unexpected main canvas reference resolution.");
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Fonts/InterTight-Variable.asset");
            if (font == null) throw new InvalidOperationException("Inter Tight is missing.");

            var panel = canvas.transform.Find("Panel_Identification");
            if (panel == null)
            {
                var source = EditorSceneManager.OpenScene("Assets/Scenes/0_Identification.unity", OpenSceneMode.Additive);
                try
                {
                    var original = source.GetRootGameObjects().Single(g => g.name == "Canvas");
                    var clone = UnityEngine.Object.Instantiate(original, canvas.transform, false);
                    Undo.RegisterCreatedObjectUndo(clone, "Migrate identification");
                    clone.name = "Panel_Identification";
                    // Instantiate remaps all internal form references before removing root services.
                    UnityEngine.Object.DestroyImmediate(clone.GetComponent<UnityEngine.UI.GraphicRaycaster>());
                    UnityEngine.Object.DestroyImmediate(clone.GetComponent<UnityEngine.UI.CanvasScaler>());
                    UnityEngine.Object.DestroyImmediate(clone.GetComponent<Canvas>());
                    panel = clone.transform;
                }
                finally { EditorSceneManager.CloseScene(source, true); }
            }
            Undo.RegisterFullObjectHierarchyUndo(panel.gameObject, "Style identification");
            Stretch((RectTransform)panel);
            // A fixed design frame fits inside the parent at any aspect ratio.
            var frame = panel.Find("DesignFrame") as RectTransform;
            if (frame == null)
            {
                frame = Child(panel, "DesignFrame");
                foreach (var child in panel.Cast<Transform>().Where(t => t != frame).ToArray())
                    child.SetParent(frame, false);
            }
            var fit = frame.GetComponent<UnityEngine.UI.AspectRatioFitter>()
                ?? frame.gameObject.AddComponent<UnityEngine.UI.AspectRatioFitter>();
            fit.aspectMode = UnityEngine.UI.AspectRatioFitter.AspectMode.FitInParent;
            fit.aspectRatio = 16f / 9;
            var rootImage = panel.GetComponent<UnityEngine.UI.Image>() ?? panel.gameObject.AddComponent<UnityEngine.UI.Image>();
            rootImage.color = Hex("#24113D"); rootImage.raycastTarget = true;
            // The reference canvas is 1920x1080, so fitting normally retains this design size.
            // Layout at other aspects uses the CanvasScaler plus this local scale adapter.
            var design = frame.Find("Content") as RectTransform;
            if (design == null)
            {
                design = Child(frame, "Content");
                foreach (var child in frame.Cast<Transform>().Where(t => t != design).ToArray())
                    child.SetParent(design, false);
            }
            design.anchorMin = design.anchorMax = new Vector2(.5f, .5f);
            design.pivot = new Vector2(.5f, .5f); design.sizeDelta = new Vector2(1920, 1080);
            design.anchoredPosition = Vector2.zero;
            if (design.GetComponent<IdentificationDesignScale>() == null)
                design.gameObject.AddComponent<IdentificationDesignScale>();

            var background = design.Find("Background") as RectTransform;
            Place(background, 0, 0, 1920, 1080);
            Paint(background, "identification/backgrounds/street_restaurant.png", false);
            background.SetAsFirstSibling();
            var left = Child(design, "OrnamentLeft"); Place(left, 0, 270, 320, 640);
            Paint(left, "identification/ornaments/arc_left.png", false, true);
            var right = Child(design, "OrnamentRight"); Place(right, 1580, 270, 320, 640);
            Paint(right, "identification/ornaments/arc_right.png", false, true);
            var welcome = Child(design, "CH01_WELCOME"); Place(welcome, 15, 57, 654, 981);
            Paint(welcome, "characters/CH01/ch01_welcome.png", false, true);
            left.SetSiblingIndex(1); right.SetSiblingIndex(2); welcome.SetSiblingIndex(3);
            var header = Child(design, "IdentificationHeader"); Place(header, 0, 0, 1920, 88);
            Paint(header, "hud/bars/topbar_violet.png", false);
            Label(header, "GameTitle", "Jogos de Empresa", 40, 18, 410, 52, 36, Ivory, true);
            Label(header, "GameSubtitle", "Gestão de restaurantes", 470, 28, 510, 36, 24, Ivory);
            Label(header, "Step", "PASSO 1 DE 7  ·  Identificação", 1230, 28, 630, 36, 24, Ivory, true);

            var form = design.Find("Popup") as RectTransform;
            Place(form, 1180, 184, 650, 772);
            Paint(form, "shared/components/panels/panel_ivory.png", true);
            form.SetAsLastSibling();
            var logo = Child(form, "CognaLogo"); Place(logo, 48, 28, 96, 96);
            Paint(logo, "shared/brand/logo_cogna.png", false, true);
            StyleText(form.Find("Title").GetComponent<TMP_Text>(), "Identificação", 44, Ink, true);
            Place((RectTransform)form.Find("Title"), 168, 26, 438, 60);
            Label(form, "Intro", "Seus dados para começar", 168, 88, 430, 34, 24, Ink);

            var ui = panel.GetComponent<UIIndentification>();
            var so = new SerializedObject(ui);
            var name = (TMP_InputField)so.FindProperty("studentNameInput").objectReferenceValue;
            var ra = (TMP_InputField)so.FindProperty("raInput").objectReferenceValue;
            StyleInput(name, 182, "Nome do aluno", "Digite seu nome", 50);
            StyleInput(ra, 370, "RA", "Digite seu RA", 10);
            Label(form, "NameLimits", "De 5 a 50 caracteres", 48, 314, 554, 30, 20, Ink);
            Label(form, "RALimits", "De 5 a 10 caracteres", 48, 502, 554, 30, 20, Ink);

            var validation = (TMP_Text)so.FindProperty("validationText").objectReferenceValue;
            Place(validation.rectTransform, 48, 542, 554, 72);
            StyleText(validation, string.Empty, 24, Hex("#A6323D"));
            validation.textWrappingMode = TextWrappingModes.Normal;
            var button = (UnityEngine.UI.Button)so.FindProperty("buttonRegister").objectReferenceValue;
            Place((RectTransform)button.transform, 48, 634, 554, 88);
            Paint((RectTransform)button.transform, "shared/components/buttons/primary_violet_normal.png", true).raycastTarget = true;
            button.transition = UnityEngine.UI.Selectable.Transition.SpriteSwap;
            button.spriteState = new UnityEngine.UI.SpriteState
            {
                highlightedSprite = Sprite("shared/components/buttons/primary_violet_hover.png", 24),
                selectedSprite = Sprite("shared/components/buttons/primary_violet_hover.png", 24),
                pressedSprite = Sprite("shared/components/buttons/primary_violet_pressed.png", 24),
                disabledSprite = Sprite("shared/components/buttons/primary_violet_disabled.png", 24)
            };
            button.targetGraphic = button.GetComponent<UnityEngine.UI.Image>();
            var buttonText = button.GetComponentInChildren<TMP_Text>(true);
            Stretch(buttonText.rectTransform);
            StyleText(buttonText, "Cadastrar", 28, Ivory, true);
            buttonText.alignment = TextAlignmentOptions.Center;
            Label(form, "StorageNote", "Seus dados ficam salvos neste dispositivo.", 48, 734, 554, 26, 20, Ink);
            foreach (var text in panel.GetComponentsInChildren<TMP_Text>(true))
            {
                text.raycastTarget = false;
                foreach (var effect in text.GetComponents<UnityEngine.UI.Shadow>())
                    UnityEngine.Object.DestroyImmediate(effect);
            }
            Navigation(name, ra, button); Navigation(ra, button, name); Navigation(button, name, ra);

            // Keep existing indicators in place; only their numbering changes in UI-01.
            foreach (var text in canvas.GetComponentsInChildren<TMP_Text>(true))
                if (text.name == "StepsText" && text.text.Contains("de 6"))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(text.text, @"Etapa (\d) de 6");
                    if (match.Success) text.text = "Passo " + (int.Parse(match.Groups[1].Value) + 1) + " de 7";
                }
            var listener = UnityEngine.Object.FindFirstObjectByType<UIStateListener>();
            var listenerSO = new SerializedObject(listener);
            listenerSO.FindProperty("identificationPanel").objectReferenceValue = panel.gameObject;
            listenerSO.FindProperty("initialTeamPanel").objectReferenceValue = canvas.transform.Find("Panel_Team").gameObject;
            listenerSO.ApplyModifiedPropertiesWithoutUndo();
            var team = canvas.transform.Find("Panel_Team");
            if (!team.GetComponentsInChildren<TMP_Text>(true).Any(t => t.name == "StepsText"))
                Label(team, "StepsText", "Passo 7 de 7", 24, 16, 300, 40, 24, Ivory);
            // Only the identification should be visible before services pick the entry state.
            foreach (var child in canvas.transform.Cast<Transform>())
                if (child.name.StartsWith("Panel_")) child.gameObject.SetActive(child == panel);
            panel.SetAsLastSibling();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene("Assets/Scenes/GameScene.unity", true) };
            foreach (var guid in AssetDatabase.FindAssets("t:BuildProfile", new[] {"Assets/Settings/Build Profiles"}))
            {
                var profile = AssetDatabase.LoadAssetAtPath<UnityEditor.Build.Profile.BuildProfile>(AssetDatabase.GUIDToAssetPath(guid));
                var profileSO = new SerializedObject(profile);
                profileSO.FindProperty("m_OverrideGlobalSceneList").boolValue = false;
                if (profileSO.FindProperty("m_BuildTarget").intValue == (int)BuildTarget.StandaloneWindows64)
                    profileSO.FindProperty("m_Subtarget").intValue = (int)StandaloneBuildSubtarget.Player;
                profileSO.FindProperty("m_Scenes").ClearArray();
                profileSO.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(profile);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[UI-01] Identification migrated and GameScene configured as the production entry.");
        }

        private static void StyleInput(TMP_InputField input, float y, string label, string placeholder, int maximum)
        {
            var group = (RectTransform)input.transform.parent;
            Place(group, 48, y, 554, 124);
            var title = group.GetComponentsInChildren<TMP_Text>(true).First(t => t.transform.parent == group);
            Place(title.rectTransform, 0, 0, 554, 32); StyleText(title, label, 22, Ink, true);
            Place((RectTransform)input.transform, 0, 44, 554, 80);
            Paint((RectTransform)input.transform, "shared/components/controls/input_normal.png", true).raycastTarget = true;
            input.transition = UnityEngine.UI.Selectable.Transition.None;
            input.characterLimit = maximum; input.lineType = TMP_InputField.LineType.SingleLine;
            input.customCaretColor = true; input.caretColor = Hex("#6D28D9");
            input.selectionColor = new Color(.725f,.604f,.91f,.5f);
            input.textViewport.anchorMin = Vector2.zero; input.textViewport.anchorMax = Vector2.one;
            input.textViewport.offsetMin = new Vector2(24, 12); input.textViewport.offsetMax = new Vector2(-24, -12);
            StyleText(input.textComponent, string.Empty, 24, Ink);
            StyleText((TMP_Text)input.placeholder, placeholder, 24, Hex("#65546F"));
            Stretch(input.textComponent.rectTransform); Stretch(((TMP_Text)input.placeholder).rectTransform);
            var appearance = input.GetComponent<IdentificationInputAppearance>() ?? input.gameObject.AddComponent<IdentificationInputAppearance>();
            var settings = new SerializedObject(appearance);
            foreach (var state in new[] {"normal", "focus", "invalid", "disabled"})
                settings.FindProperty(state).objectReferenceValue = Sprite("shared/components/controls/input_" + state + ".png", 20);
            settings.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Navigation(UnityEngine.UI.Selectable item, UnityEngine.UI.Selectable next, UnityEngine.UI.Selectable previous)
            => item.navigation = new UnityEngine.UI.Navigation {mode = UnityEngine.UI.Navigation.Mode.Explicit, selectOnDown = next, selectOnUp = previous};
        private static Color Hex(string hex) { ColorUtility.TryParseHtmlString(hex, out var c); return c; }
        private static RectTransform Child(Transform parent, string name)
        {
            var found = parent.Find(name) as RectTransform;
            if (found != null) return found;
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            Undo.RegisterCreatedObjectUndo(go, "Create " + name); return (RectTransform)go.transform;
        }
        private static void Place(RectTransform r, float x, float y, float w, float h)
        {
            r.anchorMin = r.anchorMax = new Vector2(0, 1); r.pivot = new Vector2(0, 1);
            r.anchoredPosition = new Vector2(x, -y); r.sizeDelta = new Vector2(w, h); r.localScale = Vector3.one;
        }
        private static void Stretch(RectTransform r)
        {
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.pivot = new Vector2(.5f,.5f);
            r.offsetMin = r.offsetMax = Vector2.zero; r.localScale = Vector3.one;
        }
        private static Sprite Sprite(string relative, float border = 0)
        {
            string path = Art + relative;
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer.spriteImportMode != SpriteImportMode.Single) throw new InvalidOperationException(path + " must remain Single");
            var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
            if (importer.spriteBorder != Vector4.one * border || settings.spriteMeshType != SpriteMeshType.FullRect)
            {
                settings.spriteBorder = Vector4.one * border;
                settings.spriteMeshType = SpriteMeshType.FullRect; importer.SetTextureSettings(settings);
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path) ?? throw new InvalidOperationException("Missing sprite " + path);
        }
        private static UnityEngine.UI.Image Paint(RectTransform r, string path, bool sliced, bool contain = false)
        {
            var image = r.GetComponent<UnityEngine.UI.Image>() ?? r.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.sprite = Sprite(path, sliced ? (path.Contains("input_") ? 20 : 24) : 0);
            image.color = Color.white; image.type = sliced ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
            image.preserveAspect = contain; image.raycastTarget = false; return image;
        }
        private static void StyleText(TMP_Text text, string value, float size, Color color, bool bold = false)
        {
            text.font = font; text.fontSharedMaterial = font.material; text.text = value;
            text.fontSize = size; text.enableAutoSizing = false; text.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            text.color = color; text.alignment = TextAlignmentOptions.MidlineLeft;
            text.characterSpacing = 0; text.lineSpacing = 0; text.margin = Vector4.zero;
            text.overflowMode = TextOverflowModes.Overflow; text.raycastTarget = false;
        }
        private static void Label(Transform parent, string name, string value, float x, float y, float w, float h, float size, Color color, bool bold = false)
        {
            var r = Child(parent, name); Place(r,x,y,w,h);
            var text = r.GetComponent<TextMeshProUGUI>() ?? r.gameObject.AddComponent<TextMeshProUGUI>();
            StyleText(text,value,size,color,bold);
        }
    }
}
