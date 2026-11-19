// Assets/Scripts/Game/Adapter/In/UI/Simulation/ManagementPoliciesView.cs
// Panel_Planning: as 8 políticas do mês. Cada linha tem um container vazio; ao abrir a tela o
// SystemicRoundFlow chama Bind() e a view clona o chipTemplate uma vez por opção do catálogo
// (SimulationConfig > policies). Assim rótulo e custo das opções têm uma fonte só.
// Só desenha e repassa cliques: quem grava é o SystemicRoundFlow, pelo serviço.
using System;
using System.Collections.Generic;
using System.Globalization;
using Game.Application.UseCases.Simulation;
using Game.Domain.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Adapter.In.Controllers
{
    public class ManagementPoliciesView : MonoBehaviour
    {
        [Serializable]
        public sealed class PolicyRow
        {
            public string policyId;              // "marketing", "loyalty"... (MonthPolicies.PolicyIds)
            public RectTransform options;        // recebe um chip por opção
        }

        /// Avisado no OnEnable. O SystemicRoundFlow responde chamando Bind().
        public event Action Opened;
        public event Action<MonthPolicies> SaveClicked;
        public event Action<MonthPolicies> ConfirmAndStartClicked;

        [SerializeField] private TextMeshProUGUI titleText, cashText, costText, feedbackText;
        [SerializeField] private TextMeshProUGUI noticeText;           // bloqueios de D1 a D6, avisos e mudanças do hub
        [SerializeField] private List<PolicyRow> rows = new List<PolicyRow>();
        [SerializeField] private Button chipTemplate;                 // fica desativado; é clonado
        [SerializeField] private Button saveButton, confirmButton;
        [SerializeField] private Color chipSelected = new Color32(242, 199, 92, 255);
        [SerializeField] private Color chipSelectedText = new Color32(32, 23, 3, 255);
        [SerializeField] private Color chipIdle = new Color32(19, 28, 51, 255);
        [SerializeField] private Color chipIdleText = Color.white;

        private static readonly CultureInfo PtBr = new CultureInfo("pt-BR");
        private readonly MonthPolicies _current = new MonthPolicies();
        private readonly Dictionary<string, List<(Button button, string optionId)>> _chips =
            new Dictionary<string, List<(Button, string)>>();
        private PlanningData _data;

        private void Awake()
        {
            if (chipTemplate != null) chipTemplate.gameObject.SetActive(false);
            if (saveButton != null) saveButton.onClick.AddListener(OnSaveButton);
            if (confirmButton != null) confirmButton.onClick.AddListener(OnConfirmAndStartButton);
        }

        private void OnEnable()
        {
            if (feedbackText != null) feedbackText.text = "";
            Opened?.Invoke();
        }

        // ------------------------------------------------------------ desenhar
        public void Bind(PlanningData data)
        {
            _data = data;
            if (data == null)
            {
                titleText.text = "Planejamento do mês";
                ShowSaveResult(false, "Nenhuma partida encontrada. Confirme as decisões iniciais primeiro.");
                SetButtons(false);
                return;
            }

            titleText.text = $"Planejamento do mês {data.Month} de {data.MonthsPerMatch}";
            cashText.text = "Caixa: " + Money(data.Cash);
            foreach (var (policy, option) in data.Current.All()) _current.Set(policy, option);

            foreach (var row in rows)
            {
                ClearChildren(row.options);
                var list = new List<(Button, string)>();
                _chips[row.policyId] = list;
                if (!data.Options.TryGetValue(row.policyId, out var options)) continue;
                foreach (var option in options)
                {
                    var chip = Instantiate(chipTemplate, row.options);
                    chip.gameObject.name = "Chip_" + option.OptionId;
                    chip.gameObject.SetActive(true);
                    var label = chip.GetComponentInChildren<TextMeshProUGUI>(true);
                    label.text = $"<b>{option.Label}</b>\n<size=80%>{CostLine(option)}</size>";
                    string policyId = row.policyId, optionId = option.OptionId;
                    chip.onClick.AddListener(() => Select(policyId, optionId));
                    list.Add((chip, option.OptionId));
                }
            }

            SetButtons(!data.IsFinished);
            if (confirmButton != null) confirmButton.interactable = data.CanStart;
            if (data.IsFinished) ShowSaveResult(false, "A partida já terminou.");
            if (noticeText != null)
            {
                var lines = new List<string>();
                foreach (var e in data.Errors) lines.Add($"<color=#EF5350>{e}</color>");
                foreach (var w in data.Warnings) lines.Add($"<color=#F0912E>{w}</color>");
                if (data.Changes.Count > 0) lines.Add("Mudanças do hub que entram neste mês: " + string.Join("; ", data.Changes) + ".");
                noticeText.text = string.Join("   ", lines);
            }
            Refresh();
        }

        private void Select(string policyId, string optionId)
        {
            _current.Set(policyId, optionId);
            if (feedbackText != null) feedbackText.text = "";
            Refresh();
        }

        private void Refresh()
        {
            double fixedCost = 0;
            foreach (var row in rows)
            {
                string chosen = _current.Get(row.policyId);
                if (!_chips.TryGetValue(row.policyId, out var list)) continue;
                foreach (var (button, optionId) in list)
                {
                    bool on = optionId == chosen;
                    button.image.color = on ? chipSelected : chipIdle;
                    var label = button.GetComponentInChildren<TextMeshProUGUI>(true);
                    label.color = on ? chipSelectedText : chipIdleText;
                }
                if (_data != null && _data.Options.TryGetValue(row.policyId, out var options))
                {
                    var selected = options.Find(o => o.OptionId == chosen);
                    if (selected != null) fixedCost += selected.FixedMonthlyCost;
                }
            }
            if (costText != null)
                costText.text = $"Custo fixo das políticas: {Money(fixedCost)} por mês";
        }

        private static string CostLine(PolicyOption o)
        {
            var parts = new List<string>();
            if (o.FixedMonthlyCost > 0) parts.Add(Money(o.FixedMonthlyCost) + "/mês");
            if (o.CostPerOrder > 0) parts.Add("+ " + o.CostPerOrder.ToString("C2", PtBr) + " por pedido");
            if (o.AssetValueRate > 0) parts.Add((o.AssetValueRate * 100).ToString("0.0", PtBr) + "% do valor dos equipamentos");
            return parts.Count == 0 ? "sem custo fixo" : string.Join(" ", parts);
        }

        private static string Money(double value) => "R$ " + value.ToString("N0", PtBr);

        private void SetButtons(bool interactable)
        {
            if (saveButton != null) saveButton.interactable = interactable;
            if (confirmButton != null) confirmButton.interactable = interactable;
        }

        private static void ClearChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i).gameObject;
                child.SetActive(false);                  // o layout ignora já neste frame
                Destroy(child);
            }
        }

        // ------------------------------------------------------------ botões
        public void SetMarketing(string option) => Select("marketing", option);          // none, low, medium, high
        public void SetLoyalty(string option) => Select("loyalty", option);              // none, program
        public void SetSupplier(string option) => Select("supplier", option);            // economy, standard, premium
        public void SetSupplierPayment(string option) => Select("supplierPayment", option); // cash, installments
        public void SetMaintenance(string option) => Select("maintenance", option);      // corrective, preventive, intensive
        public void SetStock(string option) => Select("stock", option);                  // low, medium, high
        public void SetPay(string option) => Select("pay", option);                      // below, market, above
        public void SetTraining(string option) => Select("training", option);            // none, basic, advanced

        public MonthPolicies CurrentSelection => _current.Clone();

        public void OnSaveButton() => SaveClicked?.Invoke(_current.Clone());
        public void OnConfirmAndStartButton() => ConfirmAndStartClicked?.Invoke(_current.Clone());

        public void ShowSaveResult(bool ok, string message)
        {
            if (feedbackText != null) feedbackText.text = ok ? "Escolhas salvas." : message;
        }
    }
}
