using System;
using System.Collections.Generic;
using System.Linq;
using Game.Adapter.In.UI;
using Game.Adapter.In.UI.Navigation;
using UnityEngine;

namespace Game.Adapter.In.Controllers
{
    public sealed class TeamScreenController : MonoBehaviour
    {
        [SerializeField] private TeamScreenView view;
        private readonly List<TeamRoleCardView> cards = new();
        private List<RoleData> roles = new();
        private RestaurantData restaurant;
        private TeamSelectionData team;
        private bool ready;
        private GameSessionService Service => new(GameSessionState.Current.userId, GameSessionState.Current.professorId);
        private void OnEnable()
        {
            if (view == null) view = GetComponent<TeamScreenView>();
            Populate(); view?.BindConfirm(Confirm);
        }
        private void OnDisable() { view?.BindConfirm(null); foreach (var card in cards) if (card != null) card.QuantityRequested -= SetQuantity; }
        private void Populate()
        {
            ready = false;
            foreach (var card in cards) if (card != null) card.QuantityRequested -= SetQuantity;
            cards.Clear(); view?.ClearCards();
            if (view == null) return;
            restaurant = GameSessionState.HasSession ? Resources.LoadAll<RestaurantData>("Restaurants").FirstOrDefault(r => r != null && r.type == GameSessionState.Current.restaurantType) : null;
            view.SetContext(GameManager.Instance?.StateMachine?.CurrentState == GameState.Initial_Team, restaurant != null ? restaurant.displayName : "Restaurante indisponível");
            roles = Resources.LoadAll<RoleData>("Roles").Where(r => r != null).OrderBy(r => r.roleType).ToList();
            try
            {
                team = GameSessionState.HasSession ? TeamSelectionHelper.FromJson(GameSessionState.Current.teamJson) : new TeamSelectionData();
                ready = view.HasRequiredReferences() && roles.Count == 5 && roles.Select(r => r.id).Distinct().Count() == roles.Count
                    && roles.All(r => !string.IsNullOrWhiteSpace(r.id) && r.salary >= 0 && r.maxClientsSupported >= 0 && view.HasVisual(r.id))
                    && team.members.All(m => m != null && m.quantity >= 0 && roles.Any(r => r.id == m.roleId))
                    && team.members.Select(m => m.roleId).Distinct().Count() == team.members.Count;
            }
            catch (Exception) { team = new TeamSelectionData(); }
            if (ready) foreach (var role in roles)
            {
                int required = restaurant?.requiredRoles?.Where(r => r != null && r.roleId == role.id).Sum(r => r.quantity) ?? 0;
                var card = view.CreateCard(role, required); card.QuantityRequested += SetQuantity; cards.Add(card);
            }
            Refresh();
            GetComponent<ContextualHelp>()?.Refresh(Available, team.members.Any(m => m.quantity > 0));
        }
        private bool Available => ready && restaurant != null && GameSessionState.HasActiveSession && GameSessionState.IsPersisted;
        private void SetQuantity(TeamRoleCardView card, int quantity)
        {
            if (!isActiveAndEnabled || !Available || card?.Role == null || quantity < 0) return;
            Service.SetTeamMemberQuantity(card.Role, quantity);
            GetComponent<ContextualHelp>()?.Complete();
            team = TeamSelectionHelper.FromJson(GameSessionState.Current.teamJson); Refresh();
        }
        private void Refresh()
        {
            bool available = Available;
            foreach (var card in cards) card.SetQuantity(TeamSelectionHelper.GetMemberQuantity(team, card.Role.id), available, ready && restaurant != null && GameSessionState.HasSession);
            if (!available)
            {
                view.SetSummary(0, 0, 0, null, "Confira a sessão, o restaurante e o catálogo de funções.", false);
                view.SetHint(!GameSessionState.HasActiveSession || !GameSessionState.IsPersisted ? "Sessão indisponível. As contratações estão bloqueadas." : "Não foi possível carregar a equipe. Volte e tente novamente.", true);
                view.SetConfirmEnabled(false); return;
            }
            var alignment = AlignmentEngine.Calculate(GameSessionState.Current.restaurantType, GameSessionState.Current.targetSegment,
                GameSessionState.Current.locationZone, restaurant, MenuPricingHelper.FromJson(GameSessionState.Current.menuPricingJson), team, roles);
            int count = team.members.Sum(m => m.quantity);
            double payroll = team.members.Sum(m => (double)roles.Single(r => r.id == m.roleId).salary * m.quantity);
            long capacity = team.members.Sum(m => (long)roles.Single(r => r.id == m.roleId).maxClientsSupported * m.quantity);
            var requirements = (restaurant.requiredRoles ?? Array.Empty<RoleRequirement>()).Where(r => r != null && r.quantity > 0).ToArray();
            bool complete = requirements.All(r => TeamSelectionHelper.GetMemberQuantity(team, r.roleId) >= r.quantity);
            string detail = string.Join("\n", requirements.Select(r => $"{TeamRoleCardView.Label(roles.FirstOrDefault(role => role.id == r.roleId))}: {TeamSelectionHelper.GetMemberQuantity(team, r.roleId)}/{r.quantity}"));
            view.SetSummary(count, payroll, capacity, alignment, detail, true);
            view.SetHint(count == 0 ? "Equipe vazia. Use + para contratar. A folha será cobrada no fechamento mensal."
                : !complete ? "Equipe incompleta. Confira os mínimos do restaurante; eles afetam a capacidade e o alinhamento."
                : "Equipe atende aos mínimos. Alterações salvas; salários entram no fechamento mensal.", !complete);
            // Existing rules allow incomplete teams, with lower alignment/capacity. Do not add a new hiring gate.
            view.SetConfirmEnabled(true);
        }
        private void Confirm()
        {
            if (!isActiveAndEnabled || !Available) return;
            try
            {
                if (!Service.SaveTeamAlignment(roles)) { view.SetHint("Não foi possível salvar a equipe. Tente novamente.", true); FeedbackToast.Show("A equipe não foi confirmada. Tente novamente.", FeedbackKind.Error); return; }
                GetComponent<ContextualHelp>()?.Complete();
                FeedbackToast.Show(team.members.Sum(m => m.quantity) == 0 ? "Equipe vazia confirmada. A capacidade e o alinhamento refletem essa escolha." : "Equipe confirmada. Salários serão cobrados no fechamento mensal.", team.members.Sum(m => m.quantity) == 0 ? FeedbackKind.Warning : FeedbackKind.Success);
                var machine = GameManager.Instance?.StateMachine;
                if (machine?.CurrentState == GameState.Initial_Team)
                {
                    if (!Service.ContinueInitialSetup(GameState.Management_Hub))
                        view.SetHint("Não foi possível avançar. Tente novamente; sua equipe foi preservada.", true);
                }
                else MenuNavigator.Instance?.Back();
            }
            catch (Exception) { view.SetHint("Não foi possível confirmar a equipe. Confira os dados e tente novamente.", true); FeedbackToast.Show("A equipe não foi confirmada. Confira os dados e tente novamente.", FeedbackKind.Error); }
        }
    }
}
