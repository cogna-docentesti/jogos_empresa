using System.Collections.Generic;
using System.Linq;
using Game.Adapter.In.UI;
using Game.Adapter.In.UI.Navigation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Adapter.In.Controllers
{
    public sealed class TeamScreenController : MonoBehaviour
    {
        private RoleData[] roles;
        private RestaurantData restaurant;
        private TeamSelectionData draft;
        [System.Serializable]
        public sealed class RoleCard
        {
            public RoleData role;
            public GameObject root;
            public TextMeshProUGUI quantity;
            public Button hire;
            public Button undo;
        }

        [SerializeField] private TextMeshProUGUI minimumText;
        [SerializeField] private TextMeshProUGUI summary;
        [SerializeField] private Button confirm;
        [SerializeField] private Button back;
        [SerializeField] private RoleCard[] roleCards;
        private readonly Dictionary<string, TextMeshProUGUI> quantities = new();
        private bool IsInitial => InitialTutorialView.IsInitial(GameState.Initial_Team);

        private void OnEnable()
        {
            if (!GameSessionState.HasSession) return;
            if (minimumText == null || summary == null || confirm == null || back == null || roleCards == null) return;
            roles = Resources.LoadAll<RoleData>("Roles").Where(r => r != null).ToArray();
            restaurant = Resources.LoadAll<RestaurantData>("Restaurants").FirstOrDefault(r => r.type == GameSessionState.Current.restaurantType);
            draft = TeamSelectionHelper.FromJson(GameSessionState.Current.teamJson);
            BuildScreen();
            Refresh();
            InitialTutorialView.Configure(transform, IsInitial);
        }

        private void BuildScreen()
        {
            quantities.Clear();
            minimumText.text = restaurant?.requiredRoles == null ? "Equipe mínima não cadastrada."
                : "Equipe mínima — " + restaurant.displayName + "\n" + string.Join("\n", restaurant.requiredRoles.Where(r => r != null).Select(r =>
                    r.quantity + " × " + RoleName(roles.FirstOrDefault(role => role.id == r.roleId), r.roleId)));
            foreach (var card in roleCards)
            {
                var role = card.role;
                card.root.SetActive(!IsInitial || Minimum(role.id) > 0);
                quantities[role.id] = card.quantity;
                card.hire.onClick.RemoveAllListeners();
                card.hire.onClick.AddListener(() =>
                {
                    if (!GameSessionState.HasActiveSession || (IsInitial && TeamSelectionHelper.GetMemberQuantity(draft, role.id) >= Minimum(role.id))) return;
                    TeamSelectionHelper.AddMember(draft, role.id);
                    Refresh();
                });
                card.undo.onClick.RemoveAllListeners();
                card.undo.onClick.AddListener(() =>
                {
                    int saved = TeamSelectionHelper.GetMemberQuantity(TeamSelectionHelper.FromJson(GameSessionState.Current.teamJson), role.id);
                    if (TeamSelectionHelper.GetMemberQuantity(draft, role.id) > saved)
                        TeamSelectionHelper.RemoveMember(draft, role.id);
                    Refresh();
                });
            }
            confirm.GetComponentInChildren<TextMeshProUGUI>(true).text = IsInitial ? "Confirmar equipe e continuar" : "Confirmar contratações";
            confirm.onClick.RemoveAllListeners();
            confirm.onClick.AddListener(Confirm);
            back.gameObject.SetActive(!IsInitial);
            back.onClick.RemoveAllListeners();
            back.onClick.AddListener(() => MenuNavigator.Instance?.Back());
        }
        private int Minimum(string id) => restaurant?.requiredRoles?.Where(r => r != null && r.roleId == id).Sum(r => Mathf.Max(0, r.quantity)) ?? 0;

        private void Refresh()
        {
            foreach (var pair in quantities)
                pair.Value.text = $"Selecionados: {TeamSelectionHelper.GetMemberQuantity(draft, pair.Key)} · mínimo: {Minimum(pair.Key)}";
            float payroll = roles.Sum(r => (float)r.salary * TeamSelectionHelper.GetMemberQuantity(draft, r.id));
            var saved = TeamSelectionHelper.FromJson(GameSessionState.Current.teamJson);
            float previous = roles.Sum(r => (float)r.salary * TeamSelectionHelper.GetMemberQuantity(saved, r.id));
            summary.text = $"Folha mensal: R$ {payroll:N2}\nAumento mensal com esta contratação: R$ {payroll - previous:N2}";
            confirm.interactable = GameSessionState.HasActiveSession && HasMinimum(restaurant, draft, roles);
        }

        public static bool HasMinimum(RestaurantData restaurant, TeamSelectionData team, IEnumerable<RoleData> roles)
        {
            var ids = new HashSet<string>(roles.Where(r => r != null).Select(r => r.id));
            return restaurant?.requiredRoles != null && restaurant.requiredRoles.Length > 0
                && restaurant.requiredRoles.All(r => r != null && ids.Contains(r.roleId)
                    && TeamSelectionHelper.GetMemberQuantity(team, r.roleId) >= r.quantity);
        }

        private void Confirm()
        {
            if (!GameSessionState.HasActiveSession || !HasMinimum(restaurant, draft, roles)) return;
            GameSessionState.SetTeamJson(TeamSelectionHelper.ToJson(draft));
            if (IsInitial)
            {
                var session = GameSessionState.Current;
                new GameSessionService(session.userId, session.professorId).ConfirmInitialTeam(roles.ToList());
            }
            else MenuNavigator.Instance?.OpenRoot();
        }

        private static string RoleName(RoleData role, string fallback) => role == null ? fallback : role.roleType switch
        {
            RoleType.ATENDENTE => "Atendente", RoleType.GARCOM => "Garçom", RoleType.CHAPEIRO => "Chapeiro",
            RoleType.CHEF => "Chef de cozinha", RoleType.SUSHIMAN => "Sushiman", RoleType.GERENTE => "Gerente",
            _ => role.displayName
        };
    }
}
