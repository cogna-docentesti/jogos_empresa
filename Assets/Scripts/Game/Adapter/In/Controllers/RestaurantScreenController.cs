using System.Collections.Generic;
using System.Linq;
using Game.Adapter.In.UI;
using Game.Infrastructure.Session;
using UnityEngine;
using Game.Adapter.In.UI.Navigation;

namespace Game.Adapter.In.Controllers
{
    /// <summary>
    /// D2 Restaurante (tipo de restaurante + classe social atendida).
    ///
    /// Le e grava SO no rascunho em memoria (PlayerSession). Como este
    /// componente fica dentro do Panel_Restaurant, o OnEnable roda toda vez
    /// que a tela aparece, vindo da D1 (avancando) ou da D3 (voltando).
    /// </summary>
    public sealed class RestaurantScreenController : MonoBehaviour
    {
        private const GameState PreviousState = InitialDecisionFlow.Location;
        private const GameState NextState     = InitialDecisionFlow.Menu;

        [SerializeField] private RestaurantScreenView view;

        private readonly Dictionary<RestaurantType, RestaurantData> _restaurantsByType = new();
        private readonly Dictionary<LocationZone, LocationData> _locationsByZone = new();
        private RestaurantType? _selectedRestaurant;
        private Segment? _selectedSegment;
        private bool _review;

        private void OnEnable()
        {
            _review = MenuNavigator.HubAvailable && MenuNavigator.Instance != null && MenuNavigator.Instance.Current == PanelId.Restaurant;
            view.SetReviewMode(false);
            LoadRestaurants();
            PopulateView();
            BindActions();
            ResetFromSession();
            UpdateSegmentAvailability();
            view.SetReviewMode(_review);
        }

        private void OnDisable()
        {
            view.CloseCompatibility();
            view.BindCompatibility(null, null);
            view.BindCardPodrao(null);
            view.BindCardJapones(null);
            view.BindCardFrances(null);
            view.BindBtnLow(null);
            view.BindBtnMedium(null);
            view.BindBtnHigh(null);
            view.BindConfirm(null);
            view.BindBack(null);
        }

        private void LoadRestaurants()
        {
            _restaurantsByType.Clear();
            _locationsByZone.Clear();

            foreach (var restaurant in Resources.LoadAll<RestaurantData>("Restaurants"))
            {
                if (restaurant == null)
                    continue;

                _restaurantsByType[restaurant.type] = restaurant;
            }

            if (_restaurantsByType.Count == 0)
                Debug.LogError("Nenhum RestaurantData encontrado em Resources/Restaurants.");

            foreach (var location in Resources.LoadAll<LocationData>("Locations"))
            {
                if (location == null)
                    continue;

                _locationsByZone[location.zone] = location;
            }
        }

        private void PopulateView()
        {
            view.SetCompatibilityAvailable(_restaurantsByType.Count > 0);
            foreach (var type in new[] { RestaurantType.PODRAO, RestaurantType.JAPONES, RestaurantType.FRANCES })
                view.SetRestaurantAvailability(type, _restaurantsByType.ContainsKey(type));
            foreach (var pair in _restaurantsByType)
            {
                var restaurant = pair.Value;
                view.SetRestaurantCardData(restaurant);
            }

            view.SetSegmentButtonData(Segment.LOW, "Classe C");
            view.SetSegmentButtonData(Segment.MEDIUM, "Classe B");
            view.SetSegmentButtonData(Segment.HIGH, "Classe A");
        }

        private void BindActions()
        {
            view.BindCardPodrao(() => OnRestaurantSelected(RestaurantType.PODRAO));
            view.BindCardJapones(() => OnRestaurantSelected(RestaurantType.JAPONES));
            view.BindCardFrances(() => OnRestaurantSelected(RestaurantType.FRANCES));
            view.BindBtnLow(() => OnSegmentSelected(Segment.LOW));
            view.BindBtnMedium(() => OnSegmentSelected(Segment.MEDIUM));
            view.BindBtnHigh(() => OnSegmentSelected(Segment.HIGH));
            view.BindConfirm(OnConfirm);
            view.BindBack(OnBack);
            view.BindCompatibility(OnCompatibilityRequested, OnCompatibilityTabSelected);
        }

        /// <summary>
        /// Reidrata a tela a partir do rascunho. Se o jogador ja passou por
        /// aqui, volta a escolha dele; se e a primeira vez, mantem o
        /// comportamento de antes (primeiro restaurante e primeira classe
        /// permitida pre-selecionados).
        /// </summary>
        private void ResetFromSession()
        {
            if (_review && GameSessionState.Current != null)
            {
                _selectedRestaurant = _restaurantsByType.ContainsKey(GameSessionState.Current.restaurantType) ? GameSessionState.Current.restaurantType : null;
                _selectedSegment = _selectedRestaurant.HasValue ? GameSessionState.Current.targetSegment : null;
            }
            else if (PlayerSession.HasRestaurant
                && _restaurantsByType.ContainsKey(PlayerSession.SelectedRestaurantType.Value))
            {
                _selectedRestaurant = PlayerSession.SelectedRestaurantType.Value;
                _selectedSegment = IsSegmentAllowed(_selectedRestaurant.Value, PlayerSession.SelectedTargetSegment.Value)
                    ? PlayerSession.SelectedTargetSegment.Value
                    : GetFirstAllowedSegment(_selectedRestaurant.Value);
            }
            else
            {
                _selectedRestaurant = GetInitialRestaurant();
                _selectedSegment = _selectedRestaurant.HasValue
                    ? GetFirstAllowedSegment(_selectedRestaurant.Value)
                    : null;
            }

            UpdateSegmentAvailability();
            view.SelectRestaurantCard(_selectedRestaurant);
            view.SelectSegmentButton(_selectedSegment);
            UpdateConfirmState();
            UpdateHint();
            UpdateCoherencePanel();
        }

        private void OnRestaurantSelected(RestaurantType type)
        {
            if (_review || view.IsCompatibilityOpen) return;
            if (!_restaurantsByType.ContainsKey(type)) return;
            _selectedRestaurant = type;

            if (_selectedSegment.HasValue && !IsSegmentAllowed(type, _selectedSegment.Value))
                _selectedSegment = GetFirstAllowedSegment(type);
            else if (!_selectedSegment.HasValue)
                _selectedSegment = GetFirstAllowedSegment(type);

            view.SelectRestaurantCard(type);
            UpdateSegmentAvailability();
            view.SelectSegmentButton(_selectedSegment);
            UpdateConfirmState();
            UpdateHint();
            UpdateCoherencePanel();
        }

        private void OnSegmentSelected(Segment segment)
        {
            if (_review || view.IsCompatibilityOpen) return;
            if (!_selectedRestaurant.HasValue || !IsSegmentAllowed(_selectedRestaurant.Value, segment))
            {
                view.SetHint("Esta classe social não está disponível para o restaurante selecionado.");
                return;
            }

            _selectedSegment = segment;
            view.SelectSegmentButton(segment);
            UpdateConfirmState();
            UpdateHint();
            UpdateCoherencePanel();
        }

        private void UpdateHint()
        {
            if (!_selectedRestaurant.HasValue && !_selectedSegment.HasValue)
            {
                view.SetHint("Restaurantes indisponíveis. Volte e tente novamente.");
                return;
            }

            string restName = _selectedRestaurant.HasValue ? GetRestaurantName(_selectedRestaurant.Value) : "-";
            string segName = _selectedSegment.HasValue ? GetSegmentName(_selectedSegment.Value) : "-";

            string hint = _selectedRestaurant.HasValue && _selectedSegment.HasValue
                ? $"Sua escolha: {restName} · {segName}"
                : _selectedRestaurant.HasValue
                    ? $"Restaurante: {restName} - Selecione o segmento"
                    : $"Segmento: {segName} - Selecione o restaurante";

            view.SetHint(hint);
        }

        private void UpdateSegmentAvailability()
        {
            foreach (Segment segment in new[] { Segment.LOW, Segment.MEDIUM, Segment.HIGH })
            {
                bool available = _selectedRestaurant.HasValue && IsSegmentAllowed(_selectedRestaurant.Value, segment);
                view.SetSegmentAvailability(segment, available);
            }
        }

        private void UpdateConfirmState()
        {
            bool canConfirm = _selectedRestaurant.HasValue
                && _selectedSegment.HasValue
                && IsSegmentAllowed(_selectedRestaurant.Value, _selectedSegment.Value);

            view.SetConfirmEnabled(canConfirm);
        }

        private void OnCompatibilityRequested()
        {
            if (view.IsCompatibilityOpen || !_selectedRestaurant.HasValue) return;

            UpdateCoherencePanel();
            view.OpenCompatibility();
        }

        private void OnCompatibilityTabSelected(RestaurantType type)
        {
            if (view.IsCompatibilityOpen && _restaurantsByType.TryGetValue(type, out var restaurant))
                ShowCompatibility(restaurant);
        }

        private void UpdateCoherencePanel()
        {
            if (_selectedRestaurant.HasValue && _restaurantsByType.TryGetValue(_selectedRestaurant.Value, out var restaurant))
                ShowCompatibility(restaurant);
            else
                view.UpdateCoherence(null, "Selecione um restaurante.", null, "Selecione um restaurante.");
        }

        private void ShowCompatibility(RestaurantData restaurant)
        {
            LocationZone? zone = _review ? GameSessionState.Current?.locationZone : PlayerSession.SelectedZone;
            int? locationScore = null;
            string locationTip = "Escolha uma localização para consultar.";
            if (zone.HasValue && _locationsByZone.TryGetValue(zone.Value, out var location))
            {
                locationScore = AlignmentEngine.GetRestaurantLocationScore(restaurant.type, zone.Value);
                locationTip = location.displayName;
            }

            int? targetScore = null;
            string targetTip = "Escolha uma classe para consultar.";
            if (_selectedSegment is Segment segment)
            {
                targetScore = AlignmentEngine.GetRestaurantTargetScore(restaurant.type, segment);
                string availability = restaurant.AllowsSegment(segment) ? "disponível" : "indisponível";
                targetTip = $"{GetSegmentName(segment)} · {availability}";
            }

            string allowed = restaurant.allowedSegments is { Length: > 0 }
                ? "Público permitido · " + string.Join(" e ", restaurant.allowedSegments.Select(GetSegmentName))
                : "Nenhuma classe disponível";
            targetTip += "\n" + allowed;

            view.SelectCompatibilityTab(restaurant.type);
            view.UpdateCoherence(locationScore, locationTip, targetScore, targetTip);
        }

        private bool IsSegmentAllowed(RestaurantType restaurantType, Segment segment)
        {
            return _restaurantsByType.TryGetValue(restaurantType, out var restaurant)
                && restaurant.AllowsSegment(segment);
        }

        private RestaurantType? GetInitialRestaurant()
        {
            if (_restaurantsByType.ContainsKey(RestaurantType.PODRAO))
                return RestaurantType.PODRAO;

            if (_restaurantsByType.ContainsKey(RestaurantType.JAPONES))
                return RestaurantType.JAPONES;

            if (_restaurantsByType.ContainsKey(RestaurantType.FRANCES))
                return RestaurantType.FRANCES;

            foreach (var pair in _restaurantsByType)
                return pair.Key;

            return null;
        }

        private Segment? GetFirstAllowedSegment(RestaurantType restaurantType)
        {
            if (!_restaurantsByType.TryGetValue(restaurantType, out var restaurant)
                || restaurant.allowedSegments == null
                || restaurant.allowedSegments.Length == 0)
            {
                return null;
            }

            return restaurant.allowedSegments[0];
        }

        private string GetRestaurantName(RestaurantType type)
        {
            return _restaurantsByType.TryGetValue(type, out var restaurant) ? restaurant.displayName : type.ToString();
        }

        private static string GetSegmentName(Segment segment) => segment switch
        {
            Segment.LOW => "Classe C",
            Segment.MEDIUM => "Classe B",
            Segment.HIGH => "Classe A",
            _ => segment.ToString()
        };

        private void OnConfirm()
        {
            if (_review || view.IsCompatibilityOpen) return;
            if (!TrySaveDraft())
                return;

            // Nada de banco aqui. O Save acontece so no confirm da D3.
            InitialDecisionFlow.GoTo(NextState);
        }

        private void OnBack()
        {
            if (_review || view.IsCompatibilityOpen) return;
            // Voltar nao apaga a escolha: o jogador esta voltando para trocar a
            // localizacao, nao para desistir. O que esta na tela vai para o
            // rascunho e sera reaplicado quando ele avancar de novo.
            TrySaveDraft();

            InitialDecisionFlow.GoTo(PreviousState);
        }

        private bool TrySaveDraft()
        {
            if (!_selectedRestaurant.HasValue
                || !_selectedSegment.HasValue
                || !IsSegmentAllowed(_selectedRestaurant.Value, _selectedSegment.Value))
                return false;

            PlayerSession.SaveRestaurant(_selectedRestaurant.Value, _selectedSegment.Value);
            return true;
        }
    }
}
