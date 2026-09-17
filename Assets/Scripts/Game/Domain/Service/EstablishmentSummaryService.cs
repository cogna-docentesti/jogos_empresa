using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Game.Domain.Service
{
    /// <summary>
    /// Le a sessao em memoria (GameSessionState) e devolve o retrato do
    /// estabelecimento. Nao grava nada, nao toca no banco, nao muda GameState.
    ///
    /// Todos os numeros saem dos ScriptableObjects que ja existem
    /// (RestaurantData, ProductData, RoleData, EquipmentData, CreditLineData).
    /// Onde o catalogo nao esta disponivel o valor entra como zero e o
    /// summary marca a flag correspondente, para a tela avisar em vez de
    /// exibir um numero inventado.
    /// </summary>
    public static class EstablishmentSummaryService
    {
        private const string RestaurantsFolder = "Restaurants";
        private const string RolesFolder       = "Roles";
        private const string EquipmentFolder   = "Equipment";
        private const string CreditLinesFolder = "CreditLines";

        public static EstablishmentSummary Build()
        {
            var summary = new EstablishmentSummary();

            if (!GameSessionState.HasSession)
            {
                summary.HasSession = false;
                return summary;
            }

            var session = GameSessionState.Current;

            summary.HasSession = true;
            summary.Round        = session.currentRound;
            summary.Cash         = session.currentCash;
            summary.Score        = session.alignmentScore;
            summary.LoanBalance  = session.loanBalance;
            summary.Reputation   = session.reputationScore;

            summary.LocationLabel   = LocationLabel(session.locationZone);
            summary.RestaurantLabel = RestaurantLabel(session.restaurantType);
            summary.SegmentLabel    = SegmentLabel(session.targetSegment);
            summary.CoherenceLabel  = CoherenceLabel(session.alignmentClassification, session.coherenceRating);

            var restaurant = FindRestaurant(session.restaurantType);
            var pricing    = MenuPricingHelper.FromJson(session.menuPricingJson);
            var team       = TeamSelectionHelper.FromJson(session.teamJson);
            var equipment  = EquipmentSelectionHelper.FromJson(session.equipmentJson);

            var roleCatalog      = Resources.LoadAll<RoleData>(RolesFolder);
            var equipmentCatalog = Resources.LoadAll<EquipmentData>(EquipmentFolder);

            summary.HasTeamCatalog      = roleCatalog != null && roleCatalog.Length > 0;
            summary.HasEquipmentCatalog = equipmentCatalog != null && equipmentCatalog.Length > 0;

            summary.TeamCount      = CountTeam(team);
            summary.EquipmentCount = EquipmentSelectionHelper.GetIds(equipment).Count;

            // Rotulos curtos: eles vivem dentro de chips estreitos, onde o
            // titulo do chip ja diz do que se trata. "0 contratados" viraria
            // duas linhas sem acrescentar nada.
            summary.TeamLabel = summary.TeamCount switch
            {
                0 => "Ninguem",
                1 => "1 pessoa",
                _ => $"{summary.TeamCount} pessoas"
            };

            summary.EquipmentLabel = summary.EquipmentCount switch
            {
                0 => "Nenhum",
                1 => "1 item",
                _ => $"{summary.EquipmentCount} itens"
            };

            // ---------- CARDAPIO E TICKET ----------
            float averageTicket = 0f;
            float inputCostRatio = 0f;
            int   dishCount = 0;

            if (restaurant?.products != null && restaurant.products.Length > 0)
            {
                float priceSum = 0f;
                float ratioSum = 0f;

                foreach (var product in restaurant.products)
                {
                    if (product == null)
                        continue;

                    float price = product.price;

                    if (MenuPricingHelper.TryGetPrice(pricing, product.id, out float saved))
                        price = product.ClampPrice(saved);

                    priceSum += price;
                    ratioSum += product.inputCostRatio;
                    dishCount++;
                }

                if (dishCount > 0)
                {
                    averageTicket  = priceSum / dishCount;
                    inputCostRatio = ratioSum / dishCount;
                }

                summary.MenuLabel = dishCount == 1 ? "1 prato" : $"{dishCount} pratos";
            }

            if (session.selectedPrice > 0f)
                averageTicket = session.selectedPrice;

            summary.PriceLabel = averageTicket > 0f ? Brl(averageTicket) : "Pendente";

            // ---------- DEMANDA ----------
            // Base = publico estimado do restaurante, ajustado pelo fator de
            // alinhamento que o AlignmentEngine ja calcula, e limitado pela
            // capacidade real de atendimento (equipe + bonus de equipamento).
            float baseDemand = restaurant != null ? restaurant.estimatedMonthlyCustomers : 0f;
            float factor     = session.alignmentFactor > 0f ? session.alignmentFactor : 1f;

            float potentialDemand = baseDemand * factor;
            float capacity        = TeamCapacity(team, roleCatalog) + EquipmentCapacity(equipment, equipmentCatalog);

            float demand = capacity > 0f
                ? Mathf.Min(potentialDemand, capacity)
                : potentialDemand;

            // ---------- RECEITA E RESULTADO ----------
            summary.EstimatedRevenue = averageTicket * demand;

            summary.SupplyCost      = summary.EstimatedRevenue * inputCostRatio;
            summary.SalariesCost    = TeamSalaries(team, roleCatalog);
            summary.FixedCost       = restaurant != null ? restaurant.baseMonthlyCost : 0f;
            summary.LoanInstallment = LoanInstallment(session.creditLineId, session.loanBalance);

            summary.MonthlyResult =
                summary.EstimatedRevenue
                - summary.SupplyCost
                - summary.SalariesCost
                - summary.FixedCost
                - summary.LoanInstallment;

            return summary;
        }

        // =====================================================
        //  CATALOGOS
        // =====================================================

        private static RestaurantData FindRestaurant(RestaurantType type)
        {
            var all = Resources.LoadAll<RestaurantData>(RestaurantsFolder);

            if (all == null || all.Length == 0)
                return null;

            return all.FirstOrDefault(r => r != null && r.type == type);
        }

        private static CreditLineData FindCreditLine(string creditLineId)
        {
            if (string.IsNullOrWhiteSpace(creditLineId))
                return null;

            var all = Resources.LoadAll<CreditLineData>(CreditLinesFolder);

            if (all == null || all.Length == 0)
                return null;

            return all.FirstOrDefault(c => c != null && c.id == creditLineId);
        }

        // =====================================================
        //  CALCULOS AUXILIARES
        // =====================================================

        private static int CountTeam(TeamSelectionData team)
        {
            var members = TeamSelectionHelper.GetMembers(team);
            return members.Sum(m => Mathf.Max(0, m.quantity));
        }

        private static float TeamSalaries(TeamSelectionData team, IReadOnlyList<RoleData> catalog)
        {
            if (catalog == null || catalog.Count == 0)
                return 0f;

            float total = 0f;

            foreach (var member in TeamSelectionHelper.GetMembers(team))
            {
                var role = catalog.FirstOrDefault(r => r != null && r.id == member.roleId);

                if (role != null)
                    total += role.salary * Mathf.Max(0, member.quantity);
            }

            return total;
        }

        private static float TeamCapacity(TeamSelectionData team, IReadOnlyList<RoleData> catalog)
        {
            if (catalog == null || catalog.Count == 0)
                return 0f;

            float total = 0f;

            foreach (var member in TeamSelectionHelper.GetMembers(team))
            {
                var role = catalog.FirstOrDefault(r => r != null && r.id == member.roleId);

                if (role != null)
                    total += role.maxClientsSupported * Mathf.Max(0, member.quantity);
            }

            return total;
        }

        private static float EquipmentCapacity(EquipmentSelectionData equipment, IReadOnlyList<EquipmentData> catalog)
        {
            if (catalog == null || catalog.Count == 0)
                return 0f;

            float total = 0f;

            foreach (var id in EquipmentSelectionHelper.GetIds(equipment))
            {
                var item = catalog.FirstOrDefault(e => e != null && e.id == id);

                if (item != null)
                    total += item.capacityBonus;
            }

            return total;
        }

        private static float LoanInstallment(string creditLineId, float loanBalance)
        {
            if (loanBalance <= 0f)
                return 0f;

            var line = FindCreditLine(creditLineId);

            if (line == null || line.termRounds <= 0)
                return 0f;

            // Parcela = principal dividido pelo prazo + juros do mes sobre o saldo.
            return (loanBalance / line.termRounds) + (loanBalance * line.monthlyInterestRate);
        }

        // =====================================================
        //  ROTULOS
        // =====================================================

        public static string Brl(float value)
        {
            return "R$ " + Mathf.Round(value).ToString("N0", new System.Globalization.CultureInfo("pt-BR"));
        }

        private static string LocationLabel(LocationZone zone)
        {
            return zone switch
            {
                LocationZone.Financas    => "Financas",
                LocationZone.Educacao    => "Educacao",
                LocationZone.Comercio    => "Comercio",
                LocationZone.Residencial => "Residencial",
                LocationZone.Servicos    => "Servicos",
                _ => "Pendente"
            };
        }

        private static string RestaurantLabel(RestaurantType type)
        {
            return type switch
            {
                RestaurantType.PODRAO  => "Lanches",
                RestaurantType.JAPONES => "Japones",
                RestaurantType.FRANCES => "Frances",
                _ => "Pendente"
            };
        }

        private static string SegmentLabel(Segment segment)
        {
            return segment switch
            {
                Segment.LOW    => "Popular",
                Segment.MEDIUM => "Medio",
                Segment.HIGH   => "Alto",
                _ => "Pendente"
            };
        }

        private static string CoherenceLabel(string classification, string fallback)
        {
            string value = string.IsNullOrWhiteSpace(classification) ? fallback : classification;

            return value switch
            {
                "HIGH"      => "Alta",
                "ADEQUATE"  => "Adequada",
                "FRAGILE"   => "Fragil",
                "CRITICAL"  => "Critica",
                _ => "A calcular"
            };
        }
    }
}
