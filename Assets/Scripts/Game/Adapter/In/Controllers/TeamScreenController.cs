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
        private Dictionary<string, int> minimums;
        private bool ready;
        private EquipmentTutorial tutorial;
        private const int Intro = 0, FirstHire = 1, Payroll = 2, Minimums = 3, ConfirmTeam = 4;
        private static readonly (string title, string body)[] TutorialCopy =
        {
            ("O que a equipe muda", "A equipe ajuda a definir quantos clientes seu restaurante consegue atender. Cada função contribui para a capacidade e a qualidade, e os equipamentos também influenciam o atendimento."),
            ("Faça a primeira contratação", "Cada tipo de restaurante precisa de uma equipe diferente. Role os cartões se precisar e clique no + de uma das funções que ainda faltam."),
            ("Confira a folha mensal", "Olhe como a folha mudou com a contratação. Esse valor será cobrado no fechamento de cada mês. Você pode voltar ao RH entre as rodadas para ajustar a equipe."),
            ("Complete a equipe inicial", "No resumo destacado, confira quantas pessoas você já contratou e qual é o mínimo de cada função. Use o + nos cartões para completar o que falta."),
            ("Confirme e abra o hub", "Sua equipe está pronta para começar. Confira a capacidade de atendimento e a folha mensal antes de abrir o hub.")
        };
        private bool IsInitial => GameManager.Instance?.StateMachine?.CurrentState == GameState.Initial_Team;

        public static bool HasMinimum(RestaurantData restaurant, TeamSelectionData team, IEnumerable<RoleData> roles)
        {
            var ids = new HashSet<string>(roles.Where(r => r != null).Select(r => r.id));
            return restaurant?.requiredRoles != null && restaurant.requiredRoles.Length > 0
                && restaurant.requiredRoles.All(r => r != null && ids.Contains(r.roleId)
                    && TeamSelectionHelper.GetMemberQuantity(team, r.roleId) >= r.quantity);
        }
        private GameSessionService Service => new(GameSessionState.Current.userId, GameSessionState.Current.professorId);
        private void OnEnable()
        {
            if (view == null) view = GetComponent<TeamScreenView>();
            Populate(); view?.BindConfirm(Confirm);
            view?.BindSetupBack(IsInitial ? new UnityEngine.Events.UnityAction(BackToEquipment) : null);

        }
        private void OnDisable()
        {
            if (tutorial != null) tutorial.Hide();
            view?.BindConfirm(null);
            view?.BindSetupBack(null);
            foreach (var card in cards) if (card != null) card.QuantityRequested -= SetQuantity;
        }
        private void Populate()
        {
            ready = false;
            foreach (var card in cards) if (card != null) card.QuantityRequested -= SetQuantity;
            cards.Clear(); view?.ClearCards();
            if (view == null) return;
            restaurant = GameSessionState.HasSession ? Resources.LoadAll<RestaurantData>("Restaurants").FirstOrDefault(r => r != null && r.type == GameSessionState.Current.restaurantType) : null;
            view.SetContext(GameManager.Instance?.StateMachine?.CurrentState == GameState.Initial_Team, restaurant != null ? restaurant.displayName : "Restaurante indisponível");
            roles = Resources.LoadAll<RoleData>("Roles").Where(r => r != null).OrderBy(r => r.roleType).ToList();
            var requirements = restaurant?.requiredRoles ?? Array.Empty<RoleRequirement>();
            try
            {
                team = GameSessionState.HasSession ? TeamSelectionHelper.FromJson(GameSessionState.Current.teamJson) : new TeamSelectionData();
                ready = view.HasRequiredReferences() && roles.Count > 0 && roles.Select(r => r.id).Distinct().Count() == roles.Count
                    && roles.All(r => !string.IsNullOrWhiteSpace(r.id) && r.salary >= 0 && r.maxClientsSupported >= 0
                        && r.qualityContribution >= 0f && r.qualityContribution <= 1f)
                    && requirements.All(req => req != null && req.quantity >= 0 && roles.Any(r => r.id == req.roleId))
                    && team.members.All(m => m != null && m.quantity >= 0 && roles.Any(r => r.id == m.roleId))
                    && team.members.Select(m => m.roleId).Distinct().Count() == team.members.Count;
                if (ready)
                    minimums = restaurant != null ? TeamSelectionHelper.GetMinimums(restaurant) : new Dictionary<string, int>();
            }
            catch (Exception) { team = new TeamSelectionData(); ready = false; }
            if (ready)
            {
                var catalog = roles.Select(role => new
                {
                    Role = role,
                    Minimum = minimums.TryGetValue(role.id, out int minimum) ? minimum : 0,
                    Hired = TeamSelectionHelper.GetMemberQuantity(team, role.id)
                }).Where(entry => restaurant == null || entry.Minimum > 0 || entry.Role.sharedAcrossRestaurants || entry.Hired > 0)
                  .OrderByDescending(entry => entry.Minimum > 0).ThenByDescending(entry => entry.Role.sharedAcrossRestaurants)
                  .ToList();

                ready = catalog.Count > 0 && catalog.All(entry => view.HasVisual(entry.Role.id));
                if (ready) foreach (var entry in catalog)
                {
                    bool removalOnly = restaurant != null && entry.Minimum == 0 && !entry.Role.sharedAcrossRestaurants;
                    var card = view.CreateCard(entry.Role, entry.Minimum, removalOnly);
                    card.QuantityRequested += SetQuantity;
                    cards.Add(card);
                }
            }
            Refresh();
            GetComponent<ContextualHelp>()?.Refresh(Available, team.members.Any(m => m != null && m.quantity > 0));
        }
        private bool Available => ready && restaurant != null && GameSessionState.HasActiveSession && GameSessionState.IsPersisted;
        private int CurrentTutorialStep(bool hasMinimum)
        {
            int saved = Mathf.Clamp(GameSessionState.Current.teamTutorialStep, Intro, Minimums);
            if (saved == FirstHire && team.members.Any(member => member.quantity > 0)) return Payroll;
            return saved >= Minimums ? (hasMinimum ? ConfirmTeam : Minimums) : saved;
        }

        private void PresentTutorial(int step)
        {
            if (tutorial == null)
            {
                tutorial = EquipmentTutorial.Create(view.transform, "TeamTutorial");
                tutorial.NextRequested += AdvanceTutorial;
            }
            var highlights = step switch
            {
                FirstHire => cards.Where(card => card.NeedsHire).Select(card => card.AddHighlight).ToArray(),
                Payroll => new[] { view.PayrollHighlight },
                Minimums => new[] { view.RequirementsHighlight },
                ConfirmTeam => new[] { view.ConfirmHighlight },
                _ => Array.Empty<RectTransform>()
            };
            var copy = TutorialCopy[step];
            tutorial.SetDock(view.TutorialScroll);
            tutorial.ShowInline($"EQUIPE  ·  {step + 1} DE {TutorialCopy.Length}", copy.title, copy.body,
                step == Intro || step == Payroll, highlights);
        }

        private void AdvanceTutorial()
        {
            if (!Available || !IsInitial) return;
            int step = CurrentTutorialStep(HasMinimum(restaurant, team, roles));
            if (step != Intro && step != Payroll) return;
            var session = GameSessionState.Current;
            int previous = session.teamTutorialStep;
            session.teamTutorialStep = step + 1;
            if (!GameSessionState.TrySave(out string error))
            {
                session.teamTutorialStep = previous;
                FeedbackToast.Show(error, FeedbackKind.Error);
                return;
            }
            Refresh();
        }

        private void SetQuantity(TeamRoleCardView card, int quantity)
        {
            if (!isActiveAndEnabled || !Available || card?.Role == null || quantity < 0) return;
            if (card.RemovalOnly && quantity >= card.Quantity) return;
            if (IsInitial)
            {
                int step = CurrentTutorialStep(HasMinimum(restaurant, team, roles));
                if (step == Intro || step == Payroll) return;
                var session = GameSessionState.Current;
                string previousTeam = session.teamJson;
                int previousStep = session.teamTutorialStep;
                Service.SetTeamMemberQuantity(card.Role, quantity, save: false);
                if (step == FirstHire && quantity > card.Quantity) session.teamTutorialStep = Payroll;
                if (!GameSessionState.TrySave(out string error))
                {
                    GameSessionState.SetTeamJson(previousTeam, save: false);
                    session.teamTutorialStep = previousStep;
                    FeedbackToast.Show(error, FeedbackKind.Error); return;
                }
            }
            else Service.SetTeamMemberQuantity(card.Role, quantity);
            GetComponent<ContextualHelp>()?.Complete();
            if (card.RemovalOnly && quantity == 0)
            {
                Populate();
                return;
            }
            team = TeamSelectionHelper.FromJson(GameSessionState.Current.teamJson);
            Refresh();
        }
        private void Refresh()
        {
            bool available = Available;
            bool initial = available && IsInitial;
            int step = initial ? CurrentTutorialStep(HasMinimum(restaurant, team, roles)) : Intro;
            bool canEdit = available && (!initial || (step != Intro && step != Payroll));
            foreach (var card in cards) card.SetQuantity(TeamSelectionHelper.GetMemberQuantity(team, card.Role.id), canEdit, ready && restaurant != null && GameSessionState.HasSession);
            if (!available)
            {
                if (tutorial != null) tutorial.Hide();
                view.SetSummary(0, 0, 0, null, "Confira a sessão, o restaurante e o catálogo de funções.", false);
                view.SetHint(!GameSessionState.HasActiveSession || !GameSessionState.IsPersisted ? "Sessão indisponível. As contratações estão bloqueadas." : "Não foi possível carregar a equipe. Volte e tente novamente.", true);
                view.SetConfirmEnabled(false); return;
            }
            var alignment = AlignmentEngine.Calculate(GameSessionState.Current.restaurantType, GameSessionState.Current.targetSegment,
                GameSessionState.Current.locationZone, restaurant, MenuPricingHelper.FromJson(GameSessionState.Current.menuPricingJson), team, roles);
            long count = team.members.Sum(m => (long)m.quantity);
            double payroll = team.members.Sum(m => (double)roles.Single(r => r.id == m.roleId).salary * m.quantity);
            long capacity = team.members.Sum(m => (long)roles.Single(r => r.id == m.roleId).maxClientsSupported * m.quantity);
            var shortfalls = TeamSelectionHelper.GetShortfalls(team, minimums);
            bool complete = shortfalls.Count == 0;
            string detail = string.Join("\n", minimums.Select(minimum =>
            {
                string label = TeamRoleCardView.Label(roles.Single(role => role.id == minimum.Key));
                int hired = TeamSelectionHelper.GetMemberQuantity(team, minimum.Key);
                string missing = shortfalls.TryGetValue(minimum.Key, out int quantity)
                    ? quantity == 1 ? "  ·  Falta 1" : $"  ·  Faltam {quantity}"
                    : "";
                return $"{label}  {hired}/{minimum.Value}{missing}";
            }));
            view.SetSummary(count, payroll, capacity, alignment, detail, true);
            view.SetHint(complete ? "Mínimos atendidos." : "Use + para completar os mínimos da equipe.", !complete);
            view.SetConfirmEnabled(complete);
            if (initial) { view.SetConfirmEnabled(complete && step == ConfirmTeam); PresentTutorial(step); }
            else tutorial?.Hide();
        }
        /// Passo 7 volta para a loja (passo 6) sem perder a equipe já escolhida.
        private void BackToEquipment()
        {
            if (!isActiveAndEnabled || !IsInitial || !GameSessionState.HasActiveSession) return;
            try
            {
                if (!Service.ContinueInitialSetup(GameState.Initial_Equipment))
                    view.SetHint("Não foi possível voltar para a loja. Tente novamente; sua equipe foi preservada.", true);
            }
            catch (Exception) { view.SetHint("Não foi possível voltar para a loja. Tente novamente; sua equipe foi preservada.", true); }
        }

        private void Confirm()
        {
            if (!isActiveAndEnabled || !Available || (IsInitial && CurrentTutorialStep(HasMinimum(restaurant, team, roles)) != ConfirmTeam)) return;
            try
            {
                team = TeamSelectionHelper.FromJson(GameSessionState.Current.teamJson);
                if (TeamSelectionHelper.GetShortfalls(team, minimums).Count > 0)
                {
                    Refresh();
                    return;
                }
                if (!Service.SaveTeamAlignment(roles)) { view.SetHint("Não foi possível salvar a equipe. Tente novamente.", true); FeedbackToast.Show("A equipe não foi confirmada. Tente novamente.", FeedbackKind.Error); return; }
                GetComponent<ContextualHelp>()?.Complete();
                var machine = GameManager.Instance?.StateMachine;
                if (machine?.CurrentState == GameState.Initial_Team)
                {
                    if (!Service.ContinueInitialSetup(GameState.Management_Hub))
                    {
                        view.SetHint("Não foi possível avançar. Tente novamente; sua equipe foi preservada.", true);
                        return;
                    }
                }
                else MenuNavigator.Instance?.Back();
                FeedbackToast.Show("Equipe confirmada. Salários serão cobrados no fechamento mensal.", FeedbackKind.Success);
            }
            catch (Exception) { view.SetHint("Não foi possível confirmar a equipe. Confira os dados e tente novamente.", true); FeedbackToast.Show("A equipe não foi confirmada. Confira os dados e tente novamente.", FeedbackKind.Error); }
        }
    }
}
