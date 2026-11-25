using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Domain.Simulation
{
    [Serializable]
    public class CanvasBlock
    {
        /// <summary>Ordem de preenchimento sugerida (1 = segmentos de clientes ... 9 = estrutura de custos).</summary>
        public int order;
        public string id;
        public string title;
        public List<string> items = new List<string>();
        /// <summary>Observacoes de coerencia calculadas (nao opinioes).</summary>
        public List<string> notes = new List<string>();
    }

    [Serializable]
    public class BusinessModelCanvas
    {
        public List<CanvasBlock> blocks = new List<CanvasBlock>();

        public CanvasBlock Block(string id) => blocks.Find(b => b.id == id);
    }

    /// <summary>
    /// Gera o Business Model Canvas a partir das decisoes do aluno (o plano de arquitetura
    /// previa um BMCMapper). O aluno nao preenche o canvas: ele ve o proprio modelo de negocio
    /// montado a partir das escolhas, com observacoes de coerencia vindas do modelo de mercado.
    /// </summary>
    public static class BusinessModelCanvasBuilder
    {
        public static BusinessModelCanvas Build(SimulationCatalog catalog, MonthInputs inputs, MonthlyReport lastReport = null)
        {
            var cfg = catalog.Config;
            var restaurant = catalog.Restaurant(inputs.RestaurantTypeId);
            var location = catalog.Location(inputs.LocationId);
            var target = string.IsNullOrEmpty(inputs.TargetSegmentId) ? null : cfg.Segment(inputs.TargetSegmentId);
            string Policy(string category)
            {
                var c = cfg.Policy(category);
                if (c == null) return null;
                string id = inputs.Policies != null && inputs.Policies.TryGetValue(category, out var chosen) ? chosen : c.defaultOptionId;
                return (c.Option(id) ?? c.Option(c.defaultOptionId))?.displayName;
            }

            var market = MarketModel.Evaluate(new MarketQuery
            {
                Restaurant = restaurant, Location = location, TargetSegmentId = inputs.TargetSegmentId, MenuPrices = inputs.MenuPrices,
                Reputation = 50d, Satisfaction = 50d, CompetitionPressure = location.CompetitionIndex
            }, cfg);
            var canvas = new BusinessModelCanvas();
            CanvasBlock Add(int order, string id, string title)
            {
                var block = new CanvasBlock { order = order, id = id, title = title };
                canvas.blocks.Add(block);
                return block;
            }

            var segments = Add(1, "customer_segments", "Segmentos de clientes");
            if (target != null) segments.items.Add("Público-alvo escolhido: " + target.displayName);
            foreach (var s in market.Segments.OrderByDescending(s => s.LocalShare).Take(2))
                segments.items.Add(cfg.Segment(s.SegmentId).displayName + ": " + SimFormat.Percent(s.LocalShare * 100d) + " do movimento da " + location.Name);
            var dominant = market.Segments.OrderByDescending(s => s.LocalShare).First();
            if (target != null && dominant.SegmentId != target.id)
                segments.notes.Add("O público predominante da região (" + cfg.Segment(dominant.SegmentId).displayName + ") é diferente do público-alvo.");

            var value = Add(2, "value_propositions", "Proposta de valor");
            value.items.Add(restaurant.Name + (restaurant.Profile.premium ? " (proposta premium e experiência)" : string.Empty));
            int premiumUnits = inputs.EquipmentOwned.Count(id => catalog.Equipment.TryGetValue(id, out var e) && e.TierId == EquipmentTierId.Premium && e.AppliesTo(restaurant.TypeId));
            if (premiumUnits > 0) value.items.Add(premiumUnits + " equipamento(s) premium que elevam a qualidade percebida");
            string supplier = Policy("supplier");
            if (supplier != null) value.items.Add("Insumos: fornecedor " + supplier);
            value.notes.Add("Encaixe de mercado estimado: " + SimFormat.Number(market.FitFactor, 2) + " (1,00 = referência).");

            var channels = Add(3, "channels", "Canais");
            channels.items.Add("Salão na " + location.Name + " (até " + SimFormat.Number(location.PhysicalCapacity) + " clientes por dia)");
            string marketing = Policy("marketing");
            if (marketing != null) channels.items.Add("Divulgação: " + marketing);

            var relationship = Add(4, "customer_relationships", "Relacionamento com clientes");
            relationship.items.Add("Atendimento presencial, padrão de " + SimFormat.Number(restaurant.Profile.serviceStandardMinutes) + " minutos");
            string loyalty = Policy("loyalty");
            if (loyalty != null) relationship.items.Add("Fidelização: " + loyalty);
            if (lastReport != null) relationship.notes.Add("NPS médio do último mês: " + SimFormat.Number(lastReport.indicators.npsAverage));

            var revenue = Add(5, "revenue_streams", "Fontes de receita");
            foreach (var product in restaurant.Products)
            {
                double price = inputs.MenuPrices != null && inputs.MenuPrices.TryGetValue(product.Id, out double p) ? p : product.ReferencePrice;
                revenue.items.Add(product.Name + ": " + SimFormat.Money(price));
            }
            revenue.items.Add("Ticket médio estimado: " + SimFormat.Money(market.Ticket));

            var resources = Add(6, "key_resources", "Recursos-chave");
            foreach (var pair in inputs.Team.Where(t => t.Value > 0))
                resources.items.Add(pair.Value + "x " + (catalog.Roles.TryGetValue(pair.Key, out var role) ? role.Name : pair.Key));
            resources.items.Add(inputs.EquipmentOwned.Count + " equipamentos");
            resources.items.Add("Caixa: " + SimFormat.Money(inputs.OpeningCash));
            foreach (var required in restaurant.RequiredRoles)
            {
                int hired = inputs.Team.TryGetValue(required.Key, out int q) ? q : 0;
                if (hired < required.Value)
                    resources.notes.Add("Equipe abaixo do mínimo: " + (catalog.Roles.TryGetValue(required.Key, out var r) ? r.Name : required.Key) + " " + hired + "/" + required.Value + ".");
            }

            var activities = Add(7, "key_activities", "Atividades-chave");
            activities.items.Add("Preparo e produção das refeições");
            activities.items.Add("Atendimento no salão");
            string stock = Policy("stock");
            if (stock != null) activities.items.Add("Compras e estoque: " + stock);
            string maintenance = Policy("maintenance");
            if (maintenance != null) activities.items.Add("Manutenção: " + maintenance);

            var partners = Add(8, "key_partnerships", "Parcerias-chave");
            if (supplier != null) partners.items.Add("Fornecedor de insumos (" + supplier + ")");
            if (!string.IsNullOrEmpty(inputs.CreditLineId) && catalog.Credits.TryGetValue(inputs.CreditLineId, out var credit) && credit.MaxAmount > 0d)
                partners.items.Add("Banco CogCred: " + credit.Name + " (" + SimFormat.Percent(credit.MonthlyRate * 100d, 1) + " ao mês)");
            partners.items.Add("Locador do ponto comercial");

            var costs = Add(9, "cost_structure", "Estrutura de custos");
            costs.items.Add("Aluguel: " + SimFormat.Money(location.Rent));
            costs.items.Add("Folha estimada: " + SimFormat.Money(OperationsModel.MonthlyPayroll(catalog, inputs.Team, cfg.staff.payrollChargesMultiplier, 1d)));
            costs.items.Add("Insumos: cerca de " + SimFormat.Percent(market.Ticket > 0d ? market.UnitCost / market.Ticket * 100d : 0d) + " do preço de venda");
            costs.items.Add("Impostos sobre vendas: " + SimFormat.Percent(cfg.finance.salesTaxRate * 100d));
            if (lastReport != null) costs.notes.Add("Ponto de equilíbrio do último mês: " + SimFormat.Number(lastReport.indicators.breakEvenAccounting) + " pedidos.");
            return canvas;
        }
    }
}
