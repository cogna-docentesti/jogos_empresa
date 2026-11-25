using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Domain.Simulation
{
    /// <summary>
    /// Feedback pedagogico. Cada frase e montada a partir de valores que entraram nos
    /// calculos do mes (fatores da demanda, perdas por causa, custos, eventos, riscos).
    /// Nenhuma justificativa e escrita sem um numero calculado que a sustente.
    /// </summary>
    public static class FeedbackComposer
    {
        public static List<FeedbackMessage> Compose(MonthlyReport report, SimulationCatalog catalog, MonthInputs inputs, MonthlyReport previous, SimulationConfig cfg)
        {
            var messages = new List<FeedbackMessage>();
            var location = catalog.Location(report.locationId);
            var restaurant = catalog.Restaurant(report.restaurantTypeId);
            Demand(messages, report, previous, location, restaurant, catalog, inputs, cfg);
            Operations(messages, report);
            Costs(messages, report, restaurant);
            Finance(messages, report, previous);
            Equipment(messages, report);
            Team(messages, report, cfg);
            Customers(messages, report, restaurant);
            Events(messages, report);
            messages.Add(Message("pontuacao", report.scorecard.total >= 60 ? FeedbackTone.Positive : (report.scorecard.total >= 40 ? FeedbackTone.Neutral : FeedbackTone.Negative),
                "Pontuação do mês: " + report.scorecard.total + " (" + report.scorecard.classification + ").", report.scorecard.total));
            return messages;
        }

        private static FeedbackMessage Message(string topic, string tone, string text, params double[] values)
        {
            var message = new FeedbackMessage { topic = topic, tone = tone, text = text };
            message.values.AddRange(values);
            return message;
        }

        private static string Join(IList<string> parts)
        {
            if (parts.Count == 0) return string.Empty;
            if (parts.Count == 1) return parts[0];
            return string.Join(", ", parts.Take(parts.Count - 1)) + " e " + parts[parts.Count - 1];
        }

        // ------------------------------------------------------------------ Demanda

        private static void Demand(List<FeedbackMessage> messages, MonthlyReport report, MonthlyReport previous, LocationSpec location,
            RestaurantSpec restaurant, SimulationCatalog catalog, MonthInputs inputs, SimulationConfig cfg)
        {
            if (report.days.Count == 0) return;
            double avg = report.days.Average(d => d.potentialDemand);
            if (previous != null && previous.days.Count > 0 && previous.demandFactors.Count == report.demandFactors.Count)
            {
                double prevAvg = previous.days.Average(d => d.potentialDemand);
                double change = prevAvg > 0d ? (avg / prevAvg - 1d) * 100d : 0d;
                var contributions = new List<(string label, double points)>();
                double totalLog = 0d;
                for (int i = 0; i < report.demandFactors.Count; i++)
                    totalLog += Math.Log(Math.Max(1e-9d, report.demandFactors[i].currentValue / Math.Max(1e-9d, previous.demandFactors[i].currentValue)));
                for (int i = 0; i < report.demandFactors.Count; i++)
                {
                    double log = Math.Log(Math.Max(1e-9d, report.demandFactors[i].currentValue / Math.Max(1e-9d, previous.demandFactors[i].currentValue)));
                    double points = Math.Abs(totalLog) > 1e-6d ? log / totalLog * change : log * 100d;
                    contributions.Add((report.demandFactors[i].label, points));
                }
                var against = contributions.Where(c => Math.Sign(c.points) == Math.Sign(change) && Math.Abs(c.points) >= 1d)
                                           .OrderByDescending(c => Math.Abs(c.points)).Take(3)
                                           .Select(c => c.label + " (" + (c.points > 0 ? "+" : "") + SimFormat.Number(c.points, 1) + " p.p.)").ToList();
                var offset = contributions.Where(c => Math.Sign(c.points) != Math.Sign(change) && Math.Abs(c.points) >= 1d)
                                          .OrderByDescending(c => Math.Abs(c.points)).Take(2)
                                          .Select(c => c.label + " (" + (c.points > 0 ? "+" : "") + SimFormat.Number(c.points, 1) + " p.p.)").ToList();
                string text = "Seu restaurante apresentou " + (change >= 0 ? "aumento" : "redução") + " de " + SimFormat.Percent(Math.Abs(change)) +
                              " na demanda em relação ao mês anterior (" + SimFormat.Number(prevAvg, 1) + " para " + SimFormat.Number(avg, 1) + " pedidos por dia).";
                if (against.Count > 0) text += " Entre os fatores que contribuíram estão " + Join(against) + ".";
                if (offset.Count > 0) text += " Em sentido contrário pesaram " + Join(offset) + ".";
                messages.Add(Message("demanda", change >= 0 ? FeedbackTone.Positive : FeedbackTone.Negative, text, change));
            }
            else
            {
                double change = report.demandChangePercent;
                var top = report.demandFactors.Where(fx => Math.Abs(fx.contributionPoints) >= 2d)
                                              .OrderByDescending(fx => Math.Abs(fx.contributionPoints)).Take(3)
                                              .Select(fx => fx.label + " (" + (fx.contributionPoints > 0 ? "+" : "") + SimFormat.Number(fx.contributionPoints, 1) + " p.p.)").ToList();
                string text = "A demanda média foi de " + SimFormat.Number(avg, 1) + " pedidos por dia, " + SimFormat.Percent(Math.Abs(change)) +
                              (change >= 0 ? " acima" : " abaixo") + " da demanda base da " + location.Name + " (" + SimFormat.Number(location.BaseDailyDemand) + " por dia).";
                if (top.Count > 0) text += " Os fatores que mais pesaram foram " + Join(top) + ".";
                messages.Add(Message("demanda", change >= 0 ? FeedbackTone.Positive : FeedbackTone.Negative, text, change));
            }

            // Diagnostico do encaixe de mercado: segmento predominante e aceitacao de preco.
            var market = MarketModel.Evaluate(new MarketQuery
            {
                Restaurant = restaurant,
                Location = location,
                TargetSegmentId = inputs.TargetSegmentId,
                MenuPrices = inputs.MenuPrices,
                Quality = report.days.Average(d => d.quality),
                Reputation = 50d,
                Satisfaction = 50d
            }, cfg);
            var dominant = market.Segments.OrderByDescending(s => s.LocalShare).First();
            var segment = catalog.Segment(dominant.SegmentId);
            double reference = segment.referenceSpend * location.ReferencePriceFactor;
            double ticket = report.indicators.averageTicket;
            if (dominant.Acceptance < 0.3d && ticket > reference)
            {
                messages.Add(Message("demanda", FeedbackTone.Warning,
                    "O público predominante da " + location.Name + " é a " + segment.displayName + " (" + SimFormat.Percent(dominant.LocalShare * 100d) +
                    " do movimento), que costuma gastar cerca de " + SimFormat.Money(reference, false) + " por refeição. Com ticket médio de " +
                    SimFormat.Money(ticket) + ", só " + SimFormat.Percent(dominant.Acceptance * 100d) + " desse público aceita os preços do cardápio. " +
                    "Pratos mais acessíveis, qualidade percebida maior ou marketing com alcance na cidade ampliam o público.",
                    dominant.LocalShare, reference, ticket, dominant.Acceptance));
            }
            else if (market.FitFactor >= 1.1d)
            {
                messages.Add(Message("demanda", FeedbackTone.Positive,
                    "O restaurante está bem encaixado na região: o fator de encaixe ficou em " + SimFormat.Number(report.indicators.fitIndex, 2) +
                    " (1,00 = referência). Restaurante, público e preços combinam com o movimento da " + location.Name + ".", report.indicators.fitIndex));
            }
        }

        // ------------------------------------------------------------------ Operacao

        private static void Operations(List<FeedbackMessage> messages, MonthlyReport report)
        {
            var x = report.indicators;
            if (x.lostOrdersShare >= 5d)
            {
                var causes = new List<string>();
                var byBottleneck = report.days.Where(d => d.lostCapacity > 0d).GroupBy(d => d.bottleneck)
                                         .Select(g => (g.Key, lost: g.Sum(d => d.lostCapacity))).OrderByDescending(g => g.lost).ToList();
                foreach (var group in byBottleneck)
                    causes.Add(SimFormat.Number(group.lost) + " por limite " + BottleneckLabel(group.Key));
                if (x.lostToStock >= 1d) causes.Add(SimFormat.Number(x.lostToStock) + " por falta de estoque");
                messages.Add(Message("operacao", FeedbackTone.Negative,
                    SimFormat.Number(x.lostOrders) + " pedidos (" + SimFormat.Percent(x.lostOrdersShare, 1) + " da demanda) não foram atendidos: " + Join(causes) + ".",
                    x.lostOrders, x.lostOrdersShare));
            }
            if (x.capacityOccupancy < 55d && report.days.Sum(d => d.served) > 0d)
            {
                messages.Add(Message("operacao", FeedbackTone.Warning,
                    "A estrutura ficou ociosa: a ocupação média da capacidade foi de " + SimFormat.Percent(x.capacityOccupancy) +
                    ". Equipe e aluguel são custos fixos e pesaram sobre um volume pequeno de vendas.", x.capacityOccupancy));
            }
        }

        private static string BottleneckLabel(string bottleneck)
        {
            switch (bottleneck)
            {
                case Bottleneck.Front: return "do atendimento";
                case Bottleneck.Kitchen: return "da cozinha";
                case Bottleneck.Physical: return "do salão";
                case Bottleneck.Stock: return "do estoque";
                default: return "operacional";
            }
        }

        // ------------------------------------------------------------------ Custos

        private static void Costs(List<FeedbackMessage> messages, MonthlyReport report, RestaurantSpec restaurant)
        {
            var i = report.income;
            var x = report.indicators;
            if (i.netRevenue <= 0d) return;
            double cmv = x.cmvPercent;
            double min = restaurant.Profile.cmvStandardMin * 100d, max = restaurant.Profile.cmvStandardMax * 100d;
            string position = cmv < min ? "abaixo" : (cmv > max ? "acima" : "dentro");
            messages.Add(Message("custos", cmv > max ? FeedbackTone.Negative : FeedbackTone.Neutral,
                "O CMV foi " + SimFormat.Percent(cmv, 1) + " da receita líquida, " + position + " do padrão para " + restaurant.Name +
                " (" + SimFormat.Percent(min) + " a " + SimFormat.Percent(max) + ").", cmv));
            double losses = i.spoilageLoss + i.eventStockLoss;
            if (x.wasteShare >= 3d)
            {
                string cause = i.eventStockLoss > i.spoilageLoss ? "principalmente por falhas e eventos" : "principalmente por perecibilidade do estoque mantido";
                messages.Add(Message("custos", FeedbackTone.Warning,
                    "Perdas de estoque somaram " + SimFormat.Money(losses) + " (" + SimFormat.Percent(x.wasteShare, 1) + " do custo de insumos), " + cause + ".",
                    losses, x.wasteShare));
            }
            double served = report.days.Sum(d => d.served);
            if (x.breakEvenAccounting >= 0d)
            {
                bool covered = served >= x.breakEvenAccounting;
                messages.Add(Message("custos", covered ? FeedbackTone.Positive : FeedbackTone.Negative,
                    "O ponto de equilíbrio contábil foi de " + SimFormat.Number(x.breakEvenAccounting) + " pedidos no mês (margem de contribuição de " +
                    SimFormat.Money(x.unitContributionMargin) + " por pedido). O restaurante atendeu " + SimFormat.Number(served) + ".",
                    x.breakEvenAccounting, served));
            }
            else
            {
                messages.Add(Message("custos", FeedbackTone.Negative, "Cada pedido vendido não cobriu o próprio custo variável: não há ponto de equilíbrio com esses preços e custos."));
            }
        }

        // ------------------------------------------------------------------ Financas

        private static void Finance(List<FeedbackMessage> messages, MonthlyReport report, MonthlyReport previous)
        {
            var i = report.income;
            var flow = report.cashFlow;
            double margin = i.netRevenue > 0d ? i.netIncome / i.netRevenue * 100d : 0d;
            double cashChange = flow.closingCash - flow.openingCash;
            string text = (i.netIncome >= 0d ? "Lucro líquido de " : "Prejuízo líquido de ") + SimFormat.Money(Math.Abs(i.netIncome)) +
                          " (margem de " + SimFormat.Percent(margin, 1) + "). O caixa " + (cashChange >= 0d ? "aumentou " : "diminuiu ") + SimFormat.Money(Math.Abs(cashChange)) + ".";
            // Lucro x caixa: conciliacao com valores do mes.
            var parts = new List<string>();
            if (flow.loanPrincipal > 0.5d) parts.Add("amortização do empréstimo (" + SimFormat.Money(-flow.loanPrincipal) + ")");
            if (i.depreciation > 0.5d) parts.Add("depreciação, que não sai do caixa (" + SimFormat.Money(i.depreciation) + ")");
            double payables = report.balance.suppliersPayable + report.balance.billsPayable;
            if (payables > 0.5d) parts.Add("contas e fornecedores a pagar depois (" + SimFormat.Money(payables) + ")");
            if (report.balance.inventory > 0.5d) parts.Add("estoque comprado e ainda não vendido");
            if (parts.Count > 0 && Math.Abs(cashChange - i.netIncome) > 1d)
                text += " Lucro e caixa diferem por causa de " + Join(parts) + ".";
            messages.Add(Message("financeiro", i.netIncome >= 0d ? FeedbackTone.Positive : FeedbackTone.Negative, text, i.netIncome, margin, cashChange));

            if (i.loanInterest > 0.5d)
            {
                double share = i.operatingResult > 0d ? i.loanInterest / i.operatingResult * 100d : 100d;
                string loanText = "Os juros do empréstimo somaram " + SimFormat.Money(i.loanInterest) + ". ";
                loanText += i.operatingResult > i.loanInterest
                    ? "Eles consumiram " + SimFormat.Percent(share) + " do resultado operacional: o capital emprestado está gerando retorno maior que o seu custo."
                    : "O resultado operacional (" + SimFormat.Money(i.operatingResult) + ") não cobriu os juros: até aqui o investimento financiado não compensou o custo financeiro.";
                messages.Add(Message("financeiro", i.operatingResult > i.loanInterest ? FeedbackTone.Neutral : FeedbackTone.Negative, loanText, i.loanInterest, share));
            }
            if (report.indicators.overdraftDays > 0)
            {
                messages.Add(Message("financeiro", FeedbackTone.Negative,
                    "O caixa ficou negativo em " + report.indicators.overdraftDays + " dias e o cheque especial cobrou " + SimFormat.Money(i.overdraftInterest) +
                    " de juros. Uma reserva de caixa de cerca de " + SimFormat.Money(report.indicators.recommendedCashReserve, false) + " cobre um mês de compromissos fixos.",
                    report.indicators.overdraftDays, i.overdraftInterest));
            }
        }

        // ------------------------------------------------------------------ Equipamentos

        private static void Equipment(List<FeedbackMessage> messages, MonthlyReport report)
        {
            var failures = report.events.Where(e => e.category == EventCategory.Equipment && e.impact == EventImpact.Negative && e.eventId != MonthSimulation.ReminderEventId).ToList();
            foreach (var failure in failures)
            {
                string text = failure.title + " no dia " + failure.day + " (risco do dia: " + SimFormat.Probability(failure.probability) + ").";
                if (!string.IsNullOrEmpty(failure.explanation)) text += " Fatores de risco: " + failure.explanation + ".";
                if (!string.IsNullOrEmpty(failure.reactionTitle)) text += " Decisão: " + failure.reactionTitle + (failure.cashImpact < 0d ? " (" + SimFormat.Money(failure.cashImpact) + ")." : ".");
                messages.Add(Message("equipamentos", FeedbackTone.Negative, text, failure.probability, failure.cashImpact));
            }
            int saves = report.events.Count(e => e.eventId == MonthSimulation.PreventiveSaveEventId);
            if (saves > 0)
                messages.Add(Message("equipamentos", FeedbackTone.Positive, "A manutenção preventiva evitou " + saves + (saves == 1 ? " falha provável" : " falhas prováveis") + " neste mês.", saves));
        }

        // ------------------------------------------------------------------ Equipe

        private static void Team(List<FeedbackMessage> messages, MonthlyReport report, SimulationConfig cfg)
        {
            int overloaded = report.days.Count(d => d.utilization > 1d);
            int absences = report.events.Count(e => e.category == EventCategory.Staff && e.eventId == "insufficient_team");
            int resignations = report.events.Count(e => e.eventId == "employee_resignation");
            var parts = new List<string>();
            if (overloaded > 0) parts.Add("a demanda superou a capacidade em " + overloaded + " dias");
            if (absences > 0) parts.Add(absences + (absences == 1 ? " falta" : " faltas"));
            if (resignations > 0) parts.Add(resignations + (resignations == 1 ? " pedido de demissão" : " pedidos de demissão"));
            double morale = report.indicators.moraleEnd;
            string text = "A moral da equipe terminou o mês em " + SimFormat.Number(morale) + " de 100" + (parts.Count > 0 ? "; " + Join(parts) + "." : ".");
            if (morale < 50d) text += " Moral baixa reduz a produtividade e aumenta faltas e pedidos de demissão.";
            messages.Add(Message("equipe", morale >= 60d ? FeedbackTone.Neutral : FeedbackTone.Warning, text, morale, overloaded));
        }

        // ------------------------------------------------------------------ Clientes

        private static void Customers(List<FeedbackMessage> messages, MonthlyReport report, RestaurantSpec restaurant)
        {
            var x = report.indicators;
            double standard = restaurant.Profile.serviceStandardMinutes;
            var drivers = new List<string>();
            if (x.waitAverage > standard) drivers.Add("tempo médio de atendimento de " + SimFormat.Number(x.waitAverage) + " min, acima do padrão de " + SimFormat.Number(standard) + " min");
            if (x.lostToStock >= 1d) drivers.Add("falta de produtos em alguns dias");
            if (report.events.Any(e => e.impact == EventImpact.Negative && string.IsNullOrEmpty(e.reactionId) == false && e.reactionId.Contains("postpone")))
                drivers.Add("problemas adiados sem solução");
            string text = "NPS médio de " + SimFormat.Number(x.npsAverage) + " (satisfação " + SimFormat.Number(x.satisfactionAverage) + " de 100).";
            if (drivers.Count > 0) text += " Pesaram contra: " + Join(drivers) + ".";
            messages.Add(Message("clientes", x.npsAverage >= 50d ? FeedbackTone.Positive : (x.npsAverage >= 0d ? FeedbackTone.Neutral : FeedbackTone.Negative), text, x.npsAverage));
        }

        // ------------------------------------------------------------------ Eventos

        private static void Events(List<FeedbackMessage> messages, MonthlyReport report)
        {
            var relevant = report.events.Where(e => e.eventId != MonthSimulation.PreventiveSaveEventId).ToList();
            if (relevant.Count == 0)
            {
                messages.Add(Message("eventos", FeedbackTone.Neutral, "Nenhum evento relevante ocorreu neste mês."));
                return;
            }
            int negative = relevant.Count(e => e.impact == EventImpact.Negative);
            int positive = relevant.Count(e => e.impact == EventImpact.Positive);
            double cash = relevant.Sum(e => e.cashImpact);
            messages.Add(Message("eventos", negative > positive ? FeedbackTone.Warning : FeedbackTone.Neutral,
                Count(relevant.Count, "evento", "eventos") + " no mês (" + Count(negative, "negativo", "negativos") + ", " + Count(positive, "positivo", "positivos")
                + "), com impacto direto no caixa de " + SimFormat.Money(cash) + ".",
                relevant.Count, cash));
        }

        private static string Count(int n, string one, string many) => n + " " + (n == 1 ? one : many);
    }
}
