#if UNITY_EDITOR
using System.Collections.Generic;
using Game.Adapter.In.UI.Theme;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Game.EditorTools
{
    /// <summary>
    /// Helpers de construcao de UI para os builders de Editor.
    ///
    /// Regra de ouro: TUDO aqui e idempotente. Cada metodo procura o objeto
    /// pelo nome antes de criar. Rodar o builder duas vezes nao duplica nada,
    /// e objetos que voce ajustou na mao continuam existindo - o builder so
    /// reescreve as propriedades que ele mesmo gerencia.
    /// </summary>
    public static class UiFactory
    {
        // =====================================================
        //  CAMINHOS DE ASSET
        // =====================================================

        public const string PathButtonSprite = "Assets/Art/Sprites/UI/botao.png";
        public const string PathMapMenu      = "Assets/Art/Sprites/MenuScene/mapa-menu.png";
        public const string PathIconsFolder  = "Assets/Layer Lab/2D Icons-PictoIconPack01/Icons/PictoIcon_128/";

        /// <summary>Borda 9-slice do botao.png medida na arte (margem 8px + raio ~36px).</summary>
        public static readonly Vector4 ButtonSpriteBorder = new Vector4(48, 48, 48, 48);

        private static readonly Dictionary<string, Sprite> SpriteCache = new Dictionary<string, Sprite>();

        // =====================================================
        //  MODO NAO DESTRUTIVO
        // =====================================================

        /// <summary>
        /// Quando FALSO (padrao), o builder so escreve cor, fonte e layout em
        /// objetos que ele acabou de criar. Tudo que ja existia na cena fica
        /// exatamente como esta - inclusive o que voce ajustou na mao.
        ///
        /// Quando VERDADEIRO, o estilo padrao e reaplicado por cima de tudo.
        ///
        /// Referencias (SetPrivate), hierarquia e areas de clique sao sempre
        /// escritas, nos dois modos: sao correcao, nao estilo.
        /// </summary>
        public static bool OverwriteExisting { get; private set; }

        private static readonly HashSet<int> CreatedThisRun = new HashSet<int>();

        public static void BeginRun(bool overwriteExisting)
        {
            OverwriteExisting = overwriteExisting;
            CreatedThisRun.Clear();
        }

        /// <summary>Pode escrever estilo/layout neste objeto?</summary>
        public static bool CanWrite(GameObject go)
        {
            return OverwriteExisting || (go != null && CreatedThisRun.Contains(go.GetInstanceID()));
        }

        private static void MarkCreated(GameObject go)
        {
            if (go != null)
                CreatedThisRun.Add(go.GetInstanceID());
        }

        // =====================================================
        //  HIERARQUIA
        // =====================================================

        /// <summary>Acha um filho direto pelo nome, ou cria um novo com RectTransform.</summary>
        public static RectTransform EnsureChild(Transform parent, string name)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);

                if (child.name == name)
                    return AsRect(child.gameObject);
            }

            var go = new GameObject(name, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, $"Criar {name}");
            go.transform.SetParent(parent, false);
            MarkCreated(go);

            return (RectTransform)go.transform;
        }

        // =====================================================
        //  AREA DE CLIQUE
        // =====================================================

        /// <summary>
        /// Garante um alvo de raycast invisivel cobrindo o objeto inteiro.
        ///
        /// Existe por um motivo concreto: um Button so recebe clique se houver
        /// algum Graphic com raycastTarget ligado nele ou em algum filho. Se o
        /// visual do card for trocado - por exemplo, apagando o Image de fundo
        /// para pintar de outro jeito - o botao simplesmente para de responder,
        /// sem erro nenhum no Console.
        ///
        /// Com um HitArea dedicado, o clique nunca depende da decisao visual.
        /// </summary>
        public static Image EnsureHitArea(RectTransform target)
        {
            var hit = EnsureChild(target, "HitArea");
            hit.SetAsFirstSibling();

            hit.anchorMin = Vector2.zero;
            hit.anchorMax = Vector2.one;
            hit.pivot     = new Vector2(0.5f, 0.5f);
            hit.offsetMin = Vector2.zero;
            hit.offsetMax = Vector2.zero;

            var image = Ensure<Image>(hit.gameObject);

            Undo.RecordObject(image, "Configurar HitArea");
            image.sprite        = null;
            image.color         = new Color(1f, 1f, 1f, 0f); // invisivel, mas clicavel
            image.raycastTarget = true;

            return image;
        }

        /// <summary>
        /// Verifica se todo Button dentro da raiz tem pelo menos um alvo de
        /// raycast. Roda no fim do build e denuncia botoes mortos no Console -
        /// esse tipo de falha nao gera erro sozinho.
        /// </summary>
        public static int WarnAboutDeadButtons(Transform root)
        {
            int dead = 0;

            foreach (var button in root.GetComponentsInChildren<Button>(true))
            {
                bool hasTarget = false;

                foreach (var graphic in button.GetComponentsInChildren<Graphic>(true))
                {
                    if (graphic.raycastTarget)
                    {
                        hasTarget = true;
                        break;
                    }
                }

                if (hasTarget)
                    continue;

                dead++;
                Debug.LogWarning($"[UiFactory] O botao '{GetPath(button.transform)}' nao tem nenhum Graphic "
                               + "com raycastTarget ligado. Ele nunca vai receber clique.", button.gameObject);
            }

            return dead;
        }

        private static string GetPath(Transform t)
        {
            string path = t.name;

            while (t.parent != null)
            {
                t = t.parent;
                path = t.name + "/" + path;
            }

            return path;
        }

        /// <summary>Acha um filho direto por qualquer um dos nomes candidatos e renomeia para o nome canonico.</summary>
        public static RectTransform EnsureChildRenamed(Transform parent, string canonicalName, params string[] legacyNames)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);

                if (child.name == canonicalName)
                    return AsRect(child.gameObject);
            }

            foreach (var legacy in legacyNames)
            {
                for (int i = 0; i < parent.childCount; i++)
                {
                    var child = parent.GetChild(i);

                    if (child.name != legacy)
                        continue;

                    Undo.RecordObject(child.gameObject, "Renomear");
                    child.name = canonicalName;
                    return AsRect(child.gameObject);
                }
            }

            return EnsureChild(parent, canonicalName);
        }

        /// <summary>
        /// Acha um filho cujo nome COMECA com o prefixo e o renomeia para o nome canonico.
        /// Existe porque a cena tem objetos com nome digitado errado (ex: "CashPill" com
        /// um i acentuado). Casar por prefixo evita colar o caractere estranho no codigo.
        /// </summary>
        public static RectTransform EnsureChildByPrefix(Transform parent, string canonicalName, string prefix)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);

                if (child.name == canonicalName)
                    return AsRect(child.gameObject);
            }

            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);

                if (child.name.EndsWith("_ANTIGO"))
                    continue;

                if (!child.name.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase))
                    continue;

                Undo.RecordObject(child.gameObject, "Renomear");
                child.name = canonicalName;
                return AsRect(child.gameObject);
            }

            return EnsureChild(parent, canonicalName);
        }

        /// <summary>
        /// Aposenta sempre, independente dos componentes. Use quando a estrutura
        /// nova e incompativel com a antiga de forma irreconciliavel.
        /// </summary>
        public static void RetireAlways(Transform parent, string legacyName)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i);

                if (child.name != legacyName)
                    continue;

                Undo.RecordObject(child.gameObject, "Aposentar objeto legado");
                child.name = legacyName + "_ANTIGO";
                child.gameObject.SetActive(false);

                Debug.Log($"[UiFactory] '{legacyName}' antigo foi renomeado para '{child.name}' e desativado. "
                        + "Nada foi apagado.");
            }
        }

        /// <summary>Garante que o objeto tem RectTransform (converte Transform simples se preciso).</summary>
        public static RectTransform AsRect(GameObject go)
        {
            var rect = go.GetComponent<RectTransform>();

            if (rect != null)
                return rect;

            return Undo.AddComponent<RectTransform>(go);
        }

        public static T Ensure<T>(GameObject go) where T : Component
        {
            var existing = go.GetComponent<T>();

            if (existing != null)
                return existing;

            // Unity permite UM unico Graphic por GameObject. Tentar somar um
            // Image onde ja existe um TextMeshProUGUI (ou vice-versa) lanca
            // excecao e aborta o builder no meio. Falhamos aqui com mensagem
            // clara em vez de deixar a excecao crua matar a execucao.
            if (typeof(Graphic).IsAssignableFrom(typeof(T)))
            {
                var current = go.GetComponent<Graphic>();

                if (current != null)
                {
                    throw new System.InvalidOperationException(
                        $"'{go.name}' ja tem o componente grafico '{current.GetType().Name}'. "
                        + $"Nao da para somar '{typeof(T).Name}' no mesmo objeto. "
                        + "Use UiFactory.RetireLegacy para aposentar o objeto antigo antes de recriar.");
                }
            }

            return Undo.AddComponent<T>(go);
        }

        /// <summary>
        /// Aposenta um objeto antigo: renomeia com sufixo _ANTIGO e desativa.
        /// Nada e apagado - se voce quiser algo de volta, esta la na Hierarchy,
        /// so reativar. Usado quando o objeto legado tem componentes
        /// incompativeis com a estrutura nova.
        /// </summary>
        public static void RetireLegacy(Transform parent, string canonicalName, string prefix = null)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i);

                if (child.name.EndsWith("_ANTIGO"))
                    continue;

                bool matches = child.name == canonicalName
                    || (!string.IsNullOrEmpty(prefix)
                        && child.name.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase));

                if (!matches)
                    continue;

                // So aposenta se realmente for incompativel: um container
                // vazio ou com Image pode ser reaproveitado sem problema.
                var graphic = child.GetComponent<Graphic>();

                if (graphic != null && !(graphic is Image))
                {
                    Undo.RecordObject(child.gameObject, "Aposentar objeto legado");
                    child.name = canonicalName + "_ANTIGO";
                    child.gameObject.SetActive(false);

                    Debug.Log($"[UiFactory] '{canonicalName}' antigo era um {graphic.GetType().Name}, "
                            + $"nao dava para reusar como caixa. Renomeado para '{child.name}' e desativado.");
                }
            }
        }

        // =====================================================
        //  ANCORAGEM
        // =====================================================

        /// <summary>Estica ocupando o pai inteiro, com margens em pixels.</summary>
        public static RectTransform Stretch(RectTransform rt, float left = 0, float top = 0, float right = 0, float bottom = 0)
        {
            if (!CanWrite(rt.gameObject)) return rt;

            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        /// <summary>Fixa no topo, esticando na horizontal.</summary>
        public static RectTransform TopBand(RectTransform rt, float height, float left = 0, float right = 0, float offsetY = 0)
        {
            if (!CanWrite(rt.gameObject)) return rt;

            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot     = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(left, -height + offsetY);
            rt.offsetMax = new Vector2(-right, offsetY);
            return rt;
        }

        /// <summary>Fixa na base, esticando na horizontal.</summary>
        public static RectTransform BottomBand(RectTransform rt, float height, float left = 0, float right = 0, float offsetY = 0)
        {
            if (!CanWrite(rt.gameObject)) return rt;

            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(1, 0);
            rt.pivot     = new Vector2(0.5f, 0f);
            rt.offsetMin = new Vector2(left, offsetY);
            rt.offsetMax = new Vector2(-right, height + offsetY);
            return rt;
        }

        /// <summary>
        /// Ancora em coordenada normalizada do pai (0..1 em x e y, y=0 embaixo).
        /// E assim que os nodes ficam exatamente sobre os predios do mapa em
        /// qualquer resolucao: a posicao acompanha o stretch da imagem.
        /// </summary>
        public static RectTransform AnchorNormalized(RectTransform rt, float u, float v, float width, float height,
                                                     float offsetX = 0, float offsetY = 0)
        {
            if (!CanWrite(rt.gameObject)) return rt;

            rt.anchorMin        = new Vector2(u, v);
            rt.anchorMax        = new Vector2(u, v);
            rt.pivot            = new Vector2(0.5f, 0.5f);
            rt.sizeDelta        = new Vector2(width, height);
            rt.anchoredPosition = new Vector2(offsetX, offsetY);
            rt.localScale       = Vector3.one;
            rt.localRotation    = Quaternion.identity;
            return rt;
        }

        /// <summary>Ancora relativa a um canto do pai, com tamanho fixo.</summary>
        public static RectTransform AnchorCorner(RectTransform rt, Vector2 anchor, Vector2 pivot,
                                                 float width, float height, float x, float y)
        {
            if (!CanWrite(rt.gameObject)) return rt;

            rt.anchorMin        = anchor;
            rt.anchorMax        = anchor;
            rt.pivot            = pivot;
            rt.sizeDelta        = new Vector2(width, height);
            rt.anchoredPosition = new Vector2(x, y);
            rt.localScale       = Vector3.one;
            return rt;
        }

        // =====================================================
        //  GRAFICOS
        // =====================================================

        /// <summary>Painel/card com cantos arredondados via 9-slice do botao.png.</summary>
        public static Image Panel(RectTransform rt, Color color, float ppuMultiplier = 2.2f, bool raycast = true)
        {
            var image = Ensure<Image>(rt.gameObject);

            if (!CanWrite(rt.gameObject)) return image;

            Undo.RecordObject(image, "Configurar Panel");
            image.sprite                 = ButtonSprite();
            image.type                   = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = ppuMultiplier;
            image.color                  = color;
            image.raycastTarget          = raycast;
            image.preserveAspect         = false;

            return image;
        }

        /// <summary>Imagem simples (icone, foto, mapa).</summary>
        public static Image Picture(RectTransform rt, Sprite sprite, Color color, bool preserveAspect = true, bool raycast = false)
        {
            var image = Ensure<Image>(rt.gameObject);

            if (!CanWrite(rt.gameObject)) return image;

            Undo.RecordObject(image, "Configurar Picture");
            image.sprite         = sprite;
            image.type           = Image.Type.Simple;
            image.color          = color;
            image.preserveAspect = preserveAspect;
            image.raycastTarget  = raycast;

            return image;
        }

        /// <summary>Retangulo solido sem sprite (veu, divisoria, trilho).</summary>
        public static Image Solid(RectTransform rt, Color color, bool raycast = false)
        {
            var image = Ensure<Image>(rt.gameObject);

            if (!CanWrite(rt.gameObject)) return image;

            Undo.RecordObject(image, "Configurar Solid");
            image.sprite        = null;
            image.type          = Image.Type.Simple;
            image.color         = color;
            image.raycastTarget = raycast;

            return image;
        }

        public static TextMeshProUGUI Text(RectTransform rt, string content, float size, FontStyles style,
                                           Color color, TextAlignmentOptions alignment = TextAlignmentOptions.Left,
                                           bool wrap = false)
        {
            var label = Ensure<TextMeshProUGUI>(rt.gameObject);

            Undo.RecordObject(label, "Configurar Text");

            // O conteudo e sempre atualizado - e dado, nao estilo.
            label.text = content;

            if (!CanWrite(rt.gameObject)) return label;

            label.fontSize             = size;
            label.fontStyle            = style;
            label.color                = color;
            label.alignment            = alignment;
            label.textWrappingMode      = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            label.overflowMode         = TextOverflowModes.Overflow;
            label.raycastTarget        = false;
            label.characterSpacing     = 0f;

            return label;
        }

        // =====================================================
        //  BOTOES
        // =====================================================

        /// <summary>
        /// Botao com ColorTint. Os parametros sao o brilho FINAL desejado:
        /// 1.0 = cor original, 1.15 = 15% mais claro, 0.90 = 10% mais escuro.
        ///
        /// Em tema escuro o hover precisa CLAREAR, e o ColorTint so multiplica -
        /// nunca passaria de 1. A saida e usar colorMultiplier como teto e
        /// guardar as cores ja divididas por ele.
        /// </summary>
        public static Button MakeButton(RectTransform rt, float highlightedTint = 1.15f, float pressedTint = 0.90f)
        {
            const float ceiling = 1.5f;

            var button = Ensure<Button>(rt.gameObject);
            var image  = rt.GetComponent<Image>();

            if (!CanWrite(rt.gameObject)) return button;

            Undo.RecordObject(button, "Configurar Button");

            button.targetGraphic = image;
            button.transition    = Selectable.Transition.ColorTint;

            var colors = button.colors;
            colors.colorMultiplier  = ceiling;
            colors.normalColor      = Gray(1f / ceiling);
            colors.highlightedColor = Gray(Mathf.Clamp(highlightedTint, 0f, ceiling) / ceiling);
            colors.pressedColor     = Gray(Mathf.Clamp(pressedTint, 0f, ceiling) / ceiling);
            colors.selectedColor    = Gray(1f / ceiling);
            colors.disabledColor    = new Color(1f / ceiling, 1f / ceiling, 1f / ceiling, 0.4f);
            colors.fadeDuration     = 0.12f;
            button.colors           = colors;

            return button;
        }

        private static Color Gray(float v)
        {
            return new Color(v, v, v, 1f);
        }

        // =====================================================
        //  SPRITES
        // =====================================================

        public static Sprite ButtonSprite()
        {
            EnsureButtonSpriteBorder();
            return Load(PathButtonSprite);
        }

        public static Sprite MapSprite()
        {
            return Load(PathMapMenu);
        }

        /// <summary>Carrega um icone do PictoIconPack01 pelo nome curto (ex: "Bank").</summary>
        public static Sprite Icon(string shortName)
        {
            return Load($"{PathIconsFolder}Icon_PictoIcon_{shortName}.Png");
        }

        public static Sprite Load(string assetPath)
        {
            if (SpriteCache.TryGetValue(assetPath, out var cached) && cached != null)
                return cached;

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);

            if (sprite == null)
                Debug.LogWarning($"[UiFactory] Sprite nao encontrado: {assetPath}");
            else
                SpriteCache[assetPath] = sprite;

            return sprite;
        }

        /// <summary>
        /// botao.png vem sem borda 9-slice. Sem borda, o Image.Type.Sliced
        /// nao funciona e os cantos esticam. Isto configura a borda uma unica
        /// vez (e nao afeta quem ja usa a sprite como Simple).
        /// </summary>
        public static void EnsureButtonSpriteBorder()
        {
            var importer = AssetImporter.GetAtPath(PathButtonSprite) as TextureImporter;

            if (importer == null)
                return;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);

            bool changed = false;

            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                changed = true;
            }

            if (settings.spriteBorder != ButtonSpriteBorder)
            {
                settings.spriteBorder = ButtonSpriteBorder;
                importer.SetTextureSettings(settings);
                changed = true;
            }

            if (!changed)
                return;

            importer.SaveAndReimport();
            SpriteCache.Remove(PathButtonSprite);

            Debug.Log("[UiFactory] botao.png configurado com borda 9-slice 48px.");
        }

        // =====================================================
        //  CAMPOS PRIVADOS SERIALIZADOS
        // =====================================================

        /// <summary>
        /// Preenche um campo [SerializeField] private de um MonoBehaviour.
        /// E como arrastar a referencia no Inspector, so que sem errar o alvo.
        /// </summary>
        public static bool SetPrivate(Object target, string fieldName, Object value)
        {
            if (target == null)
                return false;

            var so   = new SerializedObject(target);
            var prop = so.FindProperty(fieldName);

            if (prop == null)
            {
                Debug.LogWarning($"[UiFactory] Campo '{fieldName}' nao existe em {target.GetType().Name}.");
                return false;
            }

            prop.objectReferenceValue = value;
            so.ApplyModifiedProperties();
            return true;
        }

        public static bool SetPrivateBool(Object target, string fieldName, bool value)
        {
            if (target == null)
                return false;

            var so   = new SerializedObject(target);
            var prop = so.FindProperty(fieldName);

            if (prop == null)
                return false;

            prop.boolValue = value;
            so.ApplyModifiedProperties();
            return true;
        }

        public static bool SetPrivateString(Object target, string fieldName, string value)
        {
            if (target == null)
                return false;

            var so   = new SerializedObject(target);
            var prop = so.FindProperty(fieldName);

            if (prop == null)
                return false;

            prop.stringValue = value;
            so.ApplyModifiedProperties();
            return true;
        }

        // =====================================================
        //  ATALHOS DE COMPOSICAO
        // =====================================================

        /// <summary>Linha "rotulo a esquerda, valor a direita" dentro de um card.</summary>
        public static RectTransform SummaryRow(Transform parent, string name, string caption, string value,
                                               float y, float rowHeight, float sidePadding,
                                               out TextMeshProUGUI valueLabel)
        {
            var row = EnsureChild(parent, name);
            TopBand(row, rowHeight, sidePadding, sidePadding, -y);

            var captionRt = EnsureChild(row, "Caption");
            captionRt.anchorMin = new Vector2(0f, 0f);
            captionRt.anchorMax = new Vector2(0.52f, 1f);
            captionRt.pivot     = new Vector2(0.5f, 0.5f);
            captionRt.offsetMin = Vector2.zero;
            captionRt.offsetMax = Vector2.zero;
            Text(captionRt, caption, 22f, FontStyles.Normal, GamePalette.InkBody, TextAlignmentOptions.MidlineLeft);

            var valueRt = EnsureChild(row, "Value");
            valueRt.anchorMin = new Vector2(0.48f, 0f);
            valueRt.anchorMax = new Vector2(1f, 1f);
            valueRt.pivot     = new Vector2(0.5f, 0.5f);
            valueRt.offsetMin = Vector2.zero;
            valueRt.offsetMax = Vector2.zero;
            valueLabel = Text(valueRt, value, 22f, FontStyles.Bold, GamePalette.Ink, TextAlignmentOptions.MidlineRight);

            var divider = EnsureChild(row, "Divider");
            BottomBand(divider, 1f, 0, 0, 0);
            Solid(divider, GamePalette.Hairline);

            return row;
        }
    }
}
#endif
