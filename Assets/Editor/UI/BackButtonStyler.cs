using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Deixa os botoes Voltar da D1, D2 e D3 visiveis e iguais entre si.
///
/// Problema que isto resolve:
///  - D2 e D3: o Voltar estava preso na borda esquerda do rodape. O rodape tem
///    ~3000 de largura (bem mais que a tela), entao a borda esquerda dele, e o
///    botao junto, ficavam FORA da area visivel.
///  - D1: o Voltar estava na tela, mas pequeno e roxo escuro sobre roxo.
///
/// O que faz em cada botao:
///  - Ancora no centro e coloca o Voltar espelhado ao Continuar (mesma
///    distancia do centro da tela, do lado esquerdo, mesma altura). Como o
///    Canvas escala pela altura, o espelho continua dentro da tela em
///    qualquer proporcao em que o Continuar aparece.
///  - Fundo: btn-menu-base.png (o botao MENU do hub sem o texto: fundo escuro
///    translucido e borda dourada). Assim o Voltar fica com cara de botao
///    secundario e o Continuar amarelo continua sendo o foco.
///  - Texto "VOLTAR" em Inter Tight negrito, dourado claro, e uma seta.
///
/// Como usar: abra a GameScene e rode
/// Tools > Jogos de Empresa > Ajustar botoes Voltar (D1, D2, D3).
/// Pode rodar de novo sem problema. Ctrl+Z desfaz.
/// </summary>
public static class BackButtonStyler
{
    private const string BaseSpritePath = "Assets/Art/Sprites/REDESIGN-UI/btn-menu-base.png";
    private const string ChevronPath = "Assets/Art/Sprites/REDESIGN-UI/chevron-right-branco.png";
    private const string FontGuid = "89cea402e3db5bf4d9bc9d4f698b7692"; // InterTight-Variable (TMP)

    private static readonly Vector2 Size = new Vector2(260f, 68f);
    private static readonly Color LabelColor = new Color32(247, 220, 154, 255); // mesmo dourado do MENU

    private static readonly (string panel, string bar)[] Targets =
    {
        ("Panel_Location", "BottomBar"),
        ("Panel_Restaurant", "Footer"),
        ("Panel_MenuPricing", "Footer"),
    };

    [MenuItem("Tools/Jogos de Empresa/Ajustar botoes Voltar (D1, D2, D3)")]
    public static void Apply()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogError("[BackButtonStyler] Saia do Play Mode antes de rodar.");
            return;
        }

        var canvas = GameObject.Find("Canvas");
        if (canvas == null)
        {
            Debug.LogError("[BackButtonStyler] Abra a GameScene antes de rodar (Canvas nao encontrado).");
            return;
        }

        var baseSprite = AssetDatabase.LoadAssetAtPath<Sprite>(BaseSpritePath);
        var chevron = AssetDatabase.LoadAssetAtPath<Sprite>(ChevronPath);
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(FontGuid));

        if (baseSprite == null || chevron == null || font == null)
        {
            Debug.LogError($"[BackButtonStyler] Asset nao encontrado. sprite={baseSprite != null} seta={chevron != null} fonte={font != null}");
            return;
        }

        var canvasRt = (RectTransform)canvas.transform;
        int done = 0;

        foreach (var (panel, bar) in Targets)
        {
            var barTransform = canvas.transform.Find($"{panel}/{bar}");
            var back = barTransform != null ? barTransform.Find("BackButton") as RectTransform : null;
            var confirm = barTransform != null ? barTransform.Find("ConfirmButton") as RectTransform : null;

            if (back == null || confirm == null)
            {
                Debug.LogError($"[BackButtonStyler] {panel}/{bar}: BackButton ou ConfirmButton nao encontrado.");
                continue;
            }

            Undo.RegisterFullObjectHierarchyUndo(back.gameObject, "Ajustar botoes Voltar");

            back.gameObject.SetActive(true);

            // Posicao: espelho do Continuar em relacao ao centro do Canvas.
            Vector3 confirmInCanvas = canvasRt.InverseTransformPoint(confirm.TransformPoint(confirm.rect.center));
            back.anchorMin = back.anchorMax = new Vector2(0.5f, 0.5f);
            back.pivot = new Vector2(0.5f, 0.5f);
            back.sizeDelta = Size;
            back.localRotation = Quaternion.identity;
            back.localScale = Vector3.one;
            back.position = canvasRt.TransformPoint(new Vector3(-confirmInCanvas.x, confirmInCanvas.y, confirmInCanvas.z));

            // Fundo
            var image = back.GetComponent<Image>();
            image.sprite = baseSprite;
            image.type = Image.Type.Sliced;
            image.color = Color.white;
            image.raycastTarget = true;

            // Estados do botao (o Color Tint multiplica a cor do fundo)
            var button = back.GetComponent<Button>();
            button.transition = Selectable.Transition.ColorTint;
            button.targetGraphic = image;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.88f, 0.88f, 0.88f, 1f);
            colors.selectedColor = Color.white;
            colors.pressedColor = new Color(0.72f, 0.72f, 0.72f, 1f);
            colors.disabledColor = new Color(0.78f, 0.78f, 0.78f, 0.5f);
            colors.colorMultiplier = 1f;
            button.colors = colors;

            // Texto
            var label = back.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label == null)
            {
                var go = new GameObject("Text (TMP)", typeof(RectTransform));
                go.transform.SetParent(back, false);
                label = go.AddComponent<TextMeshProUGUI>();
            }

            label.gameObject.SetActive(true);
            var labelRt = label.rectTransform;
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = new Vector2(46f, 0f);   // espaco para a seta
            labelRt.offsetMax = new Vector2(-18f, 0f);
            label.text = "VOLTAR";
            label.font = font;
            label.fontStyle = FontStyles.Bold;
            label.enableAutoSizing = false;
            label.fontSize = 30f;
            label.characterSpacing = 2f;
            label.color = LabelColor;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;

            // Seta (chevron-right girado 180 graus)
            var iconTransform = back.Find("Icon") as RectTransform;
            if (iconTransform == null)
            {
                var go = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                iconTransform = (RectTransform)go.transform;
                iconTransform.SetParent(back, false);
            }

            iconTransform.anchorMin = iconTransform.anchorMax = new Vector2(0f, 0.5f);
            iconTransform.pivot = new Vector2(0.5f, 0.5f);
            iconTransform.anchoredPosition = new Vector2(38f, 0f);
            iconTransform.sizeDelta = new Vector2(26f, 26f);
            iconTransform.localRotation = Quaternion.Euler(0f, 0f, 180f);
            var icon = iconTransform.GetComponent<Image>();
            icon.sprite = chevron;
            icon.color = LabelColor;
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            EditorUtility.SetDirty(back.gameObject);
            done++;
            Debug.Log($"[BackButtonStyler] {panel}: Voltar em x={back.anchoredPosition.x:0}, y={back.anchoredPosition.y:0} (Continuar em x={confirm.anchoredPosition.x:0}).");
        }

        EditorSceneManager.MarkSceneDirty(canvas.scene);
        EditorSceneManager.SaveScene(canvas.scene);
        Debug.Log($"[BackButtonStyler] {done} botao(oes) ajustado(s) e cena salva.");
    }
}
