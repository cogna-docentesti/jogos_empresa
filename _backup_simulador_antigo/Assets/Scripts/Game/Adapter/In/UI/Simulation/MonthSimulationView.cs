using System.Collections.Generic;
using System.Linq;
using Game.Adapter.In.Controllers;
using Game.Adapter.In.UI.Theme;
using Game.Domain.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Adapter.In.UI.Simulation
{
    /// <summary>
    /// Tela do mes rodando (mockup "RODADA 3 / MES 3 - MARCO"): cabecalho, velocidade, quatro
    /// indicadores, calendario de 30 dias e "Ultimos acontecimentos". So mostra: nao calcula e
    /// nao grava. Quem manda e o MonthSimulationController.
    /// </summary>
    public sealed class MonthSimulationView : MonoBehaviour
    {
        [Header("Raiz do painel (Panel_MonthSimulation)")]
        [SerializeField] private GameObject root;

        [Header("Cabecalho")]
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text subtitleText;
        [SerializeField] private TMP_Text dayText;
        [SerializeField] private Image dayProgressFill;

        [Header("Velocidade")]
        [SerializeField] private Button speed1Button;
        [SerializeField] private Button speed2Button;
        [SerializeField] private Button speed4Button;
        [SerializeField] private Button pauseButton;
        [SerializeField] private TMP_Text pauseLabel;
        [SerializeField] private Button skipButton;

        [Header("Indicadores")]
        [SerializeField] private TMP_Text cashValue;
        [SerializeField] private TMP_Text revenueValue;
        [SerializeField] private TMP_Text expensesValue;
        [SerializeField] private TMP_Text resultValue;

        [Header("Calendario")]
        [SerializeField] private Transform calendarGrid;
        [SerializeField] private SimulationDayCellView dayCellTemplate;

        [Header("Ultimos acontecimentos")]
        [SerializeField] private Transform tickerContainer;
        [SerializeField] private TMP_Text tickerItemTemplate;
        [Min(1)] [SerializeField] private int maxTickerItems = 6;

        [Header("Rodape")]
        [SerializeField] private Button reportButton;
        [SerializeField] private Button exitMonthButton;

        [Header("Aviso de erro")]
        [SerializeField] private GameObject errorBanner;
        [SerializeField] private TMP_Text errorText;
        [SerializeField] private Button retryButton;

        private MonthSimulationController _controller;
        private readonly Dictionary<int, SimulationDayCellView> _cells = new Dictionary<int, SimulationDayCellView>();
        private readonly List<string> _ticker = new List<string>();
        private bool _wired;

        public void Bind(MonthSimulationController controller)
        {
            _controller = controller;
            if (_wired) return;
            _wired = true;
            AddClick(speed1Button, () => _controller.SetSpeed(1));
            AddClick(speed2Button, () => _controller.SetSpeed(2));
            AddClick(speed4Button, () => _controller.SetSpeed(4));
            AddClick(pauseButton, () => _controller.TogglePause());
            AddClick(skipButton, () => _controller.SkipToNextStop());
            AddClick(reportButton, () => _controller.OpenPartialReport());
            AddClick(exitMonthButton, () => _controller.AbandonMonth());
            AddClick(retryButton, () => _controller.RetryCommit());
            if (dayCellTemplate != null) dayCellTemplate.gameObject.SetActive(false);
            if (tickerItemTemplate != null) tickerItemTemplate.gameObject.SetActive(false);
        }

        private static void AddClick(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button != null) button.onClick.AddListener(action);
        }

        private MonthSimulation Simulation => _controller != null ? _controller.Service.Simulation : null;

        public void Show()
        {
            SimulationUi.SetActive(Root, true);
            SimulationUi.SetActive(errorBanner, false);
        }

        public void Hide() => SimulationUi.SetActive(Root, false);

        private GameObject Root => root != null ? root : gameObject;

        public void RenderAll()
        {
            var sim = Simulation;
            if (sim == null) return;
            var calendar = sim.Catalog.Config.calendar;
            if (titleText != null) titleText.text = SimulationUi.MonthHeader(sim.Month, calendar.firstCalendarMonth);
            if (subtitleText != null)
            {
                var session = GameSessionState.Current;
                string company = session != null && !string.IsNullOrWhiteSpace(session.companyName) ? session.companyName : sim.Catalog.Restaurant(sim.Inputs.RestaurantTypeId).Name;
                subtitleText.text = company + " · " + sim.Catalog.Location(sim.Inputs.LocationId).Name;
            }
            BuildCalendar(sim);
            _ticker.Clear();
            foreach (var day in sim.Days) AddTicker(day);
            RenderTicker();
            RenderHeader(sim);
            RenderKpis(sim);
            RenderControls();
        }

        public void AppendDay(DayResult day)
        {
            var sim = Simulation;
            if (sim == null || day == null) return;
            if (_cells.TryGetValue(day.day, out var cell)) cell.SetResult(day);
            if (sim.Status != SimulationStatus.Finished && _cells.TryGetValue(sim.CurrentDay, out var today)) today.SetToday(sim.CurrentDay);
            AddTicker(day);
            RenderTicker();
            RenderHeader(sim);
            RenderKpis(sim);
        }

        public void RenderControls()
        {
            if (_controller == null) return;
            int speed = _controller.Speed;
            bool paused = _controller.Paused;
            Highlight(speed1Button, !paused && speed == 1);
            Highlight(speed2Button, !paused && speed == 2);
            Highlight(speed4Button, !paused && speed == 4);
            Highlight(pauseButton, paused);
            if (pauseLabel != null) pauseLabel.text = paused ? "▶" : "II";
        }

        private static void Highlight(Button button, bool on)
        {
            if (button == null) return;
            var image = button.targetGraphic as Image;
            if (image != null) image.color = on ? GamePalette.Gold : GamePalette.SurfaceAlt;
            var label = button.GetComponentInChildren<TMP_Text>();
            if (label != null) label.color = on ? GamePalette.OnGold : GamePalette.Ink;
        }

        public void ShowError(string message)
        {
            SimulationUi.SetActive(errorBanner, true);
            SimulationUi.SetActive(retryButton, false);
            if (errorText != null) errorText.text = message;
        }

        public void ShowCommitFailure(string message)
        {
            SimulationUi.SetActive(errorBanner, true);
            SimulationUi.SetActive(retryButton, true);
            if (errorText != null)
                errorText.text = "O mês terminou, mas o resultado não foi gravado: " + message + "\nNada foi perdido. Toque em \"Tentar de novo\".";
        }

        // -----------------------------------------------------------------

        private void BuildCalendar(MonthSimulation sim)
        {
            _cells.Clear();
            if (calendarGrid == null || dayCellTemplate == null) return;
            SimulationUi.Clear(calendarGrid, dayCellTemplate.gameObject);
            int offset = sim.WeekdayOf(1);
            for (int i = 0; i < offset; i++)
            {
                var blank = SimulationUi.Spawn(dayCellTemplate, calendarGrid);
                blank.SetEmpty();
            }
            var done = sim.Days.ToDictionary(d => d.day);
            for (int day = 1; day <= sim.OperatingDays; day++)
            {
                var cell = SimulationUi.Spawn(dayCellTemplate, calendarGrid);
                if (done.TryGetValue(day, out var result)) cell.SetResult(result);
                else if (day == sim.CurrentDay && sim.Status != SimulationStatus.Finished) cell.SetToday(day);
                else cell.SetFuture(day);
                _cells[day] = cell;
            }
        }

        private void RenderHeader(MonthSimulation sim)
        {
            int shown = Mathf.Clamp(sim.Status == SimulationStatus.Finished ? sim.OperatingDays : sim.CurrentDay, 1, sim.OperatingDays);
            if (dayText != null) dayText.text = "Dia " + shown + " de " + sim.OperatingDays;
            if (dayProgressFill != null) dayProgressFill.fillAmount = sim.Days.Count / (float)Mathf.Max(1, sim.OperatingDays);
        }

        private void RenderKpis(MonthSimulation sim)
        {
            var flow = sim.CashFlowSoFar();
            double inflows = flow.TotalInflows;
            double outflows = flow.TotalOutflows;
            SetMoney(cashValue, sim.Cash, colorBySign: true);
            SetMoney(revenueValue, inflows, colorBySign: false);
            SetMoney(expensesValue, -outflows, colorBySign: false);
            SetMoney(resultValue, inflows - outflows, colorBySign: true);
        }

        private static void SetMoney(TMP_Text target, double value, bool colorBySign)
        {
            if (target == null) return;
            target.text = SimFormat.Money(value);
            target.color = colorBySign ? SimulationUi.AmountColor(value) : GamePalette.Ink;
        }

        private void AddTicker(DayResult day)
        {
            foreach (var entry in day.entries ?? new List<CalendarEntry>())
            {
                if (entry.routine) continue;
                string color = SimulationUi.Hex(SimulationUi.EntryColor(entry));
                string amount = System.Math.Abs(entry.amount) > 0.004d ? "  <b>" + SimFormat.SignedMoney(entry.amount) + "</b>" : string.Empty;
                _ticker.Add("<color=" + GamePalette.HexMuted + ">Dia " + day.day + "</color>  <color=" + color + ">" + entry.label + amount + "</color>");
            }
        }

        private void RenderTicker()
        {
            if (tickerContainer == null || tickerItemTemplate == null) return;
            SimulationUi.Clear(tickerContainer, tickerItemTemplate.gameObject);
            for (int i = _ticker.Count - 1, shown = 0; i >= 0 && shown < maxTickerItems; i--, shown++)
            {
                var item = SimulationUi.Spawn(tickerItemTemplate, tickerContainer);
                item.text = _ticker[i];
            }
        }
    }
}
