using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Game.Adapter.In.Controllers;
using Game.Adapter.In.UI;
using Game.Adapter.In.UI.Navigation;
using Game.Infrastructure.Session;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;

public static class UI10EquipmentValidation
{
    [Serializable] private class Check { public string name, detail; public bool passed; public int frame; }
    [Serializable] private class Report { public bool completed; public int failures; public List<Check> checks = new(); }
    private static readonly List<object> captures = new();
    private static readonly Queue<(string,Action)> steps = new();
    private static Report report; private static double next, busySince; private static int lastFrame, retries, managerId, databaseId, sceneHandle;
    private static string sessionId, snapshot, draft; private static GameObject origin;
    private static EquipmentData longEquipment; private static string oldName; private static EquipmentCardView prefab; private static GameSessionEntity savedSession;
    private static Transform CanvasRoot => GameObject.Find("Canvas").transform;
    private static EquipmentScreenView View => CanvasRoot.Find("Panel_Equipment").GetComponent<EquipmentScreenView>();
    private static EquipmentScreenController Controller => View.GetComponent<EquipmentScreenController>();
    private static EquipmentCardView[] Cards => Field<List<EquipmentCardView>>(Controller,"cards").Where(c=>c!=null&&c.Equipment!=null).ToArray();
    private static EquipmentCardView Card(string id) => Cards.Single(c=>c.Equipment.id==id);
    private static UnityEngine.UI.Button Confirm => Field<UnityEngine.UI.Button>(View,"confirmButton");
    private static UnityEngine.UI.ScrollRect Scroll => Field<UnityEngine.UI.ScrollRect>(View,"catalogScroll");
    private static T Field<T>(object o,string f) => (T)o.GetType().GetField(f,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(o);
    private static void Set(object o,string f,object v) => o.GetType().GetField(f,BindingFlags.NonPublic|BindingFlags.Instance).SetValue(o,v);
    private static void Call(object o,string m) => o.GetType().GetMethod(m,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(o,
        m=="PopulateEquipments"?new object[]{Resources.LoadAll<EquipmentData>("Equipments")}:null);
    private static void Expect(bool condition,string detail) { if(!condition) throw new InvalidOperationException(detail); }
    private static void Add(string name,Action action) => steps.Enqueue((name,action));
    private static void Capture(string name)
    {
        name=System.Text.RegularExpressions.Regex.Replace(name,@"[0-9]+x[0-9]+$",Screen.width+"x"+Screen.height);
        Fits(); string path=UI01Validation.Capture(UiValidationContext.CaptureName("UI-10",name));
        UiValidationContext.OnCapture?.Invoke(name,path);
        using var hash=System.Security.Cryptography.SHA256.Create();
        captures.Add(new {name,path,frame=Time.frameCount,width=Screen.width,height=Screen.height,sha256=BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(path))).Replace("-","").ToLowerInvariant()});
        File.WriteAllText(UiValidationContext.FileFor("UI-10","capture-frames.json"),Newtonsoft.Json.JsonConvert.SerializeObject(captures,Newtonsoft.Json.Formatting.Indented));
    }
    private static PointerEventData Pointer(UnityEngine.UI.Button b)
    {
        var r=(RectTransform)(b.transform.Find("MarkerTouchTarget")??b.transform);
        return new PointerEventData(EventSystem.current) { pointerId=-1, button=PointerEventData.InputButton.Left, position=RectTransformUtility.WorldToScreenPoint(null,r.TransformPoint(r.rect.center)) };
    }
    private static void Click(UnityEngine.UI.Button b)
    {
        Canvas.ForceUpdateCanvases();
        Expect(b.IsActive() && b.IsInteractable(),"Unavailable control "+b.name);
        var p=Pointer(b); var hits=new List<RaycastResult>(); EventSystem.current.RaycastAll(p,hits);
        Expect(hits.Count>0 && hits[0].gameObject.GetComponentInParent<UnityEngine.UI.Button>()==b,"Blocked raycast "+b.name+" by "+(hits.Count>0?hits[0].gameObject.name:"nothing"));
        ExecuteEvents.Execute(b.gameObject,p,ExecuteEvents.pointerDownHandler); ExecuteEvents.Execute(b.gameObject,p,ExecuteEvents.pointerUpHandler); ExecuteEvents.Execute(b.gameObject,p,ExecuteEvents.pointerClickHandler);
    }
    private static void Filter(EquipmentFilter f) => Click(Field<UnityEngine.UI.Button[]>(View,"filterButtons")[(int)f]);
    private static void SameSession()
    {
        Expect(GameSessionState.Current.sessionId==sessionId && GameSessionState.IsPersisted && DatabaseInitializer.DatabaseService.Connection.Table<GameSessionEntity>().Count()==1,"Session/persistence changed");
        Expect(GameManager.Instance.GetInstanceID()==managerId && DatabaseInitializer.DatabaseService.GetInstanceID()==databaseId && UnityEngine.SceneManagement.SceneManager.GetActiveScene().handle==sceneHandle,"Scene/services replaced");
    }
    private static void Fits()
    {
        Canvas.ForceUpdateCanvases();
        foreach(var text in View.GetComponentsInChildren<TMP_Text>())
        { text.ForceMeshUpdate(); Expect(text.GetComponent<Game.Adapter.In.UI.Theme.TypographyRoleBinding>() != null && !text.enableAutoSizing,"Typography "+text.name); Expect(!text.isTextOverflowing,"Text overflow "+text.name+": "+text.text); }
        Expect(Scroll.content.parent==Scroll.viewport && Scroll.viewport.GetComponent<UnityEngine.UI.Mask>()!=null && Scroll.vertical && !Scroll.horizontal,"Catalog scroll hierarchy");
        foreach(var card in Cards)
        { var art=Field<UnityEngine.UI.Image>(card,"equipmentImage"); Expect(art.sprite==card.Equipment.icon && art.color==Color.white && art.preserveAspect && art.type==UnityEngine.UI.Image.Type.Simple,"Artwork "+card.Equipment.id); }
        foreach(var n in new[]{"TitleText","ContextText","Guidance","Header","BottomBar"})
        { var r=View.transform.Find(n) as RectTransform; var corners=new Vector3[4]; r.GetWorldCorners(corners); foreach(var c in corners) { var p=RectTransformUtility.WorldToScreenPoint(null,c); Expect(p.x>=-.5 && p.y>=-.5 && p.x<=Screen.width+.5 && p.y<=Screen.height+.5,"Outside viewport "+n); } }
        Expect(UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length==1,"EventSystem count");
    }
    private static EquipmentData[] Basics => Resources.LoadAll<EquipmentData>("Equipments").Where(e=>e.category==EquipmentCategory.BASIC).OrderBy(e=>e.cost).ThenBy(e=>e.displayName).ToArray();
    private static void Fixture(RestaurantType type,bool owned=false,float cash=65000,int month=1)
    {
        var s=GameSessionState.Current; s.restaurantType=type; s.currentRound=month; s.currentCash=cash;
        s.equipmentJson=EquipmentSelectionHelper.ToJson(new EquipmentSelectionData { equipmentIds=owned?Basics.Select(e=>e.id).ToList():new List<string>() }); GameSessionState.Save();
        GameManager.Instance.StateMachine.ForceState(GameState.Initial_Capital); GameManager.Instance.StateMachine.TryChangeState(GameState.Initial_Equipment);
        EventSystem.current.SetSelectedGameObject(null);
    }
    private static void SelectItem(string id,string tag,bool capture=false)
    {
        Add("Focus whole card "+id+" "+tag,()=>Card(id).GetComponent<UnityEngine.UI.Button>().Select());
        Add("Add one unit through raycast "+id+" "+tag,()=>{ if(capture) Capture("art-"+id+"-"+tag); Click(Field<UnityEngine.UI.Button>(Card(id),"buyButton")); Expect(Card(id).IsInCart,"Item missing from cart"); });
    }
    [MenuItem("Tools/Jogos de Empresa/UI-10/Validate equipment store in isolated Play Mode")]
    public static void Run()
    {
        UI01Validation.RequireIsolatedStorage(); if(!EditorApplication.isPlaying) throw new InvalidOperationException("Use isolated Play Mode.");
        report=new Report(); steps.Clear(); captures.Clear(); lastFrame=-1; retries=0; busySince=0;
        managerId=GameManager.Instance.GetInstanceID(); databaseId=DatabaseInitializer.DatabaseService.GetInstanceID(); sceneHandle=UnityEngine.SceneManagement.SceneManager.GetActiveScene().handle;
        Add("Fresh identification and empty isolated database",()=>{ Expect(GameManager.Instance.StateMachine.CurrentState==InitialDecisionFlow.Identification,"Expected step 1"); Expect(DatabaseInitializer.DatabaseService.Connection.Table<GameSessionEntity>().Count()==0,"Database not fresh"); var f=UnityEngine.Object.FindFirstObjectByType<UIIndentification>(); Field<TMP_InputField>(f,"studentNameInput").text="Aluna UI10"; Field<TMP_InputField>(f,"raInput").text="101010"; f.Register(); });
        Add("D1 remains draft",()=>{ var v=UnityEngine.Object.FindFirstObjectByType<LocationScreenView>(); ExecuteEvents.Execute(v.Zones.Single(z=>z.id=="bank").marker.gameObject,new PointerEventData(EventSystem.current){pointerId=4},ExecuteEvents.pointerClickHandler); Click(Field<UnityEngine.UI.Button>(v,"confirmButton")); Expect(DatabaseInitializer.DatabaseService.Connection.Table<GameSessionEntity>().Count()==0,"D1 insert"); });
        Add("D2 remains draft",()=>{ var v=UnityEngine.Object.FindFirstObjectByType<RestaurantScreenView>(); Click(Field<UnityEngine.UI.Button>(v,"cardPodrao")); Click(Field<UnityEngine.UI.Button>(v,"confirmButton")); Expect(DatabaseInitializer.DatabaseService.Connection.Table<GameSessionEntity>().Count()==0,"D2 insert"); });
        Add("D3 first insert reaches Banco through technical review",()=>{ Click(Field<UnityEngine.UI.Button>(UnityEngine.Object.FindFirstObjectByType<MenuPricingScreenView>(),"confirmButton")); Expect(GameManager.Instance.StateMachine.CurrentState==GameState.Initial_Capital,"D3 destination"); sessionId=GameSessionState.Current.sessionId; SameSession(); });
        Add("Select original no-credit choice",()=>{ var c=UnityEngine.Object.FindObjectsByType<BankCardView>(FindObjectsSortMode.None).Single(c=>c.CreditLine.id=="sem_credito"); Click(Field<UnityEngine.UI.Button>(c,"selectButton")); });
        Add("Banco confirms into step 6",()=>Click(Field<UnityEngine.UI.Button>(UnityEngine.Object.FindFirstObjectByType<FinancialScreenView>(),"confirmButton")));
        foreach(var size in UiValidationContext.Resolutions)
        {
            var s=size; string tag=s.x+"x"+s.y;
            Add("Prepare setup viewport "+tag,()=>{ UI01Validation.SetGameView(s.x,s.y); Fixture(RestaurantType.PODRAO); });
            Add("Rendered setup and whole title "+tag,()=>{ SameSession(); Expect(Screen.width==s.x&&Screen.height==s.y,"Resolution"); Expect(Field<TMP_Text>(View,"stepText").text.Contains("6 DE 7"),"Step"); Expect(Cards.Length==10,"Podrao compatibility"); Capture("setup-normal-"+tag); });
            Add("Acknowledge catalog guidance before filters "+tag,()=>{ var help=View.GetComponent<ContextualHelp>(); if(help!=null&&help.IsVisible)Click(Field<UnityEngine.UI.Button>(help,"dismiss")); Filter(EquipmentFilter.Basic); });
            Add("Basic filter "+tag,()=>{ Expect(Cards.Count(c=>c.gameObject.activeSelf)==8,"Basic count"); Capture("filter-basic-"+tag); Filter(EquipmentFilter.Specific); });
            Add("Specific prerequisites and disabled state "+tag,()=>{ Expect(Cards.Count(c=>c.gameObject.activeSelf)==2 && Cards.Where(c=>c.gameObject.activeSelf).All(c=>!c.GetComponent<UnityEngine.UI.Button>().interactable),"Specific lock"); Capture("filter-specific-blocked-"+tag); Filter(EquipmentFilter.Owned); });
            Add("Owned empty filter "+tag,()=>{ Expect(Cards.All(c=>!c.gameObject.activeSelf),"Owned count"); Capture("filter-owned-empty-"+tag); Click(Field<UnityEngine.UI.Button>(View,"cartButton")); });
            Add("Empty cart uses whole illustration and native message "+tag,()=>{ Expect(View.CartIsOpen && !Field<UnityEngine.UI.Button>(View,"checkoutButton").interactable,"Empty checkout"); Capture("cart-empty-"+tag); Click(Field<UnityEngine.UI.Button>(View,"closeCartButton")); Filter(EquipmentFilter.All); });
            foreach(var e in Basics) { string id=e.id; SelectItem(id,tag,true); }
            Add("All basics unlock specific controls "+tag,()=>{ Expect(Cards.Where(c=>c.Equipment.category==EquipmentCategory.SPECIFIC).All(c=>c.GetComponent<UnityEngine.UI.Button>().interactable),"Specific unlock"); Filter(EquipmentFilter.Specific); });
            SelectItem("chapa_profissional_podrao",tag,true); SelectItem("fritadeira_eletrica_dupla_podrao",tag,true);
            Add("Selected state and total "+tag,()=>{ Expect(Cards.All(c=>c.IsInCart),"Selected count"); Expect(Field<TMP_Text>(View,"cartTotalValueText").text==FinancialScreenView.Money(27300),"Cart total"); Capture("cart-selected-"+tag); Click(Field<UnityEngine.UI.Button>(View,"cartButton")); });
            Add("Populated cart and whole rows "+tag,()=>{ Expect(View.GetComponentsInChildren<EquipmentCartLineView>().Length==10,"Cart rows"); Expect(Field<UnityEngine.UI.Button>(View,"checkoutButton").interactable,"Affordable checkout"); Capture("cart-filled-"+tag); var lines=Field<RectTransform>(View,"cartLines"); lines.GetComponentInParent<UnityEngine.UI.ScrollRect>().verticalNormalizedPosition=0; });
            Add("Last cart rows scroll without hiding total "+tag,()=>{ Capture("cart-bottom-"+tag); Click(Field<UnityEngine.UI.Button>(View,"closeCartButton")); Click(Field<UnityEngine.UI.Button>(View,"bankBackButton")); });
            Add("Back returns to Banco without purchasing "+tag,()=>{ Expect(GameManager.Instance.StateMachine.CurrentState==GameState.Initial_Capital && GameSessionState.Current.currentCash==65000 && EquipmentSelectionHelper.GetIds(EquipmentSelectionHelper.FromJson(GameSessionState.Current.equipmentJson)).Count==0,"Back purchased"); Click(Field<UnityEngine.UI.Button>(UnityEngine.Object.FindFirstObjectByType<FinancialScreenView>(),"confirmButton")); });
            Add("Banco returns with empty cart without duplicate credit "+tag,()=>{ Expect(GameManager.Instance.StateMachine.CurrentState==GameState.Initial_Equipment && Cards.All(c=>!c.IsInCart)&&GameSessionState.Current.currentCash==65000,"Bank return duplicate"); SameSession(); });
            foreach(var type in new[]{RestaurantType.JAPONES,RestaurantType.FRANCES})
            {
                var restaurant=type; string restaurantTag=restaurant.ToString().ToLowerInvariant()+"-"+tag;
                Add("Prepare restaurant compatibility "+restaurantTag,()=>Fixture(restaurant,true));
                Add("Filter loaded restaurant catalog "+restaurantTag,()=>Filter(EquipmentFilter.Specific));
                Add("Three available specific items "+restaurantTag,()=>{ Expect(Cards.Length==11&&Cards.Count(c=>c.gameObject.activeSelf)==3,"Specific restaurant filter"); Capture("specific-top-"+restaurantTag); });
                var ids=Resources.LoadAll<EquipmentData>("Equipments").Where(e=>e.category==EquipmentCategory.SPECIFIC&&e.applicableTypes.Contains(restaurant)).OrderBy(e=>e.cost).ToArray();
                foreach(var item in ids) SelectItem(item.id,restaurantTag,true);
                Add("Acquired items filter preserves quantity and lock "+restaurantTag,()=>Filter(EquipmentFilter.Owned));
                Add("Acquired state rendered "+restaurantTag,()=>{ Expect(Cards.Count(c=>c.gameObject.activeSelf)==8&&Cards.Where(c=>c.gameObject.activeSelf).All(c=>c.IsOwned&&!c.GetComponent<UnityEngine.UI.Button>().interactable),"Owned state"); Capture("filter-owned-"+restaurantTag); });
            }
        }
        Add("Prepare real purchase with prerequisite removal",()=>Fixture(RestaurantType.PODRAO));
        foreach(var e in Basics) SelectItem(e.id,"purchase");
        SelectItem("chapa_profissional_podrao","purchase"); SelectItem("fritadeira_eletrica_dupla_podrao","purchase");
        Add("Open cart for removal",()=>Click(Field<UnityEngine.UI.Button>(View,"cartButton")));
        Add("Remove a basic through cart control",()=>{ var line=View.GetComponentsInChildren<EquipmentCartLineView>().Single(l=>l.Equipment.id=="conjunto_panelas_industriais"); Click(Field<UnityEngine.UI.Button>(line,"removeButton")); });
        Add("Dependent specifics removed and total recalculated",()=>{ Expect(View.GetComponentsInChildren<EquipmentCartLineView>().Length==7,"Prerequisite removal"); Expect(Field<TMP_Text>(View,"cartTotalText").text==FinancialScreenView.Money(22800)&&GameSessionState.Current.currentCash==65000,"Removal total"); Capture("cart-prerequisite-removal-1280x720"); Click(Field<UnityEngine.UI.Button>(View,"checkoutButton")); });
        Add("Checkout debits once and advances to Team step 7",()=>{ SameSession(); Expect(GameManager.Instance.StateMachine.CurrentState==GameState.Initial_Team,"Team destination"); var ids=EquipmentSelectionHelper.GetIds(EquipmentSelectionHelper.FromJson(GameSessionState.Current.equipmentJson)); Expect(ids.Count==7&&ids.Distinct().Count()==7&&GameSessionState.Current.currentCash==42200,"Purchase economy"); Call(Controller,"CompleteCartPurchase"); Expect(GameSessionState.Current.currentCash==42200,"Duplicate checkout debit"); GameManager.Instance.StateMachine.ForceState(GameState.Initial_Equipment); });
        Add("Owned items persist on return to equipment",()=>{ Expect(Cards.Count(c=>c.IsOwned)==7,"Owned revisit"); Click(Field<UnityEngine.UI.Button>(View,"bankBackButton")); });
        Add("Actual loan held across equipment return",()=>{ var s=GameSessionState.Current; s.creditLineId="microcredito_empresarial"; s.loanBalance=50000; s.currentCash=42200; GameSessionState.Save(); GameManager.Instance.StateMachine.ForceState(GameState.Initial_Capital); });
        Add("Bank contract continuation adds no money",()=>Click(Field<UnityEngine.UI.Button>(UnityEngine.Object.FindFirstObjectByType<FinancialScreenView>(),"confirmButton")));
        Add("No duplicate loan principal",()=>{ Expect(GameSessionState.Current.currentCash==42200&&GameSessionState.Current.loanBalance==50000,"Duplicate loan"); SameSession(); Fixture(RestaurantType.PODRAO,false,100); });
        SelectItem("conjunto_panelas_industriais","insufficient");
        Add("Review insufficient balance",()=>Click(Field<UnityEngine.UI.Button>(View,"cartButton")));
        Add("Insufficient balance disables checkout and gives native error",()=>{ Expect(!Field<UnityEngine.UI.Button>(View,"checkoutButton").interactable&&Field<TMP_Text>(View,"cartStatusText").text.Contains("insuficiente"),"Insufficient guard"); Capture("cart-insufficient-1280x720"); Call(Controller,"CompleteCartPurchase"); Expect(GameSessionState.Current.currentCash==100,"Insufficient debit"); Click(Field<UnityEngine.UI.Button>(View,"closeCartButton")); Fixture(RestaurantType.PODRAO,true,65000,2); GameManager.Instance.StateMachine.ForceState(GameState.Management_Hub); });
        Add("Open store from real hub marker",()=>{ var node=UnityEngine.Object.FindObjectsByType<MapMenuNode>(FindObjectsSortMode.None).Single(n=>n.Destination==PanelId.EquipmentStore); origin=node.gameObject; node.GetComponent<UnityEngine.UI.Button>().Select(); snapshot=Newtonsoft.Json.JsonConvert.SerializeObject(GameSessionState.Current); draft=MenuPricingHelper.ToJson(PlayerSession.GetMenu()); Click(node.GetComponent<UnityEngine.UI.Button>()); });
        Add("Hub mode and native history",()=>{ Expect(MenuNavigator.Instance.Current==PanelId.EquipmentStore&&!Field<TMP_Text>(View,"stepText").text.Contains("PASSO")&&!Field<UnityEngine.UI.Button>(View,"bankBackButton").gameObject.activeSelf,"Store mode"); Capture("hub-store-month2-1280x720"); Click(Confirm); });
        Add("Visit restores hub focus without mutations",()=>{ Expect(MenuNavigator.Instance.Current==PanelId.Menu&&EventSystem.current.currentSelectedGameObject==origin,"Hub focus"); Expect(snapshot==Newtonsoft.Json.JsonConvert.SerializeObject(GameSessionState.Current)&&draft==MenuPricingHelper.ToJson(PlayerSession.GetMenu()),"Visit mutation"); MenuNavigator.Instance.Open(PanelId.Establishment); });
        Add("Open store from originating establishment shortcut",()=>{ var content=UnityEngine.Object.FindFirstObjectByType<EstablishmentReviewContent>(); var button=Field<EstablishmentReviewContent.DecisionVisual[]>(content,"decisions")[4].button; origin=button.gameObject; button.Select(); Click(button); });
        Add("Choose new item in nested store",()=>{ Filter(EquipmentFilter.Specific); Card("chapa_profissional_podrao").GetComponent<UnityEngine.UI.Button>().Select(); });
        Add("Add nested purchase",()=>{ Click(Field<UnityEngine.UI.Button>(Card("chapa_profissional_podrao"),"buyButton")); Click(Confirm); });
        Add("Confirm nested purchase",()=>Click(Field<UnityEngine.UI.Button>(View,"checkoutButton")));
        Add("Nested checkout returns to establishment and preserves focus",()=>{ Expect(MenuNavigator.Instance.Current==PanelId.Establishment&&EventSystem.current.currentSelectedGameObject==origin,"Nested focus"); Expect(GameSessionState.Current.currentCash==63500&&EquipmentSelectionHelper.ContainsId(EquipmentSelectionHelper.FromJson(GameSessionState.Current.equipmentJson),"chapa_profissional_podrao"),"Nested economy"); SameSession(); MenuNavigator.Instance.OpenRoot(); GameSessionState.LoadActiveSession(GameSessionState.Current.userId); Expect(GameSessionState.Current.currentCash==63500,"Reload cash"); MenuNavigator.Instance.Open(PanelId.EquipmentStore); });
        Add("Select acquired filter after reloaded screen has rendered",()=>Filter(EquipmentFilter.Owned));
        Add("SQLite reload restores acquired items",()=>{ Expect(Cards.Count(c=>c.IsOwned)==9,"Reload owned count"); Capture("persisted-owned-1280x720"); Filter(EquipmentFilter.All); });
        Add("Prepare card hover",()=>{ var c=Card("fritadeira_eletrica_dupla_podrao"); c.GetComponent<UnityEngine.UI.Button>().Select(); EventSystem.current.SetSelectedGameObject(null); ExecuteEvents.Execute(c.gameObject,Pointer(c.GetComponent<UnityEngine.UI.Button>()),ExecuteEvents.pointerEnterHandler); });
        Add("Rendered hover",()=>{ var c=Card("fritadeira_eletrica_dupla_podrao"); Expect(c.GetComponent<SharedControlAppearance>().CurrentState=="hover","Hover"); Capture("card-hover-1280x720"); ExecuteEvents.Execute(c.gameObject,Pointer(c.GetComponent<UnityEngine.UI.Button>()),ExecuteEvents.pointerDownHandler); });
        Add("Rendered pressed",()=>{ var c=Card("fritadeira_eletrica_dupla_podrao"); Expect(c.GetComponent<SharedControlAppearance>().CurrentState=="pressed","Pressed"); Capture("card-pressed-1280x720"); ExecuteEvents.Execute(c.gameObject,Pointer(c.GetComponent<UnityEngine.UI.Button>()),ExecuteEvents.pointerUpHandler); ExecuteEvents.Execute(c.gameObject,Pointer(c.GetComponent<UnityEngine.UI.Button>()),ExecuteEvents.pointerExitHandler); c.GetComponent<UnityEngine.UI.Button>().Select(); });
        Add("Keyboard focus inside mask",()=>{ Expect(Card("fritadeira_eletrica_dupla_podrao").GetComponent<SharedControlAppearance>().CurrentState=="focus","Focus"); Capture("card-focus-1280x720"); ExecuteEvents.Execute(Card("fritadeira_eletrica_dupla_podrao").gameObject,new BaseEventData(EventSystem.current),ExecuteEvents.submitHandler); });
        Add("Keyboard submit updates one unit without purchase",()=>{ Expect(Card("fritadeira_eletrica_dupla_podrao").IsInCart&&GameSessionState.Current.currentCash==63500,"Submit"); Click(Field<UnityEngine.UI.Button>(View,"cartButton")); });
        Add("Modal focuses and traps controls",()=>{ var modal=Field<GameObject>(View,"cartModal"); Expect(EventSystem.current.currentSelectedGameObject.transform.IsChildOf(modal.transform),"Modal focus"); Capture("modal-focus-1280x720"); Click(Field<UnityEngine.UI.Button>(View,"closeCartButton")); });
        Add("Long name fixture",()=>{ longEquipment=Resources.LoadAll<EquipmentData>("Equipments").Single(e=>e.id=="mobiliario_basico"); oldName=longEquipment.displayName; longEquipment.displayName=string.Join(" ",Enumerable.Repeat("Mobiliário básico de restaurante com descrição extensa",3)); Call(Controller,"PopulateEquipments"); Call(Controller,"RefreshScreen"); Card("mobiliario_basico").GetComponent<UnityEngine.UI.Button>().interactable=true; Card("mobiliario_basico").GetComponent<UnityEngine.UI.Button>().Select(); });
        Add("Reveal complete long name after cells reflow",()=>{ EventSystem.current.SetSelectedGameObject(null); Card("mobiliario_basico").GetComponent<UnityEngine.UI.Button>().Select(); });
        Add("Long names reflow without shrinking",()=>{ Capture("long-name-1280x720"); longEquipment.displayName=oldName; longEquipment=null; Call(Controller,"PopulateEquipments"); Call(Controller,"RefreshScreen"); });
        Add("Prepare missing presentation reference",()=>{ prefab=Field<EquipmentCardView>(View,"cardPrefab"); Set(View,"cardPrefab",null); Call(Controller,"PopulateEquipments"); Call(Controller,"RefreshScreen"); });
        Add("Missing presentation fails closed and reports error",()=>{ Expect(!Confirm.interactable&&Field<TMP_Text>(View,"hintText").text.Contains("Não foi possível"),"Missing-reference error"); Capture("catalog-error-1280x720"); Set(View,"cardPrefab",prefab); Call(Controller,"PopulateEquipments"); Call(Controller,"RefreshScreen"); });
        Add("Prepare unavailable session",()=>{ savedSession=GameSessionState.Current; GameSessionState.Clear(); Call(Controller,"OnEnable"); });
        Add("Unavailable session clears currency and disables actions",()=>{ Expect(!Confirm.interactable&&Field<TMP_Text>(View,"availableCashText").text=="Indisponível"&&Cards.All(c=>!c.GetComponent<UnityEngine.UI.Button>().interactable),"Missing session"); Capture("session-unavailable-1280x720"); GameSessionState.Set(savedSession); GameSessionState.Persist(); GameManager.Instance.StateMachine.ForceState(GameState.Management_Hub); });
        Add("Final services and isolated persistence",()=>{ SameSession(); File.WriteAllText(UiValidationContext.FileFor("UI-10","runtime.json"),Newtonsoft.Json.JsonConvert.SerializeObject(new {project=UnityEngine.Application.dataPath,unity=UnityEngine.Application.unityVersion,scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene().path,frame=Time.frameCount,managerId,databaseId,sceneHandle,sessionId,database=DatabaseInitializer.DatabaseService.Connection.DatabasePath,state=GameManager.Instance.StateMachine.CurrentState.ToString(),panel=MenuNavigator.Instance.Current.ToString()},Newtonsoft.Json.Formatting.Indented)); });
        next=EditorApplication.timeSinceStartup+.5; EditorApplication.update-=Tick; EditorApplication.update+=Tick;
    }
    private static void Tick()
    {
        if(EditorApplication.timeSinceStartup<next)return;
        if(!EditorApplication.isPlaying||steps.Count==0)
        { report.completed=steps.Count==0&&report.failures==0; EditorApplication.update-=Tick; if(longEquipment!=null){longEquipment.displayName=oldName;longEquipment=null;} File.WriteAllText(UiValidationContext.FileFor("UI-10","playmode-equipment.json"),JsonUtility.ToJson(report,true)); return; }
        if(Time.frameCount<=lastFrame){EditorApplication.QueuePlayerLoopUpdate();EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Repaint();return;}
        bool loading=View.gameObject.activeInHierarchy && View.GetComponent<SharedLoading>()?.IsBusy==true;
        if(loading)
        {
            if(busySince==0)busySince=EditorApplication.timeSinceStartup;
            if(EditorApplication.timeSinceStartup-busySince<30)
            {EditorApplication.QueuePlayerLoopUpdate();EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Repaint();return;}
        }
        else busySince=0;
        var step=steps.Dequeue(); var check=new Check{name=step.Item1,frame=Time.frameCount};
        try {Expect(!loading,"Equipment catalog did not finish loading within 30 seconds");step.Item2();check.passed=true;retries=0;}
        catch(InvalidOperationException e) when(e.Message=="The Game View must render at the evidence resolution."&&retries++<10)
        {var remaining=steps.ToArray();steps.Clear();steps.Enqueue(step);foreach(var pending in remaining)steps.Enqueue(pending);EditorApplication.QueuePlayerLoopUpdate();EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Repaint();next=EditorApplication.timeSinceStartup+1;return;}
        catch(Exception e){check.detail=e.ToString();report.failures++;steps.Clear();Debug.LogError("[UI-10 QA] "+check.name+": "+e.Message);}
        report.checks.Add(check);lastFrame=Time.frameCount;EditorApplication.QueuePlayerLoopUpdate();EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Repaint();next=EditorApplication.timeSinceStartup+.35;
    }
}
