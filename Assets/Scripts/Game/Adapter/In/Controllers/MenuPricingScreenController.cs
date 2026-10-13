using System.Collections.Generic;
using Game.Adapter.In.UI;
using Game.Adapter.In.UI.Navigation;
using Game.Infrastructure.Session;
using UnityEngine;

namespace Game.Adapter.In.Controllers
{
    /// <summary>
    /// D3 Cardapio. Ultima decisao obrigatoria.
    ///
    /// Le o restaurante e os precos do rascunho (PlayerSession). No confirm,
    /// chama GameSessionService.ConfirmInitialDecisions(), que grava tudo no
    /// banco de uma vez.
    ///
    /// Esta tela tambem abre pelo hub (Menu do Jogo) depois que a partida ja
    /// comecou. Nesse caso o rodape (Confirmar/Voltar) fica escondido e ela so
    /// mostra o cardapio salvo, que o GameManager colocou no rascunho ao carregar.
    /// </summary>
    public sealed class MenuPricingScreenController : MonoBehaviour
    {
        [SerializeField] private MenuPricingScreenView view;

        private readonly List<ProductPricingItemView> _items = new();
        private RestaurantData _currentRestaurant;

        private void OnEnable()
        {
            if (view == null)
                view = GetComponent<MenuPricingScreenView>();

            view?.SetFooterVisible(!WasOpenedFromMenu());
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

            if (view == null || !view.HasRequiredReferences())
                return;

            // O restaurante vem do rascunho da D2, nao da GameSessionEntity:
            // antes do confirm da D3 a entidade ainda nao recebeu nada.
            if (!PlayerSession.SelectedRestaurantType.HasValue)
            {
                view.SetHint("Escolha o restaurante na etapa anterior.");
                view.SetConfirmEnabled(false);
                return;
            }

            RestaurantType restaurantType = PlayerSession.SelectedRestaurantType.Value;

            var restaurant = FindRestaurant(restaurantType);
            if (restaurant == null)
            {
                view.SetHint("Restaurante selecionado nao encontrado.");
                view.SetConfirmEnabled(false);
                return;
            }

            _currentRestaurant = restaurant;
            view.SetTitle($"Cardapio - {restaurant.displayName}");

            // Reidratacao: se o jogador ja montou este cardapio (e voltou para a
            // D2 sem trocar de restaurante), os precos dele voltam aqui.
            var savedPricing = PlayerSession.MenuRestaurantType == restaurantType
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
            foreach (var item in _items)
            {
                if (item.Product == null || !item.Product.IsPriceInRange(item.SelectedPrice))
                {
                    view.SetHint("Ha precos fora do range permitido.");
                    view.SetConfirmEnabled(false);
                    return;
                }
            }

            view.SetHint("Cardapio pronto para confirmacao.");
            view.SetConfirmEnabled(_items.Count > 0);
        }

        private void UpdateCoherencePanel()
        {
            if (view == null || _currentRestaurant == null || _items.Count == 0)
            {
                view?.UpdatePriceCoherence(0f, "Defina os precos do cardapio.");
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
                    RestaurantType.PODRAO => "Cardapio alinhado a uma proposta acessivel e de volume.",
                    RestaurantType.JAPONES => "Cardapio alinhado a uma proposta de ticket medio/premium.",
                    RestaurantType.FRANCES => "Cardapio alinhado a uma proposta premium.",
                    _ => "Cardapio alinhado ao restaurante."
                };

            return restaurant switch
            {
                RestaurantType.PODRAO => "Precos altos podem reduzir o apelo de volume do podrao.",
                RestaurantType.JAPONES => "Ajuste os precos para manter uma percepcao equilibrada de valor.",
                RestaurantType.FRANCES => "Precos baixos podem enfraquecer a percepcao premium do frances.",
                _ => "Revise os precos para melhorar a coerencia."
            };
        }

        /// <summary>
        /// Confirm da D3: o UNICO ponto do fluxo inicial que grava no SQLite.
        /// Primeiro o cardapio vai para o rascunho; depois o service copia o
        /// rascunho inteiro (D1, D2, D3 e identificacao) para a sessao e grava
        /// uma vez so.
        /// </summary>
        private void OnConfirm()
        {
            if (_currentRestaurant == null)
                return;

            var pricing = BuildPricing(requireValidPrices: true);
            if (pricing == null)
            {
                ValidatePrices();
                return;
            }

            PlayerSession.SetMenu(_currentRestaurant.type, pricing);

            var session = GameSessionState.Current;
            if (session == null)
            {
                view.SetHint("Nenhuma sessao ativa. Reinicie o jogo.");
                return;
            }

            var service = new GameSessionService(session.userId, session.professorId);

            if (!service.ConfirmInitialDecisions())
            {
                view.SetHint("Nao foi possivel salvar. Confira suas escolhas e tente de novo.");
                return;
            }
        }

        private void OnBack()
        {
            // Guarda os precos que estao na tela: se o jogador voltar para a D2
            // e avancar sem trocar de restaurante, encontra o cardapio como deixou.
            if (_currentRestaurant != null)
            {
                var pricing = BuildPricing(requireValidPrices: false);
                if (pricing != null)
                    PlayerSession.SetMenu(_currentRestaurant.type, pricing);
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

                if (requireValidPrices && !item.Product.IsPriceInRange(item.SelectedPrice))
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
