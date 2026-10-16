using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Adapter.In.UI;
using Game.Adapter.In.UI.Theme;
using Game.EditorTools;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public static class UI03SharedComponentsValidation
{
    [Serializable] private class Check { public string name,detail; public bool passed; public int frame; }
    [Serializable] private class Report { public List<Check> checks=new(); public int failures; public bool completed; }
    private static readonly Queue<(string name,Action action)> steps=new();
    private static Report report; private static double next; private static int lastFrame;
    private static string reportPath="output/ui-update/UI-03/playmode-components.json";
    private static SharedComponentsShowcase view;
    private static void Expect(bool test,string detail) { if(!test) throw new InvalidOperationException(detail); }
    private static void Add(string name,Action action)=>steps.Enqueue((name,action));
    private static void Capture(string name)=>UI01Validation.Capture("../UI-03/"+name);
    private static SharedControlAppearance A(Component c)=>c.GetComponent<SharedControlAppearance>();
    private static PointerEventData Pointer(Component c,int id=-1)
    {
        return new PointerEventData(EventSystem.current){pointerId=id,button=PointerEventData.InputButton.Left,
            position=RectTransformUtility.WorldToScreenPoint(null,((RectTransform)c.transform).TransformPoint(((RectTransform)c.transform).rect.center))};
    }
    private static void Click(UnityEngine.UI.Button button,int id=-1)
    {
        var e=Pointer(button,id); ExecuteEvents.Execute(button.gameObject,e,ExecuteEvents.pointerDownHandler);
        ExecuteEvents.Execute(button.gameObject,e,ExecuteEvents.pointerUpHandler); ExecuteEvents.Execute(button.gameObject,e,ExecuteEvents.pointerClickHandler);
    }
    [MenuItem("Tools/Jogos de Empresa/UI-03/Validate shared components in Play Mode")]
    public static void Run()
    {
        UI01Validation.RequireIsolatedStorage(); if(!EditorApplication.isPlaying) throw new InvalidOperationException("Use isolated Play Mode.");
        SharedUiShowcaseBuilder.Show(); view=UnityEngine.Object.FindFirstObjectByType<SharedComponentsShowcase>();
        reportPath="output/ui-update/UI-03/playmode-components.json";
        report=new Report(); steps.Clear(); lastFrame=Time.frameCount;
        Add("GameScene is live and identification is still the initial game state",()=>{
            Expect(Time.frameCount>lastFrame,"Frozen frames"); lastFrame=Time.frameCount;
            Expect(GameManager.Instance.StateMachine.CurrentState==GameState.Config_Identification,"Entry regressed");
            Expect(UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length==1,"Duplicate EventSystem");
            Expect(view.GetComponent<UnityEngine.UI.GraphicRaycaster>()!=null,"Preview raycaster");
            Expect(DatabaseInitializer.DatabaseService.Connection.Table<GameSessionEntity>().Count()==0,"Sample wrote a session");
            EventSystem.current.SetSelectedGameObject(null); view.GetComponentInChildren<UnityEngine.UI.ScrollRect>().verticalNormalizedPosition=1;
        });
        foreach(var size in new[]{new Vector2Int(1920,1080),new Vector2Int(1366,768),new Vector2Int(1280,720)})
        {
            var s=size; string suffix=s.x+"x"+s.y;
            Add("Set viewport "+suffix,()=>UI01Validation.SetGameView(s.x,s.y));
            Add("Native text, images, masks and hit targets at "+suffix,()=>{
                Expect(Screen.width==s.x&&Screen.height==s.y,"Viewport");
                foreach(var t in view.GetComponentsInChildren<TMP_Text>().Where(t=>t.enabled && t.text!=""))
                { Expect(t.font.name=="InterTight-Variable" && !t.enableAutoSizing,"Font/autosize "+t.name); Expect(t.rectTransform.rect.height>=t.fontSize*1.2f,"Short text bounds "+t.name); }
                foreach(var button in view.GetComponentsInChildren<UnityEngine.UI.Button>().Where(b=>b.IsInteractable()))
                {
                    var rect=(RectTransform)button.transform;
                    Expect(rect.rect.height*rect.lossyScale.y>=44-.1f,"Small touch height "+button.name);
                    var parentScroll=button.GetComponentInParent<UnityEngine.UI.ScrollRect>();
                    if(parentScroll!=null && !RectTransformUtility.RectangleContainsScreenPoint(parentScroll.viewport,Pointer(button).position)) continue;
                    var hits=new List<RaycastResult>(); EventSystem.current.RaycastAll(Pointer(button),hits);
                    Expect(hits.Count>0 && hits[0].gameObject.GetComponentInParent<UnityEngine.UI.Button>()==button,"Blocked button "+button.name);
                }
                Capture("composition-normal-"+suffix);
                Expect(view.transform.Find("ControlsPanel/Secondary/Label").GetComponent<TMP_Text>().color==GamePalette.Ink,"Secondary text contrast");
                view.input.text="João Antônio da Conceição de Albuquerque e Sá Silva · componente longo"; view.input.Select(); view.input.ActivateInputField();
            });
            Add("Long input retains mask and focus at "+suffix,()=>{
                Expect(view.input.text.Length>50 && view.input.isFocused,"Input focus/value");
                Expect(view.input.textViewport.GetComponent<UnityEngine.UI.RectMask2D>()!=null,"Input mask");
                Capture("composition-input-focus-"+suffix); view.input.DeactivateInputField(); EventSystem.current.SetSelectedGameObject(null);
            });
        }
        Add("Set 1920 for state captures",()=>UI01Validation.SetGameView(1920,1080));
        Add("Hover uses approved surface sprite without tinting a child illustration",()=>{
            var illustration=view.selectCard.transform.Find("Illustration").GetComponent<UnityEngine.UI.Image>();
            ExecuteEvents.Execute(view.primary.gameObject,Pointer(view.primary),ExecuteEvents.pointerEnterHandler);
            Expect(A(view.primary).CurrentState=="hover" && view.primary.image.sprite.name=="primary_violet_hover","Hover sprite");
            Expect(illustration.color==Color.white,"Illustration tint");
        });
        Add("Capture hover",()=>{Capture("composition-hover-1920x1080"); ExecuteEvents.Execute(view.primary.gameObject,Pointer(view.primary),ExecuteEvents.pointerDownHandler);});
        Add("Pressed uses its own bounds-compatible sprite",()=>{
            Expect(A(view.primary).CurrentState=="pressed" && view.primary.image.sprite.name=="primary_violet_pressed","Pressed sprite");
            Capture("composition-pressed-1920x1080");
            ExecuteEvents.Execute(view.primary.gameObject,Pointer(view.primary),ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(view.primary.gameObject,Pointer(view.primary),ExecuteEvents.pointerExitHandler);
            Click(view.primary,1);
        });
        Add("Touch activation calls the existing button callback exactly once",()=>{
            Expect(view.status.text=="Ação recebida: 1","Missing/duplicate callback");
            var disabled=view.transform.Find("ControlsPanel/Disabled").GetComponent<UnityEngine.UI.Button>(); Click(disabled,2);
            Expect(view.status.text=="Ação recebida: 1" && A(disabled).CurrentState=="disabled","Disabled callback/state");
            Capture("composition-disabled-1920x1080"); view.input.text="Ana";
        });
        Add("Invalid input is visible with native instruction",()=>{
            Expect(A(view.input).IsInvalid && view.input.image.sprite.name=="input_invalid","Invalid input sprite");
            Capture("composition-invalid-1920x1080"); view.input.text="Componente válido";
            view.checkbox.isOn=true; view.radio.isOn=true; view.toggle.isOn=true; Click(view.selectCard,1);
            Click(view.transform.Find("ControlsPanel/Tab").GetComponent<UnityEngine.UI.Button>());
            Click(view.transform.Find("ControlsPanel/Pill").GetComponent<UnityEngine.UI.Button>());
        });
        Add("Selections use native controls and separate outlines",()=>{
            Expect(A(view.selectCard).IsSelected,"Card selection");
            Expect(view.checkbox.image.sprite.name=="checkbox_on" && view.radio.image.sprite.name=="radio_on" && view.toggle.image.sprite.name=="toggle_on","Control selection");
            Expect(view.selectCard.transform.Find("SharedSelection").GetComponent<UnityEngine.UI.Image>().enabled,"Selection outline");
            Capture("composition-selected-1920x1080");
            view.primary.Select(); InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState(Key.Tab));
        });
        Add("Keyboard Tab moves focus to the next interactable control",()=>{
            InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState());
            Expect(EventSystem.current.currentSelectedGameObject.name=="Secondary","Tab traversal");
            Expect(EventSystem.current.currentSelectedGameObject.transform.Find("SharedFocus").GetComponent<UnityEngine.UI.Image>().enabled,"Visible keyboard focus");
            Capture("composition-keyboard-focus-1920x1080");
            InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState(Key.LeftShift,Key.Tab));
        });
        Add("Shift Tab reverses traversal and skips disabled controls",()=>{
            InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState());
            Expect(EventSystem.current.currentSelectedGameObject==view.primary.gameObject,"Reverse traversal");
            var help=view.transform.Find("ControlsPanel/IconOnly").GetComponent<UnityEngine.UI.Button>(); help.Select();
        });
        Add("Tooltip responds to keyboard focus",()=>{
            Expect(view.transform.Find("ControlsPanel/Tooltip").gameObject.activeSelf,"Tooltip hidden");
            Capture("composition-tooltip-1920x1080"); view.openModal.Select(); Click(view.openModal,1);
        });
        Add("Modal blocks background raycasts and receives focus",()=>{
            Expect(view.modal.activeSelf && EventSystem.current.currentSelectedGameObject==view.closeModal.gameObject,"Modal focus");
            foreach(var t in view.modal.GetComponentsInChildren<TMP_Text>())
                if(t.name=="Title" || t.name=="Body") Expect(t.enabled && t.color==GamePalette.Ink && t.text.Length>0,"Modal text contrast "+t.name);
            var hits=new List<RaycastResult>(); EventSystem.current.RaycastAll(Pointer(view.primary),hits);
            Expect(hits.Count>0 && hits[0].gameObject.transform.IsChildOf(view.modal.transform),"Scrim raycast leak");
            Capture("composition-modal-1920x1080"); InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState(Key.Tab));
        });
        Add("Modal Tab stays within its two actions",()=>{
            InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState());
            Expect(EventSystem.current.currentSelectedGameObject==view.confirmModal.gameObject,"Modal traversal");
            Click(view.confirmModal,1);
        });
        Add("Closing modal restores the invoker and leaves game data untouched",()=>{
            Expect(!view.modal.activeSelf && EventSystem.current.currentSelectedGameObject==view.openModal.gameObject,"Modal focus restoration");
            Expect(view.status.text=="Modal confirmado","Modal callback");
            Click(view.openModal,1);
        });
        Add("Escape belongs to the active modal",()=>{
            InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState(Key.Escape));
        });
        Add("Escape closes the modal without opening the exit dialog",()=>{
            InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState());
            Expect(!view.modal.activeSelf && !Find<ExitDialogController>().IsOpen,"Escape opened a second dialog");
            var scroll=view.GetComponentInChildren<UnityEngine.UI.ScrollRect>(); scroll.verticalNormalizedPosition=1;
            ExecuteEvents.Execute(scroll.gameObject,new PointerEventData(EventSystem.current){scrollDelta=new Vector2(0,-10)},ExecuteEvents.scrollHandler);
        });
        Add("Wheel scroll changes content and keyboard reveals the last row",()=>{
            var scroll=view.GetComponentInChildren<UnityEngine.UI.ScrollRect>(); Expect(scroll.content.anchoredPosition.y>0,"Wheel scroll");
            scroll.content.Find("Row11").GetComponent<UnityEngine.UI.Button>().Select();
        });
        Add("Last item remains masked, readable and clickable",()=>{
            var scroll=view.GetComponentInChildren<UnityEngine.UI.ScrollRect>();
            Expect(scroll.verticalNormalizedPosition<.05f,"Keyboard scroll reveal"); Capture("composition-scroll-bottom-1920x1080");
            float before=view.slider.value; view.slider.Select();
            ExecuteEvents.Execute(view.slider.gameObject,new AxisEventData(EventSystem.current){moveDir=MoveDirection.Right,moveVector=Vector2.right},ExecuteEvents.moveHandler);
            Expect(view.slider.value>before,"Slider keyboard input");
            var e=Pointer(view.slider,3); e.position=RectTransformUtility.WorldToScreenPoint(null,view.slider.transform.TransformPoint(new Vector3(500,-34,0)));
            ExecuteEvents.Execute(view.slider.gameObject,e,ExecuteEvents.pointerDownHandler); ExecuteEvents.Execute(view.slider.gameObject,e,ExecuteEvents.pointerUpHandler);
        });
        Add("Touch slider and revisit retain component state without duplicating handlers",()=>{
            Expect(view.slider.value>60,"Slider touch"); int count=view.GetComponentsInChildren<SharedControlAppearance>(true).Length;
            view.gameObject.SetActive(false); view.gameObject.SetActive(true); Click(view.primary,1);
            Expect(view.status.text=="Ação recebida: 2","Duplicate callback after revisit");
            Expect(count==view.GetComponentsInChildren<SharedControlAppearance>(true).Length,"Duplicate controls");
            Expect(DatabaseInitializer.DatabaseService.Connection.Table<GameSessionEntity>().Count()==0,"Sample persisted data");
            Expect(Time.frameCount>lastFrame,"Frozen game"); Capture("composition-revisited-1920x1080");
        });
        next=EditorApplication.timeSinceStartup+.5; EditorApplication.update-=Tick; EditorApplication.update+=Tick;
    }
    private static T Find<T>() where T:UnityEngine.Object => UnityEngine.Object.FindFirstObjectByType<T>();
    private static T Field<T>(object o,string name)=>(T)o.GetType().GetField(name,System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(o);
    private static void ClickField(object o,string name)=>Click(Field<UnityEngine.UI.Button>(o,name));
    private static void VerifyFooter(UnityEngine.UI.Button confirm)
    {
        var menu=GameObject.Find("Canvas/MenuAccess/Button").GetComponent<UnityEngine.UI.Button>();
        Rect Bounds(RectTransform r) { var c=new Vector3[4]; r.GetWorldCorners(c); return Rect.MinMaxRect(c[0].x,c[0].y,c[2].x,c[2].y); }
        Expect(!Bounds((RectTransform)confirm.transform).Overlaps(Bounds((RectTransform)menu.transform)),"Menu/Confirm overlap");
        var label=menu.GetComponentInChildren<TMP_Text>(); label.ForceMeshUpdate(); Expect(label.textInfo.characterCount>0,"Menu label");
        Expect(!menu.transform.Find("Image").GetComponent<UnityEngine.UI.Image>().enabled,"Retired Menu decoration obscures label");
        foreach(var button in new[]{confirm,menu})
        { var hits=new List<RaycastResult>(); EventSystem.current.RaycastAll(Pointer(button),hits); Expect(hits.Count>0&&hits[0].gameObject.GetComponentInParent<UnityEngine.UI.Button>()==button,"Blocked footer "+button.name); }
    }
    [MenuItem("Tools/Jogos de Empresa/UI-03/Validate existing catalog consumers in Play Mode")]
    public static void RunCatalog()
    {
        UI01Validation.RequireIsolatedStorage();
        if(!EditorApplication.isPlaying || GameManager.Instance.StateMachine.CurrentState!=GameState.Config_Identification) throw new InvalidOperationException("Start at identification.");
        if(view!=null) view.gameObject.SetActive(false);
        report=new Report(); steps.Clear(); reportPath="output/ui-update/UI-03/playmode-catalog-consumers.json";
        Add("Identification keeps its native Tab order",()=>{
            var form=Find<UIIndentification>(); Field<TMP_InputField>(form,"studentNameInput").Select();
        });
        Add("Send Tab after the native input has received focus",()=>{
            InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState(Key.Tab));
        });
        Add("Identification Tab reaches RA and registration stays a draft",()=>{
            InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState());
            var form=Find<UIIndentification>(); Expect(EventSystem.current.currentSelectedGameObject==Field<TMP_InputField>(form,"raInput").gameObject,"Identification traversal changed");
            Field<TMP_InputField>(form,"studentNameInput").text="Aluna Teste UI03"; Field<TMP_InputField>(form,"raInput").text="123456"; ClickField(form,"buttonRegister");
        });
        Add("Existing location and restaurant callbacks advance D1-D2",()=>{
            Expect(GameManager.Instance.StateMachine.CurrentState==GameState.Config_Location,"Location missing");
            ClickField(Find<LocationScreenView>(),"bankButton"); ClickField(Find<LocationScreenView>(),"confirmButton");
            ClickField(Find<RestaurantScreenView>(),"cardFrances");
        });
        Add("Restaurant cards and segment selection use the shared state",()=>{
            var restaurant=Find<RestaurantScreenView>();
            Expect(A(Field<UnityEngine.UI.Button>(restaurant,"cardFrances")).IsSelected,"Restaurant selection missing");
            Capture("consumer-restaurant-1920x1080"); ClickField(restaurant,"confirmButton");
        });
        Add("Real product templates retain native prices, contain images and sliders",()=>{
            var products=Find<MenuPricingScreenView>().GetComponentsInChildren<ProductPricingItemView>(); Expect(products.Length>0,"Empty product catalog");
            foreach(var p in products)
            {
                var image=Field<UnityEngine.UI.Image>(p,"productImage"); Expect(image.type==UnityEngine.UI.Image.Type.Simple&&image.preserveAspect&&image.color==Color.white,"Product image deformation/tint");
                var slider=Field<UnityEngine.UI.Slider>(p,"priceSlider"); Expect(A(slider)!=null && slider.transform.Find("SharedTouchTarget")!=null,"Unstyled product slider");
                Expect(slider.handleRect.rect.width>=64 && slider.handleRect.rect.height>=64,"Slider thumb bounds");
            }
            Capture("consumer-pricing-1920x1080"); ClickField(Find<MenuPricingScreenView>(),"confirmButton");
        });
        Add("Menu confirmation persists exactly one isolated session",()=>{
            Expect(GameManager.Instance.StateMachine.CurrentState==GameState.Initial_Capital,"Bank missing");
            Expect(DatabaseInitializer.DatabaseService.Connection.Table<GameSessionEntity>().Count()==1,"Persistence boundary changed");
        });
        foreach(var size in new[]{new Vector2Int(1920,1080),new Vector2Int(1366,768),new Vector2Int(1280,720)})
        {
            var s=size; string suffix=s.x+"x"+s.y;
            Add("Set bank viewport "+suffix,()=>UI01Validation.SetGameView(s.x,s.y));
            Add("Bank templates and native text in "+suffix,()=>{
                var bank=Find<FinancialScreenView>(); var cards=bank.GetComponentsInChildren<BankCardView>(); Expect(cards.Length==4,"Bank duplicated/missing cards");
                var scroll=bank.GetComponentInChildren<UnityEngine.UI.ScrollRect>(); scroll.verticalNormalizedPosition=1;
                ClickField(cards.First(),"selectButton");
            });
            Add("Bank selection is a separate outline at "+suffix,()=>{
                var bank=Find<FinancialScreenView>(); Expect(bank.GetComponentsInChildren<SharedControlAppearance>().Any(a=>a.Kind==SharedControlKind.Card&&a.IsSelected),"Bank selection missing");
                VerifyFooter(Field<UnityEngine.UI.Button>(bank,"confirmButton"));
                Capture("consumer-bank-selected-"+suffix); bank.GetComponentInChildren<UnityEngine.UI.ScrollRect>().verticalNormalizedPosition=0;
            });
            Add("Bank scroll and revisit retain four cards at "+suffix,()=>{
                var bank=Find<FinancialScreenView>(); Capture("consumer-bank-bottom-"+suffix);
                bank.gameObject.SetActive(false); bank.gameObject.SetActive(true);
            });
            Add("Revisited bank creates fresh controls with no duplicates "+suffix,()=>Expect(Find<FinancialScreenView>().GetComponentsInChildren<BankCardView>().Length==4,"Duplicated bank cards"));
        }
        Add("Return to 1920 for equipment",()=>UI01Validation.SetGameView(1920,1080));
        Add("Existing bank confirm advances to equipment",()=>{
            var bank=Find<FinancialScreenView>(); ClickField(bank.GetComponentsInChildren<BankCardView>().First(),"selectButton"); ClickField(bank,"confirmButton");
        });
        Add("Equipment catalog keeps its real restaurant filter and neutral illustration tint",()=>{
            var equipment=Find<EquipmentScreenView>(); var cards=equipment.GetComponentsInChildren<EquipmentCardView>();
            var catalog=Resources.LoadAll<EquipmentData>("Equipments"); Expect(catalog.Length==16,"Source equipment catalog count");
            int visible=catalog.Count(e=>e.category==EquipmentCategory.BASIC || e.category==EquipmentCategory.SPECIFIC && e.applicableTypes.Contains(GameSessionState.Current.restaurantType));
            Expect(cards.Length==visible,"Restaurant equipment filter changed");
            foreach(var card in cards)
            {
                var image=Field<UnityEngine.UI.Image>(card,"equipmentImage"); Expect(image.color==Color.white&&image.preserveAspect&&image.type==UnityEngine.UI.Image.Type.Simple,"Equipment tint/aspect");
                var caption=Field<TMP_Text>(card,"nameText"); caption.ForceMeshUpdate(); Expect(!caption.isTextOverflowing,"Equipment name clipped: "+caption.text);
                var corners=new Vector3[4]; caption.rectTransform.GetWorldCorners(corners);
                foreach(var corner in corners) Expect(((RectTransform)card.transform).rect.Contains(card.transform.InverseTransformPoint(corner)),"Equipment caption outside the card mask bounds");
            }
            ClickField(cards.First(),"buyButton");
        });
        Add("Cart selection updates the shared outline and opens the runtime modal",()=>{
            var equipment=Find<EquipmentScreenView>(); Expect(equipment.GetComponentsInChildren<EquipmentCardView>().Any(c=>c.IsInCart&&A(c).IsSelected),"Equipment cart selection");
            VerifyFooter(Field<UnityEngine.UI.Button>(equipment,"confirmButton"));
            Capture("consumer-equipment-selected-1920x1080"); ClickField(equipment,"cartButton");
        });
        Add("Runtime cart uses shared scrim, surface, actions and modal focus",()=>{
            var equipment=Find<EquipmentScreenView>(); var modal=Field<GameObject>(equipment,"cartModal"); Expect(modal.activeSelf&&modal.GetComponent<SharedModalFocus>()!=null,"Cart modal");
            Expect(modal.GetComponentsInChildren<UnityEngine.UI.Button>().All(b=>A(b)!=null),"Runtime button styling");
            Capture("consumer-equipment-cart-1920x1080");
        });
        next=EditorApplication.timeSinceStartup+.5; EditorApplication.update-=Tick; EditorApplication.update+=Tick;
    }
    private static void Tick()
    {
        if(EditorApplication.timeSinceStartup<next) return;
        if(!EditorApplication.isPlaying || steps.Count==0)
        {
            report.completed=steps.Count==0 && report.failures==0; EditorApplication.update-=Tick;
            File.WriteAllText(reportPath,JsonUtility.ToJson(report,true)); return;
        }
        var step=steps.Dequeue(); var check=new Check{name=step.name,frame=Time.frameCount};
        try { step.action(); check.passed=true; }
        catch(Exception e) { check.detail=e.Message; report.failures++; steps.Clear(); Debug.LogError("[UI-03 QA] "+step.name+": "+e.Message); }
        report.checks.Add(check); next=EditorApplication.timeSinceStartup+.5;
    }
}
