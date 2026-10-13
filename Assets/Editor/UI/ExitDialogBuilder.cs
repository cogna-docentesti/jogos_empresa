using Game.Adapter.In.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// E-05: monta na GameScene o botao Sair e o dialogo "salvar antes de sair?".
///
/// Hierarquia criada:
///
///   Canvas
///   └── ExitUI              NOVO. Ultimo filho do Canvas: desenha por cima de tudo
///       │                   (ExitDialogController)
///       ├── ExitButton      SAIR, embaixo no centro, mesmo tamanho e mesma
///       │                   linha do MENU. Sempre visivel. btn-menu-solid
///       └── ExitDialog      desativado por padrao
///           ├── Backdrop    escurece a tela e bloqueia cliques atras
///           └── DialogPanel panel-bg.png, 900 x 440
///               ├── TitleText
///               ├── BodyText
///               └── ButtonRow (Horizontal Layout Group)
///                   ├── CancelButton    btn-menu-base     CANCELAR
///                   ├── DiscardButton   btn-menu-base     SAIR / SAIR SEM SALVAR
///                   └── SaveExitButton  btn-continuar-base SALVAR E SAIR
///
/// Como usar: abra a GameScene e rode
/// Tools > Jogos de Empresa > Criar botao Sair e dialogo (E-05).
/// Rodar de novo apaga e recria o que este script criou (so isso). Ctrl+Z desfaz.
/// </summary>
public static class ExitDialogBuilder
{
    private const string SecondarySprite = "Assets/Art/Sprites/REDESIGN-UI/btn-menu-base.png";
    // Mesmo desenho do btn-menu-base, com o fundo 92% opaco em vez de 55%:
    // embaixo, no hub, o botao fica em cima do mapa e precisa de contraste.
    private const string ExitSprite = "Assets/Art/Sprites/REDESIGN-UI/btn-menu-solid.png";
    private const string PrimarySprite = "Assets/Art/Sprites/REDESIGN-UI/btn-continuar-base.png";
    private const string PanelSprite = "Assets/Art/Sprites/REDESIGN-UI/panel-bg.png";
    private const string FontGuid = "89cea402e3db5bf4d9bc9d4f698b7692"; // InterTight-Variable (TMP)

    private static readonly Color Gold = new Color32(247, 220, 154, 255);      // texto do MENU
    private static readonly Color DarkBrown = new Color32(61, 38, 4, 255);     // texto sobre o amarelo
    private static readonly Color BodyColor = new Color32(226, 224, 236, 255);

    // Distancia do centro do botao Sair ate a borda de baixo da tela, em
    // unidades do Canvas (referencia 1920 x 1080). O MENU (MenuAccess/Button,
    // y -1005, 50 de altura, ancorado no topo) tem o centro a 50 da borda.
    private const float BottomRowCenterY = 50f;

    [MenuItem("Tools/Jogos de Empresa/Criar botao Sair e dialogo (E-05)")]
    public static void Build()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogError("[ExitDialogBuilder] Saia do Play Mode antes de rodar.");
            return;
        }

        var canvas = GameObject.Find("Canvas");
        var menuAccess = canvas != null ? canvas.transform.Find("MenuAccess") : null;
        var menuButton = menuAccess != null ? menuAccess.Find("Button") as RectTransform : null;

        if (canvas == null || menuAccess == null || menuButton == null)
        {
            Debug.LogError("[ExitDialogBuilder] Abra a GameScene (Canvas/MenuAccess/Button nao encontrado).");
            return;
        }

        var secondary = AssetDatabase.LoadAssetAtPath<Sprite>(SecondarySprite);
        var primary = AssetDatabase.LoadAssetAtPath<Sprite>(PrimarySprite);
        var panel = AssetDatabase.LoadAssetAtPath<Sprite>(PanelSprite);
        var exitSprite = AssetDatabase.LoadAssetAtPath<Sprite>(ExitSprite);
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(FontGuid));

        if (secondary == null || primary == null || panel == null || font == null || exitSprite == null)
        {
            Debug.LogError($"[ExitDialogBuilder] Asset faltando. secundario={secondary != null} primario={primary != null} painel={panel != null} fonte={font != null} sair={exitSprite != null}");
            return;
        }

        // Recria do zero o que este script criou antes. A primeira versao
        // punha o ExitButton dentro de MenuAccess; apaga de la tambem.
        RemoveIfExists(menuAccess, "ExitButton");
        RemoveIfExists(canvas.transform, "ExitUI");

        // ── Raiz, por cima de tudo ──────────────────────────────────
        var uiRt = NewRect("ExitUI", canvas.transform);
        Stretch(uiRt);
        uiRt.SetAsLastSibling();
        var controller = uiRt.gameObject.AddComponent<ExitDialogController>();

        // ── Botao Sair, embaixo no centro ───────────────────────────
        // Nas telas D1 a D3 o VOLTAR fica no canto inferior esquerdo e o
        // CONTINUAR no direito; o MENU tambem fica no canto inferior direito.
        // O centro da linha de baixo e o unico lugar livre em todas as telas.
        // Mesmo tamanho do MENU e centro na mesma altura que o dele.
        var exitButtonRt = NewRect("ExitButton", uiRt);
        exitButtonRt.anchorMin = exitButtonRt.anchorMax = new Vector2(0.5f, 0f);
        exitButtonRt.pivot = new Vector2(0.5f, 0.5f);
        exitButtonRt.sizeDelta = menuButton.sizeDelta;
        exitButtonRt.anchoredPosition = new Vector2(0f, BottomRowCenterY);
        var exitButton = MakeButton(exitButtonRt, exitSprite, Color.white);
        AddLabel(exitButtonRt, "SAIR", font, 24f, Gold);

        var dialogRt = NewRect("ExitDialog", uiRt);
        Stretch(dialogRt);

        var backdropRt = NewRect("Backdrop", dialogRt);
        Stretch(backdropRt);
        var backdrop = backdropRt.gameObject.AddComponent<Image>();
        backdrop.color = new Color(0.04f, 0.04f, 0.06f, 0.62f);
        backdrop.raycastTarget = true;   // bloqueia cliques nos paineis de tras

        var panelRt = NewRect("DialogPanel", dialogRt);
        panelRt.anchorMin = panelRt.anchorMax = new Vector2(0.5f, 0.5f);
        panelRt.sizeDelta = new Vector2(900f, 440f);
        var panelImage = panelRt.gameObject.AddComponent<Image>();
        panelImage.sprite = panel;
        panelImage.type = Image.Type.Sliced;

        var titleRt = NewRect("TitleText", panelRt);
        titleRt.anchorMin = new Vector2(0f, 1f);
        titleRt.anchorMax = new Vector2(1f, 1f);
        titleRt.pivot = new Vector2(0.5f, 1f);
        titleRt.anchoredPosition = new Vector2(0f, -40f);
        titleRt.sizeDelta = new Vector2(-80f, 60f);
        var title = AddText(titleRt, "Sair do jogo", font, 40f, Gold, FontStyles.Bold);

        var bodyRt = NewRect("BodyText", panelRt);
        bodyRt.anchorMin = new Vector2(0f, 0f);
        bodyRt.anchorMax = new Vector2(1f, 1f);
        bodyRt.offsetMin = new Vector2(60f, 140f);
        bodyRt.offsetMax = new Vector2(-60f, -110f);
        var body = AddText(bodyRt, "Texto preenchido pelo ExitDialogController.", font, 28f, BodyColor, FontStyles.Normal);
        body.textWrappingMode = TextWrappingModes.Normal;

        var rowRt = NewRect("ButtonRow", panelRt);
        rowRt.anchorMin = new Vector2(0f, 0f);
        rowRt.anchorMax = new Vector2(1f, 0f);
        rowRt.pivot = new Vector2(0.5f, 0f);
        rowRt.anchoredPosition = new Vector2(0f, 40f);
        rowRt.sizeDelta = new Vector2(-60f, 72f);
        var layout = rowRt.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 20f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        var cancel = DialogButton("CancelButton", rowRt, secondary, "CANCELAR", font, Gold, 220f, out _);
        var discard = DialogButton("DiscardButton", rowRt, secondary, "SAIR SEM SALVAR", font, Gold, 270f, out var discardLabel);
        var saveExit = DialogButton("SaveExitButton", rowRt, primary, "SALVAR E SAIR", font, DarkBrown, 270f, out _);

        dialogRt.gameObject.SetActive(false);

        controller.EditorConfigure(exitButton,
            dialogRt.gameObject, title, body, cancel, discard, discardLabel, saveExit);

        Undo.RegisterCreatedObjectUndo(uiRt.gameObject, "Criar botao e dialogo Sair");

        EditorUtility.SetDirty(controller);
        EditorSceneManager.MarkSceneDirty(canvas.scene);
        EditorSceneManager.SaveScene(canvas.scene);
        Debug.Log($"[ExitDialogBuilder] Botao Sair e dialogo criados. Sair embaixo no centro, {exitButtonRt.sizeDelta.x} x {exitButtonRt.sizeDelta.y}, centro a {BottomRowCenterY} da borda de baixo. Cena salva.");
    }

    // ─────────────────────────────────────────────────────────────

    private static void RemoveIfExists(Transform parent, string name)
    {
        var existing = parent.Find(name);
        if (existing != null)
            Undo.DestroyObjectImmediate(existing.gameObject);
    }

    private static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        return rt;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static Button MakeButton(RectTransform rt, Sprite sprite, Color tint)
    {
        var image = rt.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.type = Image.Type.Sliced;
        image.color = tint;

        var button = rt.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        var colors = button.colors;
        colors.highlightedColor = new Color(0.88f, 0.88f, 0.88f, 1f);
        colors.pressedColor = new Color(0.72f, 0.72f, 0.72f, 1f);
        button.colors = colors;
        return button;
    }

    private static Button DialogButton(string name, RectTransform row, Sprite sprite, string text,
        TMP_FontAsset font, Color textColor, float width, out TextMeshProUGUI label)
    {
        var rt = NewRect(name, row);
        rt.sizeDelta = new Vector2(width, 68f);
        var button = MakeButton(rt, sprite, Color.white);
        label = AddLabel(rt, text, font, 24f, textColor);
        return button;
    }

    private static TextMeshProUGUI AddLabel(RectTransform parent, string text, TMP_FontAsset font, float size, Color color)
    {
        var rt = NewRect("Text (TMP)", parent);
        Stretch(rt);
        rt.offsetMin = new Vector2(12f, 0f);
        rt.offsetMax = new Vector2(-12f, 0f);
        var label = AddText(rt, text, font, size, color, FontStyles.Bold);
        label.characterSpacing = 2f;
        label.raycastTarget = false;
        return label;
    }

    private static TextMeshProUGUI AddText(RectTransform rt, string text, TMP_FontAsset font, float size, Color color, FontStyles style)
    {
        var label = rt.gameObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.font = font;
        label.fontSize = size;
        label.fontStyle = style;
        label.color = color;
        label.alignment = TextAlignmentOptions.Center;
        label.enableAutoSizing = false;
        label.raycastTarget = false;
        return label;
    }
}
