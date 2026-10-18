using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Game.Infrastructure.Session;
using UnityEngine;

namespace Game.Domain.Service
{
    /// <summary>Read-only snapshot. Drafts never borrow default enum values or money from a new session.</summary>
    public static class EstablishmentSummaryService
    {
        public static EstablishmentSummary Build() => GameSessionState.IsPersisted && GameSessionState.Current != null
            ? Build(GameSessionState.Current) : BuildDraft();

        public static EstablishmentSummary BuildDraft() => BuildChoices(null);

        public static EstablishmentSummary Build(GameSessionEntity session)
        {
            if (session == null) return BuildDraft();
            var s = BuildChoices(session);
            s.HasSession = true;
            s.Round = session.currentRound;
            s.Cash = session.currentCash;
            s.LoanBalance = session.loanBalance;
            s.Reputation = session.reputationScore;
            s.Score = session.alignmentScore;
            s.HasScore = false;
            s.CoherenceLabel = CoherenceLabel(session.alignmentClassification, session.coherenceRating);
            s.ContextLabel = session.status == GameSessionStatus.IN_PROGRESS ? "Sessão confirmada · escolhas atuais" : "Sessão encerrada";
            var line = LoanService.FindLine(session.creditLineId);
            s.LoanInstallment = LoanService.Installment(session.loanBalance, line, session.currentRound);
            s.CreditLabel = session.loanBalance > 0
                ? (line != null ? line.displayName : "Linha indisponível") + " · saldo " + Brl(session.loanBalance)
                : "Sem empréstimo em aberto";
            s.Decisions[3].Detail = s.CreditLabel;
            s.Decisions[3].Ready = session.loanBalance <= 0 || line != null;
            if (s.Decisions.Take(3).All(d => d.Ready))
            {
                try
                {
                    var db = ReferenceEquals(session, GameSessionState.Current) && GameSessionState.IsPersisted
                        ? DatabaseInitializer.DatabaseService?.Connection : null;
                    RoundResultEntity estimate;
                    if (session.status != GameSessionStatus.IN_PROGRESS)
                    {
                        estimate = db != null ? new RoundResultRepository(db).GetLastRoundBySessionId(session.sessionId) : null;
                        s.IsLastResult = estimate != null;
                    }
                    else
                    {
                        var events = db != null ? new SessionEventHistoryRepository(db).GetBySessionAndRound(session.sessionId, session.currentRound)
                            : new List<SessionEventHistoryEntity>();
                        estimate = MonthlySimulationEngine.Calculate(MonthlySimulationCatalog.Load(session, events));
                    }
                    if (estimate != null)
                    {
                        s.HasMonthlyEstimate = s.HasScore = true;
                        s.Score = estimate.alignmentScore;
                        s.CoherenceLabel = CoherenceLabel(estimate.alignmentClassification, session.coherenceRating);
                        s.EstimatedRevenue = estimate.grossRevenue;
                        s.SupplyCost = estimate.supplyCost;
                        s.SalariesCost = estimate.Payroll;
                        s.FixedCost = estimate.FixedCosts;
                        s.LoanInstallment = estimate.loanPayment;
                        s.MonthlyResult = estimate.netResult;
                    }
                }
                catch (Exception) { s.EstimateFailureReason = "Não foi possível calcular com os dados disponíveis."; }
            }
            s.IsReady = s.Decisions.All(d => d.Ready);
            s.Notice = s.IsLastResult ? "Resultado registrado do último mês encerrado."
                : s.HasMonthlyEstimate ? "Projeção do mês com as escolhas atuais. Os valores podem mudar até o fechamento."
                : "Projeção indisponível. Confira as escolhas e os dados disponíveis.";
            return s;
        }

        private static EstablishmentSummary BuildChoices(GameSessionEntity session)
        {
            bool draft = session == null;
            var s = new EstablishmentSummary { IsDraft = draft };
            var restaurant = Resources.LoadAll<RestaurantData>("Restaurants")
                .FirstOrDefault(r => r != null && (draft ? PlayerSession.SelectedRestaurantType == r.type : session.restaurantType == r.type));
            var location = Resources.LoadAll<LocationData>("Locations")
                .FirstOrDefault(l => l != null && (draft ? PlayerSession.HasLocation && PlayerSession.SelectedZone == l.zone : session.locationZone == l.zone));
            Segment? segment = draft ? PlayerSession.SelectedTargetSegment : session.targetSegment;
            s.CompanyLabel = Clean(draft ? PlayerSession.RestaurantName : session.companyName, "Meu estabelecimento");
            s.HasChoices = restaurant != null || location != null || !draft;
            s.ContextLabel = s.HasChoices ? "Rascunho · ainda não confirmado" : "Nenhuma escolha disponível";
            s.Facade = restaurant?.facade;
            s.LocationLabel = location != null ? location.displayName : "Localização pendente";
            s.RestaurantLabel = restaurant != null ? restaurant.displayName : "Restaurante pendente";
            s.SegmentLabel = SegmentLabel(segment);
            var menu = draft ? PlayerSession.GetMenu() : Parse(() => MenuPricingHelper.FromJson(session.menuPricingJson), new MenuPricingData());
            bool menuMatches = !draft || PlayerSession.MenuRestaurantType == PlayerSession.SelectedRestaurantType;
            var products = restaurant?.products?.Where(p => p != null).ToArray() ?? Array.Empty<ProductData>();
            var prices = new List<float>();
            var menuLines = new List<string>();
            foreach (var p in products)
            {
                MenuPricingHelper.TryGetPrice(menu, p.id, out float price);
                bool valid = menuMatches && MenuPricingHelper.TryGetPrice(menu, p.id, out _) && p.IsPriceInRange(price);
                if (valid) prices.Add(price);
                menuLines.Add(p.displayName + " · " + (valid ? Brl(price) : "Preço pendente"));
            }
            bool menuReady = products.Length > 0 && prices.Count == products.Length && menu.items.Count == products.Length
                && menu.items.Select(item => item.productId).Distinct().Count() == products.Length;
            s.MenuLabel = products.Length == 0 ? "Cardápio indisponível" : $"{prices.Count} de {products.Length} preços definidos";
            s.MenuDetails = menuLines.Count == 0 ? "Escolha um restaurante para consultar o cardápio." : string.Join("\n", menuLines);
            s.PriceLabel = menuReady ? Brl(prices.Average()) : "A definir";
            var team = draft ? new TeamSelectionData() : Parse(() => TeamSelectionHelper.FromJson(session.teamJson), new TeamSelectionData());
            var equipment = draft ? new EquipmentSelectionData() : Parse(() => EquipmentSelectionHelper.FromJson(session.equipmentJson), new EquipmentSelectionData());
            var roles = Resources.LoadAll<RoleData>("Roles");
            var equipmentCatalog = Resources.LoadAll<EquipmentData>("Equipments");
            s.HasTeamCatalog = roles.Length > 0;
            s.HasEquipmentCatalog = equipmentCatalog.Length > 0;
            s.Members = TeamSelectionHelper.GetMembers(team).Where(m => m.quantity > 0).Select(m => new EstablishmentSummary.Member {
                RoleId = m.roleId, Quantity = m.quantity, Name = RoleLabel(roles.FirstOrDefault(r => r.id == m.roleId))
            }).ToArray();
            s.TeamCount = s.Members.Sum(m => m.Quantity);
            s.TeamLabel = s.TeamCount == 0 ? "Nenhuma contratação" : s.TeamCount == 1 ? "1 pessoa contratada" : $"{s.TeamCount} pessoas contratadas";
            s.TeamDetails = s.Members.Length > 0 ? string.Join("\n", s.Members.Select(m => $"{m.Name} · {m.Quantity}")) : "Nenhuma contratação registrada.";
            var ids = EquipmentSelectionHelper.GetIds(equipment);
            s.EquipmentCount = ids.Count;
            s.EquipmentLabel = ids.Count == 0 ? "Nenhum equipamento" : ids.Count == 1 ? "1 equipamento" : $"{ids.Count} equipamentos";
            s.EquipmentDetails = ids.Count > 0 ? string.Join("\n", ids.Select(id => equipmentCatalog.FirstOrDefault(e => e.id == id)?.displayName ?? "Equipamento indisponível")) : "Nenhum equipamento adquirido.";
            bool teamReady = !draft && restaurant != null && s.HasTeamCatalog && s.Members.All(m => roles.Any(r => r.id == m.RoleId)) && (restaurant.requiredRoles ?? Array.Empty<RoleRequirement>())
                .All(r => roles.Any(role => role.id == r.roleId) && TeamSelectionHelper.GetMemberQuantity(team, r.roleId) >= r.quantity);
            bool equipmentReady = !draft && restaurant != null && s.HasEquipmentCatalog && ids.All(id => equipmentCatalog.Any(e => e.id == id)) && (restaurant.requiredEquipmentIds ?? Array.Empty<string>())
                .All(id => ids.Contains(id) && equipmentCatalog.Any(e => e.id == id));
            s.Decisions = new[] {
                Decision("Localização", s.LocationLabel, location != null),
                Decision("Restaurante e público", s.RestaurantLabel + " · " + s.SegmentLabel, restaurant != null && segment.HasValue && restaurant.AllowsSegment(segment.Value)),
                Decision("Cardápio e preços", s.MenuLabel + " · ticket médio " + s.PriceLabel, menuReady),
                Decision("Crédito", s.CreditLabel, false),
                Decision("Equipamentos", s.EquipmentLabel, equipmentReady),
                Decision("Equipe", s.TeamLabel, teamReady)
            };
            s.Notice = s.HasChoices ? "As escolhas iniciais serão salvas ao confirmar o cardápio. Crédito, equipamentos e equipe vêm nas próximas etapas."
                : "Comece pela localização e pelo restaurante. Seus indicadores aparecerão após a confirmação do cardápio.";
            return s;
        }

        private static EstablishmentSummary.Decision Decision(string name, string detail, bool ready) => new() { Label = name, Detail = detail, Ready = ready };
        private static T Parse<T>(Func<T> parser, T fallback) { try { return parser(); } catch (Exception) { return fallback; } }
        private static string Clean(string value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value;
        private static string RoleLabel(RoleData role) => role == null ? "Função indisponível" : role.id switch {
            "attendant" => "Atendente", "grill_cook" => "Chapeiro", "head_chef" => "Chef de cozinha", "manager" => "Gerente", "sushi_chef" => "Profissional de sushi", _ => role.displayName
        };
        public static string Brl(float value) => "R$ " + ((double)value).ToString("N2", CultureInfo.GetCultureInfo("pt-BR"));
        private static string SegmentLabel(Segment? segment) => segment switch { Segment.LOW => "Popular", Segment.MEDIUM => "Médio", Segment.HIGH => "Alto", _ => "Pendente" };
        private static string CoherenceLabel(string classification, string fallback) => (string.IsNullOrWhiteSpace(classification) ? fallback : classification) switch {
            "HIGH" => "Alta", "ADEQUATE" => "Adequada", "FRAGILE" => "Frágil", "CRITICAL" => "Crítica", _ => "A calcular"
        };
    }
}
