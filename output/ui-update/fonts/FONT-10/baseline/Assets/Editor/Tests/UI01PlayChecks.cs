using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Game.Adapter.In.UI;
using Game.Infrastructure.Session;
using TMPro;
using UnityEditor;
using UnityEngine;

public static class UI01PlayChecks
{
    [Serializable] private class Check { public string name; public bool passed; public string detail; public int frame; }
    [Serializable] private class Report { public List<Check> checks=new(); public bool completed; public int failures; }
    private static Report report;
    private static readonly Queue<(string name,Action action)> steps=new();
    private static double next;
    private static int managerId, databaseId, sceneHandle;
    private const BindingFlags Fields=BindingFlags.Instance|BindingFlags.NonPublic;
    private static UIIndentification Form => UnityEngine.Object.FindFirstObjectByType<UIIndentification>();
    private static T Field<T>(object target,string name) => (T)target.GetType().GetField(name,Fields).GetValue(target);
    private static TMP_InputField Name => Field<TMP_InputField>(Form,"studentNameInput");
    private static TMP_InputField RA => Field<TMP_InputField>(Form,"raInput");
    private static TMP_Text Error => Field<TMP_Text>(Form,"validationText");
    private static UnityEngine.UI.Button Register => Field<UnityEngine.UI.Button>(Form,"buttonRegister");
    private static GameState State => GameManager.Instance.StateMachine.CurrentState;
    private static void Expect(bool condition,string detail) { if(!condition) throw new Exception(detail); }

    [MenuItem("Tools/Jogos de Empresa/UI-01/Validate new registration in Play Mode")]
    public static void Run()
    {
        UI01Validation.RequireIsolatedStorage();
        if (!EditorApplication.isPlaying) throw new InvalidOperationException("Start Play Mode with isolated storage.");
        report=new Report(); steps.Clear();
        managerId=GameManager.Instance.GetInstanceID(); databaseId=DatabaseInitializer.DatabaseService.GetInstanceID();
        sceneHandle=UnityEngine.SceneManagement.SceneManager.GetActiveScene().handle;
        Add("Direct GameScene entry has one service instance and waits at step 1",()=> {
            Expect(State==InitialDecisionFlow.Identification,"Expected identification");
            Expect(UnityEngine.Object.FindObjectsByType<GameManager>(FindObjectsSortMode.None).Length==1,"Duplicate manager");
            Expect(UnityEngine.Object.FindObjectsByType<DatabaseInitializer>(FindObjectsSortMode.None).Length==1,"Duplicate database");
            Expect(UnityEngine.Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsSortMode.None).Length==1,"Duplicate event system");
            Expect(!Register.interactable && Name.text=="" && RA.text=="","Empty form state");
            Expect(DatabaseInitializer.DatabaseService.Connection.Table<GameSessionEntity>().Count()==0,"Premature database insert");
            Expect(Screen.width==1920 && Screen.height==1080,"Expected a real 1920x1080 viewport");
            UI01Validation.Capture("identification-empty-1920x1080");
        });
        Add("Submit an invalid name using the real button",()=> { Name.text="Ana"; RA.text="12345"; Register.onClick.Invoke(); });
        Add("Invalid name shows message, coral input and field focus",()=> {
            Expect(Error.text.Contains("pelo menos 5"),Error.text);
            Expect(Name.isFocused && Name.GetComponent<IdentificationInputAppearance>().IsInvalid,"Name focus/error state");
            Expect(State==InitialDecisionFlow.Identification,"Invalid form advanced");
            UI01Validation.Capture("identification-name-error-1920x1080");
        });
        Add("Submit an invalid RA using the real button",()=> { Name.text="Aluna Teste"; RA.text="1234"; Register.onClick.Invoke(); });
        Add("Invalid RA shows message and field focus",()=> {
            Expect(Error.text.Contains("O RA") && RA.isFocused,"RA validation/focus");
            UI01Validation.Capture("identification-ra-error-1920x1080");
            Name.text=new string('A',60); RA.text="123456789012345";
            Register.onClick.Invoke();
            Expect(Error.text.Contains("no máximo 50"),"Oversized name accepted");
        });
        Add("Maximum validation and native character limits are preserved",()=> {
            Name.text=new string('A',50); Register.onClick.Invoke();
            Expect(Error.text.Contains("no máximo 10"),"Oversized RA accepted");
            Expect(Name.characterLimit==50 && RA.characterLimit==10,"Native input character limits");
            RA.text="1234567890";
            UI01Validation.SetGameView(1280,720);
        });
        Add("Long input and masks fit the real 1280x720 viewport",()=> {
            Expect(Screen.width==1280 && Screen.height==720,"Expected 1280x720");
            Expect(Register.interactable && Error.text=="","Valid maximum-length form");
            UI01Validation.Capture("identification-long-input-1280x720");
            Register.onClick.Invoke();
        });
        Add("Valid registration enters location without reloading or persisting decisions",()=> {
            Expect(State==InitialDecisionFlow.Location,"Expected location");
            Expect(PlayerSession.StudentName.Length==50 && PlayerSession.StudentRA.Length==10,"Registration limits");
            Expect(PlayerSession.RestaurantName==PlayerSession.StudentRA+" "+PlayerSession.StudentName,"Derived company name");
            Expect(DatabaseInitializer.DatabaseService.Connection.Table<GameSessionEntity>().Count()==0,"Early persistence");
            var location=UnityEngine.Object.FindFirstObjectByType<LocationScreenView>();
            Field<UnityEngine.UI.Button>(location,"bankButton").onClick.Invoke();
            Field<UnityEngine.UI.Button>(location,"backButton").onClick.Invoke();
        });
        Add("Location Back restores prefilled form and preserves its draft",()=> {
            Expect(State==InitialDecisionFlow.Identification && Name.text.Length==50,"Prefill missing");
            Expect(PlayerSession.SelectedZone==LocationZone.Financas,"Draft lost");
            Expect(GameManager.Instance.GetInstanceID()==managerId && DatabaseInitializer.DatabaseService.GetInstanceID()==databaseId
                && UnityEngine.SceneManagement.SceneManager.GetActiveScene().handle==sceneHandle,"Services or scene reloaded");
            UI01Validation.Capture("identification-prefilled-1280x720");
            Name.text="Aluna Teste"; RA.text="123456"; Register.onClick.Invoke();
        });
        Add("Repeated edit and confirmation remain usable",()=> {
            var location=UnityEngine.Object.FindFirstObjectByType<LocationScreenView>();
            Field<UnityEngine.UI.Button>(location,"backButton").onClick.Invoke();
            Expect(Name.text=="Aluna Teste" && RA.text=="123456","Edited fields not restored");
            Register.onClick.Invoke();
            Expect(State==InitialDecisionFlow.Location,"Register stuck on revisit");
            UI01Validation.SetGameView(1920,1080);
        });
        next=EditorApplication.timeSinceStartup+.4;
        EditorApplication.update-=Tick; EditorApplication.update+=Tick;
    }

    private static void Add(string name,Action action)=>steps.Enqueue((name,action));
    private static void Tick()
    {
        if(EditorApplication.timeSinceStartup<next) return;
        if(!EditorApplication.isPlaying || steps.Count==0)
        {
            report.completed=steps.Count==0 && report.failures==0; EditorApplication.update-=Tick;
            Directory.CreateDirectory("output/ui-update/UI-01");
            File.WriteAllText("output/ui-update/UI-01/playmode-registration.json",JsonUtility.ToJson(report,true));
            return;
        }
        var step=steps.Dequeue(); var check=new Check{name=step.name,frame=Time.frameCount};
        try { step.action(); check.passed=true; }
        catch(Exception e) { check.detail=e.Message; report.failures++; steps.Clear(); Debug.LogError("[UI-01 QA] "+step.name+": "+e.Message); }
        report.checks.Add(check); next=EditorApplication.timeSinceStartup+.5;
    }
}
