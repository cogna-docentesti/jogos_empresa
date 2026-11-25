using System;
using System.Collections.Generic;
using Game.Adapter.In.UI.Layout;
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
        [SerializeField] private UnityEngine.UI.Button confirmButton, returnButton;
        private readonly List<EventOptionCardView> cards = new();
        private readonly List<EventOptionCardView> historyCards = new();
        private RectTransform originalParent;
        private Vector2 originalAnchorMin, originalAnchorMax, originalPivot, originalSize;
        private Vector3 originalPosition, originalScale;
        private int originalSibling;
        private GameObject hiddenHeader;
        private readonly List<UnityEngine.UI.Image> hiddenSurfaces = new();
        public IReadOnlyList<EventOptionCardView> Cards => cards;
        public UnityEngine.UI.Button ConfirmButton => confirmButton;
        public UnityEngine.UI.Button ReturnButton => returnButton;
        public UnityEngine.UI.ScrollRect DecisionScroll => decisionScroll;
        public UnityEngine.UI.ScrollRect HistoryScroll => historyScroll;
        public bool Ready => cardPrefab != null && visuals != null && confirmButton != null;
        public Sprite Illustration(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;
            var assigned = visuals == null ? null : Array.Find(visuals, v => v != null && v.eventId == id)?.illustration;
            return assigned != null ? assigned : Theme.SharedUiSkin.Current?.Get("EVENT_" + id.ToUpperInvariant());
        }

        public void MountInCalendar(RectTransform surface)
        {
            var rect = (RectTransform)transform;
            originalParent = (RectTransform)rect.parent;
            originalSibling = rect.GetSiblingIndex();
            originalAnchorMin = rect.anchorMin;
            originalAnchorMax = rect.anchorMax;
            originalPivot = rect.pivot;
            originalSize = rect.sizeDelta;
            originalPosition = rect.anchoredPosition3D;
            originalScale = rect.localScale;
            rect.SetParent(surface, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = new Vector2(1920, 1080);
            rect.anchoredPosition3D = Vector3.zero;
            rect.localScale = Vector3.one * .8f;

            // The common modal surface supplies the frame. Keep the artwork and controls.
            var header = transform.Find("Header");
            if (header != null && header.gameObject.activeSelf)
            {
                hiddenHeader = header.gameObject;
                hiddenHeader.SetActive(false);
            }
            var illustrationSurface = illustration != null
                ? illustration.transform.parent.GetComponent<UnityEngine.UI.Image>() : null;
            foreach (var image in new[] { GetComponent<UnityEngine.UI.Image>(), illustrationSurface })
            {
                if (image == null || !image.enabled) continue;
                image.enabled = false;
                hiddenSurfaces.Add(image);
            }
        }

        public void UnmountFromCalendar()
        {
            if (originalParent == null) return;
            var rect = (RectTransform)transform;
            rect.SetParent(originalParent, false);
            rect.SetSiblingIndex(originalSibling);
            rect.anchorMin = originalAnchorMin;
            rect.anchorMax = originalAnchorMax;
            rect.pivot = originalPivot;
            rect.sizeDelta = originalSize;
            rect.anchoredPosition3D = originalPosition;
            rect.localScale = originalScale;
            originalParent.GetComponent<ProductionCanvasLayout>()?.RestorePanel(rect);
            originalParent = null;
            if (hiddenHeader != null) hiddenHeader.SetActive(true);
            hiddenHeader = null;
            foreach (var image in hiddenSurfaces) image.enabled = true;
            hiddenSurfaces.Clear();
        }

        public void BindActions(Action confirm, Action back)
        {
            confirmButton.onClick.RemoveAllListeners(); confirmButton.onClick.AddListener(() => confirm());
            returnButton.onClick.RemoveAllListeners(); returnButton.onClick.AddListener(() => back());
        }
        public void Decision(EventData data, int month, int day, int index, int count, int selected, bool editable, string guidance, Action<int> select)
        {
            decisionBody.SetActive(true); historyBody.SetActive(false);
            screenTitle.text = editable ? "Decisão de evento" : "Evento confirmado";
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
            returnButton.gameObject.SetActive(!editable);
            returnButton.GetComponentInChildren<TMP_Text>().text = "Fechar consulta";
            Canvas.ForceUpdateCanvases(); decisionScroll.verticalNormalizedPosition = 1;
        }
        public void SelectOption(int selected)
        {
            for (int i = 0; i < cards.Count; i++) cards[i].GetComponent<SharedControlAppearance>().SetSelected(i == selected);
            confirmButton.interactable = selected >= 0;
            guidanceText.text = "Opção selecionada. Confirme para registrar sua decisão.";
        }
        public void SystemicDecision(Game.Domain.Simulation.EventOccurrence occurrence,
            IReadOnlyList<Game.Domain.Simulation.ReactionOption> options, int selected, Action<int> select)
        {
            decisionBody.SetActive(true); historyBody.SetActive(false);
            screenTitle.text = "Decisão de evento";
            periodText.text = $"MÊS {occurrence.Month} DE 3  •  DIA {occurrence.Day}";
            titleText.text = occurrence.Definition.Title;
            var data = Array.Find(Resources.LoadAll<EventData>("Events"), e => e.id == occurrence.Definition.Id);
            descriptionText.text = data != null ? data.description : "Escolha como seu restaurante vai responder a este acontecimento.";
            conceptText.text = data != null && !string.IsNullOrWhiteSpace(data.educationalConcept) ? "Conceito: " + data.educationalConcept : "";
            illustration.sprite = Illustration(occurrence.Definition.Id);
            illustration.color = illustration.sprite != null ? Color.white : Color.clear;
            illustration.preserveAspect = true;
            guidanceText.text = "Leia as opções e selecione uma para confirmar.";
            footerText.text = "A reação será aplicada a partir deste dia.";
            Clear(cards);
            for (int i = 0; i < options.Count; i++)
            {
                int choice = i; var option = options[i];
                var card = Instantiate(cardPrefab, optionContainer); cards.Add(card);
                string cost = option.Cost == 0 ? "Sem custo imediato" : option.Cost < 0
                    ? $"Recebimento: {(-option.Cost).ToString("C", System.Globalization.CultureInfo.GetCultureInfo("pt-BR"))}"
                    : $"Custo: {option.Cost.ToString("C", System.Globalization.CultureInfo.GetCultureInfo("pt-BR"))}";
                card.Bind(option.Reaction.Label, option.Enabled ? cost : option.DisabledReason,
                    "", () => select(choice), option.Enabled, selected == i);
            }
            confirmButton.gameObject.SetActive(true);
            confirmButton.interactable = selected >= 0 && selected < options.Count && options[selected].Enabled;
            returnButton.gameObject.SetActive(false);
            Canvas.ForceUpdateCanvases(); decisionScroll.verticalNormalizedPosition = 1;
        }
        public void History(IReadOnlyList<SessionEventHistoryEntity> history, int month)
        {
            decisionBody.SetActive(false); historyBody.SetActive(true); Clear(historyCards);
            screenTitle.text = "Histórico de eventos"; periodText.text = $"CAMPANHA DE 3 MESES  •  MÊS ATUAL {month}";
            guidanceText.text = "Decisões registradas nesta campanha.";
            foreach (var row in history)
            {
                var card = Instantiate(cardPrefab, historyContainer); historyCards.Add(card);
                string eventName = string.IsNullOrWhiteSpace(row.eventName) ? row.eventId : row.eventName;
                string choice = string.IsNullOrWhiteSpace(row.selectedChoiceTitle) ? row.selectedChoiceId : row.selectedChoiceTitle;
                if (!string.IsNullOrWhiteSpace(row.selectedChoiceText)) choice += "\n" + row.selectedChoiceText;
                card.Bind($"Mês {row.round} • Dia {row.day}  |  {eventName}", choice,
                    EventEffectText.History(row), null, false, false, true);
            }
            if (history.Count == 0)
            {
                var card = Instantiate(cardPrefab, historyContainer); historyCards.Add(card);
                card.Bind("Nenhum evento confirmado", "As escolhas confirmadas no calendário aparecerão aqui.", "", null, false, false, true);
            }
            confirmButton.gameObject.SetActive(false); returnButton.gameObject.SetActive(true);
            returnButton.GetComponentInChildren<TMP_Text>().text = "Fechar histórico";
            footerText.text = "";
            Canvas.ForceUpdateCanvases(); historyScroll.verticalNormalizedPosition = 1;
        }
        public void Error(string message, bool pending)
        {
            screenTitle.text = "Eventos indisponíveis"; guidanceText.text = message; footerText.text = "Nenhuma nova escolha foi registrada.";
            decisionBody.SetActive(false); historyBody.SetActive(false); confirmButton.gameObject.SetActive(false);
            returnButton.gameObject.SetActive(true); returnButton.GetComponentInChildren<TMP_Text>().text = pending ? "Tentar novamente" : "Fechar consulta";
        }
        private static void Clear(List<EventOptionCardView> list)
        { foreach (var c in list) if (c != null) { c.gameObject.SetActive(false); Destroy(c.gameObject); } list.Clear(); }
    }
}
