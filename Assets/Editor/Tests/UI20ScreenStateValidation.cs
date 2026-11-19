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
using Button=UnityEngine.UI.Button;

/// <summary>Staged control-state QA on production panels backed by a real saved campaign.</summary>
public static class UI20ScreenStateValidation
{
    static readonly Queue<(string,Action)> steps=new();
    static readonly List<object> checks=new(),captures=new();
    static Button target;
    static bool interactable;
    static int frame;
    static double next;
    static string res, panel, snapshot, activeSessionId;
    static Transform Root=>GameObject.Find("Canvas").transform;
    static SQLite4Unity3d.SQLiteConnection Db=>DatabaseInitializer.DatabaseService.Connection;
    static T Field<T>(object o,string n)=>(T)o.GetType().GetField(n,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(o);
    static void Add(string n,Action a)=>steps.Enqueue((n,a));
    static void Require(bool b,string n){if(!b)throw new InvalidOperationException(n);}
    static PointerEventData Pointer(Button b)=>new(EventSystem.current){pointerId=-1,button=PointerEventData.InputButton.Left,position=RectTransformUtility.WorldToScreenPoint(null,((RectTransform)b.transform).TransformPoint(((RectTransform)b.transform).rect.center))};
    static bool Reachable(Button b){var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(Pointer(b),hits);return hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==b;}
    static void Click(Button b){Require(b.IsActive()&&b.IsInteractable()&&Reachable(b),"Blocked native setup control "+b.name);var p=Pointer(b);ExecuteEvents.Execute(b.gameObject,p,ExecuteEvents.pointerDownHandler);ExecuteEvents.Execute(b.gameObject,p,ExecuteEvents.pointerUpHandler);ExecuteEvents.Execute(b.gameObject,p,ExecuteEvents.pointerClickHandler);}
    static string PathOf(Transform t)=>t.parent==null?t.name:PathOf(t.parent)+"/"+t.name;
    static void Capture(string n)
    {
        var p=UI01Validation.Capture(UiValidationContext.CaptureName("UI-20",n+"-"+res));
        UiValidationContext.OnCapture?.Invoke(n,p);
        captures.Add(new{name=n,res,path=p,control=n.StartsWith("Panel_")&&target!=null?PathOf(target.transform):null,state=n.StartsWith("Panel_")&&target!=null?target.GetComponent<SharedControlAppearance>().CurrentState:null,frame=Time.frameCount});
    }
    static string Saved()=>Newtonsoft.Json.JsonConvert.SerializeObject(new{sessions=Db.Table<GameSessionEntity>().ToList(),results=Db.Table<RoundResultEntity>().ToList(),events=Db.Table<SessionEventHistoryEntity>().ToList(),plans=Db.Table<MonthlyEventPlanEntity>().ToList()});
    static void LoadRealCampaign(bool bankrupt=false,bool active=false)
    {
        SharedModalFocus.Active?.Cancel();FeedbackToast.Current.Hide();MenuNavigator.Instance.CloseAll();
        var s=active?new GameSessionRepository(Db).GetById(activeSessionId):Db.Table<GameSessionEntity>().ToList().Last(x=>x.userId.StartsWith("ui19-")&&x.status==(bankrupt?GameSessionStatus.BANKRUPT:GameSessionStatus.COMPLETED)&&new RoundResultRepository(Db).GetBySessionId(x.sessionId).Count==3);
        typeof(GameSessionState).GetProperty("Current").SetValue(null,s);
        typeof(GameSessionState).GetProperty("IsPersisted").SetValue(null,true);
        typeof(GameSessionState).GetProperty("HasUnsavedChanges").SetValue(null,false);
        GameManager.Instance.StateMachine.ForceState(GameState.Management_Hub);
    }
    public static void Run()
    {
        UI01Validation.RequireIsolatedStorage();Directory.CreateDirectory(UiValidationContext.OutputRoot);steps.Clear();checks.Clear();captures.Clear();
        // Initial controls need an active campaign: a completed campaign correctly locks hiring.
        // Create this record through the actual registration and D1-D6 controls, with native catalog data.
        Add("Native control fixture viewport",()=>UI01Validation.SetGameView(1920,1080));
        Add("Native fresh registration",()=>{SharedModalFocus.Active?.Cancel();MenuNavigator.Instance.CloseAll();GameSessionState.Clear();PlayerSession.Clear();new GameSessionService("ui20-controls-"+Guid.NewGuid(),"qa").CreateNewSession();GameManager.Instance.BeginNewIdentification();});
        Add("Native registration",()=>{var f=UnityEngine.Object.FindFirstObjectByType<UIIndentification>();Field<TMP_InputField>(f,"studentNameInput").text="Participante da verificação de controles";Field<TMP_InputField>(f,"raInput").text="202020";Click(Field<Button>(f,"buttonRegister"));});
        Add("Native location",()=>{var v=Root.Find("Panel_Location").GetComponent<LocationScreenView>();ExecuteEvents.Execute(v.Zones.Single(z=>z.id=="bank").marker.gameObject,new PointerEventData(EventSystem.current){pointerId=4},ExecuteEvents.pointerClickHandler);Click(Field<Button>(v,"confirmButton"));});
        Add("Native restaurant",()=>Click(Field<Button>(Root.Find("Panel_Restaurant").GetComponent<RestaurantScreenView>(),"cardPodrao")));
        Add("Native restaurant confirmation",()=>Click(Field<Button>(Root.Find("Panel_Restaurant").GetComponent<RestaurantScreenView>(),"confirmButton")));
        Add("Native D3 first persistence",()=>Click(Field<Button>(Root.Find("Panel_MenuPricing").GetComponent<MenuPricingScreenView>(),"confirmButton")));
        Add("Native no-loan choice",()=>Click(Field<Button>(UnityEngine.Object.FindObjectsByType<BankCardView>(FindObjectsSortMode.None).Single(c=>c.CreditLine.id=="sem_credito"),"selectButton")));
        Add("Native bank confirmation",()=>Click(Field<Button>(Root.Find("Panel_Financial").GetComponent<FinancialScreenView>(),"confirmButton")));
        Add("Native equipment confirmation",()=>Click(Field<Button>(Root.Find("Panel_Equipment").GetComponent<EquipmentScreenView>(),"confirmButton")));
        Add("Native team confirmation",()=>Click(Field<Button>(Root.Find("Panel_Team").GetComponent<TeamScreenView>(),"confirmButton")));
        Add("Native active fixture saved",()=>{activeSessionId=GameSessionState.Current.sessionId;Require(GameSessionState.Current.status==GameSessionStatus.IN_PROGRESS&&GameSessionState.IsPersisted,"Native active record");FeedbackToast.Current.Hide();snapshot=Saved();});
        foreach(var size in UiValidationContext.Resolutions)
        {
            var viewport=size;
            Add("Viewport",()=>{res=viewport.x+"x"+viewport.y;UI01Validation.SetGameView(viewport.x,viewport.y);});
            var targets=new[]{"Panel_Identification","Panel_Location","Panel_Restaurant","Panel_MenuPricing","Panel_Financial","Panel_Equipment","Panel_Team","Panel_Menu","Panel_Establishment","Panel_Review","Panel_Event","Panel_MonthlyResult","Panel_QuarterlyResult","Panel_FinalReport","Panel_Bankruptcy"};
            var requestedPanels=Environment.GetEnvironmentVariable("COGNA_CONCEPT_PANELS");
            if(!string.IsNullOrEmpty(requestedPanels))targets=targets.Where(t=>requestedPanels.Split(',').Contains(t)).ToArray();
            foreach(var name in targets)
            {
                var screen=name;
                Add("Open "+screen,()=>{
                    panel=screen;LoadRealCampaign(screen=="Panel_Bankruptcy",Array.IndexOf(targets,screen)<13);
                    if(screen=="Panel_Identification") {GameManager.Instance.StateMachine.ForceState(GameState.Config_Identification);var form=UnityEngine.Object.FindFirstObjectByType<UIIndentification>();Field<TMP_InputField>(form,"studentNameInput").text=GameSessionState.Current.studentName;Field<TMP_InputField>(form,"raInput").text=GameSessionState.Current.studentRA;}
                    else if(screen=="Panel_Location")GameManager.Instance.StateMachine.ForceState(GameState.Config_Location);
                    else if(screen=="Panel_Restaurant")GameManager.Instance.StateMachine.ForceState(GameState.Config_Restaurant);
                    else if(screen=="Panel_MenuPricing")GameManager.Instance.StateMachine.ForceState(GameState.Config_TargetSegment);
                    else if(screen=="Panel_Financial")GameManager.Instance.StateMachine.ForceState(GameState.Initial_Capital);
                    else if(screen=="Panel_Equipment")GameManager.Instance.StateMachine.ForceState(GameState.Initial_Equipment);
                    else if(screen=="Panel_Team")GameManager.Instance.StateMachine.ForceState(GameState.Initial_Team);
                    else if(screen=="Panel_FinalReport")GameManager.Instance.StateMachine.ForceState(GameState.FinalReport);
                    else if(screen=="Panel_Bankruptcy")GameManager.Instance.StateMachine.ForceState(GameState.GameOver_Bankruptcy);
                    else {var registry=Root.Find("UiNavigation").GetComponent<PanelRegistry>();MenuNavigator.Instance.Open(registry.Bindings.Single(x=>x.root.name==screen).id);}
                });
                Add("Normal setup "+screen,()=>{
                    Canvas.ForceUpdateCanvases();
                    target=Root.Find(screen).GetComponentsInChildren<Button>().First(b=>b.IsActive()&&b.IsInteractable()&&b.GetComponent<SharedControlAppearance>()!=null&&Reachable(b));
                    interactable=target.interactable;EventSystem.current.SetSelectedGameObject(null);target.GetComponent<SharedControlAppearance>().SetSelected(false);
                    ExecuteEvents.Execute(target.gameObject,Pointer(target),ExecuteEvents.pointerExitHandler);
                });
                foreach(var stateName in new[]{"normal","hover","pressed","selected","disabled","focus"})
                {
                    var state=stateName;
                    if(state!="normal")Add("Set "+screen+" "+state,()=>{
                        var p=Pointer(target);var appearance=target.GetComponent<SharedControlAppearance>();
                        if(state=="hover")ExecuteEvents.Execute(target.gameObject,p,ExecuteEvents.pointerEnterHandler);
                        if(state=="pressed")ExecuteEvents.Execute(target.gameObject,p,ExecuteEvents.pointerDownHandler);
                        if(state=="selected"){ExecuteEvents.Execute(target.gameObject,p,ExecuteEvents.pointerUpHandler);ExecuteEvents.Execute(target.gameObject,p,ExecuteEvents.pointerExitHandler);appearance.SetSelected(true);EventSystem.current.SetSelectedGameObject(null);}
                        if(state=="disabled"){appearance.SetSelected(false);target.interactable=false;}
                        if(state=="focus"){target.interactable=interactable;target.Select();}
                    });
                    Add("Capture "+screen+" "+state,()=>{Require(target.GetComponent<SharedControlAppearance>().CurrentState==state,"Wrong control state "+screen+" "+state+" actual "+target.GetComponent<SharedControlAppearance>().CurrentState);Capture(screen+"-"+state);});
                }
                Add("Release "+screen,()=>{target.interactable=interactable;EventSystem.current.SetSelectedGameObject(null);Require(snapshot==Saved(),"Presentation mutated stored data");});
            }
            Add("Open actual completed report",()=>{LoadRealCampaign();GameManager.Instance.StateMachine.ForceState(GameState.FinalReport);});
            for(int i=0;i<3;i++)
            {
                int tab=i;
                Add("Open final tab "+i,()=>Click(Root.Find("Panel_FinalReport").GetComponent<FinalReportView>().Tabs[tab]));
                Add("Final tab top "+i,()=>{var v=Root.Find("Panel_FinalReport").GetComponent<FinalReportView>();v.CurrentScroll.verticalNormalizedPosition=1;});
                Add("Final tab top capture "+i,()=>{Capture("final-tab-"+tab+"-top");Root.Find("Panel_FinalReport").GetComponent<FinalReportView>().CurrentScroll.verticalNormalizedPosition=0;});
                Add("Final tab bottom capture "+i,()=>{Capture("final-tab-"+tab+"-bottom");Require(snapshot==Saved(),"Tab query mutated stored data");});
            }
        }
        frame=-1;next=EditorApplication.timeSinceStartup+.3;EditorApplication.update+=Tick;
    }
    static void Tick()
    {
        if(!EditorApplication.isPlaying||Time.frameCount<=frame||EditorApplication.timeSinceStartup<next)return;
        if(UnityEngine.Object.FindObjectsByType<SharedLoading>(FindObjectsSortMode.None).Any(l=>l.IsBusy)){EditorApplication.QueuePlayerLoopUpdate();return;}
        if(steps.Count==0){Finish(true,null);return;}
        var step=steps.Dequeue();try{step.Item2();checks.Add(new{name=step.Item1,res,passed=true,frame=Time.frameCount});}
        catch(Exception e){checks.Add(new{name=step.Item1,res,passed=false,error=e.ToString(),frame=Time.frameCount});Finish(false,e.ToString());return;}
        frame=Time.frameCount;next=EditorApplication.timeSinceStartup+.12;EditorApplication.QueuePlayerLoopUpdate();
    }
    static void Finish(bool passed,string error){EditorApplication.update-=Tick;File.WriteAllText(UiValidationContext.OutputRoot+"/playmode.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{completed=passed,error,checks,captures},Newtonsoft.Json.Formatting.Indented));}
}
