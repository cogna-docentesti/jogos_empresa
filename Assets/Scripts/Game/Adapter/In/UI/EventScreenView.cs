using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Game.Adapter.In.UI
{
    public sealed class EventScreenView : MonoBehaviour
    {
        [Serializable] public sealed class Visual { public string eventId; public Sprite illustration; }
        [SerializeField] private Visual[] visuals;
        [SerializeField] private TMP_Text screenTitle, periodText, titleText, descriptionText, conceptText, guidanceText, footerText;
        [SerializeField] private UnityEngine.UI.Image illustration;
        [SerializeField] private GameObject decisionBody, historyBody;
        [SerializeField] private RectTransform optionContainer, historyContainer;
        [SerializeField] private UnityEngine.UI.ScrollRect decisionScroll, historyScroll;
        [SerializeField] private EventOptionCardView cardPrefab;
        [SerializeField] private UnityEngine.UI.Button confirmButton, historyButton, returnButton;
        private readonly List<EventOptionCardView> cards = new();
        private readonly List<EventOptionCardView> historyCards = new();
        public IReadOnlyList<EventOptionCardView> Cards => cards;
        public UnityEngine.UI.Button ConfirmButton => confirmButton;
        public UnityEngine.UI.ScrollRect DecisionScroll => decisionScroll;
        public UnityEngine.UI.ScrollRect HistoryScroll => historyScroll;
        public bool Ready => cardPrefab != null && visuals != null && confirmButton != null;
        public Sprite Illustration(string id) => Array.Find(visuals, v => v.eventId == id)?.illustration;
        public void BindActions(Action confirm, Action history, Action back)
        {
            confirmButton.onClick.RemoveAllListeners(); confirmButton.onClick.AddListener(() => confirm());
            historyButton.onClick.RemoveAllListeners(); historyButton.onClick.AddListener(() => history());
            returnButton.onClick.RemoveAllListeners(); returnButton.onClick.AddListener(() => back());
        }
        public void Decision(EventData data, int month, int day, int index, int count, int selected, bool editable, string guidance, Action<int> select)
        {
            decisionBody.SetActive(true); historyBody.SetActive(false);
            screenTitle.text = editable ? "Decisões do mês" : "Evento confirmado";
            periodText.text = $"MÊS {month} DE 3  •  DIA {day}" + (count > 1 ? $"  •  EVENTO {index + 1} DE {count}" : "");
            titleText.text = data.title; descriptionText.text = data.description;
            conceptText.text = string.IsNullOrWhiteSpace(data.educationalConcept) ? "" : "Conceito: " + data.educationalConcept;
            illustration.sprite = Illustration(data.id); illustration.color = Color.white; illustration.preserveAspect = true;
            guidanceText.text = guidance; footerText.text = editable ? "A escolha será aplicada no fechamento mensal." : "Consulta da escolha já registrada.";
            Clear(cards); int optionCount = data.options?.Length ?? 0;
            for (int i = 0; i < optionCount; i++)
            {
                int choice = i; var option = data.options[i]; var card = Instantiate(cardPrefab, optionContainer); cards.Add(card);
                card.Bind((selected == i ? (editable ? "Selecionada: " : "Confirmada: ") : "") + option.title, option.description, EventEffectText.Format(option.effects), () => select(choice), editable, selected == i);
            }
            confirmButton.gameObject.SetActive(editable); confirmButton.interactable = editable && selected >= 0;
            historyButton.gameObject.SetActive(true); returnButton.gameObject.SetActive(!editable);
            returnButton.GetComponentInChildren<TMP_Text>().text = "Fechar consulta";
            Canvas.ForceUpdateCanvases(); decisionScroll.verticalNormalizedPosition = 1;
        }
        public void SelectOption(int selected)
        {
            for (int i = 0; i < cards.Count; i++) cards[i].GetComponent<SharedControlAppearance>().SetSelected(i == selected);
            confirmButton.interactable = selected >= 0;
            guidanceText.text = "Opção selecionada. Confirme para registrar sua decisão.";
        }
        public void History(IReadOnlyList<SessionEventHistoryEntity> history, int month, bool pending)
        {
            decisionBody.SetActive(false); historyBody.SetActive(true); Clear(historyCards);
            screenTitle.text = "Histórico de eventos"; periodText.text = $"CAMPANHA DE 3 MESES  •  MÊS ATUAL {month}";
            guidanceText.text = pending ? "Consulta dos registros. Volte ao evento para concluir sua escolha." : "Escolhas confirmadas da sua campanha.";
            foreach (var row in history)
            {
                var card = Instantiate(cardPrefab, historyContainer); historyCards.Add(card);
                card.Bind($"Mês {row.round} • Dia {row.day}  |  {row.eventName}", row.selectedChoiceTitle + "\n" + row.selectedChoiceText,
                    EventEffectText.History(row), null, false, false, true);
            }
            if (history.Count == 0)
            {
                var card = Instantiate(cardPrefab, historyContainer); historyCards.Add(card);
                card.Bind("Nenhum evento confirmado", "As escolhas aparecerão aqui após a confirmação de um evento.", "Os efeitos são aplicados uma única vez no fechamento do mês.", null, false, false, true);
            }
            confirmButton.gameObject.SetActive(false); historyButton.gameObject.SetActive(false); returnButton.gameObject.SetActive(true);
            returnButton.GetComponentInChildren<TMP_Text>().text = pending ? "Voltar ao evento" : "Concluir consulta";
            footerText.text = pending ? "A escolha pendente continua aguardando confirmação." : "Histórico salvo da campanha.";
            Canvas.ForceUpdateCanvases(); historyScroll.verticalNormalizedPosition = 1;
        }
        public void Error(string message, bool pending)
        {
            screenTitle.text = "Eventos indisponíveis"; guidanceText.text = message; footerText.text = "Nenhuma nova escolha foi registrada.";
            decisionBody.SetActive(false); historyBody.SetActive(false); confirmButton.gameObject.SetActive(false); historyButton.gameObject.SetActive(false);
            returnButton.gameObject.SetActive(true); returnButton.GetComponentInChildren<TMP_Text>().text = pending ? "Tentar novamente" : "Fechar consulta";
        }
        private static void Clear(List<EventOptionCardView> list)
        { foreach (var c in list) if (c != null) { c.gameObject.SetActive(false); Destroy(c.gameObject); } list.Clear(); }
    }
}
