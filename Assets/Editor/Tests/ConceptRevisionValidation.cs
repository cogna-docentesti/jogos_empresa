using System;
using System.IO;
using System.Linq;
using Game.EditorTools;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;

/// <summary>Native campaign regression and real Game View evidence for the composition revision.</summary>
public static class ConceptRevisionValidation
{
    const string Running="Cogna.ConceptRevision.Running";
    static int stage, lastFrame;
    static double deadline;
    static readonly System.Collections.Generic.List<object> captures=new(), logs=new();
    static readonly Vector2Int[] sizes={new(1920,1080),new(1280,720),new(1600,1000)};
    static void Write(string n,object o)=>File.WriteAllText(ConceptUiRevision.Output+"/"+n+".json",JsonConvert.SerializeObject(o,Formatting.Indented));
    public static void RunAndExit()
    {
        if(string.IsNullOrEmpty(Environment.GetEnvironmentVariable("COGNA_TEST_DATABASE_PATH")))throw new InvalidOperationException("Isolated QA database required.");
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity");
        SessionState.SetBool(Running,true);EditorApplication.isPlaying=true;
    }
    [InitializeOnLoadMethod] static void Reload()
    {
        if(!SessionState.GetBool(Running,false))return;
        stage=-1;deadline=EditorApplication.timeSinceStartup+1200;
        EditorApplication.update-=Tick;EditorApplication.update+=Tick;
        Application.logMessageReceived+=Log;
    }
    static void Log(string msg,string stack,LogType type)
    {if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)logs.Add(new{type=type.ToString(),message=msg,stack});}
    static void Capture(string n,string path)
    {
        if(n=="Panel_Equipment-normal")
        {
            var cards=GameObject.Find("Canvas").transform.Find("Panel_Equipment").GetComponentsInChildren<Game.Adapter.In.UI.EquipmentCardView>();
            foreach(var card in cards)
            {
                var root=(RectTransform)card.transform;
                foreach(var child in new[]{"EquipmentPrice","Quantity","Add","Remove"})
                {
                    var target=(RectTransform)card.transform.Find(child);var corners=new Vector3[4];target.GetWorldCorners(corners);
                    if(corners.Any(c=>!root.rect.Contains((Vector2)root.InverseTransformPoint(c))))throw new InvalidOperationException("Clipped equipment control: "+child);
                }
            }
        }
        captures.Add(new{name=n,path,frame=Time.frameCount,advancing=Time.frameCount>lastFrame,width=Screen.width,height=Screen.height,
            texts=GameObject.Find("Canvas").GetComponentsInChildren<TMP_Text>().Where(t=>t.isActiveAndEnabled&&!string.IsNullOrEmpty(t.text)).Select(t=>new{t.name,t.text,t.isTextOverflowing,t.fontSize}).ToArray()});
        lastFrame=Time.frameCount;
    }
    static void Start(string name,Action action)
    {
        UiValidationContext.OutputRoot=ConceptUiRevision.Output+"/"+name;Directory.CreateDirectory(UiValidationContext.OutputRoot);
        UiValidationContext.Viewports=name=="campaigns"||name=="polish"?new[]{sizes[0]}:sizes;UiValidationContext.OnCapture=Capture;
        string p=UiValidationContext.OutputRoot+"/playmode.json";if(File.Exists(p))File.Delete(p);
        action();
    }
    static void Tick()
    {
        if(!EditorApplication.isPlaying)return;
        Application.runInBackground=true;EditorApplication.QueuePlayerLoopUpdate();
        EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Repaint();
        if(Time.frameCount<8)return;
        try
        {
            if(EditorApplication.timeSinceStartup>deadline)throw new TimeoutException("Concept QA timed out.");
            if(stage==-1){UI01Validation.RequireIsolatedStorage();var requested=Environment.GetEnvironmentVariable("COGNA_CONCEPT_SUITE");if(requested=="states"||requested=="polish"){stage=1;Start(requested,UI20ScreenStateValidation.Run);}else{stage=0;Start("campaigns",UI19NavigationValidation.Run);}return;}
            var p=UiValidationContext.OutputRoot+"/playmode.json";if(!File.Exists(p))return;
            var result=JObject.Parse(File.ReadAllText(p));if(!(bool)result["completed"])throw new InvalidOperationException("Native flow failed. See "+p);
            stage++;
            if(!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("COGNA_CONCEPT_SUITE"))){Finish(true,null);return;}
            if(stage==1)Start("states",UI20ScreenStateValidation.Run);
            else if(stage==2)Start("dialogs",UI18DialogValidation.Run);
            else Finish(true,null);
        }
        catch(Exception e){Finish(false,e.ToString());}
    }
    static void Finish(bool passed,string error)
    {
        SessionState.SetBool(Running,false);EditorApplication.update-=Tick;Application.logMessageReceived-=Log;
        bool polish=Environment.GetEnvironmentVariable("COGNA_CONCEPT_SUITE")=="polish";
        Write(polish?"polish-execution":"execution",new{passed,error,frame=Time.frameCount,unity=Application.unityVersion,database=DatabaseInitializer.DatabaseService?.Connection?.DatabasePath,captures=captures.Count,sizes=polish?new[]{sizes[0]}:sizes});
        Write(polish?"polish-captures":"captures",captures);Write(polish?"polish-console-errors":"console-errors",logs);EditorApplication.Exit(passed?0:1);
    }
}
