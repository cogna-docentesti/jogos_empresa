using System.Linq;
using System.Text;
using Game.Adapter.In.UI.Theme;
using Game.Domain.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Adapter.In.UI.Simulation
{
    /// <summary>
    /// Uma celula do calendario do mes. Prefab: DayCell (ver tutorial, secao "Celula do dia").
    /// Mostra o numero do dia, a faixa de vendas (cor de fundo) e ate tres lancamentos do dia.
    /// </summary>
    public sealed class SimulationDayCellView : MonoBehaviour
    {
        [SerializeField] private Image background;
        [SerializeField] private Image outline;
        [SerializeField] private TMP_Text dayNumber;
        [SerializeField] private TMP_Text entriesText;
        [SerializeField] private Image eventMarker;
        [Tooltip("Lancamentos mostrados na celula. O resto aparece como '+N'.")]
        [SerializeField] private int maxEntries = 3;

        public int Day { get; private set; }

        public void SetEmpty()
        {
            Day = 0;
            if (dayNumber != null) dayNumber.text = string.Empty;
            if (entriesText != null) entriesText.text = string.Empty;
            if (background != null) background.color = GamePalette.WithAlpha(GamePalette.Surface, 0.4f);
            if (outline != null) outline.enabled = false;
            if (eventMarker != null) eventMarker.enabled = false;
        }

        public void SetFuture(int day)
        {
            Day = day;
            if (dayNumber != null)
            {
                dayNumber.text = day.ToString();
                dayNumber.color = GamePalette.Gray;
            }
            if (entriesText != null) entriesText.text = string.Empty;
            if (background != null) background.color = GamePalette.Surface;
            if (outline != null) outline.enabled = false;
            if (eventMarker != null) eventMarker.enabled = false;
        }

        public void SetToday(int day)
        {
            SetFuture(day);
            if (dayNumber != null) dayNumber.color = GamePalette.Gold;
            if (outline != null)
            {
                outline.enabled = true;
                outline.color = GamePalette.Gold;
            }
        }

        public void SetResult(DayResult result)
        {
            Day = result.day;
            if (dayNumber != null)
            {
                dayNumber.text = result.day.ToString();
                dayNumber.color = GamePalette.Ink;
            }
            if (background != null) background.color = SimulationUi.SalesBandColor(result.salesBand);
            if (outline != null) outline.enabled = false;

            var entries = result.entries ?? new System.Collections.Generic.List<CalendarEntry>();
            if (eventMarker != null)
            {
                var evt = entries.FirstOrDefault(e => e.kind == EntryKind.Event);
                eventMarker.enabled = evt != null;
                if (evt != null) eventMarker.color = evt.icon == "event_positive" ? GamePalette.Money : GamePalette.Warn;
            }
            if (entriesText == null) return;

            var text = new StringBuilder();
            int shown = 0;
            // Eventos primeiro, depois as maiores quantias: e o que o aluno precisa ver.
            foreach (var entry in entries.OrderByDescending(e => e.kind == EntryKind.Event).ThenByDescending(e => System.Math.Abs(e.amount)))
            {
                if (shown >= maxEntries) break;
                string color = SimulationUi.Hex(SimulationUi.EntryColor(entry));
                text.Append("<color=").Append(color).Append('>');
                text.Append(entry.label);
                if (System.Math.Abs(entry.amount) > 0.004d) text.Append(' ').Append(SimFormat.Money(entry.amount, decimals: false));
                text.Append("</color>\n");
                shown++;
            }
            if (entries.Count > shown) text.Append("<color=").Append(GamePalette.HexMuted).Append(">+").Append(entries.Count - shown).Append("</color>");
            entriesText.text = text.ToString().TrimEnd('\n');
        }
    }
}
