using System;
using Game.Adapter.In.UI.Theme;
using Game.Domain.Simulation;
using TMPro;
using UnityEngine;

namespace Game.Adapter.In.UI.Simulation
{
    /// <summary>
    /// Popup do evento que pede decisao (mockup "Problema no freezer"). O mes fica parado ate o
    /// aluno escolher. Mostra o que aconteceu, a probabilidade calculada, por que aconteceu e as
    /// reacoes com custo e consequencia.
    /// </summary>
    public sealed class EventDecisionPopupView : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private TMP_Text categoryText;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private TMP_Text whyText;
        [SerializeField] private TMP_Text conceptText;
        [SerializeField] private Transform optionsContainer;
        [SerializeField] private ReactionOptionView optionTemplate;
        [SerializeField] private TMP_Text errorText;

        private GameObject Root => root != null ? root : gameObject;

        public void Show(PendingDecision decision, Action<string, string> onChoose)
        {
            if (decision == null) { Hide(); return; }
            Root.SetActive(true);
            if (errorText != null) errorText.text = string.Empty;
            var definition = decision.Definition;
            if (categoryText != null)
            {
                categoryText.text = "DIA " + decision.Day + " · " + CategoryLabel(definition.category);
                categoryText.color = SimulationUi.ImpactColor(definition.impact);
            }
            if (titleText != null) titleText.text = definition.title + (string.IsNullOrEmpty(decision.UnitName) ? string.Empty : " (" + decision.UnitName + ")");
            if (descriptionText != null) descriptionText.text = definition.description;
            if (whyText != null)
            {
                string probability = decision.Probability > 0d && decision.Probability < 1d
                    ? "Chance calculada para hoje: " + SimFormat.Probability(decision.Probability) + ". "
                    : string.Empty;
                whyText.text = probability + (decision.ImpactSummary ?? string.Empty);
            }
            if (conceptText != null)
            {
                bool hasConcept = !string.IsNullOrWhiteSpace(definition.educationalConcept);
                conceptText.gameObject.SetActive(hasConcept);
                if (hasConcept) conceptText.text = "<b>Conceito:</b> " + definition.educationalConcept;
            }
            if (optionsContainer == null || optionTemplate == null) return;
            optionTemplate.gameObject.SetActive(false);
            SimulationUi.Clear(optionsContainer, optionTemplate.gameObject);
            string occurrenceId = decision.OccurrenceId;
            foreach (var option in decision.Options)
            {
                var view = SimulationUi.Spawn(optionTemplate, optionsContainer);
                view.Bind(option, reactionId => onChoose?.Invoke(occurrenceId, reactionId));
            }
        }

        public void ShowError(string message)
        {
            if (errorText == null) return;
            errorText.text = message;
            errorText.color = GamePalette.Danger;
        }

        public void Hide() => Root.SetActive(false);

        public static string CategoryLabel(string category)
        {
            switch (category)
            {
                case EventCategory.Equipment: return "EQUIPAMENTO";
                case EventCategory.Staff: return "EQUIPE";
                case EventCategory.Supplier: return "FORNECEDOR";
                case EventCategory.Market: return "MERCADO";
                case EventCategory.Regulatory: return "FISCALIZAÇÃO";
                case EventCategory.Reputation: return "REPUTAÇÃO";
                case EventCategory.Financial: return "FINANCEIRO";
                default: return "OPERAÇÃO";
            }
        }
    }
}
