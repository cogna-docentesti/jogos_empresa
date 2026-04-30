using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Adapter.In.UI
{
    public sealed class LocationScreenView : MonoBehaviour
    {
        // ── Cabecalho ──────────────────────────────────────
        [Header("Header")]
        [SerializeField] private TextMeshProUGUI regionText;
        [SerializeField] private TextMeshProUGUI hintText;

        // ── Botoes de estabelecimento ───────────────────────
        [Header("Establishment Buttons")]
        [SerializeField] private Button bankButton;
        //[SerializeField] private Button storeButton;
        //[SerializeField] private Button marketingButton;
        [SerializeField] private Button universityButton;
        //[SerializeField] private Button condominiumButton;

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
        [SerializeField] private TextMeshProUGUI detailZoneNameText;      
        [SerializeField] private TextMeshProUGUI detailZoneSegmentText;   
        [SerializeField] private TextMeshProUGUI detailZoneDescriptionText; 

        // ── Botces de acao ──────────────────────────────────
        [Header("Action Buttons")]
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button backButton;

        // ── Textos publicos (header) ────────────────────────
        public void SetRegionName(string v) => regionText.text = v;
        public void SetHint(string v)       => hintText.text = v;

        // ── Visibilidade dos botoes ─────────────────────────
        public void SetBankVisible(bool v)        => bankButton.gameObject.SetActive(v);
        //public void SetStoreVisible(bool v)       => storeButton.gameObject.SetActive(v);
        //public void SetMarketingVisible(bool v)   => marketingButton.gameObject.SetActive(v);
        public void SetUniversityVisible(bool v)  => universityButton.gameObject.SetActive(v);
        //public void SetCondominiumVisible(bool v) => condominiumButton.gameObject.SetActive(v);

        // ── Bind de acoes dos botoes do mapa ───────────────
        //public void BindBankAction(UnityEngine.Events.UnityAction a)        => bankButton.onClick.AddListener(a);
        public void BindBankAction(UnityEngine.Events.UnityAction a)
        {
            Debug.Log($"[View] BindBankAction — bankButton é null? {bankButton == null}");
            bankButton.onClick.AddListener(a);
        }
        //public void BindStoreAction(UnityEngine.Events.UnityAction a)       => storeButton.onClick.AddListener(a);
        //public void BindMarketingAction(UnityEngine.Events.UnityAction a)   => marketingButton.onClick.AddListener(a);
        public void BindUniversityAction(UnityEngine.Events.UnityAction a)  => universityButton.onClick.AddListener(a);
        //public void BindCondominiumAction(UnityEngine.Events.UnityAction a) => condominiumButton.onClick.AddListener(a);

        // ── Painel lateral ──────────────────────────────────
        public void ShowEmptyState()
        {
            emptyStatePanel.SetActive(true);
            detailsPanel.SetActive(false);
            confirmButton.interactable = false;
        }


        // UI ─────────────────────
        [Header("Area Visual Components")]
        [SerializeField] private LocationAreaComponent bankArea;
        //[SerializeField] private LocationAreaComponent storeArea;
        //[SerializeField] private LocationAreaComponent marketingArea;
        [SerializeField] private LocationAreaComponent universityArea;
        //[SerializeField] private LocationAreaComponent condominiumArea;

        // ── Inicializacao visual das areas (chamado pelo Controller no Start) ─
        public void InitializeBankArea(string name, string subtitle, string colorHex, Sprite icon)
            => bankArea?.Initialize(name, subtitle, colorHex, icon); 
            
        public void InitializeUniversityArea(string name, string subtitle, string colorHex, Sprite icon)
            => universityArea?.Initialize(name, subtitle, colorHex, icon);
            
        /**
        public void InitializeStoreArea(string name, string subtitle, string colorHex, Sprite icon)
            => storeArea?.Initialize(name, subtitle, colorHex, icon);

        public void InitializeMarketingArea(string name, string subtitle, string colorHex, Sprite icon)
            => marketingArea?.Initialize(name, subtitle, colorHex, icon);


        public void InitializeCondominiumArea(string name, string subtitle, string colorHex, Sprite icon)
            => condominiumArea?.Initialize(name, subtitle, colorHex, icon);**/

        // ── Selecao visual (chamado pelo Controller ao clicar em uma area) ────
        public void SelectArea(string id)
        {
            // Desseleciona todas antes de selecionar a nova
            DeselectAllAreas();
            Debug.Log("AQUI ENTREI");
            GetAreaComponent(id)?.Select();
        }

        public void DeselectAllAreas()
        {
            bankArea?.Deselect();
            //storeArea?.Deselect();
            //marketingArea?.Deselect();
            universityArea?.Deselect();
            //condominiumArea?.Deselect();
        }

       
        public void HoverEnterArea(string id) => GetAreaComponent(id)?.OnHoverEnter();
        public void HoverExitArea(string id)  => GetAreaComponent(id)?.OnHoverExit();

        // ── Auxiliar ──────────────────────────────────────────────────────────
        private LocationAreaComponent GetAreaComponent(string id) => id switch
        {
            "bank"        => bankArea,
            "university"  => universityArea,
            /**
            "store"       => storeArea,
            "marketing"   => marketingArea,
            "condominium" => condominiumArea,**/
            _             => null
        };

        public void ShowLocationDetails(
            string name, string segment, string description,
            int rent, int ticket, int competition, string channels)
        {
            emptyStatePanel.SetActive(false);
            detailsPanel.SetActive(true);
            confirmButton.interactable = true;

            if (detailZoneNameText != null)        detailZoneNameText.text        = name;
            if (detailZoneSegmentText != null)     detailZoneSegmentText.text     = segment;
            if (detailZoneDescriptionText != null) detailZoneDescriptionText.text = description;

            detailSegmentText.text     = segment;
            detailRentText.text        = $"R$ {rent:N0}";
            detailTicketText.text      = $"{ticket}/100";
            detailCompetitionText.text = $"{competition}/100";
            detailChannelsText.text    = channels;
        }

        // ── Bind de Confirmar / Voltar ──────────────────────
        public void BindConfirmAction(UnityEngine.Events.UnityAction a) => confirmButton.onClick.AddListener(a);
        public void BindBackAction(UnityEngine.Events.UnityAction a)    => backButton.onClick.AddListener(a);
    }
}
