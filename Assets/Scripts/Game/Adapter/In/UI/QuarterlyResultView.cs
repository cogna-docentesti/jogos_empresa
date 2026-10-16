using System;
using System.Collections.Generic;
using System.Linq;
using Game.Adapter.In.UI.Theme;
using TMPro;
using UnityEngine;

namespace Game.Adapter.In.UI
{
    public sealed class QuarterlyResultView : MonoBehaviour
    {
        [Serializable] public sealed class TableRow { public TMP_Text label, rule; public TMP_Text[] cells; public UnityEngine.UI.Image background; }
        [SerializeField] private TMP_Text period, resultValue, cashValue, revenueValue, stateTitle, stateMessage, changeLabel, note;
        [SerializeField] private TMP_Text[] monthLabels;
        [SerializeField] private GameObject loadedBody, stateBody;
        [SerializeField] private TableRow[] rows;
        [SerializeField] private MonthlyResultView.ChartBar[] bars;
        [SerializeField] private UnityEngine.UI.ScrollRect tableScroll;
        [SerializeField] private UnityEngine.UI.Button backButton, retryButton;
        [SerializeField] private Sprite neutralRow, subtotalRow, positiveRow, negativeRow;
        public string DisplayState { get; private set; }
        public IReadOnlyList<TableRow> Rows => rows;
        public IReadOnlyList<MonthlyResultView.ChartBar> Bars => bars;
        public UnityEngine.UI.ScrollRect TableScroll => tableScroll;
        public UnityEngine.UI.Button BackButton => backButton;
        public UnityEngine.UI.Button RetryButton => retryButton;
        public void Actions(Action back, Action retry)
        {
            backButton.onClick.RemoveAllListeners(); backButton.onClick.AddListener(() => back());
            retryButton.onClick.RemoveAllListeners(); retryButton.onClick.AddListener(() => retry());
        }
        private static Color Semantic(double? value) => !value.HasValue || value == 0 ? GamePalette.Ink : value > 0 ? GamePalette.Money : GamePalette.ErrorText;
        public void Show(QuarterlyResultPresentation.Report data)
        {
            DisplayState = "loaded"; loadedBody.SetActive(true); stateBody.SetActive(false); retryButton.gameObject.SetActive(false);
            period.text = data.Period; changeLabel.text = data.ChangeLabel;
            revenueValue.text = QuarterlyResultPresentation.Money(data.Revenue);
            resultValue.text = QuarterlyResultPresentation.Money(data.Net); resultValue.color = Semantic(data.Net);
            cashValue.text = QuarterlyResultPresentation.Money(data.Closing); cashValue.color = Semantic(data.Closing);
            note.text = $"Agregado de {data.Count}/3 meses salvos. Δ compara valores mensais (último − primeiro), sem percentual. Principal e juros já estão na parcela. As regras de cada agregado aparecem sob o rótulo.";
            for (int m = 0; m < 3; m++) monthLabels[m].text = $"Mês {m + 1}" + (data.Months[m] == null ? "\nNão salvo" : "\nSalvo");
            for (int i = 0; i < rows.Length; i++)
            {
                var row = rows[i]; var d = data.Rows[i]; row.label.text = d.Label; row.rule.text = d.Rule;
                var cells = d.Months.Concat(new[] { d.Aggregate, d.Change }).ToArray();
                float labelHeight = row.label.GetPreferredValues(d.Label, 282, 0).y;
                row.label.rectTransform.sizeDelta = new Vector2(282, labelHeight);
                row.rule.rectTransform.anchoredPosition = new Vector2(14, -12 - labelHeight - 6);
                float ruleHeight = row.rule.GetPreferredValues(d.Rule, 282, 0).y;
                row.rule.rectTransform.sizeDelta = new Vector2(282, ruleHeight);
                float h = labelHeight + ruleHeight + 30;
                for (int c = 0; c < cells.Length; c++)
                {
                    row.cells[c].text = cells[c].Text; row.cells[c].color = d.Monetary ? Semantic(cells[c].Amount) : GamePalette.Ink;
                    h = Mathf.Max(h, row.cells[c].GetPreferredValues(cells[c].Text, row.cells[c].rectTransform.rect.width, 0).y + 24);
                }
                row.background.sprite = d.Highlight ? (d.Aggregate.Amount == 0 ? subtotalRow : d.Aggregate.Amount > 0 ? positiveRow : negativeRow) : neutralRow;
                row.background.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = Mathf.Max(88, h);
            }
            double max = Math.Max(1, data.Months.Where(r => r != null).Select(r => Math.Abs((double)r.netResult)).DefaultIfEmpty().Max());
            for (int i = 0; i < 3; i++)
            {
                var r = data.Months[i]; var b = bars[i]; b.label.text = $"Mês {i + 1}";
                b.value.text = r == null ? "Não salvo" : QuarterlyResultPresentation.SignedMoney(r.netResult);
                b.value.color = Semantic(r?.netResult); b.fill.sizeDelta = new Vector2(r == null ? 0 : (float)(232 * Math.Abs(r.netResult) / max), 14);
                b.fill.pivot = new Vector2(r != null && r.netResult < 0 ? 1 : 0, .5f); b.fill.GetComponent<UnityEngine.UI.Image>().color = Semantic(r?.netResult);
            }
            Canvas.ForceUpdateCanvases(); tableScroll.verticalNormalizedPosition = 1;
        }
        public void State(string state, string heading, string message)
        {
            DisplayState = state; loadedBody.SetActive(false); stateBody.SetActive(true);
            period.text = "COMPARATIVO DA CAMPANHA • 3 MESES"; stateTitle.text = heading; stateMessage.text = message;
            retryButton.gameObject.SetActive(state == "error");
        }
    }
}
