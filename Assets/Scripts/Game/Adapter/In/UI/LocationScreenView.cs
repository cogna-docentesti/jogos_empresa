using System;
using System.Globalization;
using Game.Infrastructure.Session;
using TMPro;
using UnityEngine;
using Game.Adapter.In.UI.Navigation;

namespace Game.Adapter.In.UI
{
    /// <summary>D1 presentation. The controller owns catalog data, availability and the session draft.</summary>
    public sealed class LocationScreenView : MonoBehaviour
    {
        [Serializable] public struct ZonePresentation
        {
            public string id;
            public MapMenuNode marker;
            public UnityEngine.UI.Image overlay;
            public Sprite vignette;
        }
        [SerializeField] private TextMeshProUGUI regionText, hintText;
        [SerializeField] private UnityEngine.UI.Button bankButton, storeButton, marketingButton, universityButton, condominiumButton;
        [SerializeField] private GameObject emptyStatePanel, infoPanel, detailsPanel;
        [SerializeField] private TextMeshProUGUI detailSegmentText, detailRentText, detailCapacidadeText, detailDemandaText, detailConcorrenciaText;
        [SerializeField] private TextMeshProUGUI detailZoneNameText, detailZoneSegmentText, detailZoneDescriptionText;
        [SerializeField] private UnityEngine.UI.Button confirmButton, backButton;
        // Kept for older scene consumers. GameScene retires these legacy area renderers.
        [SerializeField] private LocationAreaComponent bankArea, storeArea, marketingArea, universityArea, condominiumArea;
        [SerializeField] private ZonePresentation[] zones = Array.Empty<ZonePresentation>();
        [SerializeField] private UnityEngine.UI.Image zoneVignette;
        [SerializeField] private TextMeshProUGUI referencePriceText, channelsText, emptyMessage;
        [SerializeField] private UnityEngine.UI.ScrollRect detailScroll;
        [SerializeField] private HubMapView.Restaurant[] restaurants = Array.Empty<HubMapView.Restaurant>();
        [SerializeField] private UnityEngine.UI.Image centerOverlay;
        private static readonly CultureInfo Br = CultureInfo.GetCultureInfo("pt-BR");
        public ZonePresentation[] Zones => zones;
        public string SelectedId { get; private set; }
        public bool CanConfirm => confirmButton != null && confirmButton.interactable;
        private void OnEnable()
        {
            RestaurantType? type = MenuNavigator.HubAvailable && MenuNavigator.Instance != null && MenuNavigator.Instance.Current == PanelId.Location
                ? GameSessionState.Current.restaurantType : PlayerSession.SelectedRestaurantType;
            foreach(var variant in restaurants)
                if(variant.image != null) variant.image.enabled=type.HasValue && variant.type==type.Value;
            if(centerOverlay != null) centerOverlay.enabled=type.HasValue;
        }
        public void SetReviewMode(bool review)
        {
            confirmButton.gameObject.SetActive(!review);backButton.gameObject.SetActive(!review);
            if(review){confirmButton.interactable=false;foreach(var button in new[]{bankButton,storeButton,marketingButton,universityButton,condominiumButton})button.interactable=false;}
            var step=transform.Find("DecisionHeader/Step")?.GetComponent<TMP_Text>();if(step!=null)step.text=review?"REVISÃO · LOCALIZAÇÃO":"PASSO 2 DE 7 · LOCALIZAÇÃO";
            var title=transform.Find("DecisionHeader/Title")?.GetComponent<TMP_Text>();if(title!=null)title.text=review?"Localização confirmada":"Escolha sua localização";
            var legend=transform.Find("AvailableLegendLabel")?.GetComponent<TMP_Text>();if(legend!=null)legend.text=review?"Outras áreas":"Disponível";
            var footer=transform.Find("DecisionFooter/DraftHint")?.GetComponent<TMP_Text>();if(footer!=null)footer.text=review?"Localização confirmada · consulta":"Escolha em rascunho";
            if(review)SetHint("Consulte a localização confirmada. Use Voltar para retornar ao resumo.");
        }
        public void SetRegionName(string value) { if(regionText != null) regionText.text=value; }
        public void SetHint(string value) { if(hintText != null) hintText.text=value; }
        public void SetBankVisible(bool value) => bankButton.interactable=value;
        public void SetStoreVisible(bool value) => storeButton.interactable=value;
        public void SetMarketingVisible(bool value) => marketingButton.interactable=value;
        public void SetUniversityVisible(bool value) => universityButton.interactable=value;
        public void SetCondominiumVisible(bool value) => condominiumButton.interactable=value;
        public void BindBankAction(UnityEngine.Events.UnityAction action) => bankButton.onClick.AddListener(action);
        public void BindUniversityAction(UnityEngine.Events.UnityAction action) => universityButton.onClick.AddListener(action);
        public void BindMarketingAction(UnityEngine.Events.UnityAction action) => marketingButton.onClick.AddListener(action);
        public void BindStoreAction(UnityEngine.Events.UnityAction action) => storeButton.onClick.AddListener(action);
        public void BindCondominiumAction(UnityEngine.Events.UnityAction action) => condominiumButton.onClick.AddListener(action);
        public void BindConfirmAction(UnityEngine.Events.UnityAction action) => confirmButton.onClick.AddListener(action);
        public void BindBackAction(UnityEngine.Events.UnityAction action) => backButton.onClick.AddListener(action);
        public void SetConfirmEnabled(bool value) => confirmButton.interactable=value;
        public void ShowEmptyState()
        {
            if(infoPanel != null) infoPanel.SetActive(false);
            if(emptyMessage != null) emptyMessage.text="Selecione uma das cinco áreas no mapa para comparar aluguel, público e demanda.";
            emptyStatePanel.SetActive(true); detailsPanel.SetActive(false);
            SetConfirmEnabled(false); DeselectAllAreas();
        }
        public void ShowUnavailableState()
        {
            ShowEmptyState();
            if(emptyMessage != null) emptyMessage.text="Não foi possível carregar as localizações. Volte e tente novamente.";
        }
        public void InitializeBankArea(string name,string subtitle,string color,Sprite icon) => bankArea?.Initialize(name,subtitle,color,icon);
        public void InitializeUniversityArea(string name,string subtitle,string color,Sprite icon) => universityArea?.Initialize(name,subtitle,color,icon);
        public void InitializeMarketingArea(string name,string subtitle,string color,Sprite icon) => marketingArea?.Initialize(name,subtitle,color,icon);
        public void InitializeStoreArea(string name,string subtitle,string color,Sprite icon) => storeArea?.Initialize(name,subtitle,color,icon);
        public void InitializeCondominiumArea(string name,string subtitle,string color,Sprite icon) => condominiumArea?.Initialize(name,subtitle,color,icon);
        private LocationAreaComponent Area(string id) => id switch
        { "bank"=>bankArea,"university"=>universityArea,"marketing"=>marketingArea,"store"=>storeArea,"condominium"=>condominiumArea,_=>null };
        public void SelectArea(string id)
        {
            DeselectAllAreas(); SelectedId=id; Area(id)?.Select();
            foreach(var zone in zones)
            {
                bool selected=zone.id==id; zone.marker?.SetSelected(selected);
                if(zone.overlay != null) zone.overlay.enabled=selected;
                if(selected && zoneVignette != null) zoneVignette.sprite=zone.vignette;
            }
            if(detailScroll != null) { detailScroll.StopMovement(); detailScroll.verticalNormalizedPosition=1; }
        }
        public void DeselectAllAreas()
        {
            SelectedId=null;
            bankArea?.Deselect(); storeArea?.Deselect(); marketingArea?.Deselect(); universityArea?.Deselect(); condominiumArea?.Deselect();
            foreach(var zone in zones) { zone.marker?.SetSelected(false); if(zone.overlay != null) zone.overlay.enabled=false; }
        }
        public void HoverEnterArea(string id) => Area(id)?.OnHoverEnter();
        public void HoverExitArea(string id) => Area(id)?.OnHoverExit();
        public void ShowLocationDetails(string name,string segment,string description,int rent,int initialPhysicalCapacity,
            int baseDailyDemand,float referencePriceFactor,string competition,string channels)
        {
            if(infoPanel != null) infoPanel.SetActive(false);
            emptyStatePanel.SetActive(false); detailsPanel.SetActive(true);
            if(detailZoneNameText != null) detailZoneNameText.text=name;
            if(detailZoneSegmentText != null) detailZoneSegmentText.text=segment;
            if(detailZoneDescriptionText != null) detailZoneDescriptionText.text=description;
            if(detailSegmentText != null) detailSegmentText.text=segment;
            detailRentText.text="R$ "+rent.ToString("N0",Br);
            detailCapacidadeText.text=initialPhysicalCapacity+" clientes/dia";
            detailDemandaText.text=baseDailyDemand+" pedidos/dia";
            detailConcorrenciaText.text=competition;
            if(referencePriceText != null) referencePriceText.text=referencePriceFactor.ToString("0.00",Br)+"×";
            if(channelsText != null) channelsText.text=string.IsNullOrWhiteSpace(channels) || channels=="-" ? "Não informados" : channels;
        }
    }
}
