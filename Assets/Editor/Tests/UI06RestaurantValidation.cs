using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Game.Adapter.In.UI;
using Game.Adapter.In.Controllers;
using Game.Infrastructure.Session;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public static class UI06RestaurantValidation
{
    [Serializable] private class Check { public string name; public bool passed; public string detail; public int frame; }
    [Serializable] private class Report { public List<Check> checks=new(); public bool completed; public int failures; }
    private static Report report; private static readonly Queue<(string name,Action run)> steps=new(); private static double next;
    private static int managerId,databaseId,sceneHandle; private static float editedPrice;
    private static readonly string[] CardFields={"cardPodrao","cardJapones","cardFrances"}, SegmentFields={"btnLow","btnMedium","btnHigh"};
    private static RestaurantScreenView View=>UnityEngine.Object.FindFirstObjectByType<RestaurantScreenView>(FindObjectsInactive.Include);
    private static RestaurantScreenController Controller=>View.GetComponentInChildren<RestaurantScreenController>(true);
    private static GameState State=>GameManager.Instance.StateMachine.CurrentState;
    private static T Field<T>(object o,string n)=>(T)o.GetType().GetField(n,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(o);
    private static UnityEngine.UI.Button Button(string n)=>Field<UnityEngine.UI.Button>(View,n);
    private static void Call(object o,string method,params object[] args)=>o.GetType().GetMethod(method,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(o,args);
    private static void Expect(bool v,string message) { if(!v) throw new InvalidOperationException(message); }
    private static void Add(string name,Action run)=>steps.Enqueue((name,run));
    private static void Capture(string name)
    { var path=UI01Validation.Capture(UiValidationContext.CaptureName("UI-06",name)); UiValidationContext.OnCapture?.Invoke(name,path); }
    private static void NoPersistence()
    {
        Expect(DatabaseInitializer.DatabaseService.Connection.Table<GameSessionEntity>().Count()==0,"Persisted before D3");
        Expect(GameManager.Instance.GetInstanceID()==managerId && DatabaseInitializer.DatabaseService.GetInstanceID()==databaseId,"Service reload");
        Expect(UnityEngine.SceneManagement.SceneManager.GetActiveScene().handle==sceneHandle,"Scene reload");
    }
    private static PointerEventData Pointer(UnityEngine.UI.Button b,int id=-1)
    { var r=(RectTransform)b.transform; return new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,r.TransformPoint(r.rect.center)),pointerId=id,button=PointerEventData.InputButton.Left}; }
    private static void Click(UnityEngine.UI.Button b,int id=-1)
    {
        var p=Pointer(b,id); var hits=new List<RaycastResult>(); EventSystem.current.RaycastAll(p,hits);
        Expect(hits.Count>0 && hits[0].gameObject.GetComponentInParent<UnityEngine.UI.Button>()==b,"Blocked hit "+b.name);
        var target=ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
        ExecuteEvents.Execute(target,p,ExecuteEvents.pointerDownHandler); ExecuteEvents.Execute(target,p,ExecuteEvents.pointerUpHandler); ExecuteEvents.Execute(target,p,ExecuteEvents.pointerClickHandler);
    }
    private static void Fits()
    {
        Canvas.ForceUpdateCanvases();
        var overflowing=new List<string>();
        foreach(var t in View.GetComponentsInChildren<TMP_Text>())
        { t.ForceMeshUpdate(); Expect(t.GetComponent<Game.Adapter.In.UI.Theme.TypographyRoleBinding>() != null && t.font == Game.Adapter.In.UI.Theme.GameTypography.Resolve(t.GetComponent<Game.Adapter.In.UI.Theme.TypographyRoleBinding>().Role).Font && !t.enableAutoSizing,"Native type "+t.name); if(t.isTextOverflowing)overflowing.Add(t.name+" rect="+t.rectTransform.rect.size+" preferred="+t.preferredWidth+","+t.preferredHeight); }
        Expect(overflowing.Count==0,"Overflow "+string.Join("; ",overflowing));
        foreach(var s in View.GetComponentsInChildren<UnityEngine.UI.ScrollRect>())
        {
            var bounds=(RectTransform)s.transform; var corners=new Vector3[4]; s.verticalScrollbar.handleRect.GetWorldCorners(corners);
            var allowed=bounds.rect; allowed.xMin-=.1f; allowed.yMin-=.1f; allowed.xMax+=.1f; allowed.yMax+=.1f;
            foreach(var c in corners) Expect(allowed.Contains(bounds.InverseTransformPoint(c)),"Scrollbar escaped "+s.name);
        }
    }
    private static void Selection(RestaurantType type,Segment segment)
    {
        Expect(State==InitialDecisionFlow.Restaurant,"Not in D2");
        Expect(CardFields.Count(f=>Button(f).GetComponent<SharedControlAppearance>().IsSelected)==1,"Ambiguous restaurant");
        Expect(Button(CardFields[(int)type]).GetComponent<SharedControlAppearance>().IsSelected,"Wrong restaurant");
        Expect(SegmentFields.Count(f=>Button(f).GetComponent<SharedControlAppearance>().IsSelected)==1,"Ambiguous segment");
        Expect(Button(SegmentFields[(int)segment]).GetComponent<SharedControlAppearance>().IsSelected,"Wrong segment");
        var data=Resources.LoadAll<RestaurantData>("Restaurants").Single(d=>d.type==type);
        foreach(var s in new[]{Segment.LOW,Segment.MEDIUM,Segment.HIGH})
        {
            var b=Button(SegmentFields[(int)s]); Expect(b.interactable==data.AllowsSegment(s),"Availability rule");
            var img=b.transform.Find("Radio").GetComponent<UnityEngine.UI.Image>();
            Expect(img.sprite.name==(!data.AllowsSegment(s)?"radio_disabled":s==segment?"radio_on":"radio_off"),"Radio state");
        }
        foreach(var f in CardFields)
        { var facade=Button(f).transform.Find("Facade").GetComponent<UnityEngine.UI.Image>(); Expect(facade.color==Color.white && facade.preserveAspect && facade.sprite!=null,"Tint/aspect "+f); }
        Expect(Button("confirmButton").interactable,"Confirmation disabled"); Fits(); NoPersistence();
    }
    private static void MenuMatches(RestaurantType type,bool edited=false)
    {
        Expect(State==InitialDecisionFlow.Menu && PlayerSession.SelectedRestaurantType==type,"Not in D3");
        var items=UnityEngine.Object.FindObjectsByType<ProductPricingItemView>(FindObjectsSortMode.None).OrderBy(i=>i.Product.id).ToArray();
        var data=Resources.LoadAll<RestaurantData>("Restaurants").Single(d=>d.type==type);
        Expect(items.Length==3 && items.Select(i=>i.Product.id).SequenceEqual(data.products.Select(p=>p.id).OrderBy(id=>id)),"Wrong menu product IDs");
        foreach(var i in items) Expect(i.Product.IsPriceInRange(i.SelectedPrice),"Price bounds");
        if(edited) Expect(Mathf.Approximately(items[0].SelectedPrice,editedPrice),"Edited price lost");
        NoPersistence();
    }
    public static void Run()
    {
        UI01Validation.RequireIsolatedStorage(); if(!EditorApplication.isPlaying) throw new InvalidOperationException("Use isolated Play Mode");
        report=new Report(); steps.Clear(); managerId=GameManager.Instance.GetInstanceID(); databaseId=DatabaseInitializer.DatabaseService.GetInstanceID(); sceneHandle=UnityEngine.SceneManagement.SceneManager.GetActiveScene().handle;
        Add("Real identification form enters step 2",()=>{
            Expect(State==InitialDecisionFlow.Identification,"Expected clean start"); var form=UnityEngine.Object.FindFirstObjectByType<UIIndentification>();
            Field<TMP_InputField>(form,"studentNameInput").text="Aluna Teste UI06"; Field<TMP_InputField>(form,"raInput").text="123456";
            Field<UnityEngine.UI.Button>(form,"buttonRegister").onClick.Invoke(); NoPersistence();
        });
        Add("Real location selection enters step 3",()=>{
            var location=UnityEngine.Object.FindFirstObjectByType<LocationScreenView>(); var marker=location.Zones.Single(z=>z.id=="bank").marker;
            var r=(RectTransform)marker.transform.Find("MarkerTouchTarget");
            var p=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,r.TransformPoint(r.rect.center)),pointerId=3};
            ExecuteEvents.Execute(marker.gameObject,p,ExecuteEvents.pointerClickHandler);
            Field<UnityEngine.UI.Button>(location,"confirmButton").onClick.Invoke();
        });
        Add("Default D2 selection, full scene services and step number",()=>{
            Selection(RestaurantType.PODRAO,Segment.LOW);
            Expect(UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length==1,"EventSystem count");
            Expect(View.transform.Find("DecisionHeader/Step").GetComponent<TMP_Text>().text.Contains("3 DE 7"),"Wrong step");
        });
        foreach(var size in UiValidationContext.Resolutions)
        {
            var resolution=size; string suffix=size.x+"x"+size.y;
            Add("Set viewport "+suffix,()=>UI01Validation.SetGameView(resolution.x,resolution.y));
            foreach(var pair in new[]{(RestaurantType.PODRAO,Segment.LOW),(RestaurantType.PODRAO,Segment.MEDIUM),(RestaurantType.JAPONES,Segment.MEDIUM),(RestaurantType.JAPONES,Segment.HIGH),(RestaurantType.FRANCES,Segment.HIGH)})
            {
                var type=pair.Item1; var segment=pair.Item2;
                Add("Mouse selects "+type+" / "+segment+" at "+suffix,()=>{ Click(Button(CardFields[(int)type])); Click(Button(SegmentFields[(int)segment])); EventSystem.current.SetSelectedGameObject(null); });
                Add("Rendered selection, compatibility and native values "+type+" / "+segment+" at "+suffix,()=>{
                    Expect(Screen.width==resolution.x && Screen.height==resolution.y,"Wrong viewport"); Selection(type,segment);
                    var expected=type==RestaurantType.PODRAO?(segment==Segment.LOW?"95%":"70%"):type==RestaurantType.JAPONES?(segment==Segment.MEDIUM?"80%":"75%"):"95%";
                    Expect(Field<TMP_Text>(View,"coh2Value").text.Contains(expected),"Compatibility score changed");
                    Capture(type.ToString().ToLowerInvariant()+"-"+segment.ToString().ToLowerInvariant()+"-"+suffix);
                    // Touch follows the same hit target without leaving hover behind.
                    Click(Button(SegmentFields[(int)segment]),11); Selection(type,segment);
                    foreach(var f in CardFields.Concat(SegmentFields).Concat(new[]{"confirmButton","backButton"}).Where(f=>Button(f).interactable))
                    { var r=(RectTransform)Button(f).transform; Expect(r.rect.height*r.lossyScale.y>=44,"Small touch target "+f); }
                });
            }
        }
        Add("Set navigation viewport",()=>UI01Validation.SetGameView(1920,1080));
        Add("Blocked combination rejects direct callbacks",()=>{
            Click(Button("cardFrances")); Call(Controller,"OnSegmentSelected",Segment.LOW); Selection(RestaurantType.FRANCES,Segment.HIGH);
            Expect(Field<TMP_Text>(View,"hintText").text.Contains("não está disponível"),"Missing invalid message");
        });
        Add("Capture invalid feedback",()=>{Fits(); Capture("blocked-feedback-1920x1080"); Click(Button("cardPodrao")); Click(Button("btnMedium")); Click(Button("backButton"));});
        Add("Back retains draft and real location view",()=>{
            Expect(State==InitialDecisionFlow.Location && PlayerSession.SelectedRestaurantType==RestaurantType.PODRAO && PlayerSession.SelectedTargetSegment==Segment.MEDIUM,"Back draft");
            Capture("back-to-location-1920x1080"); NoPersistence();
            var v=UnityEngine.Object.FindFirstObjectByType<LocationScreenView>(); var marker=v.Zones.Single(z=>z.id=="university").marker;
            ExecuteEvents.Execute(marker.gameObject,new PointerEventData(EventSystem.current){pointerId=8},ExecuteEvents.pointerClickHandler);
            Field<UnityEngine.UI.Button>(v,"confirmButton").onClick.Invoke();
        });
        Add("D2 revisited with edited D1 and the same draft",()=>{
            Selection(RestaurantType.PODRAO,Segment.MEDIUM); Expect(PlayerSession.SelectedEstablishmentId=="university","Location edit lost");
            Expect(Field<TMP_Text>(View,"coh1Value").text.Contains("95%"),"Compatibility did not refresh"); Capture("resumed-draft-1920x1080");
        });
        foreach(var typeValue in new[]{RestaurantType.PODRAO,RestaurantType.JAPONES,RestaurantType.FRANCES})
        {
            var type=typeValue;
            Add("Confirm restaurant enters matching menu "+type,()=>{Click(Button(CardFields[(int)type])); Click(Button("confirmButton"));});
            Add("D3 has only that restaurant's three unchanged products "+type,()=>{
                MenuMatches(type); Capture("menu-"+type.ToString().ToLowerInvariant()+"-1920x1080");
                var first=UnityEngine.Object.FindObjectsByType<ProductPricingItemView>(FindObjectsSortMode.None).OrderBy(i=>i.Product.id).First();
                Field<UnityEngine.UI.Slider>(first,"priceSlider").value=(first.Product.minPrice+first.Product.maxPrice)/2f; editedPrice=first.SelectedPrice;
                Field<UnityEngine.UI.Button>(UnityEngine.Object.FindFirstObjectByType<MenuPricingScreenView>(),"backButton").onClick.Invoke();
            });
            Add("Return from menu restores selection "+type,()=>{
                Expect(State==InitialDecisionFlow.Restaurant && PlayerSession.SelectedRestaurantType==type,"D3 back"); NoPersistence();
                Capture("return-from-menu-"+type.ToString().ToLowerInvariant()+"-1920x1080"); Click(Button("confirmButton"));
            });
            Add("Same restaurant keeps edited prices "+type,()=>{
                MenuMatches(type,true); Field<UnityEngine.UI.Button>(UnityEngine.Object.FindFirstObjectByType<MenuPricingScreenView>(),"backButton").onClick.Invoke();
            });
        }
        Add("Switch from French to Lanches replaces previous menu draft",()=>{ Click(Button("cardPodrao")); Click(Button("confirmButton")); });
        Add("New restaurant resets old menu and uses its own default prices",()=>{
            MenuMatches(RestaurantType.PODRAO);
            foreach(var item in UnityEngine.Object.FindObjectsByType<ProductPricingItemView>(FindObjectsSortMode.None)) Expect(Mathf.Approximately(item.SelectedPrice,item.Product.ClampPrice(item.Product.price)),"Old menu leaked");
            Field<UnityEngine.UI.Button>(UnityEngine.Object.FindFirstObjectByType<MenuPricingScreenView>(),"backButton").onClick.Invoke();
        });
        Add("Prepare hover",()=>{Click(Button("cardPodrao")); ExecuteEvents.Execute(Button("cardJapones").gameObject,Pointer(Button("cardJapones")),ExecuteEvents.pointerEnterHandler);});
        Add("Hover state uses shared appearance",()=>{Expect(Button("cardJapones").GetComponent<SharedControlAppearance>().CurrentState=="hover" && Mathf.Approximately(Button("cardJapones").transform.Find("InteractionTint").GetComponent<UnityEngine.UI.Image>().color.a,.32f),"Hover"); Capture("hover-1920x1080"); ExecuteEvents.Execute(Button("cardJapones").gameObject,Pointer(Button("cardJapones")),ExecuteEvents.pointerDownHandler);});
        Add("Pressed state uses shared appearance",()=>{Expect(Button("cardJapones").GetComponent<SharedControlAppearance>().CurrentState=="pressed" && Mathf.Approximately(Button("cardJapones").transform.Find("InteractionTint").GetComponent<UnityEngine.UI.Image>().color.a,.65f),"Pressed"); Capture("pressed-1920x1080"); ExecuteEvents.Execute(Button("cardJapones").gameObject,Pointer(Button("cardJapones")),ExecuteEvents.pointerUpHandler); ExecuteEvents.Execute(Button("cardJapones").gameObject,Pointer(Button("cardJapones")),ExecuteEvents.pointerExitHandler); Button("cardJapones").Select();});
        Add("Keyboard focus is distinct from chosen restaurant",()=>{Expect(Button("cardJapones").GetComponent<SharedControlAppearance>().CurrentState=="focus" && Button("cardJapones").transform.Find("InteractionTint").GetComponent<UnityEngine.UI.Image>().color.a==0,"Focus"); Expect(Button("cardPodrao").GetComponent<SharedControlAppearance>().IsSelected,"Focus changed selection"); Capture("focus-1920x1080"); InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState(Key.Enter));});
        Add("Enter selects focused restaurant",()=>{InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState()); Expect(Button("cardJapones").GetComponent<SharedControlAppearance>().IsSelected,"Enter selection"); Button("btnHigh").Select();});
        Add("Arrow traversal skips unavailable public",()=>{ExecuteEvents.Execute(Button("btnHigh").gameObject,new AxisEventData(EventSystem.current){moveDir=MoveDirection.Right,moveVector=Vector2.right},ExecuteEvents.moveHandler); Expect(EventSystem.current.currentSelectedGameObject==Button("backButton").gameObject,"Navigation unavailable segment"); InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState(Key.Tab));});
        Add("Tab reaches confirm action",()=>{InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState()); Expect(EventSystem.current.currentSelectedGameObject==Button("confirmButton").gameObject,"Tab"); EventSystem.current.SetSelectedGameObject(null);});
        foreach(var sizeValue in new[]{new Vector2Int(1920,1080),new Vector2Int(1280,720)})
        {
            var size=sizeValue; string suffix=size.x+"x"+size.y;
            Add("Set long copy viewport "+suffix,()=>UI01Validation.SetGameView(size.x,size.y));
            Add("Prepare isolated long native copy "+suffix,()=>{
                View.SetRestaurantCardData(RestaurantType.PODRAO,"Restaurante de lanches com nome longo para aferir leitura",string.Join(" ",Enumerable.Repeat("Descrição extensa da proposta com atendimento e experiência do público.",12)));
                View.UpdateCoherence(.6f,string.Join(" ",Enumerable.Repeat("Texto longo de compatibilidade para aferir margens e rolagem.",12)),.8f,"Público compatível.");
                foreach(var scroll in View.GetComponentsInChildren<UnityEngine.UI.ScrollRect>()) scroll.verticalNormalizedPosition=1;
            });
            Add("Long copy grows within masks, fixed facade and actions "+suffix,()=>{
                Fits(); var card=Button("cardPodrao").transform.Find("CardDetails").GetComponent<UnityEngine.UI.ScrollRect>();
                Expect(card.content.rect.height>card.viewport.rect.height,"Long card did not grow"); Capture("long-copy-top-"+suffix);
                foreach(var scroll in View.GetComponentsInChildren<UnityEngine.UI.ScrollRect>()) scroll.verticalNormalizedPosition=0;
            });
            Add("Bottom scroll reveals native costs and last compatibility message "+suffix,()=>{
                Fits(); Capture("long-copy-bottom-"+suffix); var scroll=Button("cardPodrao").transform.Find("CardDetails").GetComponent<UnityEngine.UI.ScrollRect>();
                var before=scroll.verticalNormalizedPosition; ExecuteEvents.Execute(scroll.gameObject,new PointerEventData(EventSystem.current){scrollDelta=Vector2.up},ExecuteEvents.scrollHandler); Expect(scroll.verticalNormalizedPosition>before,"Wheel did not scroll");
                Call(Controller,"PopulateView"); Call(Controller,"ResetFromSession"); Call(Controller,"UpdateCoherencePanel");
            });
        }
        Add("Restore normal viewport",()=>UI01Validation.SetGameView(1920,1080));
        Add("Prepare catalog unavailable fixture",()=>{
            Field<Dictionary<RestaurantType,RestaurantData>>(Controller,"_restaurantsByType").Clear(); Call(Controller,"PopulateView"); Call(Controller,"ResetFromSession"); Call(Controller,"UpdateCoherencePanel");
        });
        Add("Unavailable catalog disables all selections and confirmation",()=>{
            Expect(!Button("confirmButton").interactable && CardFields.All(f=>!Button(f).interactable) && SegmentFields.All(f=>!Button(f).interactable),"Unavailable controls");
            Button("confirmButton").onClick.Invoke(); Expect(State==InitialDecisionFlow.Restaurant,"Empty confirm advanced");
            Call(Controller,"OnRestaurantSelected",RestaurantType.FRANCES); Expect(!Button("confirmButton").interactable,"Unavailable callback bypass"); Fits(); Capture("unavailable-1920x1080");
            Call(Controller,"LoadRestaurants"); Call(Controller,"PopulateView"); Call(Controller,"ResetFromSession"); Call(Controller,"UpdateCoherencePanel");
        });
        Add("Final real D2 selection before commit boundary",()=>{Click(Button("cardFrances")); Click(Button("btnHigh")); Click(Button("confirmButton"));});
        Add("D3 confirmation is the first SQLite session write",()=>{
            MenuMatches(RestaurantType.FRANCES); Field<UnityEngine.UI.Button>(UnityEngine.Object.FindFirstObjectByType<MenuPricingScreenView>(),"confirmButton").onClick.Invoke();
        });
        Add("One persisted session and preserved registration, IDs and draft",()=>{
            var rows=DatabaseInitializer.DatabaseService.Connection.Table<GameSessionEntity>().ToList(); Expect(rows.Count==1,"D3 must write exactly one session");
            Expect(rows[0].studentName=="Aluna Teste UI06" && rows[0].studentRA=="123456" && rows[0].restaurantType==RestaurantType.FRANCES && rows[0].targetSegment==Segment.HIGH && rows[0].locationZone==LocationZone.Educacao,"Persisted identification/decisions");
            Expect(State==InitialDecisionFlow.AfterCommit,"D3 progression"); Expect(PlayerSession.StudentName=="Aluna Teste UI06" && PlayerSession.SelectedRestaurantType==RestaurantType.FRANCES && PlayerSession.SelectedTargetSegment==Segment.HIGH,"Draft/cadastro");
            File.WriteAllText(UiValidationContext.FileFor("UI-06","runtime-scene.json"),Newtonsoft.Json.JsonConvert.SerializeObject(new {project=UnityEngine.Application.dataPath,scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene().path,frame=Time.frameCount,state=State.ToString(),database=DatabaseInitializer.DatabaseService.Connection.DatabasePath,sessionRows=rows.Count,selectedRestaurant=PlayerSession.SelectedRestaurantType.ToString(),selectedSegment=PlayerSession.SelectedTargetSegment.ToString()},Newtonsoft.Json.Formatting.Indented));
        });
        next=EditorApplication.timeSinceStartup+.4; EditorApplication.update-=Tick; EditorApplication.update+=Tick;
    }
    private static void Tick()
    {
        if(EditorApplication.timeSinceStartup<next) return;
        if(!EditorApplication.isPlaying || steps.Count==0)
        {
            report.completed=steps.Count==0 && report.failures==0; EditorApplication.update-=Tick;
            File.WriteAllText(UiValidationContext.FileFor("UI-06","playmode-restaurant.json"),JsonUtility.ToJson(report,true)); return;
        }
        var step=steps.Dequeue(); var check=new Check{name=step.name,frame=Time.frameCount};
        try { step.run(); check.passed=true; } catch(Exception e) {check.detail=e.ToString(); report.failures++; steps.Clear(); Debug.LogError("[UI-06 QA] "+step.name+": "+e.Message);}
        report.checks.Add(check); EditorApplication.QueuePlayerLoopUpdate();
        var window=EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")); window.Repaint();
        next=EditorApplication.timeSinceStartup+.4;
    }
}
