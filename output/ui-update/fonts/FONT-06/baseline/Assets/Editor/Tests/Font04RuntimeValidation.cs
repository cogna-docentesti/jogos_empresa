using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Game.Adapter.In.UI;
using Game.Adapter.In.UI.Theme;
using Game.EditorTools;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>FONT-04 uses the open dirty scene, isolated storage and existing behavioral assertions.</summary>
public static class Font04RuntimeValidation
{
    const string Output="output/ui-update/fonts/FONT-04";
    const string Key="Cogna.Font04.";
    static readonly List<string> failures=new();
    static readonly List<object> observations=new();
    static double due;
    static int stage;
    static GameObject sheet;
    static string Hash(string value) { using var sha=SHA256.Create(); return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-","").ToLowerInvariant(); }
    static TMP_Text[] Texts() => Enumerable.Range(0,SceneManager.sceneCount).Select(SceneManager.GetSceneAt).SelectMany(s=>s.GetRootGameObjects()).SelectMany(r=>r.GetComponentsInChildren<TMP_Text>(true)).OrderBy(t=>t.GetInstanceID()).ToArray();
    static string SceneState()
    {
        var texts=Texts();
        return JsonConvert.SerializeObject(new {scenes=Enumerable.Range(0,SceneManager.sceneCount).Select(SceneManager.GetSceneAt).Select(s=>new{s.path,s.isDirty,s.rootCount}),texts=texts.Length,
            fingerprint=Hash(string.Join("\n",texts.Select(t=>JsonUtility.ToJson(t)))),unsavedRestaurant=texts.Count(t=>t.name=="Text (TMP)"&&t.text=="Restaurante")});
    }
    static void Write(string name,object value) => File.WriteAllText(Output+"/"+name+".json",JsonConvert.SerializeObject(value,Formatting.Indented)+"\n");
    static void Require(bool value,string why) {if(!value)failures.Add(why);}

    [MenuItem("Tools/Jogos de Empresa/FONT-04/Validate theme and runtime %&F5")]
    public static void Run()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().path!="Assets/Scenes/GameScene.unity") throw new InvalidOperationException("Use the open GameScene in Edit mode.");
        Directory.CreateDirectory(Output); failures.Clear(); observations.Clear();
        SessionState.SetString(Key+"Before",SceneState()); Write("live-before",JObject.Parse(SceneState()));
        try
        {
            FontRoleMigration.ConfigureDefault();
            Require(TMP_Settings.defaultFontAsset==GameTypography.Resolve(TypographyRole.Body).Font,"Player default font is not Roboto Regular.");
            RunTests();
            Require(SessionState.GetString(Key+"Before","")==SceneState(),"Edit tests changed the existing scene.");
            if(failures.Count>0) {Complete();return;}
            SessionState.SetString(Key+"OldDb",Environment.GetEnvironmentVariable("COGNA_TEST_DATABASE_PATH")??"");
            SessionState.SetString(Key+"OldKey",Environment.GetEnvironmentVariable("COGNA_TEST_IDENTIFICATION_KEY")??"");
            Environment.SetEnvironmentVariable("COGNA_TEST_DATABASE_PATH",Path.GetFullPath(Output+"/qa-game.db"));
            Environment.SetEnvironmentVariable("COGNA_TEST_IDENTIFICATION_KEY",Key+"Identification");
            SessionState.SetString(Key+"Failures",JsonConvert.SerializeObject(failures));
            SessionState.SetBool(Key+"Playing",true); EditorApplication.isPlaying=true;
        }
        catch(Exception e) {failures.Add(e.ToString());Complete();}
    }
    static void RunTests()
    {
        var results=new List<object>();
        foreach(var type in new[]{typeof(Font04TypographyTests),typeof(UI02ThemeTests),typeof(UI03SharedComponentsTests),typeof(UI20ReadabilityTests)})
        foreach(var method in type.GetMethods().Where(m=>m.GetCustomAttributes<NUnit.Framework.TestAttribute>().Any() || m.GetCustomAttributes<NUnit.Framework.TestCaseAttribute>().Any()))
        {
            var cases=method.GetCustomAttributes<NUnit.Framework.TestCaseAttribute>().Select(c=>c.Arguments).ToArray();
            if(cases.Length==0) cases=new[]{Array.Empty<object>()};
            foreach(var args in cases)
            {
                string name=type.Name+"."+method.Name+"("+string.Join(",",args)+")";
                try {method.Invoke(Activator.CreateInstance(type),args);results.Add(new{name,passed=true});}
                catch(Exception e) {var error=(e as TargetInvocationException)?.InnerException??e; failures.Add(name+": "+error);results.Add(new{name,passed=false,error=error.ToString()});}
            }
        }
        Write("edit-tests",new{passed=failures.Count==0,count=results.Count,results});
    }
    [InitializeOnLoadMethod] static void Reload()
    {
        if(SessionState.GetBool(Key+"Restoring",false)) {EditorApplication.delayCall+=Restore;return;}
        if(!SessionState.GetBool(Key+"Playing",false))return;
        failures.Clear();failures.AddRange(JsonConvert.DeserializeObject<List<string>>(SessionState.GetString(Key+"Failures","[]")));
        stage=0; observations.Clear(); due=EditorApplication.timeSinceStartup+120;
        EditorApplication.update-=Tick;EditorApplication.update+=Tick;
    }
    static void Refresh()
    {
        Application.runInBackground=true; EditorApplication.QueuePlayerLoopUpdate();
        Canvas.ForceUpdateCanvases();EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Repaint();
    }
    static void Observe(string name,string path)
    {
        var bindings=Texts().Select(t=>new{t,b=t.GetComponent<TypographyRoleBinding>()}).Where(x=>x.b!=null).ToArray();
        foreach(var x in bindings)
        {
            var style=GameTypography.Resolve(x.b.Role);
            Require(x.t.font==style.Font && x.t.fontSharedMaterial==style.Material,"Role face/material lost at "+name+": "+x.t.name+" / "+x.b.Role);
            Require((x.t.fontStyle&FontStyles.Bold)==0 && x.t.fontWeight==FontWeight.Regular,"Synthetic weight at "+name+": "+x.t.name);
        }
        observations.Add(new{name,path,frame=Time.frameCount,count=bindings.Length,roles=bindings.GroupBy(x=>x.b.Role.ToString()).ToDictionary(g=>g.Key,g=>g.Count()),
            active=bindings.Where(x=>x.t.isActiveAndEnabled).Select(x=>new{path=PathOf(x.t.transform),role=x.b.Role.ToString(),font=x.t.font.name,size=x.t.fontSize}).ToArray()});
        Write("runtime-bindings",new{passed=failures.Count==0,observations,failures});
    }
    static string PathOf(Transform t)=>t.parent==null?t.name:PathOf(t.parent)+"/"+t.name;
    static void Tick()
    {
        if(!EditorApplication.isPlaying)return;
        Refresh(); if(Time.frameCount<12)return;
        try
        {
            UI01Validation.RequireIsolatedStorage();
            if(stage==0) {MakeSheet();stage=1;due=EditorApplication.timeSinceStartup+.8;return;}
            if(stage==1)
            {
                if(EditorApplication.timeSinceStartup<due)return;
                var path=UI01Validation.Capture("../fonts/FONT-04/runtime-role-specimen"); Observe("runtime-role-specimen",path);
                UnityEngine.Object.DestroyImmediate(sheet);sheet=null;
                UiValidationContext.OutputRoot=Output+"/campaigns";Directory.CreateDirectory(UiValidationContext.OutputRoot);
                UiValidationContext.Viewports=new[]{new Vector2Int(1920,1080)};UiValidationContext.OnCapture=Observe;
                string report=UiValidationContext.OutputRoot+"/playmode.json";if(File.Exists(report))File.Delete(report);
                UI19NavigationValidation.Run();stage=2;due=EditorApplication.timeSinceStartup+900;return;
            }
            if(EditorApplication.timeSinceStartup>due)throw new TimeoutException("FONT-04 runtime timeout.");
            var reportPath=Output+"/campaigns/playmode.json";
            if(!File.Exists(reportPath))return;
            var result=JObject.Parse(File.ReadAllText(reportPath));
            Require((bool)result["completed"] && (int)result["failures"]==0,"Existing UI-19 behavior suite failed.");
            Require(observations.Count>50,"Expected advancing captures across real panels, catalogs and reentry.");
            Finish();
        }
        catch(Exception e) {failures.Add(e.ToString());Finish();}
    }
    static void MakeSheet()
    {
        var canvas=Texts().Select(t=>t.GetComponentInParent<Canvas>()).First(c=>c!=null&&c.isActiveAndEnabled&&c.isRootCanvas);
        sheet=new GameObject("FONT04 transient factory specimen",typeof(RectTransform),typeof(UnityEngine.UI.Image));sheet.transform.SetParent(canvas.transform,false);
        sheet.transform.SetAsLastSibling();var rect=(RectTransform)sheet.transform;rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
        sheet.GetComponent<UnityEngine.UI.Image>().color=GamePalette.Surface; UiFactory.BeginRun(false);
        var roles=new[]{TypographyRole.ScreenTitle,TypographyRole.Body,TypographyRole.Button,TypographyRole.Kpi};
        var contents=new[]{"Cabeçalho · Restaurante e público","Corpo · Ação, informação e R$ -12.345,67","CONTINUAR","R$ 72.345,67"};
        for(int i=0;i<roles.Length;i++)
        {
            var r=UiFactory.EnsureChild(sheet.transform,"Arbitrary "+i);r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(100,-100-i*210);r.sizeDelta=new Vector2(1700,120);
            var text=UiFactory.Text(r,contents[i],new TypographyToken(roles[i],GameTypography.Resolve(roles[i]).Size),FontStyles.Bold,GamePalette.Ink,TextAlignmentOptions.MidlineLeft,true);
            Require(text.font==GameTypography.Resolve(roles[i]).Font,"Runtime factory role "+roles[i]);
            text.ForceMeshUpdate(); Require(!text.isTextOverflowing,"Runtime factory specimen overflows: "+roles[i]);
        }
    }
    static void Finish()
    {
        EditorApplication.update-=Tick; UiValidationContext.OnCapture=null;
        if(sheet!=null)UnityEngine.Object.DestroyImmediate(sheet);
        SessionState.SetString(Key+"Failures",JsonConvert.SerializeObject(failures));
        SessionState.SetBool(Key+"Playing",false);SessionState.SetBool(Key+"Restoring",true);
        EditorApplication.isPlaying=false;EditorApplication.delayCall+=Restore;
    }
    static void Restore()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode) {EditorApplication.delayCall+=Restore;return;}
        if(!SessionState.GetBool(Key+"Restoring",false))return;
        var before=JObject.Parse(SessionState.GetString(Key+"Before","{}"));
        if(SceneManager.GetActiveScene().rootCount!=(int)before["scenes"][0]["rootCount"]) {EditorApplication.delayCall+=Restore;return;}
        SessionState.SetBool(Key+"Restoring",false);
        failures.Clear();failures.AddRange(JsonConvert.DeserializeObject<List<string>>(SessionState.GetString(Key+"Failures","[]")));
        Environment.SetEnvironmentVariable("COGNA_TEST_DATABASE_PATH",SessionState.GetString(Key+"OldDb",""));
        Environment.SetEnvironmentVariable("COGNA_TEST_IDENTIFICATION_KEY",SessionState.GetString(Key+"OldKey",""));
        Complete();
    }
    static void Complete()
    {
        var after=SceneState();Require(SessionState.GetString(Key+"Before","")==after,"Existing dirty scene changed.");Write("live-after",JObject.Parse(after));
        object[] counts={0,0,0};typeof(Editor).Assembly.GetType("UnityEditor.LogEntries").GetMethod("GetCountsByType",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,counts);
        Require((int)counts[0]==0,"Console errors: "+counts[0]);
        Write("execution",new{task="FONT-04",passed=failures.Count==0,failures,unity=Application.unityVersion,consoleErrors=counts[0],consoleWarnings=counts[1],
            before=JObject.Parse(SessionState.GetString(Key+"Before","{}")),after=JObject.Parse(after),scenePreserved=SessionState.GetString(Key+"Before","")==after,productionSceneSaved=false,playerDefault="Roboto-Regular",viewport=new[]{1920,1080}});
        Debug.Log("FONT-04 "+(failures.Count==0?"PASS":"FAIL")+"; "+Output+"/execution.json");
    }
}
