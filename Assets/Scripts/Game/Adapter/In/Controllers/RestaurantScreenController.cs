using System.Collections.Generic;
using Game.Adapter.In.UI;
using UnityEngine;

namespace Game.Adapter.In.Controllers
{
    public sealed class RestaurantScreenController : MonoBehaviour
    {
        [SerializeField] private RestaurantScreenView view;

        private readonly Dictionary<RestaurantType, RestaurantData> _restaurantsByType = new();
        private readonly Dictionary<LocationZone, Segment> _locationSegmentsByZone = new();
        private RestaurantType? _selectedRestaurant;
        private Segment? _selectedSegment;

        private void OnEnable()
        {
            LoadRestaurants();
            PopulateView();
            BindActions();
            ResetFromSession();
            UpdateSegmentAvailability();
            UpdateCoherencePanel();
        }

        private void OnDisable()
        {
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
            _locationSegmentsByZone.Clear();

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

                _locationSegmentsByZone[location.zone] = location.primarySegment;
            }
        }

        private void PopulateView()
        {
            foreach (var pair in _restaurantsByType)
            {
                var restaurant = pair.Value;
                view.SetRestaurantCardData(restaurant.type, restaurant.displayName, restaurant.description);
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
        }

        private void ResetFromSession()
        {
            _selectedRestaurant = GetInitialRestaurant();
            _selectedSegment = _selectedRestaurant.HasValue
                ? GetFirstAllowedSegment(_selectedRestaurant.Value)
                : null;

            view.SelectRestaurantCard(_selectedRestaurant);
            view.SelectSegmentButton(_selectedSegment);
            UpdateConfirmState();
            UpdateHint();
        }

        private void OnRestaurantSelected(RestaurantType type)
        {
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
            if (!_selectedRestaurant.HasValue || !IsSegmentAllowed(_selectedRestaurant.Value, segment))
            {
                view.SetHint("Esta classe social nao esta disponivel para o restaurante selecionado.");
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
                view.SetHint("Selecione o tipo de restaurante e a classe social.");
                return;
            }

            string restName = _selectedRestaurant.HasValue ? GetRestaurantName(_selectedRestaurant.Value) : "-";
            string segName = _selectedSegment.HasValue ? GetSegmentName(_selectedSegment.Value) : "-";

            if (_selectedRestaurant.HasValue && _restaurantsByType.TryGetValue(_selectedRestaurant.Value, out var data) && !string.IsNullOrWhiteSpace(data.selectionHint))
            {
                view.SetHint(_selectedSegment.HasValue ? $"Voce selecionou: {restName} - {segName}" : data.selectionHint);
                return;
            }

            string hint = _selectedRestaurant.HasValue && _selectedSegment.HasValue
                ? $"Voce selecionou: {restName} - {segName}"
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

        private void UpdateCoherencePanel()
        {
            if (!_selectedRestaurant.HasValue)
            {
                view.UpdateCoherence(0f, "-", 0f, "Selecione um restaurante.");
                return;
            }

            LocationZone zone = GameSessionState.HasSession
                ? GameSessionState.Current.locationZone
                : LocationZone.Financas;

            Segment restaurantSegment = GetRestaurantNaturalSegment(_selectedRestaurant.Value);
            Segment locationSegment = GetLocationSegment(zone);

            float coh1 = CalcRestaurantLocationCoherence(restaurantSegment, locationSegment);
            string tip1 = GetCoherenceTip1(_selectedRestaurant.Value, restaurantSegment, zone, locationSegment);

            float coh2 = _selectedSegment.HasValue
                ? CalcRestaurantSegmentCoherence(_selectedRestaurant.Value, _selectedSegment.Value)
                : 0f;
            string tip2 = _selectedSegment.HasValue
                ? GetCoherenceTip2(_selectedRestaurant.Value, _selectedSegment.Value)
                : "Selecione um segmento disponivel.";

            view.UpdateCoherence(coh1, tip1, coh2, tip2);
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

        private Segment GetLocationSegment(LocationZone zone)
        {
            return _locationSegmentsByZone.TryGetValue(zone, out var segment)
                ? segment
                : Segment.MEDIUM;
        }

        private static Segment GetRestaurantNaturalSegment(RestaurantType restaurant) => restaurant switch
        {
            RestaurantType.PODRAO => Segment.LOW,
            RestaurantType.JAPONES => Segment.MEDIUM,
            RestaurantType.FRANCES => Segment.HIGH,
            _ => Segment.MEDIUM
        };

        private static float CalcRestaurantLocationCoherence(Segment restaurantSegment, Segment locationSegment)
        {
            int distance = Mathf.Abs((int)restaurantSegment - (int)locationSegment);

            return distance switch
            {
                0 => 0.95f,
                1 => 0.6f,
                _ => 0.25f
            };
        }

        private static float CalcRestaurantSegmentCoherence(RestaurantType r, Segment s) => (r, s) switch
        {
            (RestaurantType.PODRAO, Segment.LOW) => 0.95f,
            (RestaurantType.PODRAO, Segment.MEDIUM) => 0.7f,
            (RestaurantType.JAPONES, Segment.MEDIUM) => 0.8f,
            (RestaurantType.JAPONES, Segment.HIGH) => 0.75f,
            (RestaurantType.FRANCES, Segment.HIGH) => 0.95f,
            _ => 0.1f
        };

        private static string GetCoherenceTip1(
            RestaurantType restaurant,
            Segment restaurantSegment,
            LocationZone locationZone,
            Segment locationSegment)
        {
            string restaurantName = GetRestaurantDisplayName(restaurant);
            string restaurantClass = GetSegmentName(restaurantSegment);
            string locationClass = GetSegmentName(locationSegment);
            string locationName = GetLocationName(locationZone);

            if (restaurantSegment == locationSegment)
                return $"{restaurantName} combina com {locationName}: ambos estao associados a {restaurantClass}.";

            int distance = Mathf.Abs((int)restaurantSegment - (int)locationSegment);

            if (distance == 1)
                return $"{restaurantName} tem foco em {restaurantClass}, enquanto {locationName} indica {locationClass}. A combinacao e viavel, mas exige ajuste de proposta.";

            return $"{restaurantName} tem foco em {restaurantClass}, enquanto {locationName} indica {locationClass}. A combinacao e pouco coerente para a proposta inicial.";
        }

        private static string GetCoherenceTip2(RestaurantType r, Segment s) => (r, s) switch
        {
            (RestaurantType.PODRAO, Segment.LOW) or
            (RestaurantType.PODRAO, Segment.MEDIUM) => 
                "Restaurantes de lanches trabalham com preço acessível e maior volume de pedidos. Para essa opção, estão disponíveis as classes C e B.",

            (RestaurantType.JAPONES, Segment.MEDIUM) or
            (RestaurantType.JAPONES, Segment.HIGH) =>
                "Restaurantes japoneses possuem maior custo de insumos e preparo técnico. Para essa opção, estão disponíveis as classes B e A.",

            (RestaurantType.FRANCES, Segment.HIGH) =>
                "Restaurantes franceses possuem proposta premium, ticket elevado e maior exigência de experiência. Para essa opção, está disponível a classe A.",

            _ =>
                "Este público-alvo não está disponível para o restaurante selecionado, pois nao e coerente com sua proposta de valor e estrutura de custos."
        };

        private static string GetRestaurantDisplayName(RestaurantType restaurant) => restaurant switch
        {
            RestaurantType.PODRAO => "Lanches",
            RestaurantType.JAPONES => "Japonês",
            RestaurantType.FRANCES => "Francês",
            _ => restaurant.ToString()
        };

        private static string GetLocationName(LocationZone locationZone) => locationZone switch
        {
            LocationZone.Financas => "Área financeira",
            LocationZone.Educacao => "Área educacional",
            LocationZone.Comercio => "Área comercial",
            LocationZone.Residencial => "Área residencial",
            LocationZone.Servicos => "Área de servicos",
            _ => locationZone.ToString()
        };

        private void OnConfirm()
        {
            if (!_selectedRestaurant.HasValue || !_selectedSegment.HasValue)
                return;

            GameSessionState.SetRestaurant(_selectedRestaurant.Value);
            GameSessionState.SetTargetSegment(_selectedSegment.Value);

            GameManager.Instance.StateMachine
                .TryChangeState(GameState.Config_TargetSegment);
        }

        private void OnBack()
        {
            _selectedRestaurant = null;
            _selectedSegment = null;

            GameManager.Instance.StateMachine
                .TryChangeState(GameState.Config_Location);
        }
    }
}
