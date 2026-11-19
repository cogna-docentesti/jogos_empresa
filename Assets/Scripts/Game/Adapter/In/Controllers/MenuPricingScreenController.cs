using System.Collections.Generic;
using Game.Adapter.In.UI;
using Game.Adapter.In.UI.Navigation;
using Game.Infrastructure.Session;
using UnityEngine;

namespace Game.Adapter.In.Controllers
{
    /// <summary>
    /// D3 Cardápio. Ultima decisao obrigatoria.
    ///
    /// Le o restaurante e os preços do rascunho (PlayerSession). No confirm,
    /// chama GameSessionService.ConfirmInitialDecisions(), que grava tudo no
    /// banco de uma vez.
    ///
    /// Esta tela tambem abre pelo hub (Menu do Jogo) depois que a partida ja
    /// comecou. Nesse caso consulta os preços confirmados; o histórico do hub
    /// fornece o retorno, sem outra confirmação ou gravação.
    /// </summary>
    public sealed class MenuPricingScreenController : MonoBehaviour
    {
        [SerializeField] private MenuPricingScreenView view;

        private readonly List<ProductPricingItemView> _items = new();
        private RestaurantData _currentRestaurant;
        private readonly Dictionary<string, string> _draftInputs = new();
        private RestaurantType? _draftRestaurant;
        private bool _review;
        private bool _confirmed;

        private void OnEnable()
        {
            if (view == null)
                view = GetComponent<MenuPricingScreenView>();

            _review = WasOpenedFromMenu();
            _confirmed = false;
            view?.SetReviewMode(_review);
            BindActions();
            PopulateProducts();
        }

        private static bool WasOpenedFromMenu()
        {
            return MenuNavigator.Instance != null
                && MenuNavigator.Instance.Current == PanelId.MenuPricing;
        }

        private void OnDisable()
        {
            if (view != null)
            {
                view.BindConfirm(null);
                view.BindBack(null);
            }

            foreach (var item in _items)
                item.PriceChanged -= OnItemPriceChanged;
        }

        private void BindActions()
        {
            if (view == null)
                return;

            view.BindConfirm(OnConfirm);
            view.BindBack(OnBack);
        }

        private void PopulateProducts()
        {
            ClearItems();
            view?.SetPriceSummary(_items);

            if (view == null || !view.HasRequiredReferences())
                return;

            // O restaurante vem do rascunho da D2, nao da GameSessionEntity:
            // antes do confirm da D3 a entidade ainda nao recebeu nada.
            if (!_review && !PlayerSession.SelectedRestaurantType.HasValue)
            {
                view.SetHint("Escolha o restaurante na etapa anterior.");
                view.SetConfirmEnabled(false);
                return;
            }

            RestaurantType restaurantType = _review && GameSessionState.Current != null
                ? GameSessionState.Current.restaurantType : PlayerSession.SelectedRestaurantType.Value;

            var restaurant = FindRestaurant(restaurantType);
            if (restaurant == null)
            {
                view.SetHint("Restaurante selecionado nao encontrado.");
                view.SetConfirmEnabled(false);
                return;
            }

            _currentRestaurant = restaurant;
            view.SetTitle("Cardápio e preços");
            view.SetRestaurant(restaurant.displayName, _review);
            if (_draftRestaurant != restaurantType || !PlayerSession.HasMenu) _draftInputs.Clear();
            _draftRestaurant = restaurantType;

            // Reidratacao: se o jogador ja montou este cardápio (e voltou para a
            // D2 sem trocar de restaurante), os preços dele voltam aqui.
            var savedPricing = _review && GameSessionState.Current != null
                ? MenuPricingHelper.FromJson(GameSessionState.Current.menuPricingJson)
                : PlayerSession.MenuRestaurantType == restaurantType
                ? PlayerSession.GetMenu()
                : new MenuPricingData();

            foreach (var product in restaurant.products)
            {
                if (product == null)
                    continue;

                float initialPrice = MenuPricingHelper.TryGetPrice(savedPricing, product.id, out var savedPrice)
                    ? product.ClampPrice(savedPrice)
                    : product.ClampPrice(product.price);

                var item = view.CreateProductItem();
                item.Setup(product, initialPrice);
                item.SetReadOnly(_review);
                if (!_review && _draftInputs.TryGetValue(product.id, out var raw)) item.RestoreDraftInput(raw);
                item.PriceChanged += OnItemPriceChanged;
                _items.Add(item);
            }

            ValidatePrices();
            UpdateCoherencePanel();
        }

        private void OnItemPriceChanged(ProductPricingItemView _)
        {
            ValidatePrices();
            UpdateCoherencePanel();
        }

        private void ValidatePrices()
        {
            if (_items.Count != 3)
            {
                view.SetHint("Cardápio indisponível. Volte e confira o restaurante.", true);
                view.SetConfirmEnabled(false);
                return;
            }
            foreach (var item in _items)
            {
                if (item.Product == null || !item.IsPriceValid || !item.Product.IsPriceInRange(item.SelectedPrice))
                {
                    view.SetHint("Revise os campos destacados antes de confirmar.", true);
                    view.SetConfirmEnabled(false);
                    return;
                }
            }

            view.SetHint(_review ? "Preços salvos da sua partida." : "Confira os três preços e confirme suas decisões.");
            view.SetConfirmEnabled(!_review && _items.Count > 0);
        }

        private void UpdateCoherencePanel()
        {
            view?.SetPriceSummary(_items);
            if (view == null || _currentRestaurant == null || _items.Count == 0)
            {
                view?.UpdatePriceCoherence(0f, "Defina os preços do cardápio.");
                return;
            }

            float total = 0f;
            int count = 0;

            foreach (var item in _items)
            {
                if (item?.Product == null)
                    continue;

                total += GetProductPriceCoherence(_currentRestaurant.type, item.Product, item.SelectedPrice);
                count++;
            }

            float coherence = count > 0 ? total / count : 0f;
            view.UpdatePriceCoherence(coherence, GetPriceCoherenceTip(_currentRestaurant.type, coherence));
        }

        private static float GetProductPriceCoherence(RestaurantType restaurant, ProductData product, float selectedPrice)
        {
            if (product.maxPrice <= product.minPrice)
                return 0.5f;

            float normalizedPrice = Mathf.InverseLerp(
                product.minPrice,
                product.maxPrice,
                product.ClampPrice(selectedPrice)
            );

            return restaurant switch
            {
                RestaurantType.PODRAO => normalizedPrice switch
                {
                    <= 0.35f => 1f,
                    <= 0.60f => 0.7f,
                    _ => 0.4f
                },

                RestaurantType.JAPONES => normalizedPrice switch
                {
                    >= 0.20f and <= 0.75f => 1f,
                    > 0.75f => 0.8f,
                    _ => 0.7f
                },

                RestaurantType.FRANCES => normalizedPrice switch
                {
                    >= 0.55f => 1f,
                    >= 0.35f => 0.7f,
                    _ => 0.4f
                },

                _ => 0.5f
            };
        }

        private static string GetPriceCoherenceTip(RestaurantType restaurant, float coherence)
        {
            if (coherence >= 0.7f)
                return restaurant switch
                {
                    RestaurantType.PODRAO => "Cardápio alinhado a uma proposta acessível e de volume.",
                    RestaurantType.JAPONES => "Cardápio alinhado a uma proposta de ticket médio/premium.",
                    RestaurantType.FRANCES => "Cardápio alinhado a uma proposta premium.",
                    _ => "Cardápio alinhado ao restaurante."
                };

            return restaurant switch
            {
                RestaurantType.PODRAO => "Preços altos podem reduzir o apelo de volume do podrao.",
                RestaurantType.JAPONES => "Ajuste os preços para manter uma percepção equilibrada de valor.",
                RestaurantType.FRANCES => "Preços baixos podem enfraquecer a percepção premium do francês.",
                _ => "Revise os preços para melhorar a coerência."
            };
        }

        /// <summary>
        /// Confirm da D3: o UNICO ponto do fluxo inicial que grava no SQLite.
        /// Primeiro o cardápio vai para o rascunho; depois o service copia o
        /// rascunho inteiro (D1, D2, D3 e identificacao) para a sessao e grava
        /// uma vez so.
        /// </summary>
        private void OnConfirm()
        {
            if (_currentRestaurant == null || _review || _confirmed || GameSessionState.IsPersisted
                || GameManager.Instance?.StateMachine.CurrentState != InitialDecisionFlow.Menu)
                return;

            var pricing = BuildPricing(requireValidPrices: true);
            if (pricing == null || _items.Count != 3)
            {
                ValidatePrices();
                return;
            }

            PlayerSession.SetMenu(_currentRestaurant.type, pricing);

            var session = GameSessionState.Current;
            if (session == null)
            {
                view.SetHint("Nenhuma sessão ativa. Reinicie o jogo.");
                return;
            }

            var service = new GameSessionService(session.userId, session.professorId);

            if (!service.ConfirmInitialDecisions())
            {
                view.SetHint("Não foi possível salvar. Confira suas escolhas e tente de novo.", true);
                return;
            }
            _confirmed = true;
        }

        private void OnBack()
        {
            if (_review) { MenuNavigator.Instance?.Back(); return; }
            // Guarda os preços que estao na tela: se o jogador voltar para a D2
            // e avancar sem trocar de restaurante, encontra o cardápio como deixou.
            if (_currentRestaurant != null)
            {
                var pricing = BuildPricing(requireValidPrices: false);
                if (pricing != null)
                    PlayerSession.SetMenu(_currentRestaurant.type, pricing);
                foreach (var item in _items) _draftInputs[item.Product.id] = item.DraftInput;
            }

            InitialDecisionFlow.GoTo(InitialDecisionFlow.Restaurant);
        }

        /// <summary>
        /// Monta o MenuPricingData com o que esta nos sliders.
        /// Devolve null se requireValidPrices e algum preco estiver fora da faixa.
        /// </summary>
        private MenuPricingData BuildPricing(bool requireValidPrices)
        {
            var pricing = new MenuPricingData();

            foreach (var item in _items)
            {
                if (item == null || item.Product == null || string.IsNullOrWhiteSpace(item.Product.id))
                    continue;

                if (requireValidPrices && (!item.IsPriceValid || !item.Product.IsPriceInRange(item.SelectedPrice)))
                    return null;

                pricing.items.Add(new MenuPricingItem
                {
                    productId = item.Product.id,
                    selectedPrice = item.SelectedPrice
                });
            }

            return pricing;
        }

        private void ClearItems()
        {
            foreach (var item in _items)
            {
                if (item != null)
                    item.PriceChanged -= OnItemPriceChanged;
            }

            _items.Clear();
            _currentRestaurant = null;

            if (view != null)
                view.ClearProductItems();
        }

        private RestaurantData FindRestaurant(RestaurantType type)
        {
            foreach (var restaurant in Resources.LoadAll<RestaurantData>("Restaurants"))
            {
                if (restaurant != null && restaurant.type == type)
                    return restaurant;
            }

            return null;
        }
    }
}
