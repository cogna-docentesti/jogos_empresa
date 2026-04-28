using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Adapter.In.UI
{
    public sealed class LocationScreenView : MonoBehaviour
    {
        // ── Cabeçalho ──────────────────────────────────────
        [Header("Header")]
        [SerializeField] private TextMeshProUGUI regionText;
        [SerializeField] private TextMeshProUGUI hintText;

        // ── Botões de estabelecimento ───────────────────────
        [Header("Establishment Buttons")]
        [SerializeField] private Button bankButton;
        [SerializeField] private Button storeButton;
        [SerializeField] private Button marketingButton;
        [SerializeField] private Button universityButton;
        [SerializeField] private Button condominiumButton;

        // ── Painel lateral — estado vazio ───────────────────
        [Header("Side Panel — Empty State")]
        [SerializeField] private GameObject emptyStatePanel;

        // ── Painel lateral — detalhes ───────────────────────
        [Header("Side Panel — Details")]
        [SerializeField] private GameObject detailsPanel;
        [SerializeField] private TextMeshProUGUI detailSegmentText;
        [SerializeField] private TextMeshProUGUI detailRentText;
        [SerializeField] private TextMeshProUGUI detailTicketText;
        [SerializeField] private TextMeshProUGUI detailCompetitionText;
        [SerializeField] private TextMeshProUGUI detailChannelsText;

        // ── Botões de ação ──────────────────────────────────
        [Header("Action Buttons")]
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button backButton;

        // ── Textos públicos (header) ────────────────────────
        public void SetRegionName(string v) => regionText.text = v;
        public void SetHint(string v)       => hintText.text = v;

        // ── Visibilidade dos botões ─────────────────────────
        public void SetBankVisible(bool v)        => bankButton.gameObject.SetActive(v);
        public void SetStoreVisible(bool v)       => storeButton.gameObject.SetActive(v);
        public void SetMarketingVisible(bool v)   => marketingButton.gameObject.SetActive(v);
        public void SetUniversityVisible(bool v)  => universityButton.gameObject.SetActive(v);
        public void SetCondominiumVisible(bool v) => condominiumButton.gameObject.SetActive(v);

        // ── Bind de ações dos botões do mapa ───────────────
        public void BindBankAction(UnityEngine.Events.UnityAction a)        => bankButton.onClick.AddListener(a);
        public void BindStoreAction(UnityEngine.Events.UnityAction a)       => storeButton.onClick.AddListener(a);
        public void BindMarketingAction(UnityEngine.Events.UnityAction a)   => marketingButton.onClick.AddListener(a);
        public void BindUniversityAction(UnityEngine.Events.UnityAction a)  => universityButton.onClick.AddListener(a);
        public void BindCondominiumAction(UnityEngine.Events.UnityAction a) => condominiumButton.onClick.AddListener(a);

        // ── Painel lateral ──────────────────────────────────
        public void ShowEmptyState()
        {
            emptyStatePanel.SetActive(true);
            detailsPanel.SetActive(false);
            confirmButton.interactable = false;
        }

        public void ShowLocationDetails(
            string segment, int rent, int ticket,
            int competition, string channels)
        {
            emptyStatePanel.SetActive(false);
            detailsPanel.SetActive(true);
            confirmButton.interactable = true;

            detailSegmentText.text    = segment;
            detailRentText.text       = $"R$ {rent:N0}";
            detailTicketText.text     = $"{ticket}/100";
            detailCompetitionText.text= $"{competition}/100";
            detailChannelsText.text   = channels;
        }

        // ── Bind de Confirmar / Voltar ──────────────────────
        public void BindConfirmAction(UnityEngine.Events.UnityAction a) => confirmButton.onClick.AddListener(a);
        public void BindBackAction(UnityEngine.Events.UnityAction a)    => backButton.onClick.AddListener(a);
    }
}
