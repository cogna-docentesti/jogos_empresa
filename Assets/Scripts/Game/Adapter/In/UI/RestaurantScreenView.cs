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
        private Segment? selectedSegment;
        private static readonly CultureInfo Br = CultureInfo.GetCultureInfo("pt-BR");
        private (SharedControlAppearance appearance, UnityEngine.UI.Image tint)[] feedback;

        private void Awake()
        {
            feedback=new[]{cardPodrao,cardJapones,cardFrances,btnLow,btnMedium,btnHigh}
                .Where(b=>b!=null).Select(b=>(b.GetComponent<SharedControlAppearance>(),
                    b.transform.Find("InteractionTint")?.GetComponent<UnityEngine.UI.Image>())).ToArray();
        }
        private void LateUpdate()
        {
            // Card masters have no hover/pressed variant. Add a local surface cue while
            // the shared component continues to own focus, selection and disabled states.
            if(feedback==null) return;
            foreach(var item in feedback)
            {
                if(item.appearance==null || item.tint==null) continue;
                item.appearance.Refresh();
                float alpha=item.appearance.CurrentState=="pressed"?.65f:item.appearance.CurrentState=="hover"?.32f:0;
                item.tint.color=new Color(1,1,1,alpha);
            }
        }

        public void SetHint(string value) { if(hintText != null) hintText.text=value; }
        public void BindCardPodrao(UnityEngine.Events.UnityAction action) => Bind(cardPodrao,action);
        public void BindCardJapones(UnityEngine.Events.UnityAction action) => Bind(cardJapones,action);
        public void BindCardFrances(UnityEngine.Events.UnityAction action) => Bind(cardFrances,action);
        public void BindBtnLow(UnityEngine.Events.UnityAction action) => Bind(btnLow,action);
        public void BindBtnMedium(UnityEngine.Events.UnityAction action) => Bind(btnMedium,action);
        public void BindBtnHigh(UnityEngine.Events.UnityAction action) => Bind(btnHigh,action);
        public void BindConfirm(UnityEngine.Events.UnityAction action) => Bind(confirmButton,action);
        public void BindBack(UnityEngine.Events.UnityAction action) => Bind(backButton,action);
        public void SetRestaurantAvailability(RestaurantType type,bool available)
        {
            GetRestaurantCard(type).interactable=available;
            SetChildText(GetRestaurantCard(type),"SelectionStatus",available?"Selecionar restaurante":"Indisponível");
        }
        public void SetRestaurantCardData(RestaurantData data)
        {
            SetRestaurantCardData(data.type,data.displayName,data.description);
            var card=GetRestaurantCard(data.type);
            var image=card.transform.Find("Facade")?.GetComponent<UnityEngine.UI.Image>();
            if(image != null) { image.sprite=data.facade; image.color=Color.white; image.preserveAspect=true; image.enabled=data.facade != null; }
            SetChildText(card,"MonthlyCost","R$ "+data.baseMonthlyCost.ToString("N0",Br));
            SetChildText(card,"TicketRange","R$ "+data.ticketMin.ToString("N0",Br)+" a R$ "+data.ticketMax.ToString("N0",Br));
        }
        public void SetRestaurantCardData(RestaurantType type,string displayName,string description)
        {
            SetChildText(GetRestaurantCard(type),"RestaurantName",displayName);
            SetChildText(GetRestaurantCard(type),"RestaurantDesc",description);
        }
        public void SetSegmentButtonData(Segment segment,string displayName) => SetChildText(GetSegmentButton(segment),"SegmentLabel",displayName);
        public void SelectRestaurantCard(RestaurantType? selected)
        {
            foreach(var type in new[]{RestaurantType.PODRAO,RestaurantType.JAPONES,RestaurantType.FRANCES})
            {
                var card=GetRestaurantCard(type); bool chosen=selected==type && card.interactable;
                card.GetComponent<SharedControlAppearance>()?.SetSelected(chosen);
                SetChildText(card,"SelectionStatus",!card.interactable?"Indisponível":chosen?"Restaurante selecionado":"Selecionar restaurante");
                var check=card.transform.Find("SelectedCheck"); if(check != null) check.gameObject.SetActive(chosen);
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
        public void UpdateCoherence(float coh1,string tip1,float coh2,string tip2)
        {
            ApplyCoherenceBar(coh1BarFill,coh1); ApplyCoherenceBar(coh2BarFill,coh2);
            if(coh1Tip != null) coh1Tip.text=tip1;
            if(coh2Tip != null) coh2Tip.text=tip2;
            SetCoherenceValue(coh1Value,coh1); SetCoherenceValue(coh2Value,coh2);
        }
        private static void SetCoherenceValue(TMP_Text text,float value)
        {
            if(text==null) return;
            text.text=value<=0?"Sem seleção":$"{(value>=.7f?"Alta":value>=.4f?"Média":"Baixa")} · {Mathf.RoundToInt(value*100)}%";
            text.color=value>=.7f?GamePalette.Money:value>=.4f?GamePalette.Plum:GamePalette.ErrorText;
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
            var controls=new[]{cardPodrao,cardJapones,cardFrances,btnLow,btnMedium,btnHigh,backButton,confirmButton}.Where(b=>b!=null && b.interactable).ToArray();
            for(int i=0;i<controls.Length;i++)
            {
                var nav=new UnityEngine.UI.Navigation{mode=UnityEngine.UI.Navigation.Mode.Explicit};
                nav.selectOnRight=nav.selectOnDown=controls[(i+1)%controls.Length];
                nav.selectOnLeft=nav.selectOnUp=controls[(i+controls.Length-1)%controls.Length]; controls[i].navigation=nav;
            }
        }
        private static void Bind(UnityEngine.UI.Button button,UnityEngine.Events.UnityAction action)
        { button.onClick.RemoveAllListeners(); if(action!=null) button.onClick.AddListener(action); }
        private UnityEngine.UI.Button GetRestaurantCard(RestaurantType type) => type switch
        { RestaurantType.JAPONES=>cardJapones,RestaurantType.FRANCES=>cardFrances,_=>cardPodrao };
        private UnityEngine.UI.Button GetSegmentButton(Segment segment) => segment switch
        { Segment.MEDIUM=>btnMedium,Segment.HIGH=>btnHigh,_=>btnLow };
        private static void SetChildText(UnityEngine.UI.Button button,string name,string value)
        { var text=button.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(t=>t.name==name && t.gameObject.activeSelf); if(text!=null) text.text=value; }
        private static void ApplyCoherenceBar(UnityEngine.UI.Image bar,float value)
        {
            if(bar==null) return;
            value=Mathf.Clamp01(value); bar.color=value>=.7f?GamePalette.Money:value>=.4f?GamePalette.Warn:GamePalette.ErrorText;
            bar.rectTransform.anchorMin=Vector2.zero; bar.rectTransform.anchorMax=new Vector2(value,1);
            bar.rectTransform.offsetMin=bar.rectTransform.offsetMax=Vector2.zero; bar.enabled=value>0;
        }
    }
}
