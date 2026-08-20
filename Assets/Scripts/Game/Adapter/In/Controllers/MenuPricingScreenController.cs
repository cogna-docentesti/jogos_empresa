using System.Collections.Generic;
using Game.Adapter.In.UI;
using Game.Adapter.In.UI.Navigation;
using UnityEngine;

namespace Game.Adapter.In.Controllers
{
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

            var session = GameSessionState.Current;
            if (session == null)
            {
                view.SetHint("Nenhuma sessao ativa.");
                view.SetConfirmEnabled(false);
                return;
            }

            var restaurant = FindRestaurant(session.restaurantType);
            if (restaurant == null)
            {
                view.SetHint("Restaurante selecionado nao encontrado.");
                view.SetConfirmEnabled(false);
                return;
            }

            _currentRestaurant = restaurant;
            view.SetTitle($"Cardapio - {restaurant.displayName}");

            var savedPricing = MenuPricingHelper.FromJson(session.menuPricingJson);

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

        private void OnConfirm()
        {
            var pricing = new MenuPricingData();

            foreach (var item in _items)
            {
                if (item.Product == null || !item.Product.IsPriceInRange(item.SelectedPrice))
                {
                    ValidatePrices();
                    return;
                }

                pricing.items.Add(new MenuPricingItem
                {
                    productId = item.Product.id,
                    selectedPrice = item.SelectedPrice
                });
            }

            var session = GameSessionState.Current;
            if (session == null)
                return;

            var service = new GameSessionService(session.userId, session.professorId);
            service.ConfirmMenuPricing(pricing);
        }

        private void OnBack()
        {
            GameManager.Instance.StateMachine.ForceState(GameState.Config_Restaurant);
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
