using System.Collections.Generic;
using System.Linq;
using Game.Adapter.In.UI;
using Game.Adapter.In.UI.Navigation;
using UnityEngine;

namespace Game.Adapter.In.Controllers
{
    public sealed class EquipmentScreenController : MonoBehaviour
    {
        [SerializeField] private EquipmentScreenView view;

        private readonly List<EquipmentCardView> cards = new();
        private readonly HashSet<string> ownedEquipmentIds = new();
        private readonly HashSet<string> cartEquipmentIds = new();
        private EquipmentData[] basicEquipments = System.Array.Empty<EquipmentData>();
        private EquipmentFilter filter = EquipmentFilter.All;
        private bool ready;

        private float AvailableCash => GameSessionState.HasSession
            ? GameSessionState.Current.currentCash
            : 0f;

        private void OnEnable()
        {
            if (view == null)
                view = GetComponent<EquipmentScreenView>();

            filter = EquipmentFilter.All;
            view?.SetContext(!WasOpenedFromMenu(), RestaurantName());
            LoadOwnedEquipmentIds();
            cartEquipmentIds.Clear();
            PopulateEquipments();
            BindActions();
            RefreshScreen();
            view?.ApplyFilter(filter);
        }

        private static string RestaurantName() => !GameSessionState.HasSession ? string.Empty : GameSessionState.Current.restaurantType switch
        { RestaurantType.PODRAO => "Restaurante de lanches", RestaurantType.JAPONES => "Restaurante japonês", _ => "Restaurante francês" };

        private static bool WasOpenedFromMenu()
        {
            return MenuNavigator.Instance != null
                && MenuNavigator.Instance.Current == PanelId.EquipmentStore;
        }

        private void OnDisable()
        {
            if (view != null)
            {
                view.BindConfirm(null);
                view.BindCart(null);
                view.BindBankBack(null);
                view.HideCart();
                view.FilterRequested -= ChangeFilter;
                view.CartRemovalRequested -= RemoveFromCart;
            }

            UnbindCards();
        }

        private void LoadOwnedEquipmentIds()
        {
            ownedEquipmentIds.Clear();

            if (!GameSessionState.HasSession)
                return;

            EquipmentSelectionData data = EquipmentSelectionHelper.FromJson(
                GameSessionState.Current.equipmentJson
            );

            foreach (string id in EquipmentSelectionHelper.GetIds(data))
            {
                if (!string.IsNullOrWhiteSpace(id))
                    ownedEquipmentIds.Add(id);
            }
        }

        private void PopulateEquipments()
        {
            ready = false;
            UnbindCards();
            cards.Clear();
            view?.ClearCards();
            view?.ApplyFilter(filter);

            if (view == null || !view.HasRequiredReferences())
            {
                view?.SetHint("Não foi possível carregar o catálogo. Volte e tente novamente.");
                view?.SetConfirmEnabled(false);
                return;
            }

            RestaurantType restaurantType = GameSessionState.HasSession
                ? GameSessionState.Current.restaurantType
                : RestaurantType.PODRAO;

            EquipmentData[] allEquipments = Resources
                .LoadAll<EquipmentData>("Equipments")
                .Where(equipment => equipment != null)
                .ToArray();

            basicEquipments = allEquipments
                .Where(equipment => equipment.category == EquipmentCategory.BASIC)
                .ToArray();

            EquipmentData[] visibleEquipments = allEquipments
                .Where(equipment => IsVisibleForRestaurant(equipment, restaurantType))
                .OrderBy(equipment => equipment.category)
                .ThenBy(equipment => equipment.cost)
                .ThenBy(equipment => equipment.displayName)
                .ToArray();

            foreach (EquipmentData equipment in visibleEquipments)
            {
                EquipmentCardView card = view.CreateCard();
                card.Setup(equipment, Owns(equipment));
                card.SelectionRequested += OnCartToggleRequested;
                cards.Add(card);
            }

            ready = visibleEquipments.Length > 0;
            view.ApplyFilter(filter);

            view.SetHint(visibleEquipments.Length == 0
                ? "Nenhum equipamento foi encontrado em Resources/Equipments."
                : "Adicione os equipamentos desejados ao carrinho.");
        }

        private void BindActions()
        {
            if (view == null)
                return;

            view.BindConfirm(OnContinue);
            view.BindCart(OpenCart);
            view.BindBankBack(ReturnToBank);
            view.FilterRequested -= ChangeFilter;
            view.FilterRequested += ChangeFilter;
            view.CartRemovalRequested -= RemoveFromCart;
            view.CartRemovalRequested += RemoveFromCart;
        }

        private void ChangeFilter(EquipmentFilter selected) { filter = selected; view.ApplyFilter(filter); }
        private void RemoveFromCart(string id)
        {
            if (!cartEquipmentIds.Remove(id)) return;
            RemoveInvalidSpecificItemsFromCart(); RefreshScreen(); OpenCart();
            view.SetHint(cartEquipmentIds.Count == 0 ? "Seu carrinho está vazio." : $"{cartEquipmentIds.Count} unidade(s) no carrinho.");
        }

        private void ReturnToBank()
        {
            var machine = GameManager.Instance?.StateMachine;
            if (machine?.CurrentState == GameState.Initial_Equipment)
                machine.TryChangeState(GameState.Initial_Capital);
        }

        private void OnCartToggleRequested(EquipmentCardView card)
        {
            EquipmentData equipment = card != null ? card.Equipment : null;

            if (!GameSessionState.HasActiveSession || equipment == null || Owns(equipment))
                return;

            if (string.IsNullOrWhiteSpace(equipment.id))
            {
                Debug.LogError($"O equipamento '{equipment.displayName}' precisa ter um ID antes de ser comprado.");
                view.SetHint("Este equipamento ainda não possui um ID válido.");
                return;
            }

            if (equipment.category == EquipmentCategory.SPECIFIC && !HasAllBasicEquipmentsSelected())
            {
                view.SetHint("Adicione todos os equipamentos básicos antes dos específicos.");
                RefreshScreen();
                return;
            }

            if (!cartEquipmentIds.Add(equipment.id))
            {
                cartEquipmentIds.Remove(equipment.id);
                RemoveInvalidSpecificItemsFromCart();
            }

            view.SetHint(cartEquipmentIds.Count == 0
                ? "Seu carrinho está vazio."
                : $"{cartEquipmentIds.Count} item(ns) no carrinho.");
            RefreshScreen();
        }

        private void RefreshScreen()
        {
            if (view == null)
                return;

            bool hasAllBasicEquipmentsSelected = HasAllBasicEquipmentsSelected();

            foreach (EquipmentCardView card in cards)
            {
                if (card == null || card.Equipment == null)
                    continue;

                card.SetOwned(Owns(card.Equipment));
                card.SetInCart(cartEquipmentIds.Contains(card.Equipment.id));
                bool prerequisiteMet = card.Equipment.category == EquipmentCategory.BASIC
                    || hasAllBasicEquipmentsSelected;
                card.SetCartAvailable(ready && GameSessionState.HasActiveSession && prerequisiteMet);
            }

            float cartTotal = GetCartTotal();
            view.SetCartSummary(AvailableCash, CartItems(), GameSessionState.HasActiveSession && ready);
            view.SetConfirmEnabled(GameSessionState.HasActiveSession && ready);

            if (!GameSessionState.HasActiveSession)
                view.SetHint("Sessão indisponível. Volte ao menu para continuar.");

            if (cartTotal > AvailableCash)
                view.SetHint("Saldo insuficiente para concluir a compra do carrinho.");
        }

        private void OnContinue()
        {
            if (!ready || !GameSessionState.HasActiveSession) return;
            if (cartEquipmentIds.Count > 0)
            {
                OpenCart();
                return;
            }

            OpenMenu();
        }

        private void OpenCart()
        {
            if (view == null)
                return;

            view.ShowCart(CartItems(), AvailableCash, CompleteCartPurchase);
        }

        private List<EquipmentData> CartItems() => cards
                .Where(card => card != null
                    && card.Equipment != null
                    && cartEquipmentIds.Contains(card.Equipment.id))
                .Select(card => card.Equipment)
                .ToList();

        private void CompleteCartPurchase()
        {
            if (!isActiveAndEnabled || !ready || !GameSessionState.HasActiveSession) return;
            float cartTotal = GetCartTotal();
            if (cartEquipmentIds.Count == 0 || cartTotal > AvailableCash
                || !HasAllBasicEquipmentsSelected() && CartItems().Any(e => e.category == EquipmentCategory.SPECIFIC))
            {
                OpenCart();
                return;
            }

            int purchasedCount = cartEquipmentIds.Count;
            foreach (string equipmentId in cartEquipmentIds)
                ownedEquipmentIds.Add(equipmentId);

            var data = new EquipmentSelectionData
            {
                equipmentIds = ownedEquipmentIds.ToList()
            };

            GameSessionState.SetEquipmentJson(EquipmentSelectionHelper.ToJson(data), false);
            GameSessionState.SetCash(AvailableCash - cartTotal, false);
            GameSessionState.Save();
            cartEquipmentIds.Clear();
            view.HideCart();
            view.SetHint($"Compra concluída: {purchasedCount} item(ns) adquirido(s).");
            RefreshScreen();
            OpenMenu();
        }

        private static void OpenMenu()
        {
            var machine=GameManager.Instance!=null?GameManager.Instance.StateMachine:null;
            if(machine!=null && machine.CurrentState==GameState.Initial_Equipment)
            {
                machine.TryChangeState(GameState.Initial_Team);
                return;
            }
            if (MenuNavigator.Instance == null)
            {
                Debug.LogWarning("[EquipmentScreenController] MenuNavigator nao esta ativo na cena.");
                return;
            }

            MenuNavigator.Instance.Back();
        }

        private bool Owns(EquipmentData equipment)
        {
            return equipment != null
                && !string.IsNullOrWhiteSpace(equipment.id)
                && ownedEquipmentIds.Contains(equipment.id);
        }

        private bool HasAllBasicEquipmentsSelected()
        {
            return basicEquipments.Length > 0 && basicEquipments.All(equipment =>
                Owns(equipment) || cartEquipmentIds.Contains(equipment.id));
        }

        private float GetCartTotal()
        {
            return cards
                .Where(card => card != null
                    && card.Equipment != null
                    && cartEquipmentIds.Contains(card.Equipment.id))
                .Sum(card => Mathf.Max(0, card.Equipment.cost));
        }

        private void RemoveInvalidSpecificItemsFromCart()
        {
            if (HasAllBasicEquipmentsSelected())
                return;

            foreach (EquipmentCardView card in cards)
            {
                if (card != null && card.Equipment != null
                    && card.Equipment.category == EquipmentCategory.SPECIFIC)
                    cartEquipmentIds.Remove(card.Equipment.id);
            }
        }

        private void UnbindCards()
        {
            foreach (EquipmentCardView card in cards)
            {
                if (card != null)
                    card.SelectionRequested -= OnCartToggleRequested;
            }
        }

        private static bool IsVisibleForRestaurant(
            EquipmentData equipment,
            RestaurantType restaurantType
        )
        {
            if (equipment.category == EquipmentCategory.BASIC)
                return true;

            return equipment.category == EquipmentCategory.SPECIFIC
                && equipment.applicableTypes != null
                && equipment.applicableTypes.Contains(restaurantType);
        }
    }
}
