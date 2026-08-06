using System.Collections.Generic;
using System.Linq;
using Game.Adapter.In.UI;
using UnityEngine;

namespace Game.Adapter.In.Controllers
{
    public sealed class EquipmentScreenController : MonoBehaviour
    {
        [SerializeField] private EquipmentScreenView view;

        private readonly List<EquipmentCardView> cards = new();
        private readonly HashSet<string> ownedEquipmentIds = new();
        private EquipmentData[] basicEquipments = System.Array.Empty<EquipmentData>();

        private float AvailableCash => GameSessionState.HasSession
            ? GameSessionState.Current.currentCash
            : 0f;

        private void OnEnable()
        {
            if (view == null)
                view = GetComponent<EquipmentScreenView>();

            LoadOwnedEquipmentIds();
            PopulateEquipments();
            BindActions();
            RefreshScreen();
        }

        private void OnDisable()
        {
            if (view != null)
            {
                view.BindConfirm(null);
                view.BindBack(null);
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
                card.SelectionRequested += OnPurchaseRequested;
                cards.Add(card);
            }

            view.SetHint(visibleEquipments.Length == 0
                ? "Nenhum equipamento foi encontrado em Resources/Equipments."
                : "Equipe sua cozinha!");
        }

        private void BindActions()
        {
            if (view == null)
                return;

            view.BindConfirm(OnContinue);
            view.BindBack(OnBack);
        }

        private void OnPurchaseRequested(EquipmentCardView card)
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

            if (equipment.category == EquipmentCategory.SPECIFIC && !OwnsAllBasicEquipments())
            {
                view.SetHint("Compre todos os equipamentos básicos antes dos específicos.");
                RefreshScreen();
                return;
            }

            float price = Mathf.Max(0, equipment.cost);

            if (AvailableCash < price)
            {
                view.SetHint("Caixa insuficiente para comprar este equipamento.");
                RefreshScreen();
                return;
            }

            ownedEquipmentIds.Add(equipment.id);

            var data = new EquipmentSelectionData
            {
                equipmentIds = ownedEquipmentIds.ToList()
            };

            GameSessionState.SetEquipmentJson(EquipmentSelectionHelper.ToJson(data), false);
            GameSessionState.SetCash(AvailableCash - price, false);
            GameSessionState.Save();

            card.SetOwned(true);
            view.SetHint($"{equipment.displayName} comprado com sucesso.");
            RefreshScreen();
        }

        private void RefreshScreen()
        {
            if (view == null)
                return;

            bool ownsAllBasics = OwnsAllBasicEquipments();

            foreach (EquipmentCardView card in cards)
            {
                if (card == null || card.Equipment == null)
                    continue;

                EquipmentData equipment = card.Equipment;
                bool hasEnoughCash = AvailableCash >= Mathf.Max(0, equipment.cost);
                bool prerequisitesMet = equipment.category == EquipmentCategory.BASIC || ownsAllBasics;

                card.SetOwned(Owns(equipment));
                card.SetPurchaseAvailable(hasEnoughCash && prerequisitesMet);
            }

            view.SetAvailableCash(AvailableCash);
            view.SetConfirmEnabled(true);
        }

        private void OnContinue()
        {
            PresentationMenuOverlay.Show();
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

        private void UnbindCards()
        {
            foreach (EquipmentCardView card in cards)
            {
                if (card != null)
                    card.SelectionRequested -= OnPurchaseRequested;
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
