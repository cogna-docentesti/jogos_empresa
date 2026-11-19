using System.Collections.Generic;
using System.Collections;
using System;
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
        private EquipmentData tutorialKit;
        private bool IsInitial => GameManager.Instance?.StateMachine?.CurrentState == GameState.Initial_Equipment;
        private EquipmentFilter filter = EquipmentFilter.All;
        private bool ready;
        private EquipmentTutorial tutorial;

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
            BindActions();
            BeginCatalogLoad();

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
            GetComponent<SharedLoading>()?.Cancel();
            if (view != null)
            {
                view.CartClosed -= OnCartClosed;
                view.BindConfirm(null);
                view.BindCart(null);
                view.BindBankBack(null);
                view.HideCart();
                view.FilterRequested -= ChangeFilter;
                view.CartRemovalRequested -= RemoveFromCart;
            }

            UnbindCards();
            tutorial?.Hide();
        }

        private void OnDestroy()
        {
            if (tutorialKit != null) Destroy(tutorialKit);
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

        public void BeginCatalogLoad()
        {
            if (FeedbackToast.Current != null && FeedbackToast.Current.Kind != FeedbackKind.Success) FeedbackToast.Current.Hide();
            var loader = GetComponent<SharedLoading>();
            if (loader == null) { PopulateEquipments(Resources.LoadAll<EquipmentData>("Equipments")); FinishCatalog(); return; }
            ready = false; view.ClearCards(); view.SetConfirmEnabled(false); view.SetCatalogLoading(true);
            tutorial?.Hide();
            GetComponent<ContextualHelp>()?.Refresh(false, false);
            loader.Run(LoadCatalog(), e => CatalogFailed(false), () => CatalogFailed(true));
        }
        private IEnumerator LoadCatalog()
        {
            var indexRequest = Resources.LoadAsync<EquipmentCatalogIndex>("EquipmentCatalogIndex");
            if (!indexRequest.isDone) yield return indexRequest;
            var index = indexRequest.asset as EquipmentCatalogIndex;
            if (index == null || index.resourcePaths.Length == 0) throw new InvalidOperationException("Equipment catalog unavailable.");
            var loaded = new List<EquipmentData>();
            foreach (string path in index.resourcePaths)
            {
                var request = Resources.LoadAsync<EquipmentData>(path);
                if (!request.isDone) yield return request;
                if (request.asset is not EquipmentData item) throw new InvalidOperationException("Equipment resource unavailable.");
                loaded.Add(item);
            }
            PopulateEquipments(loaded.ToArray()); FinishCatalog();
        }
        private void FinishCatalog()
        {
            view.SetCatalogLoading(false); view.ResetCatalogMessage(); RefreshScreen(); view.ApplyFilter(filter);
            GetComponent<ContextualHelp>()?.Refresh(ready && GameSessionState.HasActiveSession, ownedEquipmentIds.Count > 0);
            PresentTutorial();
        }
        private void CatalogFailed(bool cancelled)
        {
            if (!isActiveAndEnabled) return;
            ready = false; view.ClearCards(); cards.Clear(); view.SetCatalogLoading(false);
            tutorial?.Hide();
            RefreshScreen(); view.ShowCatalogFailure(cancelled ? "Carregamento cancelado. Tente novamente para consultar o catálogo." : "Catálogo indisponível. Tente carregar novamente.");
            FeedbackToast.Show(cancelled ? "Carregamento cancelado. Nenhuma compra foi feita." : "O catálogo não foi carregado. Nenhuma compra foi feita.", cancelled ? FeedbackKind.Warning : FeedbackKind.Error);
        }
        private void PopulateEquipments(EquipmentData[] allEquipments)
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

            basicEquipments = allEquipments
                .Where(equipment => equipment.category == EquipmentCategory.BASIC)
                .ToArray();

            EquipmentData[] visibleEquipments = allEquipments
                .Where(equipment => IsVisibleForRestaurant(equipment, restaurantType))
                .OrderBy(equipment => equipment.category)
                .ThenBy(equipment => equipment.cost)
                .ThenBy(equipment => equipment.displayName)
                .ToArray();

            if (tutorialKit != null) Destroy(tutorialKit);
            tutorialKit = null;
            if (IsInitial && basicEquipments.Length > 0)
            {
                tutorialKit = ScriptableObject.CreateInstance<EquipmentData>();
                tutorialKit.id = "__tutorial_basic_kit";
                tutorialKit.displayName = "Kit básico (" + basicEquipments.Length + " equipamentos)";
                tutorialKit.category = EquipmentCategory.BASIC;
                tutorialKit.cost = basicEquipments.Where(e => !Owns(e)).Sum(e => Mathf.Max(0, e.cost));
                tutorialKit.capacityBonus = basicEquipments.Sum(e => e.capacityBonus);
                tutorialKit.qualityBonus = basicEquipments.Sum(e => e.qualityBonus);
                tutorialKit.icon = basicEquipments.FirstOrDefault(e => e.icon != null)?.icon;
                visibleEquipments = new[] { tutorialKit }.Concat(visibleEquipments
                    .Where(e => e.category == EquipmentCategory.SPECIFIC).Take(2)).ToArray();
            }

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
                ? "Nenhum equipamento disponível no catálogo."
                : "Adicione os equipamentos desejados ao carrinho.");
        }

        private void BindActions()
        {
            if (view == null)
                return;

            view.BindConfirm(OnContinue);
            view.BindCart(OpenCart);
            view.BindBankBack(ReturnToBank);
            view.BindRetry(BeginCatalogLoad);
            view.FilterRequested -= ChangeFilter;
            view.FilterRequested += ChangeFilter;
            view.CartRemovalRequested -= RemoveFromCart;
            view.CartRemovalRequested += RemoveFromCart;
            view.CartClosed -= OnCartClosed;
            view.CartClosed += OnCartClosed;
        }

        private void PresentTutorial()
        {
            if (!isActiveAndEnabled || !IsInitial || !ready || !GameSessionState.HasActiveSession
                || !GameSessionState.IsPersisted) return;
            var session = GameSessionState.Current;
            int saved = session.equipmentTutorialStep;
            if (session.equipmentTutorialVersion < EquipmentTutorial.Version && saved >= 5)
                saved = EquipmentTutorial.CompletedStep;
            int step = EquipmentTutorial.ResumeStep(saved, OwnsAllBasicEquipments(),
                tutorialKit != null && cartEquipmentIds.Contains(tutorialKit.id), view.CartIsOpen);
            if (step >= EquipmentTutorial.CompletedStep)
            {
                if (step != session.equipmentTutorialStep || session.equipmentTutorialVersion != EquipmentTutorial.Version)
                    if (!SaveTutorialStep(step)) return;
                tutorial?.Hide(); return;
            }
            if (tutorial == null)
            {
                tutorial = EquipmentTutorial.Create(transform);
                tutorial.NextRequested += NextTutorialStep;
            }
            if ((step != session.equipmentTutorialStep || session.equipmentTutorialVersion != EquipmentTutorial.Version)
                && !SaveTutorialStep(step)) return;
            RectTransform action = step == 1 ? cards.FirstOrDefault(c => c != null && c.Equipment == tutorialKit)?.AddAction
                : step == EquipmentTutorial.ReviewStep ? view.CartAction
                : step == EquipmentTutorial.CheckoutStep ? view.CheckoutAction : null;
            tutorial.SetDock(view.TutorialScroll);
            tutorial.Show(step, action);
            view.RevealAction(action);
            tutorial.RefreshLayout();
        }

        private bool SaveTutorialStep(int step)
        {
            if (!GameSessionState.HasActiveSession || !GameSessionState.IsPersisted) return false;
            int previous = GameSessionState.Current.equipmentTutorialStep;
            int previousVersion = GameSessionState.Current.equipmentTutorialVersion;
            GameSessionState.Current.equipmentTutorialStep = step;
            GameSessionState.Current.equipmentTutorialVersion = EquipmentTutorial.Version;
            if (GameSessionState.TrySave(out string error)) return true;
            GameSessionState.Current.equipmentTutorialStep = previous;
            GameSessionState.Current.equipmentTutorialVersion = previousVersion;
            FeedbackToast.Show(error, FeedbackKind.Error);
            return false;
        }

        private void NextTutorialStep()
        {
            if (!IsInitial || !ready || !GameSessionState.HasActiveSession) return;
            int step = GameSessionState.Current.equipmentTutorialStep;
            if (step == EquipmentTutorial.PurchasedStep)
            {
                if (!OwnsAllBasicEquipments() || !SaveTutorialStep(EquipmentTutorial.CompletedStep)) return;
                tutorial.Hide();
                view.SetHint("Explore os equipamentos básicos e específicos. Quando terminar, continue para a equipe.");
                return;
            }
            // Action steps only advance from the actual store/cart events.
            if (step == 0 || step == 3)
                if (SaveTutorialStep(step + 1)) PresentTutorial();
        }

        private void OnCartClosed()
        {
            if (!isActiveAndEnabled || !IsInitial || !GameSessionState.HasActiveSession) return;
            if (!OwnsAllBasicEquipments() && GameSessionState.Current.equipmentTutorialStep >= 3
                && GameSessionState.Current.equipmentTutorialStep < EquipmentTutorial.PurchasedStep)
                SaveTutorialStep(EquipmentTutorial.ReviewStep);
            PresentTutorial();
        }

        private void ChangeFilter(EquipmentFilter selected) { filter = selected; view.ApplyFilter(filter); }
        private void RemoveFromCart(string id)
        {
            if (tutorialKit != null && basicEquipments.Any(e => e.id == id)
                && cartEquipmentIds.Contains(tutorialKit.id)) id = tutorialKit.id;
            if (!cartEquipmentIds.Remove(id)) return;
            RemoveInvalidSpecificItemsFromCart(); RefreshScreen();
            if (IsInitial && !HasAllBasicEquipmentsSelected()
                && GameSessionState.Current.equipmentTutorialStep < EquipmentTutorial.CompletedStep)
            {
                SaveTutorialStep(1); view.HideCart(); PresentTutorial(); return;
            }
            OpenCart();
            view.SetHint(cartEquipmentIds.Count == 0 ? "Seu carrinho está vazio." : $"{cartEquipmentIds.Count} unidade(s) no carrinho.");
        }

        private void ReturnToBank()
        {
            var machine = GameManager.Instance?.StateMachine;
            if (machine?.CurrentState == GameState.Initial_Equipment)
                AdvanceSetup(GameState.Initial_Capital);
        }

        private void OnCartToggleRequested(EquipmentCardView card)
        {
            EquipmentData equipment = card != null ? card.Equipment : null;

            if (!GameSessionState.HasActiveSession || equipment == null || Owns(equipment))
                return;

            if (string.IsNullOrWhiteSpace(equipment.id))
            {
                Debug.LogError($"O equipamento '{equipment.displayName}' precisa ter um ID antes de ser comprado.");
                view.SetHint("Este equipamento está indisponível para compra.");
                FeedbackToast.Show("Equipamento indisponível para compra.", FeedbackKind.Error);
                return;
            }

            if (equipment.category == EquipmentCategory.SPECIFIC && !HasAllBasicEquipmentsSelected())
            {
                view.SetHint("Adicione todos os equipamentos básicos antes dos específicos.");
                FeedbackToast.Show("Adicione os equipamentos básicos antes dos específicos.", FeedbackKind.Warning);
                RefreshScreen();
                return;
            }

            if (!cartEquipmentIds.Add(equipment.id))
            {
                cartEquipmentIds.Remove(equipment.id);
                RemoveInvalidSpecificItemsFromCart();
            }
            GetComponent<ContextualHelp>()?.Complete();

            view.SetHint(cartEquipmentIds.Count == 0
                ? "Seu carrinho está vazio."
                : $"{cartEquipmentIds.Count} item(ns) no carrinho.");
            RefreshScreen();
            if (IsInitial && GameSessionState.Current.equipmentTutorialStep == 1 && HasAllBasicEquipmentsSelected())
            {
                if (SaveTutorialStep(EquipmentTutorial.ReviewStep)) PresentTutorial();
                else
                {
                    cartEquipmentIds.Remove(equipment.id);
                    RemoveInvalidSpecificItemsFromCart(); RefreshScreen();
                }
            }
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
            view.SetConfirmEnabled(GameSessionState.HasActiveSession && ready
                && (!IsInitial || OwnsAllBasicEquipments() || HasAllBasicEquipmentsSelected()));

            if (!GameSessionState.HasActiveSession)
                view.SetHint("Sessão indisponível. As compras estão bloqueadas.");

            if (cartTotal > AvailableCash)
                view.SetHint("Saldo insuficiente para concluir a compra do carrinho.");
        }

        private void OnContinue()
        {
            if (!ready || !GameSessionState.HasActiveSession) return;
            GetComponent<ContextualHelp>()?.Complete();
            if (cartEquipmentIds.Count > 0)
            {
                OpenCart();
                return;
            }

            if (IsInitial)
            {
                if (!OwnsAllBasicEquipments())
                {
                    view.SetHint("Compre o kit básico no carrinho antes de continuar.");
                    return;
                }
                if (GameSessionState.Current.equipmentTutorialStep < EquipmentTutorial.CompletedStep)
                { PresentTutorial(); return; }
                AdvanceSetup(GameState.Initial_Team);
            }
            else OpenMenu();
        }

        private void OpenCart()
        {
            if (view == null)
                return;

            if (IsInitial && GameSessionState.HasActiveSession
                && GameSessionState.Current.equipmentTutorialStep < EquipmentTutorial.ReviewStep) return;

            view.ShowCart(CartItems(), AvailableCash, CompleteCartPurchase);
            if (view.CartIsOpen && IsInitial && GameSessionState.HasActiveSession
                && GameSessionState.Current.equipmentTutorialStep == EquipmentTutorial.ReviewStep)
            {
                if (SaveTutorialStep(3)) PresentTutorial();
                else view.HideCart();
            }
            else if (view.CartIsOpen && (!IsInitial || GameSessionState.Current.equipmentTutorialStep >= EquipmentTutorial.CompletedStep))
                tutorial?.Hide();
        }

        private List<EquipmentData> CartItems()
        {
            var items = cards
                .Where(card => card != null
                    && card.Equipment != null
                    && cartEquipmentIds.Contains(card.Equipment.id))
                .Select(card => card.Equipment)
                .ToList();
            if (tutorialKit != null && items.Remove(tutorialKit))
                items.AddRange(basicEquipments.Where(e => !Owns(e)));
            return items;
        }

        private void CompleteCartPurchase()
        {
            if (!isActiveAndEnabled || !ready || !GameSessionState.HasActiveSession) return;
            if (IsInitial && GameSessionState.Current.equipmentTutorialStep < EquipmentTutorial.CheckoutStep) return;
            // A repeated checkout callback must not reopen a modal over the final explanation.
            if (cartEquipmentIds.Count == 0) return;
            float cartTotal = GetCartTotal();
            if (cartTotal > AvailableCash
                || !HasAllBasicEquipmentsSelected() && CartItems().Any(e => e.category == EquipmentCategory.SPECIFIC))
            {
                OpenCart();
                return;
            }

            int purchasedCount = cartEquipmentIds.Count;
            foreach (string equipmentId in cartEquipmentIds)
            {
                if (tutorialKit != null && equipmentId == tutorialKit.id)
                    foreach (var basic in basicEquipments) ownedEquipmentIds.Add(basic.id);
                else ownedEquipmentIds.Add(equipmentId);
            }

            var data = new EquipmentSelectionData
            {
                equipmentIds = ownedEquipmentIds.ToList()
            };

            GameSessionState.SetEquipmentJson(EquipmentSelectionHelper.ToJson(data), false);
            GameSessionState.SetCash(AvailableCash - cartTotal, false);
            if (IsInitial && GameSessionState.Current.equipmentTutorialStep < EquipmentTutorial.CompletedStep)
                GameSessionState.Current.equipmentTutorialStep = EquipmentTutorial.PurchasedStep;
            GameSessionState.Save();
            cartEquipmentIds.Clear();
            view.HideCart();
            view.SetHint($"Compra concluída: {purchasedCount} item(ns) adquirido(s).");
            FeedbackToast.Show($"Compra concluída: {purchasedCount} equipamento(s) adquirido(s).", FeedbackKind.Success);
            RefreshScreen();
            if (IsInitial) PresentTutorial();
            else OpenMenu();
        }

        private void OpenMenu()
        {
            var machine=GameManager.Instance!=null?GameManager.Instance.StateMachine:null;
            if(machine!=null && machine.CurrentState==GameState.Initial_Equipment)
            {
                AdvanceSetup(GameState.Initial_Team);
                return;
            }
            if (MenuNavigator.Instance == null)
            {
                Debug.LogWarning("[EquipmentScreenController] MenuNavigator nao esta ativo na cena.");
                return;
            }

            MenuNavigator.Instance.Back();
        }

        private void AdvanceSetup(GameState next)
        {
            try
            {
                var s = GameSessionState.Current;
                if (new GameSessionService(s.userId, s.professorId).ContinueInitialSetup(next)) return;
            }
            catch (Exception) { /* Keep the current screen and the already committed purchase. */ }
            view.SetHint("Não foi possível avançar. Tente novamente; suas compras foram preservadas.");
            FeedbackToast.Show("Não foi possível avançar. Tente novamente.", FeedbackKind.Error);
        }

        private bool Owns(EquipmentData equipment)
        {
            if (tutorialKit != null && equipment == tutorialKit) return OwnsAllBasicEquipments();
            return equipment != null
                && !string.IsNullOrWhiteSpace(equipment.id)
                && ownedEquipmentIds.Contains(equipment.id);
        }

        private bool OwnsAllBasicEquipments() => basicEquipments.Length > 0
            && basicEquipments.All(e => ownedEquipmentIds.Contains(e.id));

        private bool HasAllBasicEquipmentsSelected()
        {
            return basicEquipments.Length > 0 && basicEquipments.All(equipment =>
                Owns(equipment) || cartEquipmentIds.Contains(equipment.id)
                || (tutorialKit != null && cartEquipmentIds.Contains(tutorialKit.id)));
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
