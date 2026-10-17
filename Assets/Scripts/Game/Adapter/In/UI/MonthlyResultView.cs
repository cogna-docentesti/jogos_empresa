using System;
using System.Collections.Generic;
using Game.Adapter.In.UI.Theme;
using TMPro;
using UnityEngine;

namespace Game.Adapter.In.UI
{
    public sealed class MonthlyResultView : MonoBehaviour
    {
        [Serializable] public sealed class TableRow { public TMP_Text label, value; public UnityEngine.UI.Image background; }
        [Serializable] public sealed class ChartBar { public TMP_Text label, value; public RectTransform fill; }
        [SerializeField] private TMP_Text title, period, resultValue, cashValue, clientsValue, footer, stateTitle, stateMessage;
        [SerializeField] private GameObject loadedBody, stateBody;
        [SerializeField] private TableRow[] rows;
        [SerializeField] private ChartBar[] bars;
        [SerializeField] private UnityEngine.UI.ScrollRect tableScroll;
        [SerializeField] private UnityEngine.UI.Button continueButton, retryButton;
        [SerializeField] private UnityEngine.UI.Button quarterlyButton;
        public UnityEngine.UI.Button QuarterlyButton => quarterlyButton;
        [SerializeField] private Sprite neutralRow, subtotalRow, positiveRow, negativeRow;
        public UnityEngine.UI.Button ContinueButton => continueButton;
        public UnityEngine.UI.Button RetryButton => retryButton;
        public UnityEngine.UI.ScrollRect TableScroll => tableScroll;
        public string DisplayState { get; private set; }
        public IReadOnlyList<TableRow> Rows => rows;
        public IReadOnlyList<ChartBar> Bars => bars;
        public void Actions(Action proceed, Action retry)
        {
            continueButton.onClick.RemoveAllListeners(); continueButton.onClick.AddListener(() => proceed());
            retryButton.onClick.RemoveAllListeners(); retryButton.onClick.AddListener(() => retry());
        }
        public static Color Semantic(float value) => value == 0 ? GamePalette.Ink : value > 0 ? GamePalette.Money : GamePalette.ErrorText;
        public void Show(RoundResultEntity result, bool awaiting)
        {
            var values = MonthlyResultPresentation.Build(result);
            DisplayState = "loaded"; loadedBody.SetActive(true); stateBody.SetActive(false);
            title.text = "DRE mensal • resultado de caixa"; period.text = $"MÊS {result.round} DE 3  •  RESULTADO SALVO";
            resultValue.text = MonthlyResultPresentation.Money(result.netResult); resultValue.color = Semantic(result.netResult);
            cashValue.text = MonthlyResultPresentation.Money(result.closingCash); cashValue.color = Semantic(result.closingCash);
            clientsValue.text = MonthlyResultPresentation.Number(result.customers);
            for (int i = 0; i < rows.Length; i++)
            {
                var row = rows[i]; var data = values[i]; row.label.text = data.Label; row.value.text = data.Value;
                row.value.color = data.Monetary ? Semantic(data.Amount) : GamePalette.Ink;
                row.background.sprite = data.Total ? (data.Amount == 0 ? subtotalRow : data.Amount > 0 ? positiveRow : negativeRow) : neutralRow;
                var height = Mathf.Max(48, row.label.GetPreferredValues(data.Label, 810, 0).y + 20, row.value.GetPreferredValues(data.Value, 354, 0).y + 20);
                row.background.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = height;
            }
            float[] amounts = { result.grossRevenue, -result.TotalCosts, result.eventCashImpact, result.netResult };
            float max = 1; foreach (float amount in amounts) max = Mathf.Max(max, Mathf.Abs(amount));
            for (int i = 0; i < bars.Length; i++)
            {
                var b = bars[i]; b.value.text = MonthlyResultPresentation.SignedMoney(amounts[i]); b.value.color = Semantic(amounts[i]);
                b.fill.sizeDelta = new Vector2(232 * Mathf.Abs(amounts[i]) / max, 12);
                b.fill.pivot = new Vector2(amounts[i] < 0 ? 1 : 0, .5f);
                b.fill.GetComponent<UnityEngine.UI.Image>().color = Semantic(amounts[i]);
            }
            footer.text = awaiting ? "Leia o resumo. A campanha aguarda seu clique em Continuar." : "Consulta do último mês salvo. Nenhum valor será aplicado novamente.";
            continueButton.interactable = true; continueButton.GetComponentInChildren<TMP_Text>().text = awaiting ? "Continuar" : "Voltar ao hub";
            retryButton.gameObject.SetActive(false); Canvas.ForceUpdateCanvases(); tableScroll.verticalNormalizedPosition = 1;
        }
        public void State(string state, string heading, string message, bool awaiting)
        {
            DisplayState = state; loadedBody.SetActive(false); stateBody.SetActive(true); title.text = "DRE mensal";
            period.text = "RESULTADOS MENSAIS"; stateTitle.text = heading; stateMessage.text = message;
            footer.text = awaiting ? "Consulte novamente o resultado salvo antes de continuar." : "Consulta de resultados persistidos da campanha.";
            continueButton.interactable = !awaiting; continueButton.GetComponentInChildren<TMP_Text>().text = awaiting ? "Continuar" : "Voltar ao hub";
            retryButton.gameObject.SetActive(state == "error");
        }
        public void FinalReportContext(int month)
        {
            continueButton.interactable = true;
            continueButton.GetComponentInChildren<TMP_Text>().text = "Voltar ao relatório";
            footer.text = $"Consulta do mês {month}. Nenhum valor é aplicado novamente.";
            if (DisplayState != "loaded") period.text = $"MÊS {month} DE 3 • CONSULTA DO RELATÓRIO";
        }
    }
}
