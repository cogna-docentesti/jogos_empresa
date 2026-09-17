using System;
using System.Collections.Generic;
using Game.Adapter.In.UI.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Adapter.In.UI
{
    /// <summary>
    /// Camada burra da tela "Abertura do Restaurante" (V2).
    /// Recebe dados prontos e escreve na UI. Nao carrega ScriptableObject,
    /// nao decide regra e nao conhece GameSessionState.
    /// </summary>
    public sealed class LocationScreenV2View : MonoBehaviour
    {
        [Header("Cabecalho")]
        [SerializeField] private TextMeshProUGUI stepLabel;
        [SerializeField] private TextMeshProUGUI titleLabel;
        [SerializeField] private TextMeshProUGUI subtitleLabel;
        [SerializeField] private Button menuButton;

        [Header("Mapa")]
        [SerializeField] private List<LocationMarkerV2> markers = new List<LocationMarkerV2>();

        [Header("Painel lateral - identidade")]
        [SerializeField] private Image panelIconBadge;
        [SerializeField] private Image panelIcon;
        [SerializeField] private TextMeshProUGUI panelTitle;
        [SerializeField] private TextMeshProUGUI panelSubtitle;
        [SerializeField] private TextMeshProUGUI panelDescription;

        [Header("Painel lateral - indicadores")]
        [SerializeField] private TextMeshProUGUI investmentValue;
        [SerializeField] private TextMeshProUGUI trafficValue;
        [SerializeField] private TextMeshProUGUI competitionValue;

        [Header("Painel lateral - destaque")]
        [SerializeField] private GameObject highlightBox;
        [SerializeField] private TextMeshProUGUI highlightText;
        [SerializeField] private Image highlightIcon;

        [Header("Estados")]
        [SerializeField] private GameObject emptyState;
        [SerializeField] private GameObject detailsState;
        [SerializeField] private Button continueButton;

        // =====================================================
        //  SKIN (redesign)
        // =====================================================
        // Tudo daqui para baixo e opcional: com `skin` vazia a tela continua
        // exatamente como era, pintada pelo builder procedural.

        [Header("Skin - opcional")]
        [SerializeField] private LocationSkinV2 skin;

        [Tooltip("Image de fundo do painel lateral. Troca de sprite conforme a zona.")]
        [SerializeField] private Image panelBackground;

        [Tooltip("Image de fundo da caixa de destaque. Troca junto com a zona.")]
        [SerializeField] private Image highlightBackground;

        [Tooltip("Fundos das tres linhas de indicador.")]
        [SerializeField] private List<Image> statRowBackgrounds = new List<Image>();

        [Tooltip("Pastilhas dos indicadores, na ordem: Investimento, Movimento, Concorrencia.")]
        [SerializeField] private List<Image> statTiles = new List<Image>();

        [Header("Skin - cabecalho")]
        [SerializeField] private Image topBarBackground;
        [SerializeField] private Image brandBadge;
        [SerializeField] private Image ornamentLeftImage;
        [SerializeField] private Image ornamentRightImage;

        /// <summary>Zona pintada agora. null = estado vazio.</summary>
        private string _zoneId;

        public IReadOnlyList<LocationMarkerV2> Markers => markers;

        public LocationSkinV2 Skin => skin;

        private void Awake()
        {
            ApplyStaticSkin();
        }

        // =====================================================
        //  CABECALHO
        // =====================================================

        public void SetStep(int current, int total, string screenName)
        {
            if (stepLabel != null)
                stepLabel.text = $"ETAPA {current} DE {total}  \u00B7  {screenName.ToUpperInvariant()}";
        }

        public void SetTitle(string title, string subtitle)
        {
            if (titleLabel != null)    titleLabel.text = title;
            if (subtitleLabel != null) subtitleLabel.text = subtitle;
        }

        public void BindMenu(Action action)
        {
            Bind(menuButton, action);
        }

        // =====================================================
        //  MAPA
        // =====================================================

        public void BindMarkers(Action<string> onSelected)
        {
            foreach (var marker in markers)
            {
                if (marker == null) continue;

                marker.OnClicked -= onSelected;
                marker.OnClicked += onSelected;
            }
        }

        public LocationMarkerV2 FindMarker(string id)
        {
            foreach (var marker in markers)
            {
                if (marker != null && marker.LocationId == id)
                    return marker;
            }

            return null;
        }

        /// <summary>Acende apenas o marcador escolhido e apaga os demais.</summary>
        public void SelectMarker(string id)
        {
            _zoneId = id;

            foreach (var marker in markers)
            {
                if (marker != null)
                    marker.SetSelected(marker.LocationId == id);
            }

            ApplyZoneSkin();
        }

        // =====================================================
        //  PAINEL LATERAL
        // =====================================================

        public void ShowEmptyState()
        {
            if (emptyState != null)   emptyState.SetActive(true);
            if (detailsState != null) detailsState.SetActive(false);

            _zoneId = null;
            ApplyZoneSkin();

            SetContinueEnabled(false);
        }

        public void ShowDetails(
            string zoneName,
            string zoneSubtitle,
            string description,
            string investment,
            string traffic,
            string competition,
            string highlight,
            Color accent,
            Sprite icon)
        {
            if (emptyState != null)   emptyState.SetActive(false);
            if (detailsState != null) detailsState.SetActive(true);

            // Com skin, a cor vem da skin (casa com os PNGs); sem skin, do LocationData.
            var zoneSkin = skin != null ? skin.For(_zoneId) : null;

            if (zoneSkin != null)
                accent = zoneSkin.accent;

            Color accentSoft = zoneSkin != null ? zoneSkin.accentSoft : accent;

            // Conteudo apenas. A cor e a caixa alta do titulo sao ajuste do TMP
            // na hierarquia - nao ha razao para o codigo decidir isso, o titulo
            // e sempre dourado.
            if (panelTitle != null)
                panelTitle.text = zoneName;

            if (panelSubtitle != null)    panelSubtitle.text = zoneSubtitle;
            if (panelDescription != null) panelDescription.text = description;

            if (panelIcon != null)
            {
                // O panel-icon da skin ja vem com circulo, degrade e icone
                // desenhados - por isso entra branco e o badge atras some.
                if (zoneSkin != null && zoneSkin.panelIcon != null)
                {
                    panelIcon.sprite = zoneSkin.panelIcon;
                    panelIcon.color  = Color.white;
                }
                else
                {
                    panelIcon.color = accent;

                    if (icon != null)
                        panelIcon.sprite = icon;
                }
            }

            if (panelIconBadge != null)
            {
                bool badgeBakedIntoSprite = zoneSkin != null && zoneSkin.panelIcon != null;

                panelIconBadge.enabled = !badgeBakedIntoSprite;

                if (!badgeBakedIntoSprite)
                    panelIconBadge.color = GamePalette.WithAlpha(accent, 0.18f);
            }

            SetIndicator(investmentValue,  investment,  ValueColor(investment,  highIsGood: false));
            SetIndicator(trafficValue,     traffic,     ValueColor(traffic,     highIsGood: true));
            SetIndicator(competitionValue, competition, ValueColor(competition, highIsGood: false));

            bool hasHighlight = !string.IsNullOrWhiteSpace(highlight);

            if (highlightBox != null)
                highlightBox.SetActive(hasHighlight);

            if (hasHighlight)
            {
                if (highlightText != null)
                {
                    highlightText.text  = highlight;
                    highlightText.color = accentSoft;
                }

                if (highlightIcon != null)
                {
                    highlightIcon.color = accentSoft;

                    if (skin != null && skin.starIcon != null)
                        highlightIcon.sprite = skin.starIcon;
                }
            }

            ApplyZoneSkin();

            SetContinueEnabled(true);
        }

        public void BindContinue(Action action)
        {
            Bind(continueButton, action);
        }

        public void SetContinueEnabled(bool value)
        {
            if (continueButton != null)
                continueButton.interactable = value;
        }

        // =====================================================
        //  APOIO
        // =====================================================

        /// <summary>
        /// Verde quando o valor favorece o jogador, dourado no meio, vermelho
        /// quando pesa contra. Concorrencia Alta e ruim; Movimento Alto e bom -
        /// por isso o parametro.
        ///
        /// Com skin as tres cores vem dela (as mesmas do PALETA.md do redesign);
        /// sem skin, cai no GamePalette de antes.
        /// </summary>
        private Color ValueColor(string value, bool highIsGood)
        {
            string v = (value ?? string.Empty).Trim().ToLowerInvariant();

            bool high = v.StartsWith("alt");
            bool low  = v.StartsWith("baix");

            Color good = skin != null ? skin.valueGood : GamePalette.Money;
            Color warn = skin != null ? skin.valueWarn : GamePalette.Gold;
            Color bad  = skin != null ? skin.valueBad  : GamePalette.Gold;

            if (high) return highIsGood ? good : bad;
            if (low)  return highIsGood ? bad  : good;

            return warn;
        }

        private static void SetIndicator(TextMeshProUGUI label, string value, Color color)
        {
            if (label == null) return;

            label.text  = value;
            label.color = color;
        }

        private static void Bind(Button button, Action action)
        {
            if (button == null) return;

            button.onClick.RemoveAllListeners();

            if (action != null)
                button.onClick.AddListener(() => action());
        }

        /// <summary>Usado pelo builder de Editor para registrar os marcadores.</summary>
        public void EditorSetMarkers(List<LocationMarkerV2> value)
        {
            markers = value;
        }

        // =====================================================
        //  SKIN
        // =====================================================

        /// <summary>
        /// Sprites que nao dependem da zona: cabecalho, linhas de indicador,
        /// pastilhas e os dois botoes. Roda uma vez, no Awake.
        /// </summary>
        private void ApplyStaticSkin()
        {
            if (skin == null) return;

            SetSprite(topBarBackground,    skin.topbarBackground);
            SetSprite(brandBadge,          skin.brandBadge);
            SetSprite(ornamentLeftImage,   skin.ornamentLeft);
            SetSprite(ornamentRightImage,  skin.ornamentRight);

            foreach (var row in statRowBackgrounds)
                SetSprite(row, skin.statRowBackground);

            // Ordem fixa: Investimento, Movimento, Concorrencia.
            SetSpriteAt(statTiles, 0, skin.statTileInvestment);
            SetSpriteAt(statTiles, 1, skin.statTileTraffic);
            SetSpriteAt(statTiles, 2, skin.statTileCompetition);

            ApplyButtonSkin(continueButton, skin.continueNormal, skin.continueHover, skin.continuePressed);
            ApplyButtonSkin(menuButton,     skin.menuNormal,     skin.menuHover,     skin.menuHover);
        }

        /// <summary>
        /// Sprites que TROCAM com a zona: fundo do painel e caixa de destaque.
        /// Chamado por SelectMarker, ShowDetails e ShowEmptyState.
        /// </summary>
        private void ApplyZoneSkin()
        {
            if (skin == null) return;

            var zoneSkin = skin.For(_zoneId);

            SetSprite(panelBackground,
                zoneSkin != null && zoneSkin.panelBackground != null
                    ? zoneSkin.panelBackground
                    : skin.panelBackgroundNeutral);

            if (zoneSkin != null)
                SetSprite(highlightBackground, zoneSkin.highlightBox);
        }

        /// <summary>
        /// Liga a troca por sprite no Button. Passa a transicao para SpriteSwap
        /// para os PNGs de hover/pressed valerem - com ColorTint o Unity so
        /// escureceria o sprite normal.
        /// </summary>
        private static void ApplyButtonSkin(Button button, Sprite normal, Sprite hover, Sprite pressed)
        {
            if (button == null || normal == null) return;

            var image = button.targetGraphic as Image;

            if (image == null)
                image = button.GetComponent<Image>();

            if (image == null) return;

            image.sprite         = normal;
            image.color          = Color.white;
            button.targetGraphic = image;
            button.transition    = Selectable.Transition.SpriteSwap;

            var state = button.spriteState;
            state.highlightedSprite = hover   != null ? hover   : normal;
            state.pressedSprite     = pressed != null ? pressed : normal;
            state.selectedSprite    = normal;
            button.spriteState      = state;
        }

        private static void SetSprite(Image image, Sprite sprite)
        {
            if (image == null || sprite == null) return;

            image.sprite  = sprite;
            image.color   = Color.white;
            image.enabled = true;
        }

        private static void SetSpriteAt(List<Image> list, int index, Sprite sprite)
        {
            if (list == null || index < 0 || index >= list.Count) return;

            SetSprite(list[index], sprite);
        }

        /// <summary>Usado pelo builder de Editor para plugar a skin e as Images novas.</summary>
        public void EditorSetSkin(
            LocationSkinV2 value,
            Image panelBg,
            Image highlightBg,
            List<Image> rows,
            List<Image> tiles)
        {
            skin                = value;
            panelBackground     = panelBg;
            highlightBackground = highlightBg;
            statRowBackgrounds  = rows  ?? new List<Image>();
            statTiles           = tiles ?? new List<Image>();
        }

        /// <summary>Usado pelo builder de Editor para plugar as Images do cabecalho.</summary>
        public void EditorSetHeaderSkin(Image topBar, Image crest, Image ornLeft, Image ornRight)
        {
            topBarBackground   = topBar;
            brandBadge         = crest;
            ornamentLeftImage  = ornLeft;
            ornamentRightImage = ornRight;
        }
    }
}
