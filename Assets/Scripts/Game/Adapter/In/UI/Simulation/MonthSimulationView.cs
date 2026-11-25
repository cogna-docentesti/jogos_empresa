// Assets/Scripts/Game/Adapter/In/UI/Simulation/MonthSimulationView.cs
// Tela do mês rodando (Panel_MonthSimulation). Só desenha; nenhuma conta aqui.
// O calendário (EventCalendarView) mostra a cor de vendas de cada dia e a etiqueta de cada evento com a reação escolhida.
using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Adapter.In.Controllers
{
    public class MonthSimulationView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI dayText, cashText, inflowText, outflowText, resultText, speedText, lastEventsText;
        [SerializeField] private EventCalendarView calendar;
        [SerializeField] private GameObject commitErrorPanel;
        [SerializeField] private TextMeshProUGUI commitErrorText;
        [SerializeField] private Button retryButton;

        [Header("Confirmação do ABANDONAR MÊS")]
        [SerializeField] private GameObject abandonConfirmPanel;
        [SerializeField] private Button abandonConfirmButton, abandonCancelButton;

        private double _cashIn, _cashStart, _revenueSum;
        private int _days;
        private readonly List<string> _eventLines = new List<string>();

        public EventCalendarView Calendar => calendar;
        public bool AbandonConfirmOpen => abandonConfirmPanel != null && abandonConfirmPanel.activeSelf;

        public void ShowMonthStart(int month, double cash)
        {
            _cashIn = 0; _revenueSum = 0; _days = 0;
            _cashStart = cash;
            _eventLines.Clear();
            dayText.text = $"Mês {month} · dia 0 de 30";
            cashText.text = $"R$ {cash:N0}";
            inflowText.text = outflowText.text = resultText.text = "R$ 0";
            lastEventsText.text = "Nenhum evento até agora.";
            if (commitErrorPanel != null) commitErrorPanel.SetActive(false);
            HideAbandonConfirm();
            calendar?.Clear();
        }

        /// events = ocorrências do dia (já com a reação escolhida, quando houve decisão).
        public void RenderDay(DayResult d, IEnumerable<EventOccurrence> events)
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

            if (calendar != null)
            {
                calendar.SetTone(d.Day, EventCalendarView.ToneFor(d.Revenue, _revenueSum / _days));
                foreach (var occ in events ?? Enumerable.Empty<EventOccurrence>())
                {
                    string reaction = ReactionLabel(occ);
                    calendar.AddEvent(d.Day, occ.Definition.Title, occ.Definition.IsNegative, reaction);
                    _eventLines.Insert(0, $"<b>Dia {d.Day}</b>  {occ.Definition.Title}" + (string.IsNullOrEmpty(reaction) ? "" : $"  →  {reaction}"));
                }
                if (_eventLines.Count > 0) lastEventsText.text = string.Join("\n", _eventLines);
            }
        }

        /// Popup aberto: o dia fica marcado no calendário até a escolha.
        public void ShowPending(EventOccurrence occurrence) => calendar?.MarkPending(occurrence.Day, occurrence.Definition.Title);

        private static string ReactionLabel(EventOccurrence occ)
            => occ.ChosenReactionId == null ? null : occ.Definition.Reactions.FirstOrDefault(r => r.Id == occ.ChosenReactionId)?.Label;

        public void ShowSpeed(float speed) => speedText.text = speed <= 0 ? "II" : $"{speed:0}x";

        public void ShowCommitError(string message, Action retry)
        {
            commitErrorPanel.SetActive(true);
            commitErrorText.text = "Não foi possível salvar o fechamento: " + message;
            retryButton.onClick.RemoveAllListeners();
            retryButton.onClick.AddListener(() => { commitErrorPanel.SetActive(false); retry(); });
        }

        /// "Abandonar o mês?" com Continuar jogando (cancelar) e Abandonar mês (confirmar).
        public void ShowAbandonConfirm(Action confirm, Action cancel)
        {
            if (abandonConfirmPanel == null) { confirm(); return; }       // cena antiga sem o diálogo: comportamento anterior
            abandonConfirmPanel.SetActive(true);
            abandonConfirmPanel.transform.SetAsLastSibling();
            abandonConfirmButton.onClick.RemoveAllListeners();
            abandonConfirmButton.onClick.AddListener(() => { HideAbandonConfirm(); confirm(); });
            abandonCancelButton.onClick.RemoveAllListeners();
            abandonCancelButton.onClick.AddListener(() => { HideAbandonConfirm(); cancel(); });
            abandonCancelButton.Select();
        }

        public void HideAbandonConfirm()
        {
            if (abandonConfirmPanel != null) abandonConfirmPanel.SetActive(false);
        }
    }
}
