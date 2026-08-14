using System.Collections.Generic;
using Game.Adapter.In.UI;
using Game.Adapter.In.UI.Navigation;
using Game.Adapter.In.UI.Theme;
using UnityEngine;

namespace Game.Adapter.In.Controllers
{
    /// <summary>
    /// Controller da tela "Abertura do Restaurante" (V2).
    ///
    /// Espelha o comportamento do LocationScreenController original:
    /// carrega os LocationData de Resources/Locations, deixa o jogador
    /// escolher uma area e, ao confirmar, grava a zona em memoria e pede
    /// ao GameManager para avancar de estado.
    ///
    /// O controller antigo continua intacto. Os dois podem conviver na cena
    /// enquanto voce compara as duas telas.
    /// </summary>
    public sealed class LocationScreenV2Controller : MonoBehaviour
    {
        [SerializeField] private LocationScreenV2View view;

        [Header("Cabecalho")]
        [SerializeField] private string screenTitle    = "Abertura do Restaurante";
        [SerializeField] private string screenSubtitle = "Escolha onde abrir seu primeiro negocio";
        [SerializeField] private string screenName     = "Localizacao";
        [SerializeField] private int    stepIndex      = 1;
        [SerializeField] private int    stepTotal      = 4;

        /// <summary>Ordem dos marcadores no mapa. Cada id casa com Resources/Locations/LOC_{id}.</summary>
        [SerializeField]
        private List<string> locationIds = new List<string>
        {
            "bank", "university", "store", "condominium", "marketing"
        };

        private readonly Dictionary<string, LocationData> _catalog = new Dictionary<string, LocationData>();
        private string _selectedId;

        private void Awake()
        {
            if (view == null)
                view = GetComponent<LocationScreenV2View>();
        }

        private void OnEnable()
        {
            LoadCatalog();
            BindMarkers();
            Render();
        }

        // =====================================================
        //  CARGA
        // =====================================================

        private void LoadCatalog()
        {
            _catalog.Clear();

            foreach (var id in locationIds)
            {
                var data = Resources.Load<LocationData>($"Locations/LOC_{id}");

                if (data == null)
                {
                    Debug.LogWarning($"[LocationScreenV2Controller] Nao encontrei Resources/Locations/LOC_{id}.");
                    continue;
                }

                _catalog[id] = data;
            }
        }

        private void BindMarkers()
        {
            if (view == null) return;

            foreach (var marker in view.Markers)
            {
                if (marker == null) continue;

                if (!_catalog.TryGetValue(marker.LocationId, out var data))
                {
                    marker.gameObject.SetActive(false);
                    continue;
                }

                marker.gameObject.SetActive(true);
                marker.Bind(data.id, data.displayName, data.SubtitleOrDefault, data.colorHex, data.pinIcon);

                // Com skin, a arte do redesign entra por cima do Bind procedural.
                var skin = view.Skin;
                var zoneSkin = skin != null ? skin.For(data.id) : null;

                if (zoneSkin != null)
                {
                    marker.ApplySkin(
                        pinIdle:    zoneSkin.pinIdle,
                        pinActive:  zoneSkin.pinActive,
                        glowIdle:   zoneSkin.glowIdle,
                        glowActive: zoneSkin.glowActive,
                        accent:     zoneSkin.accent,
                        accentSoft: zoneSkin.accentSoft,
                        cardSprite: skin.labelPill);
                }
            }

            view.BindMarkers(OnMarkerSelected);
            view.BindContinue(OnContinue);
            view.BindMenu(OnMenu);
        }

        private void Render()
        {
            if (view == null) return;

            view.SetStep(stepIndex, stepTotal, screenName);
            view.SetTitle(screenTitle, screenSubtitle);
            view.ShowEmptyState();
            view.SelectMarker(null);

            _selectedId = null;
        }

        // =====================================================
        //  INTERACAO
        // =====================================================

        private void OnMarkerSelected(string id)
        {
            if (!_catalog.TryGetValue(id, out var data))
                return;

            _selectedId = id;

            view.SelectMarker(id);

            view.ShowDetails(
                zoneName:     data.displayName,
                zoneSubtitle: data.SubtitleOrDefault,
                description:  data.description,
                investment:   data.InvestmentLabel,
                traffic:      data.TrafficLabel,
                competition:  data.CompetitionLabel,
                highlight:    data.highlight,
                accent:       GamePalette.Parse(data.colorHex),
                icon:         data.pinIcon);
        }

        private void OnContinue()
        {
            if (string.IsNullOrEmpty(_selectedId))
                return;

            if (!_catalog.TryGetValue(_selectedId, out var data))
                return;

            // Igual ao controller original: grava so em memoria. O SQLite
            // continua sendo escrito apenas na confirmacao de equipamentos.
            GameSessionState.SetLocation(data.zone);

            if (GameManager.Instance == null || GameManager.Instance.StateMachine == null)
            {
                Debug.LogWarning("[LocationScreenV2Controller] Sem GameManager na cena; "
                               + "a escolha foi gravada mas o estado nao avancou.");
                return;
            }

            GameManager.Instance.StateMachine.TryChangeState(GameState.Config_Restaurant);
        }

        private void OnMenu()
        {
            var navigator = MenuNavigator.Instance;

            if (navigator == null)
            {
                Debug.LogWarning("[LocationScreenV2Controller] Nenhum MenuNavigator ativo na cena.");
                return;
            }

            navigator.OpenRoot();
        }
    }
}
