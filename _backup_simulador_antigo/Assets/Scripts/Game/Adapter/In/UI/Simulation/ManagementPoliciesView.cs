using System.Collections.Generic;
using Game.Adapter.In.Controllers;
using Game.Adapter.In.UI.Theme;
using Game.Domain.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Adapter.In.UI.Simulation
{
    /// <summary>
    /// Planejamento do mes no hub (Panel_MonthPlanning): politicas de gestao (marketing,
    /// manutencao, compras, fornecedor, treinamento, remuneracao, fidelizacao...), custo fixo
    /// previsto e o botao "Iniciar mes". Voltar fecha o painel sem gravar.
    ///
    /// Gravacao: so no botao "Confirmar e iniciar" (ou "Salvar planejamento"). Mudar um dropdown
    /// muda so a tela; Voltar descarta.
    /// </summary>
    public sealed class ManagementPoliciesView : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text summaryText;
        [SerializeField] private Transform rowsContainer;
        [SerializeField] private PolicyRowView rowTemplate;
        [SerializeField] private TMP_Text totalCostText;
        [SerializeField] private TMP_Text errorText;

        [Header("Botoes")]
        [SerializeField] private Button backButton;
        [SerializeField] private Button saveButton;
        [SerializeField] private Button startButton;
        [SerializeField] private TMP_Text startLabel;

        [Header("Canvas do modelo de negocio (opcional)")]
        [SerializeField] private Button canvasButton;
        [SerializeField] private BusinessModelCanvasView canvasView;

        private MonthSimulationController _controller;
        private readonly Dictionary<string, string> _draft = new Dictionary<string, string>();
        private bool _wired;

        private GameObject Root => root != null ? root : gameObject;

        public void Bind(MonthSimulationController controller)
        {
            _controller = controller;
            if (_wired) return;
            _wired = true;
            if (backButton != null) backButton.onClick.AddListener(Hide);
            if (saveButton != null) saveButton.onClick.AddListener(() => Save());
            if (startButton != null) startButton.onClick.AddListener(() => { if (Save()) _controller.StartMonth(); });
            if (canvasButton != null) canvasButton.onClick.AddListener(ShowCanvas);
            if (rowTemplate != null) rowTemplate.gameObject.SetActive(false);
        }

        public void Show()
        {
            var session = GameSessionState.Current;
            if (_controller == null || session == null) return;
            Root.SetActive(true);
            if (canvasView != null) canvasView.Hide();
            ShowError(null);
            _draft.Clear();
            foreach (var pair in _controller.Service.CurrentPolicies(session.sessionId)) _draft[pair.Key] = pair.Value;

            if (titleText != null) titleText.text = "Planejamento do mês " + session.currentRound;
            if (summaryText != null)
                summaryText.text = "Caixa disponível: " + SimFormat.Money(session.currentCash) + "   ·   Reputação: " + session.reputationScore
                                   + "\nAs escolhas valem para o mês inteiro e mudam demanda, custos e riscos.";
            if (startLabel != null) startLabel.text = "Confirmar e iniciar o mês " + session.currentRound;

            if (rowsContainer != null && rowTemplate != null)
            {
                SimulationUi.Clear(rowsContainer, rowTemplate.gameObject);
                foreach (var category in _controller.Service.PolicyCategories())
                {
                    var row = SimulationUi.Spawn(rowTemplate, rowsContainer);
                    _draft.TryGetValue(category.id, out string selected);
                    row.Bind(category, selected ?? category.defaultOptionId, OnChanged);
                }
            }
            RenderTotal();
        }

        public void Hide() => Root.SetActive(false);

        public void ShowError(string message)
        {
            if (errorText == null) return;
            errorText.gameObject.SetActive(!string.IsNullOrEmpty(message));
            errorText.text = message ?? string.Empty;
            errorText.color = GamePalette.Danger;
        }

        private void OnChanged(string categoryId, string optionId)
        {
            if (string.IsNullOrEmpty(categoryId) || string.IsNullOrEmpty(optionId)) return;
            _draft[categoryId] = optionId;
            RenderTotal();
        }

        private void RenderTotal()
        {
            if (totalCostText == null || _controller == null) return;
            double total = _controller.Service.MonthlyPolicyCost(_draft);
            totalCostText.text = "Custo fixo das políticas: <b>" + SimFormat.Money(total) + "</b> por mês";
        }

        private bool Save()
        {
            var session = GameSessionState.Current;
            if (session == null) return false;
            try
            {
                _controller.Service.SavePolicies(session.sessionId, _draft);
                ShowError(null);
                return true;
            }
            catch (System.InvalidOperationException exception)
            {
                ShowError(exception.Message);
                return false;
            }
        }

        private void ShowCanvas()
        {
            if (canvasView == null || _controller == null) return;
            var canvas = _controller.Service.Canvas();
            if (canvas != null) canvasView.Show(canvas);
        }
    }
}
