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

        private float AvailableCash => GameSessionState.HasSession
            ? GameSessionState.Current.currentCash
            : 0f;

        private void OnEnable()
        {
            if (view == null)
                view = GetComponent<EquipmentScreenView>();

            view?.SetFooterVisible(!WasOpenedFromMenu());
            LoadOwnedEquipmentIds();
            cartEquipmentIds.Clear();
            PopulateEquipments();
            BindActions();
            RefreshScreen();
        }

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
                view.BindBack(null);
                view.BindCart(null);
                view.HideCart();
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
            UnbindCards();
            cards.Clear();

            if (view == null || !view.HasRequiredReferences())
            {
                Debug.LogError("EquipmentScreenView nao possui todas as referencias obrigatorias.");
                return;
            }

            view.ClearCards();

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

            view.SetHint(visibleEquipments.Length == 0
                ? "Nenhum equipamento foi encontrado em Resources/Equipments."
                : "Adicione os equipamentos desejados ao carrinho.");
        }

        private void BindActions()
        {
            if (view == null)
                return;

            view.BindConfirm(OnContinue);
            view.BindBack(OnBack);
            view.BindCart(OpenCart);
        }

        private void OnCartToggleRequested(EquipmentCardView card)
        {
            EquipmentData equipment = card != null ? card.Equipment : null;

            if (!GameSessionState.HasSession || equipment == null || Owns(equipment))
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
                card.SetCartAvailable(prerequisiteMet);
            }

            float cartTotal = GetCartTotal();
            view.SetCartSummary(AvailableCash, cartTotal);
            view.SetCartButtonCount(cartEquipmentIds.Count);
            view.SetConfirmEnabled(true);

            if (cartTotal > AvailableCash)
                view.SetHint("Saldo insuficiente para concluir a compra do carrinho.");
        }

        private void OnContinue()
        {
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

            List<EquipmentData> cartItems = cards
                .Where(card => card != null
                    && card.Equipment != null
                    && cartEquipmentIds.Contains(card.Equipment.id))
                .Select(card => card.Equipment)
                .ToList();

            view.ShowCart(cartItems, AvailableCash, CompleteCartPurchase);
        }

        private void CompleteCartPurchase()
        {
            float cartTotal = GetCartTotal();
            if (cartEquipmentIds.Count == 0 || cartTotal > AvailableCash)
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
            if (MenuNavigator.Instance == null)
            {
                Debug.LogWarning("[EquipmentScreenController] MenuNavigator nao esta ativo na cena.");
                return;
            }

            MenuNavigator.Instance.OpenRoot();
        }

        private void OnBack()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.StateMachine.TryChangeState(GameState.Initial_Capital);
        }

        private bool Owns(EquipmentData equipment)
        {
            return equipment != null
                && !string.IsNullOrWhiteSpace(equipment.id)
                && ownedEquipmentIds.Contains(equipment.id);
        }

        private bool OwnsAllBasicEquipments()
        {
            return basicEquipments.Length > 0 && basicEquipments.All(Owns);
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
