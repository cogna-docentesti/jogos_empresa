using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Game.Adapter.In.UI;
using Game.Adapter.In.Controllers;
using Game.Domain.Entities;
using Game.Infrastructure.Session;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public static class UI05LocationValidation
{
    [Serializable] private class Check { public string name; public bool passed; public string detail; public int frame; }
    [Serializable] private class Report { public List<Check> checks=new(); public bool completed; public int failures; }
    private static Report report; private static readonly Queue<(string name,Action run)> steps=new();
    private static double next; private static int sceneHandle,managerId,databaseId;
    private static LocationScreenData original;
    private static LocationScreenView View=>UnityEngine.Object.FindFirstObjectByType<LocationScreenView>(FindObjectsInactive.Include);
    private static LocationScreenController Controller=>View.GetComponentsInChildren<LocationScreenController>(true).First(c=>c.gameObject.activeSelf);
    private static Transform Master=>View.transform.Find("MapMaster");
    private static T Field<T>(object o,string name)=>(T)o.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(o);
    private static void Field(object o,string name,object value)=>o.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).SetValue(o,value);
    private static void Call(object o,string method,params object[] args)=>o.GetType().GetMethod(method,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(o,args);
    private static void Expect(bool value,string detail) { if(!value) throw new InvalidOperationException(detail); }
    private static void Add(string name,Action run)=>steps.Enqueue((name,run));
    private static void Capture(string name)=>UI01Validation.Capture("../UI-05/"+name);
    private static GameState State=>GameManager.Instance.StateMachine.CurrentState;
    private static void NoPersistence()
    {
        Expect(DatabaseInitializer.DatabaseService.Connection.Table<GameSessionEntity>().Count()==0,"Decisions persisted before D3");
        Expect(UnityEngine.SceneManagement.SceneManager.GetActiveScene().handle==sceneHandle,"Scene reload");
        Expect(GameManager.Instance.GetInstanceID()==managerId && DatabaseInitializer.DatabaseService.GetInstanceID()==databaseId,"Services reloaded");
    }
    private static PointerEventData Pointer(MapMenuNode marker,int id,bool label=false)
    {
        Vector3 point;
        if(label)
        { var r=(RectTransform)marker.transform.Find("MarkerTouchTarget"); point=r.TransformPoint(r.rect.center); }
        else
        { var p=marker.GetComponentInChildren<MapPolygonHitArea>().Polygon; var c=p.Aggregate(Vector2.zero,(a,b)=>a+b)/p.Length; point=Master.TransformPoint(new Vector3(c.x,-c.y)); }
        return new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,point),pointerId=id,button=PointerEventData.InputButton.Left};
    }
    private static void Click(MapMenuNode marker,int id,bool label=false)
    {
        var pointer=Pointer(marker,id,label); var hits=new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer,hits);
        Expect(hits.Count>0 && hits[0].gameObject.GetComponentInParent<UnityEngine.UI.Button>()==marker.GetComponent<UnityEngine.UI.Button>(),"Blocked/misaligned input "+marker.LocationId);
        var target=ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
        ExecuteEvents.Execute(target,pointer,ExecuteEvents.pointerDownHandler); ExecuteEvents.Execute(target,pointer,ExecuteEvents.pointerUpHandler);
        ExecuteEvents.Execute(target,pointer,ExecuteEvents.pointerClickHandler);
    }
    private static void TextFits()
    {
        Canvas.ForceUpdateCanvases();
        foreach(var t in View.GetComponentsInChildren<TMP_Text>().Where(t=>t.enabled && t.text.Length>0))
        { t.ForceMeshUpdate(); Expect(t.GetComponent<Game.Adapter.In.UI.Theme.TypographyRoleBinding>()!=null && t.font==Game.Adapter.In.UI.Theme.GameTypography.Resolve(t.GetComponent<Game.Adapter.In.UI.Theme.TypographyRoleBinding>().Role).Font && !t.enableAutoSizing,"Native typography "+t.name); Expect(!t.isTextOverflowing,"Text overflow "+t.name); }
    }
    private static void ScrollbarFits()
    {
        var scroll=Field<UnityEngine.UI.ScrollRect>(View,"detailScroll");
        var card=(RectTransform)View.transform.Find("DecisionSidebar");
        var corners=new Vector3[4]; scroll.verticalScrollbar.handleRect.GetWorldCorners(corners);
        foreach(var corner in corners) Expect(card.rect.Contains(card.InverseTransformPoint(corner)),"Scrollbar thumb escaped sidebar");
        ((RectTransform)scroll.verticalScrollbar.transform).GetWorldCorners(corners);
        foreach(var corner in corners) Expect(card.rect.Contains(card.InverseTransformPoint(corner)),"Scrollbar track escaped sidebar");
    }
    [MenuItem("Tools/Jogos de Empresa/UI-05/Validate location in isolated Play Mode")]
    public static void Run()
    {
        UI01Validation.RequireIsolatedStorage();
        if(!EditorApplication.isPlaying) throw new InvalidOperationException("Use isolated Play Mode.");
        report=new Report(); steps.Clear();
        sceneHandle=UnityEngine.SceneManagement.SceneManager.GetActiveScene().handle;
        managerId=GameManager.Instance.GetInstanceID(); databaseId=DatabaseInitializer.DatabaseService.GetInstanceID();
        Add("Correct project, live player loop and step 1 registration",()=>{
            Expect(UnityEngine.Application.dataPath=="D:/Cogna/jogos_empresa/Assets" && Time.frameCount>1,"Wrong/frozen Editor");
            Expect(State==InitialDecisionFlow.Identification,"Expected step 1");
            var form=UnityEngine.Object.FindFirstObjectByType<UIIndentification>();
            Field<TMP_InputField>(form,"studentNameInput").text="Aluna Teste UI05"; Field<TMP_InputField>(form,"raInput").text="123456";
            Field<UnityEngine.UI.Button>(form,"buttonRegister").onClick.Invoke(); NoPersistence();
        });
        Add("Step 2 starts empty and confirmation cannot advance without a selection",()=>{
            Expect(State==InitialDecisionFlow.Location && !View.CanConfirm && View.SelectedId==null,"Empty state");
            Field<UnityEngine.UI.Button>(View,"confirmButton").onClick.Invoke(); Expect(State==InitialDecisionFlow.Location,"Empty confirm advanced");
            Expect(View.transform.Find("DecisionHeader/Step").GetComponent<TMP_Text>().text.Contains("2 DE 7"),"Wrong onboarding step");
            Expect(UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length==1,"Duplicate EventSystem");
            original=Field<LocationScreenData>(Controller,"_data");
        });
        foreach(var resolution in new[]{new Vector2Int(1920,1080),new Vector2Int(1366,768),new Vector2Int(1280,720)})
        {
            var size=resolution; string suffix=size.x+"x"+size.y;
            Add("Set viewport "+suffix,()=>UI01Validation.SetGameView(size.x,size.y));
            Add("Prepare empty state "+suffix,()=>{PlayerSession.ClearDecisions(); EventSystem.current.SetSelectedGameObject(null); Call(Controller,"ShowCurrentSelection");});
            Add("Empty layout, full canvas, exact map matrix and disabled action "+suffix,()=>{
                Expect(Screen.width==size.x && Screen.height==size.y,"Viewport mismatch");
                Expect(((RectTransform)Master).anchoredPosition==new Vector2(24,-176) && Master.localScale==Vector3.one*(2f/3f),"Map contract");
                TextFits(); Capture("empty-"+suffix); NoPersistence();
            });
            foreach(var zoneId in new[]{"university","bank","store","condominium","marketing"})
            {
                string id=zoneId;
                Add("Mouse polygon selects "+id+" at "+suffix,()=>Click(View.Zones.Single(z=>z.id==id).marker,-1));
                Add("Catalog numbers, matching art, unique selection and layout "+id+" at "+suffix,()=>{
                    var zone=View.Zones.Single(z=>z.id==id); var raw=Resources.LoadAll<LocationData>("Locations").Single(l=>l.id==id);
                    Expect(View.SelectedId==id && View.CanConfirm,"Selection/confirm "+id);
                    Expect(View.Zones.Count(z=>z.marker.IsSelected)==1 && View.Zones.Count(z=>z.overlay.enabled)==1 && zone.overlay.enabled,"Selection ambiguity");
                    Expect(Field<UnityEngine.UI.Image>(View,"zoneVignette").sprite==zone.vignette,"Wrong vignette");
                    Expect(Field<TMP_Text>(View,"detailZoneNameText").text==raw.displayName && Field<TMP_Text>(View,"detailZoneDescriptionText").text==raw.description,"Catalog copy");
                    Expect(Field<TMP_Text>(View,"detailRentText").text=="R$ "+raw.rent.ToString("N0",System.Globalization.CultureInfo.GetCultureInfo("pt-BR")),"Rent");
                    Expect(Field<TMP_Text>(View,"detailCapacidadeText").text==raw.initialPhysicalCapacity+" clientes/dia","Capacity");
                    Expect(Field<TMP_Text>(View,"detailDemandaText").text==raw.baseDailyDemand+" pedidos/dia","Demand");
                    Expect(Field<TMP_Text>(View,"referencePriceText").text==raw.referencePriceFactor.ToString("0.00",System.Globalization.CultureInfo.GetCultureInfo("pt-BR"))+"×","Price factor");
                    TextFits(); EventSystem.current.SetSelectedGameObject(null); Capture(id+"-"+suffix); NoPersistence();
                    Click(zone.marker,5,true); Expect(View.SelectedId==id,"Touch label");
                    var target=(RectTransform)zone.marker.transform.Find("MarkerTouchTarget");
                    Expect(target.rect.height*target.lossyScale.y>=44,"Touch target smaller than 44 physical pixels");
                });
            }
        }
        Add("Set navigation viewport",()=>UI01Validation.SetGameView(1920,1080));
        Add("Back stores D1 draft and opens identification within GameScene",()=>{
            Click(View.Zones.Single(z=>z.id=="bank").marker,-1);
            Field<UnityEngine.UI.Button>(View,"backButton").onClick.Invoke();
            Expect(State==InitialDecisionFlow.Identification && PlayerSession.SelectedEstablishmentId=="bank","Draft/back");
            NoPersistence();
        });
        Add("Capture rendered identification after Back",()=>Capture("back-to-identification-1920x1080"));
        Add("Edit actual registration form and return to the preserved draft",()=>{
            var form=UnityEngine.Object.FindFirstObjectByType<UIIndentification>();
            Expect(Field<TMP_InputField>(form,"studentNameInput").text=="Aluna Teste UI05","Registration did not rehydrate");
            Field<TMP_InputField>(form,"studentNameInput").text="Aluna da Conceição UI05";
            Field<UnityEngine.UI.Button>(form,"buttonRegister").onClick.Invoke();
        });
        Add("Edited registration, selected art and draft restored without reload",()=>{
            Expect(State==InitialDecisionFlow.Location && View.SelectedId=="bank" && View.CanConfirm,"Draft resume");
            Expect(PlayerSession.StudentName=="Aluna da Conceição UI05","Edited registration lost");
            NoPersistence(); Capture("resumed-draft-1920x1080");
            Field<UnityEngine.UI.Button>(View,"confirmButton").onClick.Invoke();
        });
        Add("Confirm enters D2 with D1 draft and no SQLite session row",()=>{
            Expect(State==InitialDecisionFlow.Restaurant && PlayerSession.SelectedEstablishmentId=="bank","Advance");
            NoPersistence(); Capture("advanced-to-step3-1920x1080");
            var restaurantView=UnityEngine.Object.FindFirstObjectByType<RestaurantScreenView>();
            Field<UnityEngine.UI.Button>(restaurantView,"backButton").onClick.Invoke();
        });
        Add("D2 Back keeps D1 selection and presents the actual draft restaurant",()=>{
            Expect(State==InitialDecisionFlow.Location && View.SelectedId=="bank" && PlayerSession.HasRestaurant,"D2 return");
            Expect(Master.GetComponentsInChildren<UnityEngine.UI.Image>().Count(i=>i.name.StartsWith("MAP_REST_") && i.enabled)==1,"Restaurant exclusivity"); NoPersistence();
        });
        foreach(var i in Enumerable.Range(0,5))
        {
            int index=i;
            Add("Focus keyboard zone "+index,()=>View.Zones[index].marker.GetComponent<UnityEngine.UI.Button>().Select());
            Add("Submit keyboard zone "+index,()=>InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState(Key.Enter)));
            Add("Keyboard selection "+index,()=>{
                InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState());
                Expect(View.SelectedId==View.Zones[index].id,"Keyboard selection");
            });
        }
        Add("Hover visual",()=>{ var n=View.Zones[0].marker; EventSystem.current.SetSelectedGameObject(null); ExecuteEvents.Execute(n.gameObject,Pointer(n,-1),ExecuteEvents.pointerEnterHandler); });
        Add("Capture hover and press",()=>{ Expect(View.Zones[0].marker.CurrentState=="hover","Hover"); Capture("hover-1920x1080"); var n=View.Zones[0].marker; ExecuteEvents.Execute(n.gameObject,Pointer(n,-1),ExecuteEvents.pointerDownHandler); });
        Add("Pressed appearance",()=>{Expect(View.Zones[0].marker.CurrentState=="pressed","Press"); Capture("pressed-1920x1080"); var n=View.Zones[0].marker; ExecuteEvents.Execute(n.gameObject,Pointer(n,-1),ExecuteEvents.pointerUpHandler); n.GetComponent<UnityEngine.UI.Button>().Select();});
        Add("Visible keyboard focus",()=>{Expect(View.Zones[0].marker.transform.Find("MapFocus").GetComponent<UnityEngine.UI.Image>().enabled,"Focus");Capture("keyboard-focus-1920x1080");});
        Add("Prepare disabled zone",()=>{
            var n=View.Zones[0].marker; n.GetComponent<UnityEngine.UI.Button>().interactable=false; n.Refresh();
        });
        Add("Disabled zone rejects touch and preserves the valid selection",()=>{
            var n=View.Zones[0].marker;
            string previous=View.SelectedId; Click(n,7); Expect(View.SelectedId==previous && n.CurrentState=="disabled","Disabled selection");
            Capture("disabled-1920x1080"); n.GetComponent<UnityEngine.UI.Button>().interactable=true;
        });
        Add("Prepare unavailable catalog",()=>{
            PlayerSession.ClearDecisions(); Field(Controller,"_data",new LocationScreenData("Localização",Array.Empty<Establishment>()));
            Call(Controller,"ShowCurrentSelection");
        });
        Add("Unavailable catalog is clear and confirmation is disabled",()=>{
            Expect(!View.CanConfirm && View.SelectedId==null,"Unavailable confirmation");
            foreach(var z in View.Zones) Expect(!z.marker.GetComponent<UnityEngine.UI.Button>().interactable,"Unavailable marker");
            Capture("unavailable-1920x1080");
            Field(Controller,"_data",original); Call(Controller,"ShowCurrentSelection");
        });
        Add("Locked catalog entry cannot be selected or confirmed even by direct callbacks",()=>{
            var e=original.Establishments.First(); var locked=new Establishment(e.Id,e.Name,e.Description,e.Segment,e.RentCost,e.InitialPhysicalCapacity,e.BaseDailyDemand,e.ReferencePriceFactor,e.CompetitionLevel,e.Channels,false,false);
            Field(Controller,"_data",new LocationScreenData("Localização",new[]{locked})); Call(Controller,"ShowCurrentSelection");
            Call(Controller,"OnLocationSelected",e.Id); Field<UnityEngine.UI.Button>(View,"confirmButton").onClick.Invoke();
            Expect(!View.CanConfirm && View.SelectedId==null && State==InitialDecisionFlow.Location,"Locked bypass");
            Field(Controller,"_data",original); Call(Controller,"ShowCurrentSelection");
        });
        foreach(var size in new[]{new Vector2Int(1920,1080),new Vector2Int(1280,720)})
        {
            var s=size; string suffix=s.x+"x"+s.y;
            Add("Set long text viewport "+suffix,()=>UI01Validation.SetGameView(s.x,s.y));
            Add("Long copy fixture remains within the scroll viewport "+suffix,()=>{
                Click(View.Zones[0].marker,-1); var e=original.Establishments.First(x=>x.Id==View.SelectedId);
                View.ShowLocationDetails("Área Educacional com um nome extenso para aferir quebra de linha",e.Segment,
                    string.Join(" ",Enumerable.Repeat(e.Description,5)),e.RentCost,e.InitialPhysicalCapacity,e.BaseDailyDemand,e.ReferencePriceFactor,e.CompetitionLevel,"Salão · Entrega · Retirada com atendimento agendado");
            });
            Add("Long copy top and scrollable bounds "+suffix,()=>{
                TextFits(); ScrollbarFits(); var scroll=Field<UnityEngine.UI.ScrollRect>(View,"detailScroll");
                Expect(scroll.content.rect.height>scroll.viewport.rect.height,"Long text did not grow");
                Capture("long-copy-top-"+suffix); scroll.verticalNormalizedPosition=0;
            });
            Add("Long copy bottom keeps real indicators and actions accessible "+suffix,()=>{
                TextFits(); ScrollbarFits(); Capture("long-copy-bottom-"+suffix);
                var scroll=Field<UnityEngine.UI.ScrollRect>(View,"detailScroll");
                Expect(scroll.verticalNormalizedPosition<.01f,"Scroll did not reach bottom");
                var pointer=new PointerEventData(EventSystem.current){scrollDelta=new Vector2(0,1)};
                ExecuteEvents.Execute(scroll.gameObject,pointer,ExecuteEvents.scrollHandler);
                Expect(scroll.verticalNormalizedPosition>0,"Wheel scroll did not move");
                Call(Controller,"ShowCurrentSelection");
            });
        }
        Add("Restore final evidence viewport",()=>UI01Validation.SetGameView(1920,1080));
        foreach(var type in new[]{RestaurantType.FRANCES,RestaurantType.JAPONES,RestaurantType.PODRAO})
        {
            var t=type;
            Add("Draft restaurant variant "+t,()=>{
                PlayerSession.SaveRestaurant(t,t==RestaurantType.FRANCES?Segment.HIGH:t==RestaurantType.JAPONES?Segment.MEDIUM:Segment.LOW);
                View.enabled=false; View.enabled=true;
                Click(View.Zones.Single(z=>z.id=="bank").marker,-1);
            });
            Add("Exclusive restaurant art "+t,()=>{
                string id=t==RestaurantType.FRANCES?"MAP_REST_FRANCES":t==RestaurantType.JAPONES?"MAP_REST_JAPONES":"MAP_REST_LANCHES";
                Expect(Master.GetComponentsInChildren<UnityEngine.UI.Image>().Count(i=>i.enabled && i.name.StartsWith("MAP_REST_"))==1 && Master.Find(id).GetComponent<UnityEngine.UI.Image>().enabled,"Wrong draft restaurant art");
                Capture("draft-restaurant-"+t.ToString().ToLowerInvariant()+"-1920x1080"); NoPersistence();
            });
        }
        Add("Arrow and Tab traversal",()=>{
            var first=View.Zones[0].marker.GetComponent<UnityEngine.UI.Button>(); first.Select();
            ExecuteEvents.Execute(first.gameObject,new AxisEventData(EventSystem.current){moveDir=MoveDirection.Right,moveVector=Vector2.right},ExecuteEvents.moveHandler);
            Expect(EventSystem.current.currentSelectedGameObject==View.Zones[1].marker.gameObject,"Arrow traversal");
            InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState(Key.Tab));
        });
        Add("Tab uses shared navigation and focus",()=>{
            InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState());
            Expect(EventSystem.current.currentSelectedGameObject==View.Zones[2].marker.gameObject,"Tab traversal");
        });
        Add("Footer actions are unobstructed mouse/touch targets and roads stay inert",()=>{
            foreach(var name in new[]{"backButton","confirmButton"})
            {
                var button=Field<UnityEngine.UI.Button>(View,name); var r=(RectTransform)button.transform;
                var pointer=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,r.TransformPoint(r.rect.center)),pointerId=11};
                var hits=new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer,hits);
                Expect(hits.Count>0 && hits[0].gameObject.GetComponentInParent<UnityEngine.UI.Button>()==button,"Blocked footer "+name);
                Expect(r.rect.height*r.lossyScale.y>=44,"Small footer target");
            }
            var road=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,Master.TransformPoint(new Vector3(900,-500)))};
            var roadHits=new List<RaycastResult>(); EventSystem.current.RaycastAll(road,roadHits);
            Expect(!roadHits.Any(h=>h.gameObject.GetComponentInParent<MapMenuNode>()!=null),"Road is interactive");
        });
        Add("Final invariants: no sixth ID, no persistence and no legacy scene load",()=>{
            Expect(Resources.LoadAll<LocationData>("Locations").Length==5,"Changed catalog");
            NoPersistence(); Expect(Time.frameCount>100,"Frozen loop");
            File.WriteAllText("output/ui-update/UI-05/runtime-scene.json",Newtonsoft.Json.JsonConvert.SerializeObject(new {project=UnityEngine.Application.dataPath,scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene().path,frame=Time.frameCount,ids=View.Zones.Select(z=>z.id),databaseRows=DatabaseInitializer.DatabaseService.Connection.Table<GameSessionEntity>().Count()},Newtonsoft.Json.Formatting.Indented));
        });
        next=EditorApplication.timeSinceStartup+.4; EditorApplication.update-=Tick; EditorApplication.update+=Tick;
    }
    private static void Tick()
    {
        if(EditorApplication.timeSinceStartup<next) return;
        if(!EditorApplication.isPlaying || steps.Count==0)
        {
            report.completed=steps.Count==0 && report.failures==0; EditorApplication.update-=Tick;
            File.WriteAllText("output/ui-update/UI-05/playmode-location.json",JsonUtility.ToJson(report,true)); return;
        }
        var step=steps.Dequeue(); var check=new Check{name=step.name,frame=Time.frameCount};
        try { step.run(); check.passed=true; } catch(Exception e) {check.detail=e.ToString(); report.failures++; steps.Clear(); Debug.LogError("[UI-05 QA] "+step.name+": "+e.Message);}
        report.checks.Add(check); next=EditorApplication.timeSinceStartup+.4;
    }
}
