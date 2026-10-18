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

public static class UI11TeamValidation
{
    [Serializable] private class Check { public string name, detail; public bool passed; public int frame; }
    [Serializable] private class Report { public bool completed; public int failures; public List<Check> checks = new(); }
    private static readonly Queue<(string,Action)> steps = new();
    private static readonly List<object> captures = new();
    private static Report report; private static double next; private static int lastFrame, retries, managerId, databaseId, sceneHandle;
    private static string reportPath = "output/ui-update/UI-11/playmode-team.json";
    private static string sessionId, snapshot; private static GameObject origin; private static TeamRoleCardView prefab; private static GameSessionEntity savedSession;
    private static Transform Root => GameObject.Find("Canvas").transform;
    private static TeamScreenView View => Root.Find("Panel_Team").GetComponent<TeamScreenView>();
    private static TeamScreenController Controller => View.GetComponent<TeamScreenController>();
    private static TeamRoleCardView[] Cards => Field<List<TeamRoleCardView>>(Controller,"cards").Where(c=>c!=null).ToArray();
    private static TeamRoleCardView Card(string id) => Cards.Single(c=>c.Role.id==id);
    private static UnityEngine.UI.Button Confirm => Field<UnityEngine.UI.Button>(View,"confirmButton");
    private static UnityEngine.UI.ScrollRect Scroll => Field<UnityEngine.UI.ScrollRect>(View,"catalogScroll");
    private static T Field<T>(object o,string n) => (T)o.GetType().GetField(n,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(o);
    private static void Set(object o,string n,object v) => o.GetType().GetField(n,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(o,v);
    private static void Call(object o,string n) => o.GetType().GetMethod(n,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(o,null);
    private static void Expect(bool value,string detail) { if(!value) throw new InvalidOperationException(detail); }
    private static void Add(string name,Action action) => steps.Enqueue((name,action));
    private static PointerEventData Pointer(UnityEngine.UI.Button b)
    {
        var r=(RectTransform)(b.transform.Find("MarkerTouchTarget")??b.transform);
        return new PointerEventData(EventSystem.current){pointerId=-1,button=PointerEventData.InputButton.Left,position=RectTransformUtility.WorldToScreenPoint(null,r.TransformPoint(r.rect.center))};
    }
    private static void Click(UnityEngine.UI.Button b)
    {
        Canvas.ForceUpdateCanvases(); Expect(b.IsActive()&&b.IsInteractable(),"Unavailable "+b.name);
        var p=Pointer(b); var hits=new List<RaycastResult>(); EventSystem.current.RaycastAll(p,hits);
        Expect(hits.Count>0&&hits[0].gameObject.GetComponentInParent<UnityEngine.UI.Button>()==b,"Blocked "+b.name+" by "+(hits.Count>0?hits[0].gameObject.name:"nothing"));
        ExecuteEvents.Execute(b.gameObject,p,ExecuteEvents.pointerDownHandler); ExecuteEvents.Execute(b.gameObject,p,ExecuteEvents.pointerUpHandler); ExecuteEvents.Execute(b.gameObject,p,ExecuteEvents.pointerClickHandler);
    }
    private static void Fits()
    {
        Canvas.ForceUpdateCanvases();
        foreach(var text in View.GetComponentsInChildren<TMP_Text>())
        { text.ForceMeshUpdate(); Expect(text.font.name=="InterTight-Variable"&&!text.enableAutoSizing,"Typography "+text.name); Expect(!text.isTextOverflowing,"Overflow "+text.name+": "+text.text); }
        Expect(Scroll.content.parent==Scroll.viewport&&Scroll.viewport.GetComponent<UnityEngine.UI.Mask>()!=null&&Scroll.vertical&&!Scroll.horizontal,"Scroll hierarchy");
        foreach(var c in Cards) foreach(var n in new[]{"portrait","environment"})
        { var i=Field<UnityEngine.UI.Image>(c,n); Expect(i.sprite!=null&&i.preserveAspect&&i.color==Color.white&&i.type==UnityEngine.UI.Image.Type.Simple,"Artwork "+c.Role.id+" "+n); }
        foreach(var name in new[]{"Header","TitleText","ContextText","Guidance","BottomBar"})
        { var corners=new Vector3[4]; ((RectTransform)View.transform.Find(name)).GetWorldCorners(corners); foreach(var c in corners){var p=RectTransformUtility.WorldToScreenPoint(null,c);Expect(p.x>=-.5&&p.y>=-.5&&p.x<=Screen.width+.5&&p.y<=Screen.height+.5,"Viewport "+name);} }
        Expect(UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length==1,"EventSystem count");
        var menu=GameObject.Find("Canvas/MenuAccess/Button");
        if(menu!=null&&menu.activeInHierarchy)
        {
            Rect Bounds(RectTransform r) { var corners=new Vector3[4];r.GetWorldCorners(corners);return Rect.MinMaxRect(corners[0].x,corners[0].y,corners[2].x,corners[2].y); }
            Expect(!Bounds((RectTransform)Confirm.transform).Overlaps(Bounds((RectTransform)menu.transform)),"Menu access overlaps team confirmation");
        }
    }
    private static void Capture(string name, bool teamScreen = true)
    {
        if(teamScreen) Fits(); string path=UI01Validation.Capture("../UI-11/"+name); using var hash=System.Security.Cryptography.SHA256.Create();
        captures.Add(new {name,path,frame=Time.frameCount,width=Screen.width,height=Screen.height,sha256=BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(path))).Replace("-","").ToLowerInvariant()});
        File.WriteAllText("output/ui-update/UI-11/capture-frames.json",Newtonsoft.Json.JsonConvert.SerializeObject(captures,Newtonsoft.Json.Formatting.Indented));
    }
    private static void SameSession()
    {
        Expect(GameSessionState.Current.sessionId==sessionId&&GameSessionState.IsPersisted&&DatabaseInitializer.DatabaseService.Connection.Table<GameSessionEntity>().Count()==1,"Session persistence");
        Expect(GameManager.Instance.GetInstanceID()==managerId&&DatabaseInitializer.DatabaseService.GetInstanceID()==databaseId&&UnityEngine.SceneManagement.SceneManager.GetActiveScene().handle==sceneHandle,"Services replaced");
    }
    private static RestaurantData Restaurant => Resources.LoadAll<RestaurantData>("Restaurants").Single(r=>r.type==GameSessionState.Current.restaurantType);
    private static void Fixture(RestaurantType type,int stage=0)
    {
        var s=GameSessionState.Current; s.restaurantType=type; s.currentCash=65000; s.currentRound=1;
        s.targetSegment=type==RestaurantType.PODRAO?Segment.LOW:type==RestaurantType.JAPONES?Segment.MEDIUM:Segment.HIGH;
        var r=Resources.LoadAll<RestaurantData>("Restaurants").Single(x=>x.type==type); var data=new TeamSelectionData();
        if(stage==1)TeamSelectionHelper.SetMemberQuantity(data,"attendant",1);
        if(stage==2)foreach(var item in r.requiredRoles)TeamSelectionHelper.SetMemberQuantity(data,item.roleId,item.quantity);
        s.teamJson=TeamSelectionHelper.ToJson(data); s.menuPricingJson=MenuPricingHelper.ToJson(MenuPricingHelper.FromProducts(r.products));
        GameSessionState.Save(); GameManager.Instance.StateMachine.ForceState(GameState.Initial_Equipment); GameManager.Instance.StateMachine.TryChangeState(GameState.Initial_Team); EventSystem.current.SetSelectedGameObject(null);
    }
    private static void VerifyTotals(int stage)
    {
        var team=TeamSelectionHelper.FromJson(GameSessionState.Current.teamJson); var roles=Resources.LoadAll<RoleData>("Roles").ToList();
        var a=AlignmentEngine.Calculate(GameSessionState.Current.restaurantType,GameSessionState.Current.targetSegment,GameSessionState.Current.locationZone,Restaurant,MenuPricingHelper.FromJson(GameSessionState.Current.menuPricingJson),team,roles);
        double salary=team.members.Sum(m=>(double)roles.Single(r=>r.id==m.roleId).salary*m.quantity); long capacity=team.members.Sum(m=>(long)roles.Single(r=>r.id==m.roleId).maxClientsSupported*m.quantity);
        Expect(Field<TMP_Text>(View,"payrollText").text==FinancialScreenView.Money((float)salary),"Payroll");
        Expect(Field<TMP_Text>(View,"capacityText").text==$"{capacity:N0} clientes/mês","Capacity");
        Expect(Field<TMP_Text>(View,"alignmentText").text==$"{a.totalScore}/80  •  Equipe {a.restaurantTeamScore}/20"&&a.restaurantTeamScore==(stage==2?20:5),"Alignment");
        Expect(Confirm.interactable&&Cards.Length==5&&Cards.All(c=>c.Role.id!="waiter"),"Catalog/confirmation preserves rules");
        Expect(Field<GameObject>(View,"emptyTeam").activeSelf==(stage==0),"Pending empty");
    }
    private static void Change(string id,int quantity,string name)
    {
        Add("Focus role "+name,()=>Field<UnityEngine.UI.Button>(Card(id),quantity>Card(id).Quantity?"addButton":"removeButton").Select());
        Add("Adjust quantity by raycast "+name,()=>{var c=Card(id);Click(Field<UnityEngine.UI.Button>(c,quantity>c.Quantity?"addButton":"removeButton"));Expect(c.Quantity==quantity,"Quantity "+id);Expect(GameSessionState.Current.currentCash==65000,"Payroll debited immediately");});
    }
    [MenuItem("Tools/Jogos de Empresa/UI-11/Validate team in isolated Play Mode")]
    public static void Run()
    {
        UI01Validation.RequireIsolatedStorage(); if(!EditorApplication.isPlaying)throw new InvalidOperationException("Use isolated Play Mode.");
        reportPath="output/ui-update/UI-11/playmode-team.json"; report=new Report(); steps.Clear(); captures.Clear(); lastFrame=-1; retries=0;
        managerId=GameManager.Instance.GetInstanceID(); databaseId=DatabaseInitializer.DatabaseService.GetInstanceID(); sceneHandle=UnityEngine.SceneManagement.SceneManager.GetActiveScene().handle;
        Add("Fresh isolated identification",()=>{Expect(DatabaseInitializer.DatabaseService.Connection.Table<GameSessionEntity>().Count()==0,"Fresh database required");var f=UnityEngine.Object.FindFirstObjectByType<UIIndentification>();Field<TMP_InputField>(f,"studentNameInput").text="Aluna UI11";Field<TMP_InputField>(f,"raInput").text="111111";f.Register();});
        Add("D1 stays draft",()=>{var v=UnityEngine.Object.FindFirstObjectByType<LocationScreenView>();ExecuteEvents.Execute(v.Zones.Single(z=>z.id=="bank").marker.gameObject,new PointerEventData(EventSystem.current){pointerId=4},ExecuteEvents.pointerClickHandler);Click(Field<UnityEngine.UI.Button>(v,"confirmButton"));Expect(DatabaseInitializer.DatabaseService.Connection.Table<GameSessionEntity>().Count()==0,"D1 insert");});
        Add("D2 stays draft",()=>{var v=UnityEngine.Object.FindFirstObjectByType<RestaurantScreenView>();Click(Field<UnityEngine.UI.Button>(v,"cardPodrao"));Click(Field<UnityEngine.UI.Button>(v,"confirmButton"));Expect(DatabaseInitializer.DatabaseService.Connection.Table<GameSessionEntity>().Count()==0,"D2 insert");});
        Add("D3 first insert through technical review",()=>{Click(Field<UnityEngine.UI.Button>(UnityEngine.Object.FindFirstObjectByType<MenuPricingScreenView>(),"confirmButton"));Expect(GameManager.Instance.StateMachine.CurrentState==GameState.Initial_Capital,"D3 destination");sessionId=GameSessionState.Current.sessionId;SameSession();});
        Add("Choose no credit",()=>Click(Field<UnityEngine.UI.Button>(UnityEngine.Object.FindObjectsByType<BankCardView>(FindObjectsSortMode.None).Single(c=>c.CreditLine.id=="sem_credito"),"selectButton")));
        Add("Bank to equipment step 6",()=>Click(Field<UnityEngine.UI.Button>(UnityEngine.Object.FindFirstObjectByType<FinancialScreenView>(),"confirmButton")));
        Add("Equipment step 6 to team step 7",()=>{Click(Field<UnityEngine.UI.Button>(UnityEngine.Object.FindFirstObjectByType<EquipmentScreenView>(),"confirmButton"));Expect(GameManager.Instance.StateMachine.CurrentState==GameState.Initial_Team&&View.gameObject.activeInHierarchy,"Initial team binding");});
        foreach(var size in new[]{new Vector2Int(1920,1080),new Vector2Int(1366,768),new Vector2Int(1280,720)})
        {
            var viewport=size;string res=size.x+"x"+size.y;
            foreach(var restaurant in new[]{RestaurantType.PODRAO,RestaurantType.JAPONES,RestaurantType.FRANCES})
            {
                var type=restaurant;string tag=type.ToString().ToLowerInvariant()+"-"+res;
                Add("Prepare empty "+tag,()=>{UI01Validation.SetGameView(viewport.x,viewport.y);Fixture(type);});
                Add("Empty zero/pending/minus disabled "+tag,()=>{Expect(Screen.width==viewport.x&&Screen.height==viewport.y,"Resolution");SameSession();VerifyTotals(0);Expect(Cards.All(c=>!Field<UnityEngine.UI.Button>(c,"removeButton").interactable&&c.Quantity==0),"Zero controls");Capture("empty-"+tag);});
                Change("attendant",1,tag);
                Add("Incomplete team keeps original confirmation rule "+tag,()=>{VerifyTotals(1);Capture("incomplete-"+tag);});
                Change("attendant",2,tag);
                string specialist=type==RestaurantType.PODRAO?"grill_cook":type==RestaurantType.JAPONES?"sushi_chef":"head_chef";
                Change(specialist,1,tag);
                Add("Complete team calculation and selected card "+tag,()=>{VerifyTotals(2);Capture("valid-"+tag);Scroll.verticalNormalizedPosition=0;EventSystem.current.SetSelectedGameObject(null);});
                Add("All five roles and whole environments "+tag,()=>{Capture("catalog-bottom-"+tag);Click(Confirm);});
                Add("Confirmation reaches hub with no blank panel "+tag,()=>{SameSession();Expect(GameManager.Instance.StateMachine.CurrentState==GameState.Management_Hub&&MenuNavigator.Instance.Current==PanelId.Menu&&Root.Find("Panel_Menu").gameObject.activeInHierarchy&&!View.gameObject.activeInHierarchy,"Hub destination");Expect(GameSessionState.Current.currentCash==65000,"Hiring debit");var hud=UnityEngine.Object.FindFirstObjectByType<MenuHudView>();var summary=Game.Domain.Service.EstablishmentSummaryService.Build();Expect(Field<TMP_Text>(hud,"scoreValue").text==summary.Score.ToString()&&Field<TMP_Text>(hud,"cashValue").text==FinancialScreenView.Money(summary.Cash),"HUD refresh");Capture("hub-"+tag,false);Call(Controller,"Confirm");GameSessionState.LoadActiveSession(GameSessionState.Current.userId);MenuNavigator.Instance.Open(PanelId.Team);});
                Add("SQLite quantities survive revisit "+tag,()=>{VerifyTotals(2);Expect(!Field<TMP_Text>(View,"stepText").text.Contains("PASSO"),"Hub context");Capture("revisit-"+tag);Click(Confirm);});
            }
            Add("Prepare normal controls "+res,()=>{Fixture(RestaurantType.PODRAO);EventSystem.current.SetSelectedGameObject(null);});
            Add("Normal minus disabled and full typography "+res,()=>{Capture("normal-"+res);var b=Field<UnityEngine.UI.Button>(Card("attendant"),"addButton");ExecuteEvents.Execute(b.gameObject,Pointer(b),ExecuteEvents.pointerEnterHandler);});
            Add("Hover "+res,()=>{var b=Field<UnityEngine.UI.Button>(Card("attendant"),"addButton");Expect(b.GetComponent<SharedControlAppearance>().CurrentState=="hover","Hover");Capture("hover-"+res);ExecuteEvents.Execute(b.gameObject,Pointer(b),ExecuteEvents.pointerDownHandler);});
            Add("Pressed "+res,()=>{var b=Field<UnityEngine.UI.Button>(Card("attendant"),"addButton");Expect(b.GetComponent<SharedControlAppearance>().CurrentState=="pressed","Pressed");Capture("pressed-"+res);ExecuteEvents.Execute(b.gameObject,Pointer(b),ExecuteEvents.pointerUpHandler);ExecuteEvents.Execute(b.gameObject,Pointer(b),ExecuteEvents.pointerExitHandler);b.Select();});
            Add("Keyboard focus "+res,()=>{var b=Field<UnityEngine.UI.Button>(Card("attendant"),"addButton");Expect(b.GetComponent<SharedControlAppearance>().CurrentState=="focus","Focus");Capture("focus-"+res);ExecuteEvents.Execute(b.gameObject,new BaseEventData(EventSystem.current),ExecuteEvents.submitHandler);});
            Add("Keyboard submit adds one and selected state "+res,()=>{Expect(Card("attendant").Quantity==1&&Card("attendant").GetComponent<SharedControlAppearance>().IsSelected,"Submit/selected");Capture("selected-"+res);Click(Confirm);});
            Add("Prepare empty confirmation "+res,()=>Fixture(RestaurantType.PODRAO));
            Add("Empty team may confirm without new hiring gate "+res,()=>{Click(Confirm);Expect(MenuNavigator.Instance.Current==PanelId.Menu,"Empty confirmation");});
            Add("Prepare incomplete confirmation "+res,()=>Fixture(RestaurantType.FRANCES,1));
            Add("Incomplete team may confirm without new hiring gate "+res,()=>{Click(Confirm);Expect(MenuNavigator.Instance.Current==PanelId.Menu,"Incomplete confirmation");});
        }
        Add("Open RH from actual hub node",()=>{var node=UnityEngine.Object.FindObjectsByType<MapMenuNode>(FindObjectsSortMode.None).Single(n=>n.Destination==PanelId.Team);origin=node.gameObject;node.GetComponent<UnityEngine.UI.Button>().Select();Click(node.GetComponent<UnityEngine.UI.Button>());});
        Change("attendant",2,"hub quantity");
        Add("Return to originating hub node",()=>Click(Confirm));
        Add("Hub focus restored and no extra debit",()=>{Expect(MenuNavigator.Instance.Current==PanelId.Menu&&EventSystem.current.currentSelectedGameObject==origin&&GameSessionState.Current.currentCash==65000,"Hub focus/economy");MenuNavigator.Instance.Open(PanelId.Establishment);});
        Add("Establishment reflects hired staff and opens RH",()=>{var summary=Game.Domain.Service.EstablishmentSummaryService.Build();Expect(summary.TeamCount==2&&summary.TeamDetails.Contains("Atendente"),"Establishment refresh");var content=UnityEngine.Object.FindFirstObjectByType<EstablishmentReviewContent>();var b=Field<EstablishmentReviewContent.DecisionVisual[]>(content,"decisions")[5].button;origin=b.gameObject;b.Select();Click(b);});
        Change("head_chef",1,"nested complete");
        Change("manager",1,"optional manager");
        Add("Nested confirmation saves alignment",()=>{snapshot=GameSessionState.Current.menuPricingJson;Capture("nested-manager-1280x720");Click(Confirm);});
        Add("Nested focus and establishment totals update",()=>{Expect(MenuNavigator.Instance.Current==PanelId.Establishment&&EventSystem.current.currentSelectedGameObject==origin,"Nested focus");var s=Game.Domain.Service.EstablishmentSummaryService.Build();Expect(s.TeamCount==4&&s.SalariesCost==17400&&s.Decisions[5].Ready,"Summary payroll/count");Expect(GameSessionState.Current.menuPricingJson==snapshot&&GameSessionState.Current.currentCash==65000,"Unrelated mutation");MenuNavigator.Instance.Open(PanelId.Team);});
        Change("manager",0,"remove optional manager");
        Add("Removing quantity zero removes persisted role ID",()=>{Expect(!TeamSelectionHelper.FromJson(GameSessionState.Current.teamJson).members.Any(m=>m.roleId=="manager"),"Zero persisted");Click(Confirm);GameSessionState.LoadActiveSession(GameSessionState.Current.userId);MenuNavigator.Instance.Open(PanelId.Team);});
        Add("Long native role name",()=>{var c=Card("head_chef");Field<TMP_Text>(c,"nameText").text=string.Join(" ",Enumerable.Repeat("Chef de cozinha com descrição extensa de responsabilidade",3));Field<UnityEngine.UI.Button>(c,"addButton").Select();});
        Add("Reveal long role after layout reflows",()=>{EventSystem.current.SetSelectedGameObject(null);Field<UnityEngine.UI.Button>(Card("head_chef"),"addButton").Select();});
        Add("Long text stays readable without autosize",()=>{Capture("long-text-1280x720");Call(Controller,"Populate");});
        Add("Missing presentation reference",()=>{prefab=Field<TeamRoleCardView>(View,"cardPrefab");Set(View,"cardPrefab",null);Call(Controller,"Populate");});
        Add("Missing reference fails closed",()=>{Expect(!Confirm.interactable&&Field<TMP_Text>(View,"hintText").text.Contains("Não foi possível"),"Missing error");Capture("reference-error-1280x720");Set(View,"cardPrefab",prefab);Call(Controller,"Populate");});
        Add("Malformed team data fails closed without rewriting",()=>{snapshot=GameSessionState.Current.teamJson;GameSessionState.Current.teamJson="{broken";Call(Controller,"Populate");});
        Add("Malformed data gives error without mutating persistence",()=>{Expect(!Confirm.interactable&&GameSessionState.Current.teamJson=="{broken","Malformed guard");Capture("data-error-1280x720");GameSessionState.Current.teamJson=snapshot;Call(Controller,"Populate");});
        Add("Unavailable session",()=>{savedSession=GameSessionState.Current;GameSessionState.Clear();Call(Controller,"Populate");});
        Add("No preview numbers when session absent",()=>{Expect(!Confirm.interactable&&Field<TMP_Text>(View,"payrollText").text=="Indisponível"&&Cards.All(c=>!Field<UnityEngine.UI.Button>(c,"addButton").interactable),"Unavailable state");Capture("session-unavailable-1280x720");GameSessionState.Set(savedSession);GameSessionState.Persist();GameManager.Instance.StateMachine.ForceState(GameState.Management_Hub);});
        foreach(int month in new[]{2,3})
        {
            int currentMonth=month;
            Add("Open hiring month "+currentMonth,()=>{GameSessionState.Current.currentRound=currentMonth;GameSessionState.Save();MenuNavigator.Instance.Open(PanelId.Team);});
            Add("Month "+currentMonth+" preserves hiring and quantities",()=>{Expect(Cards.Length==5&&Card("attendant").Quantity==2&&Card("head_chef").Quantity==1&&Confirm.interactable,"Month hiring");Capture("month-"+currentMonth+"-1280x720");Click(Confirm);SameSession();});
        }
        Add("Final services and runtime evidence",()=>{File.WriteAllText("output/ui-update/UI-11/runtime.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{project=UnityEngine.Application.dataPath,unity=UnityEngine.Application.unityVersion,frame=Time.frameCount,sessionId,managerId,databaseId,sceneHandle,database=DatabaseInitializer.DatabaseService.Connection.DatabasePath,state=GameManager.Instance.StateMachine.CurrentState.ToString(),panel=MenuNavigator.Instance.Current.ToString()},Newtonsoft.Json.Formatting.Indented));});
        next=EditorApplication.timeSinceStartup+.5;EditorApplication.update-=Tick;EditorApplication.update+=Tick;
    }
    public static void RunFinalSavedScene()
    {
        UI01Validation.RequireIsolatedStorage(); if(!EditorApplication.isPlaying) throw new InvalidOperationException("Use isolated Play Mode.");
        reportPath="output/ui-update/UI-11/playmode-saved-scene.json";report=new Report();steps.Clear();captures.Clear();lastFrame=-1;retries=0;
        foreach(var entry in Newtonsoft.Json.Linq.JArray.Parse(File.ReadAllText("output/ui-update/UI-11/capture-frames.json")))
            if(!((string)entry["name"]).StartsWith("session-unavailable-")&&!((string)entry["name"]).StartsWith("saved-scene-"))captures.Add(entry);
        Add("Prepare isolated saved-scene fixture",()=>{var s=GameSessionState.Current;var r=Resources.LoadAll<RestaurantData>("Restaurants").Single(r=>r.type==RestaurantType.FRANCES);s.restaurantType=r.type;s.targetSegment=Segment.HIGH;s.locationZone=LocationZone.Financas;s.currentCash=65000;s.menuPricingJson=MenuPricingHelper.ToJson(MenuPricingHelper.FromProducts(r.products));GameSessionState.Persist();});
        foreach(var size in new[]{new Vector2Int(1920,1080),new Vector2Int(1366,768),new Vector2Int(1280,720)})
        {
            var viewport=size;string res=size.x+"x"+size.y;
            Add("Prepare saved viewport "+res,()=>{UI01Validation.SetGameView(viewport.x,viewport.y);Fixture(RestaurantType.FRANCES,2);});
            Add("Saved scene renders complete team "+res,()=>{Expect(Screen.width==viewport.x&&Screen.height==viewport.y,"Resolution");VerifyTotals(2);Expect(Field<TMP_Text>(View,"footerText").text=="Use + e − para ajustar as contratações.","Available footer guidance");Capture("saved-scene-"+res);Click(Confirm);});
            Add("Saved scene confirmation reaches hub "+res,()=>{Expect(MenuNavigator.Instance.Current==PanelId.Menu&&Root.Find("Panel_Menu").gameObject.activeInHierarchy,"Saved hub");MenuNavigator.Instance.Open(PanelId.Team);});
            Add("Saved revisit and missing-session fixture "+res,()=>{VerifyTotals(2);Expect(Field<TMP_Text>(View,"footerText").text=="Use + e − para ajustar as contratações.","Revisit footer guidance");savedSession=GameSessionState.Current;GameSessionState.Clear();Call(Controller,"Populate");});
            Add("Unavailable session has no text overlay on disabled cards "+res,()=>{Expect(!Confirm.interactable&&!Field<GameObject>(View,"catalogError").activeSelf&&Field<TMP_Text>(View,"payrollText").text=="Indisponível"&&Cards.All(c=>!Field<UnityEngine.UI.Button>(c,"addButton").interactable),"Unavailable presentation");Expect(Field<TMP_Text>(View,"hintText").text=="Sessão indisponível. As contratações estão bloqueadas."&&Field<TMP_Text>(View,"footerText").text=="Contratações indisponíveis no momento.","Unavailable guidance must not suggest blocked controls or hidden navigation");Capture("session-unavailable-"+res);GameSessionState.Set(savedSession);GameSessionState.Persist();GameManager.Instance.StateMachine.ForceState(GameState.Management_Hub);});
        }
        next=EditorApplication.timeSinceStartup+.5;EditorApplication.update-=Tick;EditorApplication.update+=Tick;
    }
    private static void Tick()
    {
        if(EditorApplication.timeSinceStartup<next)return;
        if(!EditorApplication.isPlaying||steps.Count==0){report.completed=steps.Count==0&&report.failures==0;EditorApplication.update-=Tick;File.WriteAllText(reportPath,JsonUtility.ToJson(report,true));return;}
        if(Time.frameCount<=lastFrame){EditorApplication.QueuePlayerLoopUpdate();EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Repaint();return;}
        var step=steps.Dequeue();var check=new Check{name=step.Item1,frame=Time.frameCount};
        try{step.Item2();check.passed=true;retries=0;}
        catch(InvalidOperationException e)when(e.Message=="The Game View must render at the evidence resolution."&&retries++<10){var rest=steps.ToArray();steps.Clear();steps.Enqueue(step);foreach(var pending in rest)steps.Enqueue(pending);next=EditorApplication.timeSinceStartup+1;return;}
        catch(Exception e){check.detail=e.ToString();report.failures++;steps.Clear();Debug.LogError("[UI-11 QA] "+check.name+": "+e.Message);}
        report.checks.Add(check);lastFrame=Time.frameCount;EditorApplication.QueuePlayerLoopUpdate();EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Repaint();next=EditorApplication.timeSinceStartup+.35;
    }
}
