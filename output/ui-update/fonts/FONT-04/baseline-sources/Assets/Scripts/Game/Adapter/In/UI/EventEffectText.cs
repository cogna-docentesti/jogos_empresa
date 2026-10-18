using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Game.Adapter.In.UI
{
    public static class EventEffectText
    {
        private static readonly CultureInfo Pt = CultureInfo.GetCultureInfo("pt-BR");
        public static string Format(EventEffectData effects)
        {
            if (effects == null) return "Efeitos indisponíveis.";
            var lines = new List<string>();
            if (effects.cashDelta != 0) lines.Add("Caixa: " + (effects.cashDelta > 0 ? "+" : "−") + FinancialScreenView.Money(Math.Abs(effects.cashDelta)));
            if (effects.reputationDelta != 0) lines.Add("Reputação: " + (effects.reputationDelta > 0 ? "+" : "") + effects.reputationDelta + " pontos");
            Multiplier(lines, "Demanda", effects.demandMultiplier);
            Multiplier(lines, "Capacidade", effects.capacityMultiplier);
            Multiplier(lines, "Disponibilidade de insumos", effects.stockMultiplier);
            Multiplier(lines, "Custo de ingredientes", effects.ingredientCostMultiplier);
            Multiplier(lines, "Ticket médio", effects.averageTicketMultiplier);
            if (lines.Count == 0) lines.Add("Sem alteração dos indicadores.");
            lines.Add("Duração operacional: " + (effects.duration == EventEffectDuration.CURRENT_MONTH ? "do dia do evento ao fim do mês" : effects.duration == EventEffectDuration.CURRENT_DAY ? "dia do evento" : "imediata (dia do evento)"));
            return string.Join("  •  ", lines);
        }
        private static void Multiplier(List<string> lines, string label, float value)
        { if (value != 1) { float delta = (value - 1) * 100; lines.Add(label + ": " + (delta > 0 ? "+" : "") + delta.ToString("0.##", Pt) + "%"); } }
        public static string History(SessionEventHistoryEntity item)
        {
            if (!string.IsNullOrWhiteSpace(item.effectsJson))
            { try { return Format(JsonUtility.FromJson<EventEffectData>(item.effectsJson)); } catch (ArgumentException) { return "Efeitos registrados indisponíveis."; } }
            return "Registro anterior • Caixa: " + FinancialScreenView.Money(item.cashChange) + " • Reputação: " + item.reputationChange + " • Clientes: " + item.clientsChange;
        }
    }
}
