using System.Collections.Generic;
using Game.Adapter.In.UI;
using UnityEngine;

namespace Game.Adapter.In.Controllers
{
    public sealed class RestaurantScreenController : MonoBehaviour
    {
        [SerializeField] private RestaurantScreenView view;

        private readonly Dictionary<RestaurantType, RestaurantData> _restaurantsByType = new();
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

            foreach (var restaurant in Resources.LoadAll<RestaurantData>("Restaurants"))
            {
                if (restaurant == null)
                    continue;

                _restaurantsByType[restaurant.type] = restaurant;
            }

            if (_restaurantsByType.Count == 0)
                Debug.LogError("Nenhum RestaurantData encontrado em Resources/Restaurants.");
        }

        private void PopulateView()
        {
            foreach (var pair in _restaurantsByType)
            {
                var restaurant = pair.Value;
                view.SetRestaurantCardData(restaurant.type, restaurant.displayName, restaurant.description);
            }

            view.SetSegmentButtonData(Segment.LOW, "Baixo");
            view.SetSegmentButtonData(Segment.MEDIUM, "Medio");
            view.SetSegmentButtonData(Segment.HIGH, "Alto");
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
                view.SetHint("Este publico-alvo nao esta disponivel para o restaurante selecionado.");
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
                view.SetHint("Selecione o tipo de restaurante e o publico-alvo.");
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

            float coh1 = CalcRestaurantLocationCoherence(_selectedRestaurant.Value, zone);
            string tip1 = GetCoherenceTip1(_selectedRestaurant.Value, zone);

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
            Segment.LOW => "Publico Baixo",
            Segment.MEDIUM => "Publico Medio",
            Segment.HIGH => "Publico Alto",
            _ => segment.ToString()
        };

        private static float CalcRestaurantLocationCoherence(RestaurantType r, LocationZone z) => (r, z) switch
        {
            (RestaurantType.PODRAO, LocationZone.Comercio) => 0.9f,
            (RestaurantType.PODRAO, LocationZone.Educacao) => 0.8f,
            (RestaurantType.PODRAO, LocationZone.Financas) => 0.6f,
            (RestaurantType.PODRAO, LocationZone.Residencial) => 0.5f,
            (RestaurantType.PODRAO, LocationZone.Servicos) => 0.7f,
            (RestaurantType.JAPONES, LocationZone.Financas) => 0.85f,
            (RestaurantType.JAPONES, LocationZone.Educacao) => 0.75f,
            (RestaurantType.JAPONES, LocationZone.Comercio) => 0.7f,
            (RestaurantType.JAPONES, LocationZone.Residencial) => 0.6f,
            (RestaurantType.JAPONES, LocationZone.Servicos) => 0.65f,
            (RestaurantType.FRANCES, LocationZone.Financas) => 0.95f,
            (RestaurantType.FRANCES, LocationZone.Residencial) => 0.8f,
            (RestaurantType.FRANCES, LocationZone.Comercio) => 0.65f,
            (RestaurantType.FRANCES, LocationZone.Educacao) => 0.55f,
            (RestaurantType.FRANCES, LocationZone.Servicos) => 0.7f,
            _ => 0.5f
        };

        private static float CalcRestaurantSegmentCoherence(RestaurantType r, Segment s) => (r, s) switch
        {
            (RestaurantType.PODRAO, Segment.LOW) => 0.95f,
            (RestaurantType.PODRAO, Segment.MEDIUM) => 0.7f,
            (RestaurantType.JAPONES, Segment.MEDIUM) => 0.8f,
            (RestaurantType.JAPONES, Segment.HIGH) => 0.75f,
            (RestaurantType.FRANCES, Segment.HIGH) => 0.95f,
            _ => 0.1f
        };

        private static string GetCoherenceTip1(RestaurantType r, LocationZone z) => (r, z) switch
        {
            (RestaurantType.FRANCES, LocationZone.Financas) => "Otimo! Frances se destaca no centro financeiro.",
            (RestaurantType.PODRAO, LocationZone.Comercio) => "Otimo! Lanches funcionam bem no comercio popular.",
            (RestaurantType.JAPONES, LocationZone.Financas) => "Bom! Executivos apreciam culinaria japonesa.",
            _ => "Combinacao viavel. Avalie o segmento de publico."
        };

        private static string GetCoherenceTip2(RestaurantType r, Segment s) => (r, s) switch
        {
            (RestaurantType.PODRAO, Segment.LOW) => "Boa escolha. Lanches combinam com preco acessivel e volume.",
            (RestaurantType.PODRAO, Segment.MEDIUM) => "Boa escolha. Publico medio tambem pode buscar conveniencia.",
            (RestaurantType.JAPONES, Segment.MEDIUM) => "Boa escolha. Japones atrai bem o publico medio.",
            (RestaurantType.JAPONES, Segment.HIGH) => "Boa escolha. O ticket comporta uma proposta premium.",
            (RestaurantType.FRANCES, Segment.HIGH) => "Boa escolha. Frances exige publico de alta renda.",
            _ => "Este publico nao esta liberado para o restaurante escolhido."
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
