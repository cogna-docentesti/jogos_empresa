using System;
using System.Collections.Generic;
using Game.Adapter.In.UI.Theme;
using Game.Domain.Simulation;
using UnityEngine;

namespace Game.Adapter.In.UI.Simulation
{
    /// <summary>Cores e textos comuns das telas do simulador. Cores sempre do GamePalette.</summary>
    public static class SimulationUi
    {
        public static Color AmountColor(double value) => value > 0.004d ? GamePalette.Money : value < -0.004d ? GamePalette.Danger : GamePalette.Muted;

        public static Color EntryColor(CalendarEntry entry)
        {
            switch (entry?.kind)
            {
                case EntryKind.Revenue: return GamePalette.Money;
                case EntryKind.Expense: return GamePalette.Danger;
                case EntryKind.Event: return GamePalette.Warn;
                default: return GamePalette.Muted;
            }
        }

        /// <summary>Fundo da celula do dia conforme a faixa de vendas (HIGH, MEDIUM, LOW).</summary>
        public static Color SalesBandColor(string band)
        {
            switch (band)
            {
                case "HIGH": return GamePalette.WithAlpha(GamePalette.Money, 0.16f);
                case "LOW": return GamePalette.WithAlpha(GamePalette.Danger, 0.16f);
                default: return GamePalette.SurfaceAlt;
            }
        }

        public static Color ImpactColor(string impact)
            => impact == EventImpact.Positive ? GamePalette.Money : impact == EventImpact.Negative ? GamePalette.Danger : GamePalette.Muted;

        public static Color ToneColor(string tone)
        {
            switch (tone)
            {
                case FeedbackTone.Positive: return GamePalette.Money;
                case FeedbackTone.Negative: return GamePalette.Danger;
                case FeedbackTone.Warning: return GamePalette.Warn;
                default: return GamePalette.Ink;
            }
        }

        public static string Hex(Color color) => "#" + ColorUtility.ToHtmlStringRGB(color);

        /// <summary>Texto rico para o TextMeshPro: valor com cor de sinal.</summary>
        public static string ColoredMoney(double value) => "<color=" + Hex(AmountColor(value)) + ">" + SimFormat.Money(value) + "</color>";

        public static readonly string[] WeekdayShort = { "SEG", "TER", "QUA", "QUI", "SEX", "SÁB", "DOM" };

        public static string MonthHeader(int month, int firstCalendarMonth)
            => "RODADA " + month + " / MÊS " + month + " – " + SimFormat.CalendarMonthName(month, firstCalendarMonth).ToUpperInvariant();

        /// <summary>Remove filhos instanciados de um container (mantem o template, se for filho).</summary>
        public static void Clear(Transform container, GameObject keep = null)
        {
            if (container == null) return;
            for (int i = container.childCount - 1; i >= 0; i--)
            {
                var child = container.GetChild(i).gameObject;
                if (child == keep) continue;
                // Desliga antes de destruir: o Destroy so acontece no fim do quadro e o
                // layout nao deve contar a celula velha junto com a nova.
                child.SetActive(false);
                UnityEngine.Object.Destroy(child);
            }
        }

        public static T Spawn<T>(T template, Transform container) where T : Component
        {
            if (template == null || container == null) return null;
            var item = UnityEngine.Object.Instantiate(template, container);
            item.gameObject.SetActive(true);
            return item;
        }

        public static void SetActive(Component component, bool active)
        {
            if (component != null) component.gameObject.SetActive(active);
        }

        public static void SetActive(GameObject target, bool active)
        {
            if (target != null) target.SetActive(active);
        }
    }

    /// <summary>Par nome de icone (CalendarEntry.icon) e sprite, para o Inspector.</summary>
    [Serializable]
    public sealed class SimulationIcon
    {
        public string name;
        public Sprite sprite;
    }

    [Serializable]
    public sealed class SimulationIconSet
    {
        public Sprite fallback;
        public List<SimulationIcon> icons = new List<SimulationIcon>();

        public Sprite Get(string iconName)
        {
            var found = icons.Find(i => i != null && i.name == iconName);
            return found != null && found.sprite != null ? found.sprite : fallback;
        }
    }
}
