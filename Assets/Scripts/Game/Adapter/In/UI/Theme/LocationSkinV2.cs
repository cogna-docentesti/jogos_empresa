using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Adapter.In.UI.Theme
{
    /// <summary>
    /// Skin da tela "Abertura do Restaurante" (V2).
    ///
    /// Junta num so lugar os PNGs exportados do redesign (Art/Sprites/REDESIGN-UI)
    /// e as cores que acompanham cada zona. A View e o marcador leem daqui; se o
    /// campo estiver vazio, cada um mantem o visual procedural que ja tinha.
    ///
    /// Por que a skin existe em vez de arrastar sprite a sprite no Inspector:
    /// os elementos coloridos (fundo do painel, caixa de destaque, pino, brilho)
    /// TROCAM conforme a zona escolhida. Sem uma tabela id -> sprites, essa troca
    /// viraria um switch espalhado pela UI.
    ///
    /// Para criar/atualizar o asset: menu Tools > Redesign UI > Criar skin da tela
    /// de localizacao (ele acha os PNGs pelo nome do arquivo).
    /// </summary>
    [CreateAssetMenu(
        fileName = "LocationSkinV2",
        menuName = "Game Data/Location Skin V2")]
    public sealed class LocationSkinV2 : ScriptableObject
    {
        /// <summary>Conjunto de arte de UMA zona. `id` casa com LocationData.id.</summary>
        [Serializable]
        public sealed class ZoneSkin
        {
            [Tooltip("Mesmo id do LocationData: bank, university, store, condominium, marketing.")]
            public string id;

            [Header("Cores")]
            [Tooltip("Cor base da zona. Substitui o colorHex do LocationData quando a skin esta ativa.")]
            public Color accent = Color.white;

            [Tooltip("Versao clara: anel do pino escolhido e texto da caixa de destaque.")]
            public Color accentSoft = Color.white;

            [Header("Painel lateral")]
            public Sprite panelBackground;   // panel-bg-<cor>.png
            public Sprite highlightBox;      // highlight-<cor>.png
            public Sprite panelIcon;         // panel-icon-<zona>.png (circulo + icone)

            [Header("Marcador no mapa")]
            public Sprite pinIdle;           // pin-<zona>-idle.png
            public Sprite pinActive;         // pin-<zona>-active.png
            public Sprite glowIdle;          // glow-<zona>-idle.png
            public Sprite glowActive;        // glow-<zona>-active.png

            [Header("Icone solto")]
            [Tooltip("Icone monocromatico da zona, para onde a cor vem do Image.color.")]
            public Sprite icon;              // <icone>-branco.png
        }

        [Header("Zonas")]
        [SerializeField] private List<ZoneSkin> zones = new List<ZoneSkin>();

        [Header("Painel lateral - partes fixas")]
        public Sprite panelBackgroundNeutral;   // panel-bg.png (estado vazio)
        public Sprite statRowBackground;        // stat-row-bg.png
        public Sprite statTileInvestment;       // stat-tile-blue-icon.png
        public Sprite statTileTraffic;          // stat-tile-violet-icon.png
        public Sprite statTileCompetition;      // stat-tile-green-icon.png
        public Sprite starIcon;                 // star-<cor>.png / branco

        [Header("Botoes")]
        public Sprite continueNormal;           // btn-continuar.png
        public Sprite continueHover;            // btn-continuar-hover.png
        public Sprite continuePressed;          // btn-continuar-pressed.png
        public Sprite menuNormal;               // btn-menu.png
        public Sprite menuHover;                // btn-menu-hover.png
        public Sprite menuDropdown;             // menu-dropdown-bg.png
        public Sprite menuItemHover;            // menu-item-hover.png

        [Header("Cabecalho e mapa")]
        public Sprite brandBadge;               // brand-badge.png
        public Sprite ornamentLeft;             // ornament-left.png
        public Sprite ornamentRight;            // ornament-right.png
        public Sprite topbarBackground;         // topbar-bg.png
        public Sprite mapBase;                  // map-base.png
        public Sprite mapVignette;              // map-vignette.png
        public Sprite labelPill;                // label-pill-bg.png
        public Sprite toastBackground;          // toast-bg.png

        [Header("Cores de texto do redesign")]
        [Tooltip("Titulo da zona no painel. No redesign ele e dourado, nao a cor da zona.")]
        public Color panelTitleColor = new Color(0.910f, 0.714f, 0.298f);   // #E8B64C

        public Color valueGood = new Color(0.290f, 0.871f, 0.502f);         // #4ADE80
        public Color valueWarn = new Color(0.941f, 0.749f, 0.310f);         // #F0BF4F
        public Color valueBad  = new Color(0.973f, 0.443f, 0.443f);         // #F87171

        private Dictionary<string, ZoneSkin> _byId;

        public IReadOnlyList<ZoneSkin> Zones => zones;

        /// <summary>Devolve a arte da zona, ou null quando o id nao esta na skin.</summary>
        public ZoneSkin For(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;

            if (_byId == null || _byId.Count != zones.Count)
            {
                _byId = new Dictionary<string, ZoneSkin>(zones.Count);

                foreach (var z in zones)
                {
                    if (z != null && !string.IsNullOrEmpty(z.id))
                        _byId[z.id] = z;
                }
            }

            return _byId.TryGetValue(id, out var found) ? found : null;
        }

        /// <summary>Usado pelo builder de Editor.</summary>
        public void EditorSetZones(List<ZoneSkin> value)
        {
            zones = value;
            _byId = null;
        }

        private void OnValidate()
        {
            _byId = null;
        }
    }
}
