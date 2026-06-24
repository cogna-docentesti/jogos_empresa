using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Adapter.In.UI
{
    public sealed class LocationScreenView : MonoBehaviour
    {
        // â”€â”€ Cabecalho â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        [Header("Header")]
        [SerializeField] private TextMeshProUGUI regionText;
        [SerializeField] private TextMeshProUGUI hintText;

        // â”€â”€ Botoes de estabelecimento â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        [Header("Establishment Buttons")]
        [SerializeField] private Button bankButton;
        [SerializeField] private Button storeButton;
        [SerializeField] private Button marketingButton;
        [SerializeField] private Button universityButton;
        [SerializeField] private Button condominiumButton;

        // â”€â”€ Painel lateral â€” estado vazio â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        [Header("Side Panel â€” Empty State")]
        [SerializeField] private GameObject emptyStatePanel;

        // â”€â”€ Painel lateral â€” detalhes â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        [Header("Side Panel â€” Details")]
        [SerializeField] private GameObject detailsPanel;
        [SerializeField] private TextMeshProUGUI detailSegmentText;
        [SerializeField] private TextMeshProUGUI detailRentText;
        [SerializeField] private TextMeshProUGUI detailCapacidadeText;
        [SerializeField] private TextMeshProUGUI detailDemandaText;
        [SerializeField] private TextMeshProUGUI detailConcorrenciaText;
        [SerializeField] private TextMeshProUGUI detailZoneNameText;      
        [SerializeField] private TextMeshProUGUI detailZoneSegmentText;   
        [SerializeField] private TextMeshProUGUI detailZoneDescriptionText; 

        // â”€â”€ Botces de acao â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        [Header("Action Buttons")]
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button backButton;

        // â”€â”€ Textos publicos (header) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        public void SetRegionName(string v) => regionText.text = v;
        public void SetHint(string v)       => hintText.text = v;

        // â”€â”€ Visibilidade dos botoes â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        public void SetBankVisible(bool v)        => bankButton.gameObject.SetActive(v);
        public void SetStoreVisible(bool v)       => storeButton.gameObject.SetActive(v);
        public void SetMarketingVisible(bool v)   => marketingButton.gameObject.SetActive(v);
        public void SetUniversityVisible(bool v)  => universityButton.gameObject.SetActive(v);
        public void SetCondominiumVisible(bool v) => condominiumButton.gameObject.SetActive(v);

        // â”€â”€ Bind de acoes dos botoes do mapa â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        public void BindBankAction(UnityEngine.Events.UnityAction a)
        {
            Debug.Log($"[View] BindBankAction â€” bankButton Ã© null? {bankButton == null}");
            bankButton.onClick.AddListener(a);
        }
        public void BindUniversityAction(UnityEngine.Events.UnityAction a)  => universityButton.onClick.AddListener(a);
        
        public void BindMarketingAction(UnityEngine.Events.UnityAction a)   => marketingButton.onClick.AddListener(a);
        public void BindStoreAction(UnityEngine.Events.UnityAction a)       => storeButton.onClick.AddListener(a);
        public void BindCondominiumAction(UnityEngine.Events.UnityAction a) => condominiumButton.onClick.AddListener(a);

        // â”€â”€ Painel lateral â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        public void ShowEmptyState()
        {
            emptyStatePanel.SetActive(true);
            detailsPanel.SetActive(false);
            confirmButton.interactable = false;
            DeselectAllAreas();
        }


        // UI â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        [Header("Area Visual Components")]
        [SerializeField] private LocationAreaComponent bankArea;
        [SerializeField] private LocationAreaComponent storeArea;
        [SerializeField] private LocationAreaComponent marketingArea;
        [SerializeField] private LocationAreaComponent universityArea;
        [SerializeField] private LocationAreaComponent condominiumArea;

        // â”€â”€ Inicializacao visual das areas (chamado pelo Controller no Start) â”€
        public void InitializeBankArea(string name, string subtitle, string colorHex, Sprite icon)
            => bankArea?.Initialize(name, subtitle, colorHex, icon); 
            
        public void InitializeUniversityArea(string name, string subtitle, string colorHex, Sprite icon)
            => universityArea?.Initialize(name, subtitle, colorHex, icon);
            
        public void InitializeMarketingArea(string name, string subtitle, string colorHex, Sprite icon)
            => marketingArea?.Initialize(name, subtitle, colorHex, icon);
        
        public void InitializeStoreArea(string name, string subtitle, string colorHex, Sprite icon)
            => storeArea?.Initialize(name, subtitle, colorHex, icon);

        public void InitializeCondominiumArea(string name, string subtitle, string colorHex, Sprite icon)
            => condominiumArea?.Initialize(name, subtitle, colorHex, icon);

        // â”€â”€ Selecao visual (chamado pelo Controller ao clicar em uma area) â”€â”€â”€â”€
        public void SelectArea(string id)
        {
            // Desseleciona todas antes de selecionar a nova
            DeselectAllAreas();
            GetAreaComponent(id)?.Select();
        }

        public void DeselectAllAreas()
        {
            bankArea?.Deselect();
            storeArea?.Deselect();
            marketingArea?.Deselect();
            universityArea?.Deselect();
            condominiumArea?.Deselect();
        }

       
        public void HoverEnterArea(string id) => GetAreaComponent(id)?.OnHoverEnter();
        public void HoverExitArea(string id)  => GetAreaComponent(id)?.OnHoverExit();

        // â”€â”€ Auxiliar â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        private LocationAreaComponent GetAreaComponent(string id) => id switch
        {
            "bank"        => bankArea,
            "university"  => universityArea,
            "marketing"   => marketingArea,
            "store"       => storeArea,
            "condominium" => condominiumArea,
            _             => null
        };

        public void ShowLocationDetails(
            string name, string segment, string description,
            int rent, int initialPhysicalCapacity, int baseDailyDemand,
            float referencePriceFactor, string competition, string channels)
        {
            emptyStatePanel.SetActive(false);
            detailsPanel.SetActive(true);
            confirmButton.interactable = true;

            if (detailZoneNameText != null)        detailZoneNameText.text        = name;
            if (detailZoneSegmentText != null)     detailZoneSegmentText.text     = segment;
            if (detailZoneDescriptionText != null) detailZoneDescriptionText.text = description;

            detailSegmentText.text     = segment;
            detailRentText.text        = $"R$ {rent:N0}";
            detailCapacidadeText.text      = $"{initialPhysicalCapacity} clientes/dia";
            detailDemandaText.text = $"{baseDailyDemand} pedidos/dia";
            detailConcorrenciaText.text    = competition;
        }

        // â”€â”€ Bind de Confirmar / Voltar â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        public void BindConfirmAction(UnityEngine.Events.UnityAction a) => confirmButton.onClick.AddListener(a);
        public void BindBackAction(UnityEngine.Events.UnityAction a)    => backButton.onClick.AddListener(a);

    }
}

