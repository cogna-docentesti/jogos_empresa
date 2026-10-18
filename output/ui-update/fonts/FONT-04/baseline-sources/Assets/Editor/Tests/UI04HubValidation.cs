using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Game.Adapter.In.UI;
using Game.Adapter.In.UI.Navigation;
using Game.Domain.Service;
using Game.Infrastructure.Session;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

/// <summary>Opt-in fixture for the hub only. Does not pretend to validate future catalog screens.</summary>
public static class UI04HubValidation
{
    [Serializable] private class Check { public string name,detail; public bool passed; public int frame; }
    [Serializable] private class Report { public List<Check> checks=new(); public int failures; public bool completed; }
    private static readonly Queue<(string name,Action action)> steps=new();
    private static Report report;
    private static double next;
    private static HubMapView hub;
    private static MenuNavigator Nav => MenuNavigator.Instance;
    private static Transform Master => hub.transform.Find("MapMaster");
    private static T Field<T>(object o,string name)=>(T)o.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(o);
    private static T Find<T>() where T:UnityEngine.Object => UnityEngine.Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
    private static void Expect(bool test,string message) { if(!test) throw new InvalidOperationException(message); }
    private static void Add(string name,Action action)=>steps.Enqueue((name,action));
    private static void Capture(string name)=>UI01Validation.Capture("../UI-04/"+name);
    private static Vector2 ScreenPoint(Vector2 point)=>RectTransformUtility.WorldToScreenPoint(null,Master.TransformPoint(new Vector3(point.x,-point.y)));
    private static PointerEventData Pointer(Vector2 point,int id=-1)=>new(EventSystem.current){pointerId=id,position=ScreenPoint(point),button=PointerEventData.InputButton.Left};
    private static Vector2 Centroid(MapMenuNode node)
    { var p=node.GetComponentInChildren<MapPolygonHitArea>().Polygon; return p.Aggregate(Vector2.zero,(a,b)=>a+b)/p.Length; }
    private static void Click(MapMenuNode node,int id)
    {
        var pointer=Pointer(Centroid(node),id); var hits=new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer,hits);
        Expect(hits.Count>0 && hits[0].gameObject.GetComponentInParent<UnityEngine.UI.Button>()==node.GetComponent<UnityEngine.UI.Button>(),"Misaligned/blocked polygon "+node.name);
        var target=ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
        ExecuteEvents.Execute(target,pointer,ExecuteEvents.pointerDownHandler);
        ExecuteEvents.Execute(target,pointer,ExecuteEvents.pointerUpHandler);
        ExecuteEvents.Execute(target,pointer,ExecuteEvents.pointerClickHandler);
    }
    private static void AssertDestination(MapMenuNode node)
    {
        var registry=Find<PanelRegistry>();
        Expect(Nav.Current==node.Destination && registry.Resolve(node.Destination).activeInHierarchy,"Wrong destination "+node.LocationId);
        Expect(GameManager.Instance.StateMachine.CurrentState==GameState.Management_Hub,"Navigation changed campaign state");
        Nav.Back(); Expect(Nav.Current==PanelId.Menu && hub.gameObject.activeInHierarchy,"Back lost hub");
    }
    [MenuItem("Tools/Jogos de Empresa/UI-04/Validate hub in isolated Play Mode")]
    public static void Run()
    {
        UI01Validation.RequireIsolatedStorage();
        if(!EditorApplication.isPlaying) throw new InvalidOperationException("Use isolated Play Mode.");
        hub=Find<HubMapView>(); report=new Report(); steps.Clear();
        Add("Correct project, live identification and no financial HUD or hub access",()=>{
            Expect(UnityEngine.Application.dataPath=="D:/Cogna/jogos_empresa/Assets","Wrong project");
            Expect(Time.frameCount>1,"Frozen Play Mode");
            Expect(GameManager.Instance.StateMachine.CurrentState==GameState.Config_Identification,"Wrong opening state");
            Expect(!hub.gameObject.activeInHierarchy && !Find<MenuHudView>().gameObject.activeInHierarchy,"HUD over identification");
            Expect(!GameObject.Find("Canvas").transform.Find("MenuAccess/Button").gameObject.activeInHierarchy,"Menu over identification");
            Nav.Open(PanelId.Financial); Expect(Nav.Current==PanelId.None,"Hub bypassed identification");
            Expect(DatabaseInitializer.DatabaseService.Connection.Table<GameSessionEntity>().Count()==0,"QA fixture was not empty");
            Capture("identification-no-hud-1920x1080");
        });
        Add("Isolated persisted session fixture with real catalog and unchanged economic rules",()=>{
            PlayerSession.SaveIdentification("Aluna Teste UI04","123456","123456 - Restaurante da Aluna Teste UI04");
            var session=GameSessionState.Current;
            session.restaurantType=RestaurantType.FRANCES; session.targetSegment=Segment.HIGH; session.locationZone=LocationZone.Financas;
            session.companyName=PlayerSession.RestaurantName; session.studentName=PlayerSession.StudentName; session.studentRA=PlayerSession.StudentRA;
            session.currentRound=1; session.currentCash=18000; session.initialCapital=18000;
            var restaurant=Resources.LoadAll<RestaurantData>("Restaurants").First(r=>r.type==session.restaurantType);
            var pricing=new MenuPricingData(); foreach(var product in restaurant.products) pricing.items.Add(new MenuPricingItem{productId=product.id,selectedPrice=product.price});
            session.menuPricingJson=MenuPricingHelper.ToJson(pricing); PlayerSession.LoadDecisionsFrom(session);
            Expect(GameSessionState.Persist(),"QA fixture failed to persist");
            // Future equipment/team screens are outside UI-04: use a fixture, not their unfinished UI.
            GameManager.Instance.StateMachine.ForceState(GameState.Management_Hub);
            Expect(Nav.Current==PanelId.Menu,"State listener did not initialize navigator");
        });
        foreach(var size in new[]{new Vector2Int(1920,1080),new Vector2Int(1366,768),new Vector2Int(1280,720)})
        {
            var s=size; string suffix=s.x+"x"+s.y;
            Add("Viewport "+suffix,()=>UI01Validation.SetGameView(s.x,s.y));
            Add("Geometry, native typography, marker targets and real HUD at "+suffix,()=>{
                Expect(Screen.width==s.x && Screen.height==s.y,"Viewport mismatch");
                Expect(Master.localScale==Vector3.one*(2f/3f) && ((RectTransform)Master).anchoredPosition==new Vector2(24,-176),"Master transform changed");
                Expect(UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length==1,"Duplicate EventSystem");
                var hud=Find<MenuHudView>(); hud.Refresh(); var summary=EstablishmentSummaryService.Build();
                Expect(Field<TextMeshProUGUI>(hud,"cashValue").text==EstablishmentSummaryService.Brl(summary.Cash),"Cash not from live session");
                Expect(Field<TextMeshProUGUI>(hud,"scoreValue").text==summary.Score.ToString(),"Score not from real summary");
                foreach(var node in hub.Nodes)
                {
                    var hit=node.transform.Find("MarkerTouchTarget") as RectTransform;
                    Expect(hit.rect.height*hit.lossyScale.y>=44,"Small marker touch target");
                    var p=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,hit.TransformPoint(hit.rect.center))};
                    var hits=new List<RaycastResult>(); EventSystem.current.RaycastAll(p,hits);
                    Expect(hits.Count>0 && hits[0].gameObject.GetComponentInParent<MapMenuNode>()==node,"Blocked marker label "+node.name);
                }
                foreach(var text in hub.GetComponentsInChildren<TMP_Text>().Where(t=>t.enabled && t.text.Length>0))
                {
                    text.ForceMeshUpdate(); Expect(text.font.name=="InterTight-Variable" && !text.enableAutoSizing,"Font "+text.name);
                    Expect(!text.isTextOverflowing,"Overflow "+text.name+": "+text.text);
                }
                foreach(var button in hub.transform.Find("HubNavbar").GetComponentsInChildren<UnityEngine.UI.Button>().Concat(new[]{GameObject.Find("Canvas/ExitUI/ExitButton").GetComponent<UnityEngine.UI.Button>()}))
                {
                    var r=(RectTransform)button.transform;
                    var pointer=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,r.TransformPoint(r.rect.center))};
                    var hits=new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer,hits);
                    Expect(hits.Count>0 && hits[0].gameObject.GetComponentInParent<UnityEngine.UI.Button>()==button,"Navbar/exit overlap "+button.name);
                    Expect(r.rect.height*r.lossyScale.y>=44,"Small navbar target "+button.name);
                }
                Capture("map-frances-bank-"+suffix);
            });
            foreach(var type in new[]{RestaurantType.JAPONES,RestaurantType.PODRAO})
            {
                var t=type;
                Add("Restaurant "+t+" at "+suffix,()=>{
                    GameSessionState.Current.restaurantType=t; hub.Refresh();
                    foreach(var id in new[]{"university","bank","store","condominium","marketing"})
                    {
                        GameSessionState.Current.locationZone=LocationZoneMap.ToZone(id); hub.Refresh();
                        Expect(hub.Nodes.Single(n=>n.IsSelected).LocationId==id,"Variant/selection mismatch "+t+" / "+id);
                    }
                    GameSessionState.Current.locationZone=LocationZone.Financas; hub.Refresh();
                });
                Add("Capture restaurant "+t+" at "+suffix,()=>{
                    string id=t==RestaurantType.JAPONES?"MAP_REST_JAPONES":"MAP_REST_LANCHES";
                    Expect(Master.GetComponentsInChildren<UnityEngine.UI.Image>().Count(i=>i.enabled && i.name.StartsWith("MAP_REST_"))==1 && Master.Find(id).GetComponent<UnityEngine.UI.Image>().enabled,"Variant exclusivity");
                    Capture("map-"+t.ToString().ToLowerInvariant()+"-"+suffix);
                });
            }
            Add("Restore French variant",()=>{GameSessionState.Current.restaurantType=RestaurantType.FRANCES; hub.Refresh();});
        }
        Add("Set 1920 for navigation",()=>UI01Validation.SetGameView(1920,1080));
        foreach(var nodeIndex in Enumerable.Range(0,6))
        {
            int index=nodeIndex;
            Add("Mouse polygon destination "+index,()=>{Click(hub.Nodes[index],-1); AssertDestination(hub.Nodes[index]);});
            Add("Touch polygon destination "+index,()=>{Click(hub.Nodes[index],index+1); AssertDestination(hub.Nodes[index]);});
            Add("Focus marker "+index,()=>hub.Nodes[index].GetComponent<UnityEngine.UI.Button>().Select());
            Add("Keyboard submit marker "+index,()=>InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState(Key.Enter)));
            Add("Keyboard destination and history "+index,()=>{InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState()); AssertDestination(hub.Nodes[index]);});
        }
        Add("Keyboard arrow and Tab traversal",()=>{
            var first=hub.Nodes[0].GetComponent<UnityEngine.UI.Button>(); first.Select();
            ExecuteEvents.Execute(first.gameObject,new AxisEventData(EventSystem.current){moveDir=MoveDirection.Right,moveVector=Vector2.right},ExecuteEvents.moveHandler);
            Expect(EventSystem.current.currentSelectedGameObject==hub.Nodes[1].gameObject,"Arrow traversal");
            InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState(Key.Tab));
        });
        Add("Tab changes marker focus and shows its outline",()=>{
            InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState());
            Expect(EventSystem.current.currentSelectedGameObject==hub.Nodes[2].gameObject,"Tab traversal");
            Expect(hub.Nodes[2].transform.Find("MapFocus").GetComponent<UnityEngine.UI.Image>().enabled,"Missing keyboard focus");
            Capture("map-keyboard-focus-1920x1080");
        });
        foreach(var id in new[]{"university","bank","store","condominium","marketing"})
        {
            string zone=id;
            Add("Location ID "+zone+" selects only its canonical overlay",()=>{
                GameSessionState.Current.locationZone=LocationZoneMap.ToZone(zone); hub.Refresh();
                Expect(hub.Nodes.Count(n=>n.IsSelected)==1 && hub.Nodes.Single(n=>n.IsSelected).LocationId==zone,"Wrong selected marker");
                Expect(Master.GetComponentsInChildren<UnityEngine.UI.Image>().Count(i=>i.name.StartsWith("OVERLAY_") && i.name!="OVERLAY_CENTER" && i.enabled)==1,"Multiple selected overlays");
                Expect(Master.Find("OVERLAY_"+zone.ToUpperInvariant()).GetComponent<UnityEngine.UI.Image>().enabled,"Wrong overlay");
            });
            Add("Capture selected location "+zone,()=>{EventSystem.current.SetSelectedGameObject(null); Capture("selected-"+zone+"-1920x1080");});
        }
        Add("Hover marker uses approved selected surface",()=>{
            var node=hub.Nodes[0]; ExecuteEvents.Execute(node.gameObject,Pointer(Centroid(node)),ExecuteEvents.pointerEnterHandler);
            Expect(node.CurrentState=="hover" && node.transform.Find("MapPin").GetComponent<UnityEngine.UI.Image>().sprite.name=="pin_selected","Hover state");
        });
        Add("Capture hover and press",()=>{
            Capture("map-hover-1920x1080"); var node=hub.Nodes[0]; ExecuteEvents.Execute(node.gameObject,Pointer(Centroid(node)),ExecuteEvents.pointerDownHandler);
        });
        Add("Pressed state remains native and focus is separate",()=>{
            Expect(hub.Nodes[0].CurrentState=="pressed","Pressed state"); Capture("map-pressed-1920x1080");
            ExecuteEvents.Execute(hub.Nodes[0].gameObject,Pointer(Centroid(hub.Nodes[0])),ExecuteEvents.pointerUpHandler);
            hub.Nodes[0].GetComponent<UnityEngine.UI.Button>().interactable=false; hub.Nodes[0].Refresh();
        });
        Add("Disabled marker rejects activation and uses approved disabled sprites",()=>{
            var node=hub.Nodes[0]; Click(node,10); Expect(Nav.Current==PanelId.Menu,"Disabled activated");
            Expect(node.CurrentState=="disabled" && node.transform.Find("MapPin").GetComponent<UnityEngine.UI.Image>().sprite.name=="pin_disabled","Disabled visuals");
            Capture("map-disabled-1920x1080"); node.GetComponent<UnityEngine.UI.Button>().interactable=true;
        });
        Add("Navbar destinations and nested Back history",()=>{
            foreach(var button in hub.transform.Find("HubNavbar").GetComponentsInChildren<UnityEngine.UI.Button>())
            { button.onClick.Invoke(); Expect(Nav.Current!=PanelId.None,"Missing navbar action"); Nav.OpenRoot(); }
            hub.Nodes[1].GetComponent<UnityEngine.UI.Button>().Select(); Nav.Open(PanelId.Financial); Nav.Open(PanelId.EquipmentStore);
            Nav.Back(); Expect(Nav.Current==PanelId.Financial,"Nested Back failed"); Nav.Back();
            Expect(Nav.Current==PanelId.Menu && EventSystem.current.currentSelectedGameObject==hub.Nodes[1].gameObject,"Hub focus/history not restored");
            Nav.Open(PanelId.Financial);
        });
        Add("Visible Back button returns to the recorded panel",()=>{
            var back=GameObject.Find("Canvas/HubHistory/Back"); Expect(back!=null,"Back button hidden");
            back.GetComponent<UnityEngine.UI.Button>().onClick.Invoke(); Expect(Nav.Current==PanelId.Menu,"Back callback");
            var session=GameSessionState.Current; session.currentRound=2; session.currentCash=-1250; session.restaurantType=RestaurantType.JAPONES;
            PlayerSession.SaveIdentification("Aluna UI04","123456","123456 - Restaurante da Conceição de Albuquerque e Sá Silva com Cozinha Brasileira");
            Nav.Open(PanelId.Establishment); Nav.Back();
        });
        foreach(var size in new[]{new Vector2Int(1920,1080),new Vector2Int(1366,768),new Vector2Int(1280,720)})
        {
            var s=size; string suffix=s.x+"x"+s.y;
            Add("Set dynamic HUD viewport "+suffix,()=>UI01Validation.SetGameView(s.x,s.y));
            Add("Dynamic negative cash, score, company and campaign progress "+suffix,()=>{
                var hud=Find<MenuHudView>(); hud.Refresh();
                Expect(Field<TextMeshProUGUI>(hud,"cashValue").text=="R$ -1.250","Cash update");
                Expect(Field<TextMeshProUGUI>(hud,"cashValue").color==Game.Adapter.In.UI.Theme.GamePalette.Parse(Game.Adapter.In.UI.Theme.GamePalette.HexNegativeOnDark),"Negative cash contrast");
                Expect(Field<TextMeshProUGUI>(hud,"roundValue").text=="Mês 2 de 3","Period update");
                Expect(Mathf.Abs(Field<RectTransform>(hud,"progressFill").anchorMax.x-1f/3)<.001f,"Progress is not completed months");
                foreach(var text in hub.transform.Find("HubSummary").GetComponentsInChildren<TMP_Text>()) { text.ForceMeshUpdate(); Expect(!text.isTextOverflowing,"Long company overflow "+text.name); }
                var company=Field<TextMeshProUGUI>(hud,"companyValue"); company.ForceMeshUpdate(); Expect(!company.isTextOverflowing,"Topbar long company overflow");
                Capture("hud-negative-long-company-"+suffix);
            });
        }
        Add("Roads are not interactive and no sixth LocationData exists",()=>{
            var pointer=Pointer(new Vector2(900,500)); var hits=new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer,hits);
            Expect(!hits.Any(h=>h.gameObject.GetComponentInParent<MapMenuNode>()!=null),"Road stole clicks");
            Expect(Resources.LoadAll<LocationData>("Locations").Length==5,"Location catalog changed");
            Expect(DatabaseInitializer.DatabaseService.Connection.Table<GameSessionEntity>().Count()==1,"Unexpected fixture rows");
            Expect(Time.frameCount>100,"Game loop did not advance");
            File.WriteAllText("output/ui-update/UI-04/runtime-scene.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{project=UnityEngine.Application.dataPath,scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene().path,frame=Time.frameCount,screen=new[]{Screen.width,Screen.height},nodes=hub.Nodes.Select(n=>new{n.LocationId,n.Destination}),registry=Find<PanelRegistry>().Bindings.Select(b=>new{id=b.id.ToString(),root=b.root.name})},Newtonsoft.Json.Formatting.Indented));
        });
        Add("Initial capital and identification remain owned by configuration",()=>{
            GameManager.Instance.StateMachine.ForceState(GameState.Initial_Capital);
            Nav.OpenRoot(); Expect(Nav.Current==PanelId.None && !hub.gameObject.activeInHierarchy,"Hub bypassed initial capital");
            GameManager.Instance.StateMachine.TryChangeState(GameState.Initial_Equipment);
        });
        Add("Existing equipment continuation advances to step 7 without opening the hub",()=>{
            Field<UnityEngine.UI.Button>(Find<EquipmentScreenView>(),"confirmButton").onClick.Invoke();
            Expect(GameManager.Instance.StateMachine.CurrentState==GameState.Initial_Team,"Equipment continuation did not preserve setup order");
            Expect(Nav.Current==PanelId.None && Find<PanelRegistry>().Resolve(PanelId.Team).activeInHierarchy,"Setup confused with hub team navigation");
            GameManager.Instance.StateMachine.ForceState(GameState.Config_Identification);
            Expect(!hub.gameObject.activeInHierarchy && !Find<MenuHudView>().gameObject.activeInHierarchy,"HUD after configuration return");
        });
        next=EditorApplication.timeSinceStartup+.5; EditorApplication.update-=Tick; EditorApplication.update+=Tick;
    }
    private static void Tick()
    {
        if(EditorApplication.timeSinceStartup<next) return;
        if(!EditorApplication.isPlaying || steps.Count==0)
        {
            report.completed=steps.Count==0 && report.failures==0; EditorApplication.update-=Tick;
            File.WriteAllText("output/ui-update/UI-04/playmode-hub.json",JsonUtility.ToJson(report,true)); return;
        }
        var step=steps.Dequeue(); var check=new Check{name=step.name,frame=Time.frameCount};
        try { step.action(); check.passed=true; }
        catch(Exception e) {check.detail=e.ToString(); report.failures++; steps.Clear(); Debug.LogError("[UI-04 QA] "+step.name+": "+e.Message);}
        report.checks.Add(check); next=EditorApplication.timeSinceStartup+.5;
    }
}
