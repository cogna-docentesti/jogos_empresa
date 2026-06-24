using System.Collections.Generic;
using Game.Adapter.In.UI;
using UnityEngine;

namespace Game.Adapter.In.Controllers
{
    public sealed class MenuPricingScreenController : MonoBehaviour
    {
        [SerializeField] private MenuPricingScreenView view;

        private readonly List<ProductPricingItemView> _items = new();

        private void OnEnable()
        {
            if (view == null)
                view = GetComponent<MenuPricingScreenView>();

            BindActions();
            PopulateProducts();
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
        }

        private void OnItemPriceChanged(ProductPricingItemView _)
        {
            ValidatePrices();
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
