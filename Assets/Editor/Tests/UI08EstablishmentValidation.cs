using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Game.Adapter.In.Controllers;
using Game.Adapter.In.UI;
using Game.Adapter.In.UI.Navigation;
using Game.Domain.Service;
using Game.Infrastructure.Session;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Runs only in opt-in isolated storage. Each capture is queued after the state has rendered.</summary>
public static class UI08EstablishmentValidation
{
    public static void VerifySavedScene()
    {
        Expect(UnityEngine.Application.dataPath=="D:/Cogna/jogos_empresa/Assets" && !EditorApplication.isPlaying,"Use the saved Cogna scene outside Play Mode");
        var scene=UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity");
        var canvas=scene.GetRootGameObjects().Single(g=>g.name=="Canvas").transform;
        var registry=canvas.Find("UiNavigation").GetComponent<PanelRegistry>();
        foreach(var pair in new[]{(PanelId.Establishment,"Panel_Establishment"),(PanelId.Review,"Panel_Review"),(PanelId.Location,"Panel_Location"),(PanelId.Restaurant,"Panel_Restaurant")})
            Expect(registry.Resolve(pair.Item1)==canvas.Find(pair.Item2).gameObject,"Saved registry "+pair.Item1);
        Expect(canvas.GetComponentsInChildren<EstablishmentSummaryView>(true).Length==1,"Reset owner");
        foreach(var content in canvas.GetComponentsInChildren<EstablishmentReviewContent>(true))
        {
            Expect(Field<SelectedEstablishmentView>(content,"selected")!=null,"Selected business reference");
            Expect(Field<EstablishmentReviewContent.DecisionVisual[]>(content,"decisions").Length==6,"Six saved decisions");
            foreach(var d in Field<EstablishmentReviewContent.DecisionVisual[]>(content,"decisions"))Expect(d.button!=null && d.check!=null && d.tick!=null && d.detail!=null && d.state!=null,"Saved decision references");
            var arts=Field<EstablishmentReviewContent.RoleArtwork[]>(content,"roleArtwork");Expect(arts.Length==5 && arts.All(a=>a.portrait!=null && a.environment!=null),"Five saved role artwork mappings");
        }
        Expect(canvas.GetComponentsInChildren<EstablishmentReviewContent>(true).Length==2,"Two saved compositions");
        int missing=scene.GetRootGameObjects().Sum(g=>g.GetComponentsInChildren<Component>(true).Count(c=>c==null));Expect(missing==0,"Missing scene components");
        var sprites=new List<object>();
        foreach(string file in Directory.GetFiles("Assets/Art/new","*.png",SearchOption.AllDirectories).OrderBy(p=>p))
        {
            string path=file.Replace('\\','/');
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(path);var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            Expect(importer.spriteImportMode==SpriteImportMode.Single && sprite!=null && sprite.rect.size==new Vector2(texture.width,texture.height),"Whole Single sprite "+path);
            sprites.Add(new{path=path.Replace('\\','/'),guid=AssetDatabase.AssetPathToGUID(path),width=texture.width,height=texture.height,mode=importer.spriteImportMode.ToString(),whole=true});
        }
        Expect(sprites.Count==277,"Sprite count");
        File.WriteAllText("output/ui-update/UI-08/saved-scene-validation.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{success=true,project=UnityEngine.Application.dataPath,unity=UnityEngine.Application.unityVersion,scene=scene.path,missingComponents=missing,compositions=2,resetOwners=1,wholeSingleSprites=sprites.Count,sprites},Newtonsoft.Json.Formatting.Indented));
    }
    [Serializable] private class Check {public string name,detail;public bool passed;public int frame;}
    [Serializable] private class Report {public bool completed;public int failures;public List<Check> checks=new();}
    private static Report report;private static readonly Queue<(string,Action)> steps=new();private static double next;
    private static string sessionId;private static GameObject origin;private static int sceneHandle,managerId,databaseId;
    private static string shortcutSession,shortcutDraft;
    private static Transform CanvasRoot=>GameObject.Find("Canvas").transform;
    private static Transform Panel(string n)=>CanvasRoot.Find(n);
    private static EstablishmentReviewContent Content=>Panel(MenuNavigator.Instance.Current==PanelId.Review?"Panel_Review":"Panel_Establishment").GetComponent<EstablishmentReviewContent>();
    private static T Field<T>(object o,string f)=>(T)o.GetType().GetField(f,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(o);
    private static void Expect(bool v,string m){if(!v)throw new InvalidOperationException(m);}
    private static void Add(string n,Action action)=>steps.Enqueue((n,action));
    private static void Capture(string n)=>UI01Validation.Capture("../UI-08/"+n);
    private static void Click(UnityEngine.UI.Button button)
    {
        Expect(button.IsActive() && button.interactable,"Unavailable control "+button.name);
        var r=(RectTransform)(button.transform.Find("MarkerTouchTarget")??button.transform);
        var p=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,r.TransformPoint(r.rect.center)),button=PointerEventData.InputButton.Left,pointerId=11};
        var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(p,hits);Expect(hits.Count>0 && hits[0].gameObject.GetComponentInParent<UnityEngine.UI.Button>()==button,"Blocked pointer "+button.name);
        ExecuteEvents.Execute(button.gameObject,p,ExecuteEvents.pointerDownHandler);ExecuteEvents.Execute(button.gameObject,p,ExecuteEvents.pointerUpHandler);ExecuteEvents.Execute(button.gameObject,p,ExecuteEvents.pointerClickHandler);
    }
    private static UnityEngine.UI.Button Back=>CanvasRoot.Find("HubHistory/Back").GetComponent<UnityEngine.UI.Button>();
    private static void Top(){foreach(var s in Content.GetComponentsInChildren<UnityEngine.UI.ScrollRect>())s.verticalNormalizedPosition=1;EventSystem.current.SetSelectedGameObject(null);}
    private static void Bottom(){foreach(var s in Content.GetComponentsInChildren<UnityEngine.UI.ScrollRect>())s.verticalNormalizedPosition=0;EventSystem.current.SetSelectedGameObject(null);}
    private static void Fits()
    {
        UnityEngine.Canvas.ForceUpdateCanvases();
        foreach(var t in Content.GetComponentsInChildren<TMP_Text>()) {t.ForceMeshUpdate();Expect(t.font.name=="InterTight-Variable" && !t.enableAutoSizing,"Typography "+t.name);Expect(!t.isTextOverflowing,"Overflow "+t.name+" "+t.text);}
        foreach(var scroll in Content.GetComponentsInChildren<UnityEngine.UI.ScrollRect>())
        {
            Expect(scroll.viewport.GetComponent<UnityEngine.UI.Mask>()!=null,"Missing mask");Expect(scroll.vertical && !scroll.horizontal,"Scroll direction");
            Expect(scroll.verticalScrollbar.handleRect.sizeDelta==Vector2.zero,"Scrollbar dimensions");
        }
        Expect(UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length==1,"EventSystem count");
        var footer=UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsSortMode.None).Where(b=>b.IsInteractable() && b.GetComponentInParent<UnityEngine.UI.ScrollRect>()==null).ToArray();
        for(int i=0;i<footer.Length;i++)for(int j=i+1;j<footer.Length;j++)
        {
            var a=RectTransformUtility.CalculateRelativeRectTransformBounds(CanvasRoot,footer[i].transform);var b=RectTransformUtility.CalculateRelativeRectTransformBounds(CanvasRoot,footer[j].transform);
            float x=Mathf.Min(a.max.x,b.max.x)-Mathf.Max(a.min.x,b.min.x),y=Mathf.Min(a.max.y,b.max.y)-Mathf.Max(a.min.y,b.min.y);
            Expect(x<1 || y<1,"Overlapping fixed controls "+footer[i].name+" and "+footer[j].name);
        }
    }
    private static void SingleSession()
    {
        Expect(DatabaseInitializer.DatabaseService.Connection.Table<GameSessionEntity>().Count()==1 && GameSessionState.IsPersisted,"Session count/persistence");
        Expect(GameSessionState.Current.sessionId==sessionId && GameManager.Instance.GetInstanceID()==managerId && DatabaseInitializer.DatabaseService.GetInstanceID()==databaseId,"Services or session replaced");
        Expect(UnityEngine.SceneManagement.SceneManager.GetActiveScene().handle==sceneHandle,"Scene changed");
    }
    private static void ApplyFixture(RestaurantType type,bool complete=true)
    {
        var f=UI08EstablishmentTests.Fixture(type);var s=GameSessionState.Current;
        s.companyName=f.companyName;s.locationZone=f.locationZone;s.restaurantType=f.restaurantType;s.targetSegment=f.targetSegment;s.menuPricingJson=f.menuPricingJson;s.currentCash=f.currentCash;s.reputationScore=f.reputationScore;s.creditLineId=f.creditLineId;s.loanBalance=0;s.teamJson=complete?f.teamJson:"{}";
        var e=new EquipmentSelectionData();EquipmentSelectionHelper.AddUniqueIds(e,Resources.LoadAll<EquipmentData>("Equipments").Take(2).Select(item=>item.id));s.equipmentJson=EquipmentSelectionHelper.ToJson(e);GameSessionState.Save();
        MenuNavigator.Instance.Open(PanelId.Establishment);Content.GetComponent<EstablishmentSummaryController>().Refresh();Top();
    }
    private static void VerifySnapshot()
    {
        var s=EstablishmentSummaryService.Build();Expect(s.HasSession && s.HasMonthlyEstimate,"Missing real forecast");
        Expect(Field<TMP_Text>(Content,"cash").text==EstablishmentSummaryService.Brl(GameSessionState.Current.currentCash),"Cash mismatch");
        Expect(Field<TMP_Text>(Content,"result").text==EstablishmentSummaryService.Brl(s.MonthlyResult),"Result mismatch");
        Expect(Field<TMP_Text>(Content,"result").color==(s.MonthlyResult>=0?Game.Adapter.In.UI.Theme.GamePalette.Money:Game.Adapter.In.UI.Theme.GamePalette.ErrorText),"Semantic result color");
        Expect(Field<TMP_Text>(Content,"menuDetails").text==s.MenuDetails && Field<TMP_Text>(Content,"equipmentDetails").text==s.EquipmentDetails && Field<TMP_Text>(Content,"teamDetails").text==s.TeamDetails,"Stale details");
        var selected=Field<SelectedEstablishmentView>(Content,"selected");var facade=Field<UnityEngine.UI.Image>(selected,"facade");Expect(facade.sprite==s.Facade && facade.preserveAspect && facade.color==Color.white,"Facade binding");
        Expect(Mathf.Approximately(Field<RectTransform>(Content,"scoreFill").anchorMax.x,s.ScoreProgress),"Score geometry");
        var members=Field<EstablishmentReviewContent.MemberVisual[]>(Content,"members");Expect(members.Count(m=>m.root.activeSelf)==s.Members.Length,"Team count/artwork");
        for(int i=0;i<s.Members.Length;i++)Expect(members[i].portrait.sprite!=null && members[i].environment.sprite!=null && members[i].label.text.Contains(s.Members[i].Quantity.ToString()),"Member layers/quantity");
        var decisions=Field<EstablishmentReviewContent.DecisionVisual[]>(Content,"decisions");for(int i=0;i<6;i++)Expect(decisions[i].detail.text==s.Decisions[i].Detail && decisions[i].state.text==(s.Decisions[i].Ready?"Pronto":"Pendente"),"Checklist data "+i);
        Fits();SingleSession();
    }
    public static void Run()
    {
        UI01Validation.RequireIsolatedStorage();Expect(EditorApplication.isPlaying,"Use Play Mode");Expect(GameManager.Instance.StateMachine.CurrentState==InitialDecisionFlow.Identification,"Start with a clean isolated registration");
        report=new Report();steps.Clear();sceneHandle=UnityEngine.SceneManagement.SceneManager.GetActiveScene().handle;managerId=GameManager.Instance.GetInstanceID();databaseId=DatabaseInitializer.DatabaseService.GetInstanceID();
        Add("Prepare empty summary without entering a new setup state",()=>{foreach(Transform t in CanvasRoot)if(t.name.StartsWith("Panel_"))t.gameObject.SetActive(t.name=="Panel_Establishment");PlayerSession.ClearDecisions();Panel("Panel_Establishment").GetComponent<EstablishmentSummaryController>().Refresh();});
        Add("Empty state is explicit, disabled and nonpersistent",()=>{Expect(!EstablishmentSummaryService.Build().HasSession,"Draft used a default session");Expect(Field<TMP_Text>(Content,"cash").text=="A confirmar","Fake cash");Expect(Field<EstablishmentReviewContent.DecisionVisual[]>(Content,"decisions").All(d=>!d.button.interactable),"Setup shortcut leaked hub navigation");Fits();Capture("empty-1920x1080");Expect(DatabaseInitializer.DatabaseService.Connection.Table<GameSessionEntity>().Count()==0,"Premature insert");});
        Add("Prepare complete D1-D3 draft",()=>{PlayerSession.LoadDecisionsFrom(UI08EstablishmentTests.Fixture(RestaurantType.PODRAO));Panel("Panel_Establishment").GetComponent<EstablishmentSummaryController>().Refresh();Top();});
        Add("Complete initial draft still has no invented money/team",()=>{var s=EstablishmentSummaryService.Build();Expect(s.IsDraft && s.Decisions.Take(3).All(d=>d.Ready) && !s.HasMonthlyEstimate,"Draft completeness");Expect(Field<TMP_Text>(Content,"teamEmpty").gameObject.activeSelf,"Fictional hires");Fits();Capture("draft-1920x1080");PlayerSession.ClearDecisions();Panel("Panel_Establishment").gameObject.SetActive(false);Panel("Panel_Identification").gameObject.SetActive(true);});
        Add("Register with the real identification form",()=>{var form=UnityEngine.Object.FindFirstObjectByType<UIIndentification>();Field<TMP_InputField>(form,"studentNameInput").text="Aluna Teste UI08";Field<TMP_InputField>(form,"raInput").text="123456";form.Register();});
        Add("Select actual location control",()=>{var v=UnityEngine.Object.FindFirstObjectByType<LocationScreenView>();var marker=v.Zones.Single(z=>z.id=="bank").marker;ExecuteEvents.Execute(marker.gameObject,new PointerEventData(EventSystem.current){pointerId=4},ExecuteEvents.pointerClickHandler);Click(Field<UnityEngine.UI.Button>(v,"confirmButton"));});
        Add("Select actual restaurant after its layout has rendered",()=>{var r=UnityEngine.Object.FindFirstObjectByType<RestaurantScreenView>();Click(Field<UnityEngine.UI.Button>(r,"cardPodrao"));Click(Field<UnityEngine.UI.Button>(r,"confirmButton"));});
        Add("Confirm D3 remains the first INSERT and goes straight to Banco",()=>{Expect(DatabaseInitializer.DatabaseService.Connection.Table<GameSessionEntity>().Count()==0,"Inserted early");Click(Field<UnityEngine.UI.Button>(UnityEngine.Object.FindFirstObjectByType<MenuPricingScreenView>(),"confirmButton"));Expect(GameManager.Instance.StateMachine.CurrentState==InitialDecisionFlow.AfterCommit,"Review became mandatory");sessionId=GameSessionState.Current.sessionId;SingleSession();GameManager.Instance.StateMachine.TryChangeState(GameState.Initial_Equipment);GameManager.Instance.StateMachine.TryChangeState(GameState.Initial_Team);GameManager.Instance.StateMachine.TryChangeState(GameState.Management_Hub);});
        Add("Render hub before clicking its establishment marker",()=>MenuNavigator.Instance.OpenRoot());
        Add("Open establishment through real hub marker",()=>{var node=UnityEngine.Object.FindObjectsByType<MapMenuNode>(FindObjectsSortMode.None).Single(n=>n.Destination==PanelId.Establishment && string.IsNullOrEmpty(n.LocationId));origin=node.gameObject;node.GetComponent<UnityEngine.UI.Button>().Select();Click(node.GetComponent<UnityEngine.UI.Button>());});
        foreach(var sizeValue in new[]{new Vector2Int(1920,1080),new Vector2Int(1366,768),new Vector2Int(1280,720)})
        {
            var size=sizeValue;string suffix=size.x+"x"+size.y;Add("Set viewport "+suffix,()=>UI01Validation.SetGameView(size.x,size.y));
            foreach(var typeValue in new[]{RestaurantType.PODRAO,RestaurantType.FRANCES,RestaurantType.JAPONES})
            {
                var type=typeValue;string name=type.ToString().ToLowerInvariant()+"-"+suffix;
                Add("Prepare real "+name,()=>ApplyFixture(type));
                Add("Rendered independent facade, team, indicators "+name,()=>{Expect(Screen.width==size.x && Screen.height==size.y,"Evidence resolution");VerifySnapshot();Expect(EstablishmentSummaryService.Build().IsReady,"Complete checklist");Capture(name+"-top");Bottom();});
                Add("Rendered complete choices, prices and scrolling "+name,()=>{VerifySnapshot();Capture(name+"-details");var scroll=Content.transform.Find("MetricsReviewPanel/ReviewScroll").GetComponent<UnityEngine.UI.ScrollRect>();float before=scroll.verticalNormalizedPosition;ExecuteEvents.Execute(scroll.gameObject,new PointerEventData(EventSystem.current){scrollDelta=Vector2.up},ExecuteEvents.scrollHandler);Expect(scroll.verticalNormalizedPosition>before,"Wheel scroll");});
            }
        }
        Add("Prepare team pending revisited review",()=>{ApplyFixture(RestaurantType.PODRAO,false);MenuNavigator.Instance.Open(PanelId.Review);Top();});
        Add("Pending review has honest team and projection status",()=>{VerifySnapshot();Expect(!EstablishmentSummaryService.Build().IsReady,"Missing team marked ready");Expect(Field<TMP_Text>(Content,"teamEmpty").gameObject.activeSelf,"Missing team placeholder");Capture("review-pending-1280x720");Click(Back);});
        Add("Revisited establishment refreshes after changed location",()=>{GameSessionState.SetLocation(LocationZone.Educacao);GameSessionState.Save();MenuNavigator.Instance.OpenRoot();MenuNavigator.Instance.Open(PanelId.Establishment);Expect(Field<EstablishmentReviewContent.DecisionVisual[]>(Content,"decisions")[0].detail.text==EstablishmentSummaryService.Build().LocationLabel,"Location stale");});
        Add("Revisited establishment refreshes after changed price",()=>{var menu=MenuPricingHelper.FromJson(GameSessionState.Current.menuPricingJson);menu.items[0].selectedPrice+=.5f;GameSessionState.SetMenuPricingJson(MenuPricingHelper.ToJson(menu),true);MenuNavigator.Instance.OpenRoot();MenuNavigator.Instance.Open(PanelId.Establishment);Expect(Field<TMP_Text>(Content,"menuDetails").text.Contains(EstablishmentSummaryService.Brl(menu.items[0].selectedPrice)),"Price stale");});
        Add("Revisited establishment refreshes after credit and cash change",()=>{var line=Resources.LoadAll<CreditLineData>("CreditLines").First(c=>c.maxAmount>0);GameSessionState.SetLoan(line.id,line.maxAmount,false);GameSessionState.SetCash(-1234.5f);MenuNavigator.Instance.OpenRoot();MenuNavigator.Instance.Open(PanelId.Establishment);Expect(Field<EstablishmentReviewContent.DecisionVisual[]>(Content,"decisions")[3].detail.text.Contains(line.displayName),"Credit stale");Top();});
        Add("Negative values use accessible semantic colors",()=>{VerifySnapshot();Expect(Field<TMP_Text>(Content,"cash").color==Game.Adapter.In.UI.Theme.GamePalette.ErrorText,"Negative cash color");Expect(EstablishmentSummaryService.Build().MonthlyResult<0 && Field<TMP_Text>(Content,"result").color==Game.Adapter.In.UI.Theme.GamePalette.ErrorText,"Negative result color");Capture("negative-1280x720");});
        Add("Revisited review refreshes after equipment and team changes",()=>{var f=UI08EstablishmentTests.Fixture(RestaurantType.PODRAO);GameSessionState.SetTeamJson(f.teamJson,false);var data=new EquipmentSelectionData();EquipmentSelectionHelper.AddUniqueIds(data,Resources.LoadAll<EquipmentData>("Equipments").Select(e=>e.id));GameSessionState.SetEquipmentJson(EquipmentSelectionHelper.ToJson(data));MenuNavigator.Instance.Open(PanelId.Review);Expect(Field<TMP_Text>(Content,"equipmentDetails").text==EstablishmentSummaryService.Build().EquipmentDetails,"Equipment stale");VerifySnapshot();Top();});
        Add("Complete revisited review is informational",()=>{Fits();SingleSession();Capture("review-complete-1280x720");Click(Back);});
        var destinations=new[]{PanelId.Location,PanelId.Restaurant,PanelId.MenuPricing,PanelId.Financial,PanelId.EquipmentStore,PanelId.Team};
        for(int i=0;i<destinations.Length;i++)
        {
            int index=i;var destination=destinations[i];
            Add("Focus scrolls to shortcut "+destination,()=>{shortcutSession=Newtonsoft.Json.JsonConvert.SerializeObject(GameSessionState.Current);shortcutDraft=MenuPricingHelper.ToJson(PlayerSession.GetMenu());Field<EstablishmentReviewContent.DecisionVisual[]>(Content,"decisions")[index].button.Select();});
            Add("Shortcut opens "+destination+" in management context",()=>{Click(Field<EstablishmentReviewContent.DecisionVisual[]>(Content,"decisions")[index].button);Expect(MenuNavigator.Instance.Current==destination && GameManager.Instance.StateMachine.CurrentState==GameState.Management_Hub,"Shortcut context");});
            Add("Shortcut "+destination+" returns through one history action",()=>{
                if(destination==PanelId.MenuPricing){var menu=UnityEngine.Object.FindFirstObjectByType<MenuPricingScreenView>();Expect(!Field<UnityEngine.UI.Button>(menu,"backButton").gameObject.activeSelf && !Field<UnityEngine.UI.Button>(menu,"confirmButton").gameObject.activeSelf,"Menu review changed");}
                if(destination==PanelId.Location){var location=UnityEngine.Object.FindFirstObjectByType<LocationScreenView>();Expect(location.SelectedId==LocationZoneMap.ToId(GameSessionState.Current.locationZone),"Location used stale draft");Expect(!Field<UnityEngine.UI.Button>(location,"backButton").gameObject.activeSelf && !Field<UnityEngine.UI.Button>(location,"confirmButton").gameObject.activeSelf,"Location review actions");Field<UnityEngine.UI.Button>(location,"confirmButton").onClick.Invoke();Capture("shortcut-location-review-1280x720");}
                if(destination==PanelId.Restaurant){var restaurant=UnityEngine.Object.FindFirstObjectByType<RestaurantScreenView>();Expect(Field<RestaurantType?>(UnityEngine.Object.FindFirstObjectByType<RestaurantScreenController>(),"_selectedRestaurant")==GameSessionState.Current.restaurantType,"Restaurant used stale draft");Expect(!Field<UnityEngine.UI.Button>(restaurant,"backButton").gameObject.activeSelf && !Field<UnityEngine.UI.Button>(restaurant,"confirmButton").gameObject.activeSelf,"Restaurant review actions");Field<UnityEngine.UI.Button>(restaurant,"confirmButton").onClick.Invoke();Capture("shortcut-restaurant-review-1280x720");}
                Click(Back);
            });
            Add("History restores review focus after "+destination,()=>{Expect(MenuNavigator.Instance.Current==PanelId.Establishment,"History panel");Expect(EventSystem.current.currentSelectedGameObject==Field<EstablishmentReviewContent.DecisionVisual[]>(Content,"decisions")[index].button.gameObject,"Shortcut focus");Expect(shortcutSession==Newtonsoft.Json.JsonConvert.SerializeObject(GameSessionState.Current) && shortcutDraft==MenuPricingHelper.ToJson(PlayerSession.GetMenu()),"Shortcut mutated confirmed data or draft");SingleSession();});
        }
        Add("Prepare hover",()=>{var b=Field<EstablishmentReviewContent.DecisionVisual[]>(Content,"decisions")[0].button;b.Select();EventSystem.current.SetSelectedGameObject(null);ExecuteEvents.Execute(b.gameObject,new PointerEventData(EventSystem.current),ExecuteEvents.pointerEnterHandler);});
        Add("Rendered hover",()=>{var b=Field<EstablishmentReviewContent.DecisionVisual[]>(Content,"decisions")[0].button;Expect(b.GetComponent<SharedControlAppearance>().CurrentState=="hover","Hover");Capture("shortcut-hover-1280x720");ExecuteEvents.Execute(b.gameObject,new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left},ExecuteEvents.pointerDownHandler);});
        Add("Rendered pressed",()=>{var b=Field<EstablishmentReviewContent.DecisionVisual[]>(Content,"decisions")[0].button;Expect(b.GetComponent<SharedControlAppearance>().CurrentState=="pressed","Pressed");Capture("shortcut-pressed-1280x720");ExecuteEvents.Execute(b.gameObject,new PointerEventData(EventSystem.current),ExecuteEvents.pointerUpHandler);ExecuteEvents.Execute(b.gameObject,new PointerEventData(EventSystem.current),ExecuteEvents.pointerExitHandler);Field<EstablishmentReviewContent.DecisionVisual[]>(Content,"decisions")[5].button.Select();});
        Add("Keyboard focus reveals the last masked shortcut",()=>{Fits();Capture("shortcut-focus-1280x720");var b=Field<EstablishmentReviewContent.DecisionVisual[]>(Content,"decisions")[5].button;ExecuteEvents.Execute(b.gameObject,new BaseEventData(EventSystem.current),ExecuteEvents.submitHandler);});
        Add("Submit opens the focused team shortcut and keeps history",()=>{Expect(MenuNavigator.Instance.Current==PanelId.Team,"Submit");Click(Back);});
        Add("Prepare long name and all five real roles",()=>{ApplyFixture(RestaurantType.PODRAO);var team=new TeamSelectionData();foreach(var r in Resources.LoadAll<RoleData>("Roles"))TeamSelectionHelper.SetMemberQuantity(team,r.id,2);GameSessionState.SetTeamJson(TeamSelectionHelper.ToJson(team),false);GameSessionState.Current.companyName=string.Join(" ",Enumerable.Repeat("Restaurante com nome extenso para conferir leitura",5));GameSessionState.SetCash(1234567.5f);MenuNavigator.Instance.Open(PanelId.Review);Top();});
        Add("Long copy expands without overlapping facade or footer",()=>{VerifySnapshot();Capture("long-copy-top-1280x720");Bottom();});
        Add("All real hires and long detail lists remain reachable",()=>{VerifySnapshot();Capture("long-copy-bottom-1280x720");});
        Add("Prepare unavailable catalog and prices snapshot",()=>{var s=GameSessionState.Current;s.restaurantType=(RestaurantType)999;s.menuPricingJson="{}";s.teamJson="{}";s.equipmentJson="{}";GameSessionState.Save();MenuNavigator.Instance.Open(PanelId.Establishment);Top();});
        Add("Unavailable facade, forecast and checklist clear stale values",()=>{var s=EstablishmentSummaryService.Build();Expect(!s.HasMonthlyEstimate && s.Facade==null,"Unavailable data reused");Expect(Field<TMP_Text>(Content,"result").text=="Indisponível","Stale result");Expect(!Field<UnityEngine.UI.Image>(Field<SelectedEstablishmentView>(Content,"selected"),"facade").enabled,"Stale facade");Fits();Capture("unavailable-1280x720");});
        Add("Restore normal snapshot before reset dialog check",()=>{MenuNavigator.Instance.OpenRoot();ApplyFixture(RestaurantType.PODRAO);Top();var view=Content.GetComponent<EstablishmentSummaryView>();Click(Field<UnityEngine.UI.Button>(view,"resetButton"));});
        Add("Reset warning is modal and cancel preserves the isolated session",()=>{var view=Content.GetComponent<EstablishmentSummaryView>();var warning=Field<GameObject>(view,"resetWarning");Expect(warning.activeInHierarchy,"Reset modal");Expect(EventSystem.current.currentSelectedGameObject.transform.IsChildOf(warning.transform),"Modal focus");var r=(RectTransform)Back.transform;var p=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,r.TransformPoint(r.rect.center))};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(p,hits);Expect(hits.Count>0 && hits[0].gameObject.transform.IsChildOf(warning.transform),"Modal did not block underlying history");Capture("reset-warning-1280x720");Click(Field<UnityEngine.UI.Button>(view,"cancelResetButton"));SingleSession();});
        Add("Review button uses the same hub history",()=>{Click(Field<UnityEngine.UI.Button>(Content,"reviewButton"));Expect(MenuNavigator.Instance.Current==PanelId.Review,"Review action");});
        Add("Review remains revisitable without new persistence",()=>{VerifySnapshot();Click(Back);Click(Back);});
        Add("Hub origin focus and session preserved",()=>{Expect(MenuNavigator.Instance.Current==PanelId.Menu,"Final hub return");Expect(EventSystem.current.currentSelectedGameObject==origin,"Origin focus");SingleSession();File.WriteAllText("output/ui-update/UI-08/runtime.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{project=UnityEngine.Application.dataPath,scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene().path,frame=Time.frameCount,sessionId,sceneHandle,managerId,databaseId,database=DatabaseInitializer.DatabaseService.Connection.DatabasePath,sessionRows=1,state=GameManager.Instance.StateMachine.CurrentState.ToString(),panel=MenuNavigator.Instance.Current.ToString()},Newtonsoft.Json.Formatting.Indented));});
        next=EditorApplication.timeSinceStartup+.4;EditorApplication.update-=Tick;EditorApplication.update+=Tick;
    }
    private static void Tick()
    {
        if(EditorApplication.timeSinceStartup<next)return;
        if(!EditorApplication.isPlaying || steps.Count==0){report.completed=steps.Count==0 && report.failures==0;EditorApplication.update-=Tick;File.WriteAllText("output/ui-update/UI-08/playmode-establishment.json",JsonUtility.ToJson(report,true));return;}
        var step=steps.Dequeue();var check=new Check{name=step.Item1,frame=Time.frameCount};
        try{step.Item2();check.passed=true;}catch(Exception e){check.detail=e.ToString();report.failures++;steps.Clear();Debug.LogError("[UI-08 QA] "+check.name+": "+e.Message);}
        report.checks.Add(check);EditorApplication.QueuePlayerLoopUpdate();EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Repaint();next=EditorApplication.timeSinceStartup+.4;
    }
}
