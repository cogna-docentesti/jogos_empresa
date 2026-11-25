// Assets/Scripts/Game/Adapter/In/UI/Simulation/EventCalendarView.cs
// Calendário de 30 dias (dia 1 = segunda, colunas Seg a Dom) com a cor de vendas de cada dia e uma etiqueta
// com o evento do dia e a reação escolhida. Usado na tela do mês rodando (MonthSimulationView) e na tela
// "Eventos do mês" do hub (EventCalendarScreen). Só desenha: quem chama decide o que mostrar.
// Montado pelo menu Game > Simulação > Montar painéis na GameScene (SimulationPanelsBuilder).
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Adapter.In.Controllers
{
    public sealed class EventCalendarView : MonoBehaviour
    {
        public enum Tone { None, Weak, Normal, Strong }

        [Serializable]
        public sealed class DayCell
        {
            public Image background;
            public TextMeshProUGUI number;
            public Image marker;               // etiqueta do evento (some quando o dia não tem evento)
            public TextMeshProUGUI markerText;
            public TextMeshProUGUI reactionText;
        }

        [SerializeField] private DayCell[] days = new DayCell[30];
        [SerializeField] private Color emptyDay = Color.white;
        [SerializeField] private Color weakDay = new Color(0.85f, 0.35f, 0.30f), normalDay = new Color(0.95f, 0.75f, 0.30f), strongDay = new Color(0.30f, 0.65f, 0.40f);
        [SerializeField] private Color negativeEvent = new Color(0.65f, 0.20f, 0.24f), positiveEvent = new Color(0.08f, 0.32f, 0.27f);
        [SerializeField] private Color pendingEvent = new Color(0.43f, 0.16f, 0.85f);

        private readonly List<(string title, bool negative, string reaction)>[] _events = Enumerable.Range(0, 30)
            .Select(_ => new List<(string, bool, string)>()).ToArray();

        public int DayCount => days.Length;

        /// Dias sem cor e sem etiqueta.
        public void Clear()
        {
            for (int i = 0; i < days.Length; i++)
            {
                _events[i].Clear();
                var cell = days[i];
                if (cell == null) continue;
                if (cell.background != null) cell.background.color = emptyDay;
                if (cell.marker != null) cell.marker.gameObject.SetActive(false);
                if (cell.reactionText != null) cell.reactionText.text = "";
            }
        }

        /// Cor do dia pela receita (fraco, normal ou forte).
        public void SetTone(int day, Tone tone)
        {
            var cell = Cell(day);
            if (cell?.background == null) return;
            cell.background.color = tone == Tone.Weak ? weakDay : tone == Tone.Normal ? normalDay : tone == Tone.Strong ? strongDay : emptyDay;
        }

        /// Etiqueta do evento no dia. reaction = rótulo da reação escolhida (vazio para evento sem decisão).
        public void AddEvent(int day, string title, bool negative, string reaction)
        {
            if (Cell(day) == null) return;
            _events[day - 1].Add((title, negative, reaction));
            Draw(day);
        }

        /// Dia com um popup aberto esperando a escolha do jogador.
        public void MarkPending(int day, string title)
        {
            var cell = Cell(day);
            if (cell?.marker == null) return;
            cell.marker.gameObject.SetActive(true);
            cell.marker.color = pendingEvent;
            cell.markerText.text = title;
            if (cell.reactionText != null) cell.reactionText.text = "decidindo...";
        }

        private void Draw(int day)
        {
            var cell = Cell(day);
            var list = _events[day - 1];
            if (cell?.marker == null) return;
            cell.marker.gameObject.SetActive(list.Count > 0);
            if (list.Count == 0) return;
            cell.marker.color = list.Any(e => e.negative) ? negativeEvent : positiveEvent;
            cell.markerText.text = list.Count == 1 ? list[0].title : $"{list.Count} eventos";
            if (cell.reactionText != null)
                cell.reactionText.text = list.Count == 1 ? list[0].reaction ?? "" : string.Join(" · ", list.Select(e => e.reaction).Where(r => !string.IsNullOrEmpty(r)));
        }

        private DayCell Cell(int day) => day >= 1 && day <= days.Length ? days[day - 1] : null;

        public static Tone ToneFor(double revenue, double average)
            => average <= 0 ? Tone.Normal : revenue < 0.85 * average ? Tone.Weak : revenue > 1.15 * average ? Tone.Strong : Tone.Normal;
    }
}
