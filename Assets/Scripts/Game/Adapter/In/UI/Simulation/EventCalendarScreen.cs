// Assets/Scripts/Game/Adapter/In/UI/Simulation/EventCalendarScreen.cs
// Tela "Eventos do mês" do hub (Panel_EventCalendar, PanelId.EventHistory): um calendário por mês jogado com a cor de
// vendas de cada dia e os eventos com a reação escolhida, mais a lista do mês ao lado.
// Lê o que o fechamento gravou: SessionEventHistoryEntity (eventos e reações) e os relatórios do mês (vendas por dia).
// Não muda nada no banco.
using System.Collections.Generic;
using System.Linq;
using Game.Adapter.In.UI;
using Game.Domain.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Adapter.In.Controllers
{
    public sealed class EventCalendarScreen : MonoBehaviour
    {
        [SerializeField] private EventCalendarView calendar;
        [SerializeField] private TextMeshProUGUI monthTitle, listText, emptyText;
        [SerializeField] private Button[] monthTabs = new Button[3];
        [SerializeField] private GameObject calendarCard;

        private List<SessionEventHistoryEntity> _rows = new List<SessionEventHistoryEntity>();
        private IReadOnlyList<MonthReport> _reports;
        private int _played, _selected;

        public int SelectedMonth => _selected;
        public int MonthsPlayed => _played;
        public int EventsShown { get; private set; }

        private void Awake()
        {
            for (int i = 0; i < monthTabs.Length; i++)
            {
                int month = i + 1;
                if (monthTabs[i] != null) monthTabs[i].onClick.AddListener(() => Show(month));
            }
        }

        private void OnEnable() => Refresh();

        public void Refresh()
        {
            string sessionId = GameSessionState.Current?.sessionId;
            var db = DatabaseInitializer.DatabaseService?.Connection;
            _rows = sessionId != null && db != null ? new SessionEventHistoryRepository(db).GetBySession(sessionId) : new List<SessionEventHistoryEntity>();
            _reports = FindFirstObjectByType<SystemicRoundFlow>(FindObjectsInactive.Include)?.MatchReports();
            int fromReports = _reports?.Count ?? 0;
            int fromRows = _rows.Count > 0 ? _rows.Max(r => r.round) : 0;
            _played = Mathf.Clamp(Mathf.Max(fromReports, fromRows), 0, monthTabs.Length);
            Show(_played);
        }

        public void Show(int month)
        {
            _selected = Mathf.Clamp(month, 0, _played);
            for (int i = 0; i < monthTabs.Length; i++)
            {
                if (monthTabs[i] == null) continue;
                monthTabs[i].interactable = i < Mathf.Max(1, _played);      // Mês 1 sempre clicável (mostra o estado vazio)
                monthTabs[i].GetComponent<SharedControlAppearance>()?.SetSelected(i + 1 == _selected);
            }
            bool empty = _selected == 0;
            if (emptyText != null) emptyText.gameObject.SetActive(empty);
            if (calendarCard != null) calendarCard.SetActive(!empty);
            calendar.Clear();
            EventsShown = 0;
            if (empty)
            {
                monthTitle.text = "Nenhum mês jogado ainda";
                listText.text = "Os eventos aparecem aqui depois do primeiro mês.";
                return;
            }

            monthTitle.text = $"Mês {_selected}";
            var report = _reports?.FirstOrDefault(r => r.Month == _selected);
            if (report?.Days != null && report.Days.Count > 0)
            {
                double average = report.Days.Average(d => d.Revenue);
                foreach (var d in report.Days) calendar.SetTone(d.Day, EventCalendarView.ToneFor(d.Revenue, average));
            }

            var rows = _rows.Where(r => r.round == _selected).OrderBy(r => r.day).ToList();
            foreach (var r in rows)
                calendar.AddEvent(r.day, r.eventName, r.polarity == EventPolarity.NEGATIVE.ToString(), r.selectedChoiceTitle);
            EventsShown = rows.Count;
            listText.text = rows.Count == 0
                ? "Nenhum evento neste mês."
                : string.Join("\n", rows.Select(Line));
        }

        private static string Line(SessionEventHistoryEntity r)
        {
            string reaction = string.IsNullOrEmpty(r.selectedChoiceTitle) ? "" : $"  →  {r.selectedChoiceTitle}";
            string money = Mathf.Abs(r.cashChange) < 0.005f ? "" : $"  ({(r.cashChange < 0 ? "-" : "+")}R$ {Mathf.Abs(r.cashChange):N0})";
            return $"<b>Dia {r.day}</b>  {r.eventName}{reaction}{money}";
        }
    }
}
