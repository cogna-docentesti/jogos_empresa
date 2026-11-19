// Assets/Scripts/Game/Adapter/In/UI/Simulation/EventDecisionPopupView.cs
// Popup_EventDecision. Sem botão de fechar: sempre há uma reação sem custo.
using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Adapter.In.Controllers
{
    public class EventDecisionPopupView : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private TextMeshProUGUI titleText, chanceText, factorsText;
        [SerializeField] private Button[] reactionButtons = new Button[4];
        [SerializeField] private TextMeshProUGUI[] reactionLabels = new TextMeshProUGUI[4];

        public void Show(EventOccurrence occurrence, IReadOnlyList<ReactionOption> options, Action<EventOccurrence, string> onChosen)
        {
            root.SetActive(true);
            titleText.text = occurrence.Definition.Title;
            chanceText.text = $"Chance hoje: {occurrence.ChanceToday:P1}";
            factorsText.text = string.Join("\n", occurrence.Explanation.Select(f => $"{f.factor}: {f.value:0.###}"));
            for (int i = 0; i < reactionButtons.Length; i++)
            {
                bool used = i < options.Count;
                reactionButtons[i].gameObject.SetActive(used);
                if (!used) continue;
                var option = options[i];
                string price = option.Cost < 0 ? $"+R$ {-option.Cost:N0}" : $"R$ {option.Cost:N0}";
                reactionLabels[i].text = option.Enabled
                    ? $"{option.Reaction.Label} · {price}"
                    : $"{option.Reaction.Label} · {option.DisabledReason}";      // reação sem caixa fica cinza com o motivo
                reactionButtons[i].interactable = option.Enabled;
                reactionButtons[i].onClick.RemoveAllListeners();
                string reactionId = option.Reaction.Id;
                reactionButtons[i].onClick.AddListener(() => onChosen(occurrence, reactionId));
            }
        }

        public void Hide()
        {
            if (root != null) root.SetActive(false);
        }
    }
}
