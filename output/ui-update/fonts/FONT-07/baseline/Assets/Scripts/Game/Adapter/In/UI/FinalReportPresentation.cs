using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain.Service;

namespace Game.Adapter.In.UI
{
    /// <summary>Read-only documentary projection. Saved choices are evidence, never authored BMC answers.</summary>
    public static class FinalReportPresentation
    {
        public sealed class Block { public string Title, Text, Source; }
        public sealed class Report
        {
            public string Company, Identity, Period, Conclusion, Revenue, Net, Cash, Reputation, Alignment;
            public QuarterlyResultPresentation.Report Quarter;
            public Block[] Bmc, Reflection;
        }
        private static string Available(string value, string missing) => string.IsNullOrWhiteSpace(value) ? missing : value;
        private static Block Item(string title, string text, string source) => new() { Title = title, Text = text, Source = source };
        private static bool HasRecordedList(string json, string field)
        {
            if (string.IsNullOrWhiteSpace(json)) return false;
            var value = Newtonsoft.Json.Linq.JObject.Parse(json)[field];
            if (value == null || value.Type == Newtonsoft.Json.Linq.JTokenType.Null) return false;
            if (value.Type != Newtonsoft.Json.Linq.JTokenType.Array) throw new InvalidOperationException("Invalid saved choices.");
            return true;
        }
        public static Report Build(GameSessionEntity session, IEnumerable<RoundResultEntity> results, IEnumerable<SessionEventHistoryEntity> events)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            if (session.status != GameSessionStatus.COMPLETED) throw new InvalidOperationException("The campaign is not completed.");
            var quarter = QuarterlyResultPresentation.Build(session.sessionId, results);
            var history = events?.ToArray() ?? throw new ArgumentNullException(nameof(events));
            if (history.Any(e => e == null || e.sessionId != session.sessionId || e.round < 1 || e.round > 3))
                throw new InvalidOperationException("Invalid event history.");
            var choices = EstablishmentSummaryService.Build(session.Copy()); // No forecast or query for a completed detached session.
            if (!HasRecordedList(session.teamJson, "members")) choices.TeamDetails = "Equipe não registrada.";
            if (!HasRecordedList(session.equipmentJson, "equipmentIds")) choices.EquipmentDetails = "Equipamentos não registrados.";
            if (!HasRecordedList(session.menuPricingJson, "items")) choices.MenuDetails = "Cardápio e preços não registrados.";
            var last = quarter.Last > 0 ? quarter.Months[quarter.Last - 1] : null;
            string months = quarter.Count == 3 ? "3 meses encerrados e salvos" : $"{quarter.Count} de 3 meses com resultado salvo";
            string flow = quarter.Count > 0 ? $"Receita de vendas: {QuarterlyResultPresentation.Money(quarter.Revenue)}\nResultado de caixa: {QuarterlyResultPresentation.SignedMoney(quarter.Net)}" : "Nenhum resultado mensal salvo.";
            var report = new Report {
                Quarter = quarter, Company = Available(session.companyName, "Nome do estabelecimento não registrado"),
                Identity = "Participante: " + Available(session.studentName, "Não registrado") + " • RA: " + Available(session.studentRA, "Não registrado"),
                Period = $"CAMPANHA CONCLUÍDA • {quarter.Count}/3 MESES SALVOS",
                Conclusion = $"Sua campanha de três meses foi concluída. Há {months}. Consulte as escolhas e os resultados registrados antes de iniciar uma nova campanha.",
                Revenue = quarter.Count > 0 ? QuarterlyResultPresentation.Money(quarter.Revenue) : "Não disponível",
                Net = quarter.Count > 0 ? QuarterlyResultPresentation.SignedMoney(quarter.Net) : "Não disponível",
                Cash = last != null ? QuarterlyResultPresentation.Money(last.closingCash) : "Não disponível",
                Reputation = last != null ? last.reputationAtEnd + " / 100" : "Não disponível",
                Alignment = last != null ? MonthlyResultPresentation.ClassificationLabel(last.alignmentClassification) + " • " + last.alignmentScore + " pontos" : "Não disponível"
            };
            report.Bmc = new[] {
                Item("Parcerias principais", "Parcerias não registradas nesta campanha.", "Informação indisponível para consulta."),
                Item("Atividades principais", "Tipo escolhido: " + choices.RestaurantLabel + ".\nAtividades detalhadas: não registradas.", "Origem: tipo de restaurante escolhido. Sem detalhamento de atividades."),
                Item("Recursos principais", "Equipe registrada:\n" + choices.TeamDetails + "\n\nEquipamentos registrados:\n" + choices.EquipmentDetails, "Origem: escolhas salvas de equipe e equipamentos."),
                Item("Proposta de valor", choices.RestaurantLabel + " • " + choices.LocationLabel + "\nCardápio registrado:\n" + choices.MenuDetails + "\n\nProposta textual: não registrada.", "Origem: restaurante, localização, cardápio e preços salvos. Sem resposta BMC autoral."),
                Item("Relacionamento com clientes", "Reputação ao fim do último mês salvo: " + report.Reputation + ".\nEstratégia de relacionamento: não registrada.", "Origem: reputação do último resultado mensal salvo."),
                Item("Canais", "Canais de venda não registrados nesta campanha.", "Informação indisponível para consulta."),
                Item("Segmentos de clientes", "Público escolhido: " + choices.SegmentLabel + ".\nLocalização escolhida: " + choices.LocationLabel + ".", "Origem: escolhas salvas de público e localização."),
                Item("Estrutura de custos", quarter.Count == 0 ? "Nenhum resultado mensal salvo." : "Saídas dos meses salvos: " + QuarterlyResultPresentation.Money(-quarter.Rows[7].Aggregate.Amount.Value) + ".\nInclui a parcela de crédito. Consulte o detalhamento na DRE.", "Origem: soma das saídas mensais salvas; principal e juros já incluídos na parcela."),
                Item("Fontes de receita", flow + "\nReceitas por produto ou canal: não disponíveis.", "Origem: receitas e resultados mensais salvos, sem divisão por produto ou canal.")
            };
            string decisions = string.Join("\n", choices.Decisions.Select(d => d.Label + ": " + d.Detail));
            string recordedEvents = history.Length == 0 ? "Nenhuma decisão de evento registrada." : string.Join("\n\n", history.OrderBy(e => e.round).ThenBy(e => e.day).Select(e =>
                $"Mês {e.round} • {Available(e.eventName, "Evento sem título salvo")}\nEscolha: {Available(e.selectedChoiceTitle, "Título não registrado")}\n{Available(e.selectedChoiceText, "Descrição da escolha não registrada.")}"));
            report.Reflection = new[] {
                Item("Escolhas da campanha", decisions, "Origem: escolhas salvas ao fim da campanha; sem histórico de alterações."),
                Item("Decisões de evento registradas", recordedEvents, "Origem: evento e opção registrados no momento da escolha."),
                Item("Reflexão e justificativas", "Perguntas, respostas do participante, justificativas e feedback docente não estão disponíveis. O jogo ainda não oferece um formulário de reflexão. As escolhas exibidas acima não são respostas reflexivas.", "Complemento documental ainda indisponível; nenhuma resposta foi presumida."),
                Item("Limites dos relatórios", "A DRE apresenta o resultado de caixa mensal e seu comparativo trimestral. Balanço patrimonial e fluxo de caixa completo não estão disponíveis nesta versão.", "Origem disponível: resultados mensais salvos. Os demais documentos permanecem pendentes.")
            };
            return report;
        }
    }
}
