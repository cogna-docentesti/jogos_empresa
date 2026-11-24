using System.Globalization;
using System.Linq;
using TMPro;
using Game.Adapter.In.UI.Theme;
using UnityEngine;

namespace Game.Adapter.In.UI
{
    /// <summary>D2 presentation only. Catalog rules and the in-memory draft belong to the controller.</summary>
    public sealed class RestaurantScreenView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI hintText;
        [SerializeField] private UnityEngine.UI.Button cardPodrao, cardJapones, cardFrances;
        [SerializeField] private UnityEngine.UI.Button btnLow, btnMedium, btnHigh;
        [SerializeField] private UnityEngine.UI.Image coh1BarFill, coh2BarFill;
        [SerializeField] private TextMeshProUGUI coh1Tip, coh2Tip, coh1Value, coh2Value;
        [SerializeField] private UnityEngine.UI.Button confirmButton, backButton;
        [SerializeField] private SharedModalFocus compatibilityModal;
        [SerializeField] private UnityEngine.UI.Button compatibilityButton, compatibilityCloseButton;
        [SerializeField] private UnityEngine.UI.Button[] compatibilityTabs;
        private static readonly RestaurantType[] RestaurantTypes = { RestaurantType.PODRAO, RestaurantType.JAPONES, RestaurantType.FRANCES };
        private const string CardCheckPath = "CardDetails/Copy/Heading/Indicator/SelectedCheck";
        private Segment? selectedSegment;
        private static readonly CultureInfo Br = CultureInfo.GetCultureInfo("pt-BR");
        public bool IsCompatibilityOpen => compatibilityModal.gameObject.activeInHierarchy;

        private void OnEnable() => ApplyTypography();
        public void ApplyTypography()
        {
            foreach (var binding in GetComponentsInChildren<TypographyRoleBinding>(true)) binding.Reapply();
            GameTypography.KeepSize(hintText, TypographyRole.Body);
        }

        public void SetHint(string value) { if(hintText != null) hintText.text=value; }
        public void SetReviewMode(bool review)
        {
            ApplyTypography();
            confirmButton.gameObject.SetActive(!review);backButton.gameObject.SetActive(!review);
            if(review)
            {
                confirmButton.interactable=false;
                foreach(var button in new[]{cardPodrao,cardJapones,cardFrances,btnLow,btnMedium,btnHigh})button.interactable=false;
                SetHint("Consulte o restaurante e o público confirmados. Use Voltar para retornar ao resumo.");
                foreach(var card in new[]{cardPodrao,cardJapones,cardFrances})
                    if(card.transform.Find(CardCheckPath)?.gameObject.activeSelf!=true)SetChildText(card,"SelectionStatus","Consulta");
            }
            var step=transform.Find("DecisionHeader/Step")?.GetComponent<TMP_Text>();if(step!=null)step.text=review?"REVISÃO · RESTAURANTE E PÚBLICO":"RESTAURANTE";
            var choice=transform.Find("ChoiceTitle")?.GetComponent<TMP_Text>();if(choice!=null)choice.text=review?"Proposta confirmada":"Qual é a sua proposta?";
            var title=transform.Find("DecisionSidebar/Title")?.GetComponent<TMP_Text>();if(title!=null)title.text=review?"Público confirmado":"Quem você vai atender?";
            var help=transform.Find("DecisionSidebar/Help")?.GetComponent<TMP_Text>();if(help!=null)help.text=review?"Consulte a classe definida para o seu restaurante.":"Escolha uma classe disponível para o restaurante selecionado.";
            var footer=transform.Find("DecisionFooter/DraftHint")?.GetComponent<TMP_Text>();if(footer!=null)footer.text=review?"Escolhas confirmadas · consulta":"Escolha em rascunho";
            RebuildNavigation();
        }
        public void BindCardPodrao(UnityEngine.Events.UnityAction action) => Bind(cardPodrao,action);
        public void BindCardJapones(UnityEngine.Events.UnityAction action) => Bind(cardJapones,action);
        public void BindCardFrances(UnityEngine.Events.UnityAction action) => Bind(cardFrances,action);
        public void BindBtnLow(UnityEngine.Events.UnityAction action) => Bind(btnLow,action);
        public void BindBtnMedium(UnityEngine.Events.UnityAction action) => Bind(btnMedium,action);
        public void BindBtnHigh(UnityEngine.Events.UnityAction action) => Bind(btnHigh,action);
        public void BindConfirm(UnityEngine.Events.UnityAction action) => Bind(confirmButton,action);
        public void BindBack(UnityEngine.Events.UnityAction action) => Bind(backButton,action);
        public void BindCompatibility(UnityEngine.Events.UnityAction open, System.Action<RestaurantType> consult)
        {
            Bind(compatibilityButton, open);
            Bind(compatibilityCloseButton, open != null ? CloseCompatibility : null);
            for (int i = 0; i < RestaurantTypes.Length; i++)
            {
                var type = RestaurantTypes[i];
                Bind(compatibilityTabs[i], consult != null ? () => consult(type) : null);
            }
        }

        public void OpenCompatibility()
        {
            compatibilityButton.Select();
            compatibilityModal.gameObject.SetActive(true);
        }

        public void CloseCompatibility() => compatibilityModal.gameObject.SetActive(false);

        public void SelectCompatibilityTab(RestaurantType type)
        {
            compatibilityModal.Configure(GetCompatibilityTab(type), compatibilityCloseButton);
            for (int i = 0; i < RestaurantTypes.Length; i++)
                compatibilityTabs[i].GetComponent<SharedControlAppearance>().SetSelected(RestaurantTypes[i] == type);
        }

        public void SetCompatibilityAvailable(bool available) => compatibilityButton.interactable = available;

        public void SetRestaurantAvailability(RestaurantType type,bool available)
        {
            GetRestaurantCard(type).interactable=available;
            SetChildText(GetRestaurantCard(type),"SelectionStatus",available?"Selecionar restaurante":"Indisponível");
            var tab = GetCompatibilityTab(type);
            tab.interactable = available;
            if (!available)
            {
                SetChildText(tab, "Label", "Indisponível");
                SetFacade(tab.transform, null);
            }
        }
        public void SetRestaurantCardData(RestaurantData data)
        {
            SetRestaurantCardData(data.type,data.displayName,data.description);
            var card=GetRestaurantCard(data.type);
            SetFacade(card.transform.Find("CardDetails/Artwork"), data.facade);
            SetMenuProducts(card.transform.Find("CardDetails/MenuPreview"), data.products);
            SetChildText(card,"MonthlyCost","R$ "+data.baseMonthlyCost.ToString("N0",Br));
            SetChildText(card,"TicketRange","R$ "+data.ticketMin.ToString("N0",Br)+" a R$ "+data.ticketMax.ToString("N0",Br));
            var tab = GetCompatibilityTab(data.type);
            SetChildText(tab, "Label", data.displayName);
            SetFacade(tab.transform, data.facade);
        }
        public void SetRestaurantCardData(RestaurantType type,string displayName,string description)
        {
            SetChildText(GetRestaurantCard(type),"RestaurantName",displayName);
            SetChildText(GetRestaurantCard(type),"RestaurantDesc",description);
        }
        public void SetSegmentButtonData(Segment segment,string displayName) => SetChildText(GetSegmentButton(segment),"SegmentLabel",displayName);
        public void SelectRestaurantCard(RestaurantType? selected)
        {
            foreach(var type in RestaurantTypes)
            {
                var card=GetRestaurantCard(type); bool chosen=selected==type && card.interactable;
                card.GetComponent<SharedControlAppearance>()?.SetSelected(chosen);
                SetChildText(card,"SelectionStatus",!card.interactable?"Indisponível":chosen?"Restaurante selecionado":"Selecionar restaurante");
                var check=card.transform.Find(CardCheckPath); if(check != null) check.gameObject.SetActive(chosen);
            }
        }
        public void SelectSegmentButton(Segment? selected)
        { selectedSegment=selected.HasValue && GetSegmentButton(selected.Value).interactable?selected:null; RefreshSegments(); }
        public void SetSegmentAvailability(Segment segment,bool available)
        {
            GetSegmentButton(segment).interactable=available;
            if(!available && selectedSegment==segment) selectedSegment=null;
            RefreshSegments();
        }
        public void SetConfirmEnabled(bool enabled) { confirmButton.interactable=enabled; RebuildNavigation(); }
        public void UpdateCoherence(int? locationScore, string locationTip, int? targetScore, string targetTip)
        {
            SetCoherenceScore(coh1Value, coh1BarFill, locationScore);
            SetCoherenceScore(coh2Value, coh2BarFill, targetScore);
            if (coh1Tip != null) coh1Tip.text = locationTip;
            if (coh2Tip != null) coh2Tip.text = targetTip;
        }

        private static void SetCoherenceScore(TMP_Text text, UnityEngine.UI.Image bar, int? score)
        {
            if (text != null)
            {
                text.text = score.HasValue ? $"{score.Value} / {AlignmentEngine.MaxCriterionScore} pontos" : "Sem seleção";
                text.color = GamePalette.Plum;
            }
            if (bar == null) return;

            float fraction = score.GetValueOrDefault() / (float)AlignmentEngine.MaxCriterionScore;
            bar.color = GamePalette.Money;
            bar.rectTransform.anchorMin = Vector2.zero;
            bar.rectTransform.anchorMax = new Vector2(fraction, 1f);
            bar.rectTransform.offsetMin = bar.rectTransform.offsetMax = Vector2.zero;
            bar.enabled = fraction > 0f;
        }

        private void RefreshSegments()
        {
            foreach(var segment in new[]{Segment.LOW,Segment.MEDIUM,Segment.HIGH})
            {
                var button=GetSegmentButton(segment); bool chosen=selectedSegment==segment && button.interactable;
                button.GetComponent<SharedControlAppearance>()?.SetSelected(chosen);
                SetChildText(button,"Availability",!button.interactable?"Indisponível":chosen?"Selecionado":"Disponível");
                var radio=button.transform.Find("Radio")?.GetComponent<UnityEngine.UI.Image>();
                if(radio!=null) radio.sprite=SharedUiSkin.Current.Get(!button.interactable?"UI_RADIO_DISABLED":chosen?"UI_RADIO_ON":"UI_RADIO_OFF");
            }
            RebuildNavigation();
        }
        private void RebuildNavigation()
        {
            var controls=new[]{cardPodrao,cardJapones,cardFrances,btnLow,btnMedium,btnHigh,compatibilityButton,backButton,confirmButton}
                .Where(b=>b!=null && b.interactable && b.gameObject.activeSelf).ToArray();
            for(int i=0;i<controls.Length;i++)
            {
                var nav=new UnityEngine.UI.Navigation{mode=UnityEngine.UI.Navigation.Mode.Explicit};
                nav.selectOnRight=nav.selectOnDown=controls[(i+1)%controls.Length];
                nav.selectOnLeft=nav.selectOnUp=controls[(i+controls.Length-1)%controls.Length]; controls[i].navigation=nav;
            }
        }
        private static void Bind(UnityEngine.UI.Button button,UnityEngine.Events.UnityAction action)
        { button.onClick.RemoveAllListeners(); if(action!=null) button.onClick.AddListener(action); }
        private UnityEngine.UI.Button GetCompatibilityTab(RestaurantType type) => compatibilityTabs[System.Array.IndexOf(RestaurantTypes, type)];
        private static void SetFacade(Transform artwork, Sprite facade)
        {
            var image = artwork?.Find("Facade")?.GetComponent<UnityEngine.UI.Image>();
            if (image == null) return;
            image.sprite = facade;
            image.color = Color.white;
            image.preserveAspect = true;
            image.enabled = facade != null;
        }

        private static void SetMenuProducts(Transform menu, ProductData[] products)
        {
            if (menu == null) return;

            var row = menu.Find("Products");
            int count = products?.Length ?? 0;
            bool hasProducts = false;
            for (int i = 0; i < row.childCount; i++)
            {
                var slot = row.GetChild(i);
                var product = i < count ? products[i] : null;
                slot.gameObject.SetActive(product != null);
                if (product == null) continue;

                hasProducts = true;
                slot.Find("Name").GetComponent<TMP_Text>().text = product.displayName;
                var image = slot.Find("Artwork").GetComponent<UnityEngine.UI.Image>();
                image.sprite = product.productSprite;
                image.enabled = image.sprite != null;
            }
            menu.gameObject.SetActive(hasProducts);
        }

        private UnityEngine.UI.Button GetRestaurantCard(RestaurantType type) => type switch
        { RestaurantType.JAPONES=>cardJapones,RestaurantType.FRANCES=>cardFrances,_=>cardPodrao };
        private UnityEngine.UI.Button GetSegmentButton(Segment segment) => segment switch
        { Segment.MEDIUM=>btnMedium,Segment.HIGH=>btnHigh,_=>btnLow };
        private static void SetChildText(UnityEngine.UI.Button button,string name,string value)
        { var text=button.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(t=>t.name==name && t.gameObject.activeSelf); if(text!=null) text.text=value; }
    }
}
