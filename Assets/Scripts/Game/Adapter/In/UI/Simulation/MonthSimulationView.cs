// Assets/Scripts/Game/Adapter/In/UI/Simulation/MonthSimulationView.cs
// Tela do mês rodando (Panel_MonthSimulation). Só desenha; nenhuma conta aqui.
using System;
using Game.Domain.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Adapter.In.Controllers
{
    public class MonthSimulationView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI dayText, cashText, inflowText, outflowText, resultText, speedText, lastEventsText;
        [SerializeField] private Image[] calendarCells = new Image[30];
        [SerializeField] private Color weakDay = new Color(0.85f, 0.35f, 0.30f), normalDay = new Color(0.95f, 0.75f, 0.30f), strongDay = new Color(0.30f, 0.65f, 0.40f);
        [SerializeField] private GameObject commitErrorPanel;
        [SerializeField] private TextMeshProUGUI commitErrorText;
        [SerializeField] private Button retryButton;

        private double _cashIn, _cashStart, _revenueSum;
        private int _days;

        public void ShowMonthStart(int month, double cash)
        {
            _cashIn = 0; _revenueSum = 0; _days = 0;
            _cashStart = cash;
            dayText.text = $"Mês {month} · dia 0 de 30";
            cashText.text = $"R$ {cash:N0}";
            inflowText.text = outflowText.text = resultText.text = "R$ 0";
            lastEventsText.text = "";
            if (commitErrorPanel != null) commitErrorPanel.SetActive(false);
            foreach (var cell in calendarCells) if (cell != null) cell.color = Color.white;
        }

        public void RenderDay(DayResult d)
        {
            _days++;
            _revenueSum += d.Revenue;
            _cashIn += d.Revenue;
            double cashOut = _cashStart + _cashIn - d.CashEnd;
            dayText.text = $"Dia {d.Day} de 30";
            cashText.text = $"R$ {d.CashEnd:N0}";
            inflowText.text = $"R$ {_cashIn:N0}";
            outflowText.text = $"R$ {cashOut:N0}";
            resultText.text = $"R$ {d.CashEnd - _cashStart:N0}";
            if (d.Events.Count > 0) lastEventsText.text = $"Dia {d.Day}: " + string.Join(", ", d.Events) + "\n" + lastEventsText.text;

            int i = d.Day - 1;
            if (i >= 0 && i < calendarCells.Length && calendarCells[i] != null)
            {
                double avg = _revenueSum / _days;                  // faixa de vendas do dia (cor do calendário)
                calendarCells[i].color = d.Revenue < 0.85 * avg ? weakDay : d.Revenue > 1.15 * avg ? strongDay : normalDay;
            }
        }

        public void ShowSpeed(float speed) => speedText.text = speed <= 0 ? "II" : $"{speed:0}x";

        public void ShowCommitError(string message, Action retry)
        {
            commitErrorPanel.SetActive(true);
            commitErrorText.text = "Não foi possível salvar o fechamento: " + message;
            retryButton.onClick.RemoveAllListeners();
            retryButton.onClick.AddListener(() => { commitErrorPanel.SetActive(false); retry(); });
        }
    }
}
