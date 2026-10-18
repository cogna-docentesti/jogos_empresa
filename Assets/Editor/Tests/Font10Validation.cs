using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Game.Adapter.In.UI;
using Game.Adapter.In.UI.Theme;
using Game.Adapter.In.UI.Layout;
using Game.EditorTools;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Profile;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Final typography audit in the existing Editor, with isolated Player services.</summary>
public static class Font10Validation
{
    public const string Output="output/ui-update/fonts/FONT-10";
    const string Key="Cogna.Font10.";
    static readonly Vector2Int[] Sizes={new(1920,1080),new(1366,768),new(1280,720),new(1600,1000),new(2340,1080)};
    static readonly List<object> matrix=new(), logs=new();
    static double next,deadline;
    static int stage,lastFrame,suite;
    static readonly string[] SuiteNames={"campaigns","supplemental","states","events","feedback","dialogs","stress"};
    static readonly Type[] SuiteTypes={typeof(UI19NavigationValidation),typeof(UI19NavigationValidation),typeof(UI20ScreenStateValidation),typeof(UI12EventValidation),typeof(UI17FeedbackValidation),typeof(UI18DialogValidation),typeof(Font10StressValidation)};
    static string Suffix=>SessionState.GetInt(Key+"StartSuite",0)>0?"-stress":"";
    static void Write(string n,object o){Directory.CreateDirectory(Output);File.WriteAllText(Output+"/"+n+".json",JsonConvert.SerializeObject(o,Formatting.Indented));}
    static void Require(bool b,string n){if(!b)throw new InvalidOperationException(n);}
    static string Snapshot()=>JsonConvert.SerializeObject(Enumerable.Range(0,SceneManager.sceneCount).Select(SceneManager.GetSceneAt).Select(s=>new{s.path,s.isDirty,s.rootCount,texts=s.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<TMP_Text>(true)).OrderBy(t=>t.GetInstanceID()).Select(t=>new{id=t.GetInstanceID(),json=JsonUtility.ToJson(t)})}));
    [InitializeOnLoadMethod] static void Init()
    {
        Directory.CreateDirectory(Output);Write("bridge-ready",new{compiledAt=DateTime.UtcNow,assembly=typeof(Font10Validation).Assembly.ManifestModule.ModuleVersionId});
        EditorApplication.update-=Request;EditorApplication.update+=Request;
        if(SessionState.GetBool(Key+"PendingAndroid",false)&&EditorUserBuildSettings.activeBuildTarget==BuildTarget.Android){SessionState.SetBool(Key+"PendingAndroid",false);File.WriteAllText(Output+"/editor-request.txt","android");}
        if(SessionState.GetBool(Key+"Playing",false)){if(SessionState.GetInt(Key+"NativeStartSuite",0)>0&&SessionState.GetInt(Key+"StartSuite",0)==0){matrix.AddRange(JArray.Parse(File.ReadAllText(Output+"/retained-native-matrix.json")).Cast<object>());logs.AddRange(JArray.Parse(File.ReadAllText(Output+"/retained-native-console.json")).Cast<object>());}stage=0;lastFrame=-1;deadline=EditorApplication.timeSinceStartup+3600;EditorApplication.update-=Tick;EditorApplication.update+=Tick;Application.logMessageReceived+=Log;Canvas.willRenderCanvases+=Safe;}
        if(SessionState.GetBool(Key+"Restoring",false))EditorApplication.delayCall+=Restore;
    }
    static void Request()
    {
        if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+1;
        string p=Output+"/editor-request.txt";if(!File.Exists(p))return;string cmd=File.ReadAllText(p).Trim();
        if(EditorApplication.isPlayingOrWillChangePlaymode&&cmd!="status"&&cmd!="abort")return;File.Delete(p);
        try{if(cmd=="audit")Audit();else if(cmd=="native-session-check")SaveNativeSession();else if(cmd=="validate"){SessionState.SetInt(Key+"NativeStartSuite",0);SessionState.SetInt(Key+"StartSuite",0);Begin();}else if(cmd=="resume"){SessionState.SetInt(Key+"NativeStartSuite",3);Begin();}else if(cmd=="stress"){SessionState.SetInt(Key+"StartSuite",6);Begin();}else if(cmd=="session-check")SessionCheck();else if(cmd=="status")Write("status",new{stage,suite,frame=Time.frameCount,playing=EditorApplication.isPlaying,restoring=SessionState.GetBool(Key+"Restoring",false)});else if(cmd=="abort")Finish("Explicit abort");else if(cmd=="windows")Build(false);else if(cmd=="windows-release")Build(false,true);else if(cmd=="android")Build(true);else throw new Exception("Unknown FONT-10 command");}
        catch(Exception e){Write("request-error",new{cmd,error=e.ToString()});Debug.LogException(e);}
    }
    static void SaveNativeSession()
    {
        Require(!EditorApplication.isPlaying&&!EditorApplication.isPlayingOrWillChangePlaymode,"Use Edit mode");
        var scene=SceneManager.GetSceneByPath("Assets/Scenes/GameScene.unity");Require(scene.isLoaded&&scene.rootCount==7,"Wait for the original scene");
        EditorSceneManager.SaveScene(scene,Output+"/segment-01-live-session.unity",true);
        Write("segment-01-session",new{scene.isDirty,scene.rootCount,viewport=new[]{Screen.width,Screen.height},byteExact=File.ReadAllBytes(Output+"/segment-01-live-session.unity").SequenceEqual(File.ReadAllBytes(Output+"/live-session-before.unity"))});
    }
    static void Audit()
    {
        Require(!EditorApplication.isPlayingOrWillChangePlaymode,"Use Edit mode");string before=Snapshot();
        var live=SceneManager.GetSceneByPath("Assets/Scenes/GameScene.unity");
        Write("live-before",new{snapshot=before,viewport=new[]{Screen.width,Screen.height},live.isDirty,live.rootCount});
        if(!File.Exists(Output+"/live-session-before.unity"))EditorSceneManager.SaveScene(live,Output+"/live-session-before.unity",true);
        var rows=new List<object>();var issues=new List<string>();
        string copy="Assets/Scenes/Font10AuditCopy.unity";var active=SceneManager.GetActiveScene();
        File.Copy("Assets/Scenes/GameScene.unity",copy,true);AssetDatabase.ImportAsset(copy,ImportAssetOptions.ForceSynchronousImport);
        var scene=EditorSceneManager.OpenScene(copy,OpenSceneMode.Additive);
        try{AuditRoots(scene.GetRootGameObjects(),"Assets/Scenes/GameScene.unity",rows,issues);}
        finally{EditorSceneManager.CloseScene(scene,true);SceneManager.SetActiveScene(active);AssetDatabase.DeleteAsset(copy);}
        var coverage=JObject.Parse(File.ReadAllText("docs/ui-update/font-coverage.json"));
        foreach(string path in coverage["consumers"].Where(c=>(string)c["kind"]=="serialized"&&((string)c["asset"]).EndsWith(".prefab")).Select(c=>(string)c["asset"]).Distinct())
        {var root=PrefabUtility.LoadPrefabContents(path);try{AuditRoots(new[]{root},path,rows,issues);}finally{PrefabUtility.UnloadPrefabContents(root);}}
        Require(before==Snapshot(),"Saved audit changed the user's live scene");
        Write("saved-audit",new{passed=issues.Count==0,count=rows.Count,rows,issues,sessionPreserved=true});
Write("environment",new{unity=Application.unityVersion,project=Application.dataPath,buildTarget=EditorUserBuildSettings.activeBuildTarget.ToString(),windows=BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone,BuildTarget.StandaloneWindows64),android=BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android,BuildTarget.Android),scenes=EditorBuildSettings.scenes.Select(s=>new{s.path,s.enabled}),pipeline=File.ReadAllText("Packages/manifest.json").Contains("com.unity.pipeline"),dependencies=AssetDatabase.GetDependencies("Assets/Scenes/GameScene.unity",true)});
        Require(issues.Count==0,"Saved typography audit has issues");
    }
    static void SessionCheck()
    {
        Require(!EditorApplication.isPlayingOrWillChangePlaymode,"Use Edit mode");
        var scene=SceneManager.GetSceneByPath("Assets/Scenes/GameScene.unity");Require(scene.isLoaded&&scene.rootCount==7,"Original scene must remain open");
        EditorSceneManager.MarkSceneDirty(scene);UI01Validation.SetGameView(873,491);
        var view=EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView"));
        var zoomField=view.GetType().GetField("m_ZoomArea",BindingFlags.Instance|BindingFlags.NonPublic);
        var zoom=zoomField?.GetValue(view);var scale=zoom?.GetType().GetProperty("scale",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);if(scale?.CanWrite==true)scale.SetValue(zoom,Vector2.one);
        EditorSceneManager.SaveScene(scene,Output+"/final-live-session.unity",true);
        Write("final-session",new{scene.path,scene.isDirty,scene.rootCount,viewport=new[]{873,491},target=EditorUserBuildSettings.activeBuildTarget.ToString(),profile=AssetDatabase.GetAssetPath(BuildProfile.GetActiveBuildProfile()),zoomReset=scale?.CanWrite==true});
    }
    static void AuditRoots(GameObject[] roots,string asset,List<object> rows,List<string> issues)
    {
        foreach(var t in roots.SelectMany(r=>r.GetComponentsInChildren<TMP_Text>(true)))
        {
            var binding=t.GetComponent<TypographyRoleBinding>();string error=null;
            if(t.font==null||t.fontSharedMaterial==null)error="Null font/material";
            else if(binding==null)error="Missing semantic role";
            else if(t.font!=GameTypography.Resolve(binding.Role).Font||t.fontSharedMaterial!=t.font.material)error="Wrong face/shared material";
            else if((t.fontStyle&FontStyles.Bold)!=0||t.enableAutoSizing||t.rectTransform.localScale!=Vector3.one)error="Synthetic bold/autosize/scale";
            if(error!=null)issues.Add(asset+":"+Font09Migration.PathOf(t.transform)+": "+error);
            rows.Add(new{asset,path=Font09Migration.PathOf(t.transform),font=t.font?.name,material=t.fontSharedMaterial?.name,role=binding?.Role.ToString(),t.fontSize,t.enableAutoSizing,style=t.fontStyle.ToString(),active=t.gameObject.activeInHierarchy,error});
        }
    }
    static void RunTests()
    {
        var results=new List<object>();var types=typeof(Font10Validation).Assembly.GetTypes().Where(t=>t.Namespace==null&&(t.Name.StartsWith("UI")&&t.Name.EndsWith("Tests")||new[]{"Font04TypographyTests","InitialDecisionsTests","MonthlySimulationTests","PersistenceAndLocationTests"}.Contains(t.Name)));
        foreach(var type in types.OrderBy(t=>t.Name))foreach(var m in type.GetMethods().Where(m=>m.GetCustomAttributes<NUnit.Framework.TestAttribute>().Any()||m.GetCustomAttributes<NUnit.Framework.TestCaseAttribute>().Any()||m.GetCustomAttributes<NUnit.Framework.TestCaseSourceAttribute>().Any()))
        {
            var cases=m.GetCustomAttributes<NUnit.Framework.TestCaseAttribute>().Select(c=>c.Arguments).ToList();
            foreach(var source in m.GetCustomAttributes<NUnit.Framework.TestCaseSourceAttribute>())
            {var member=(MemberInfo)type.GetProperty(source.SourceName,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static)??type.GetField(source.SourceName,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static);var values=member is PropertyInfo prop?prop.GetValue(null):((FieldInfo)member).GetValue(null);foreach(var entry in (System.Collections.IEnumerable)values)cases.Add(entry is NUnit.Framework.TestCaseData data?data.Arguments:(object[])entry);}
            if(cases.Count==0)cases.Add(Array.Empty<object>());
            foreach(var args in cases){object instance=Activator.CreateInstance(type);Exception error=null;try{foreach(var setup in type.GetMethods().Where(x=>x.GetCustomAttributes<NUnit.Framework.SetUpAttribute>().Any()))setup.Invoke(instance,null);m.Invoke(instance,args);}catch(Exception e){error=(e as TargetInvocationException)?.InnerException??e;}finally{try{foreach(var teardown in type.GetMethods().Where(x=>x.GetCustomAttributes<NUnit.Framework.TearDownAttribute>().Any()))teardown.Invoke(instance,null);}catch(Exception e){error??=e;}}results.Add(new{name=type.Name+"."+m.Name+"("+string.Join(",",args)+")",passed=error==null,error=error?.ToString()});}
        }
        var json=JArray.FromObject(results);Write("edit-tests",new{count=results.Count,passed=json.All(r=>(bool)r["passed"]),results});
    }
    static void Begin()
    {
        Require(!EditorApplication.isPlayingOrWillChangePlaymode,"Use Edit mode");SessionState.SetString(Key+"Before",Snapshot());
        SessionState.SetInt(Key+"Width",Screen.width);SessionState.SetInt(Key+"Height",Screen.height);
        SessionState.SetString(Key+"OldDb",Environment.GetEnvironmentVariable("COGNA_TEST_DATABASE_PATH")??"");SessionState.SetString(Key+"OldKey",Environment.GetEnvironmentVariable("COGNA_TEST_IDENTIFICATION_KEY")??"");
        string db=Path.GetFullPath(Output+"/qa-"+Guid.NewGuid()+".db"),key=Key+Guid.NewGuid();Environment.SetEnvironmentVariable("COGNA_TEST_DATABASE_PATH",db);Environment.SetEnvironmentVariable("COGNA_TEST_IDENTIFICATION_KEY",key);SessionState.SetString(Key+"TestKey",key);
        SessionState.SetBool(Key+"Playing",true);EditorApplication.isPlaying=true;
    }
    static void Safe()
    {
        if(Screen.width!=2340||Screen.height!=1080)return;var safe=new Rect(90,24,2160,1056);
        UnityEngine.Object.FindFirstObjectByType<ProductionCanvasLayout>()?.Apply(safe);
        foreach(var fitter in UnityEngine.Object.FindObjectsByType<SafeAreaFitter>(FindObjectsSortMode.None).Where(f=>f.isActiveAndEnabled))typeof(SafeAreaFitter).GetMethod("Apply",BindingFlags.Instance|BindingFlags.NonPublic,null,new[]{typeof(Rect),typeof(Vector2Int),typeof(bool)},null).Invoke(fitter,new object[]{safe,new Vector2Int(2340,1080),true});
    }
    static void Log(string m,string s,LogType t){if(t is LogType.Warning or LogType.Error or LogType.Exception or LogType.Assert)logs.Add(new{type=t.ToString(),message=m,stack=s,frame=Time.frameCount});}
    static void Capture(string name,string path)
    {
        var texts=GameObject.Find("Canvas").GetComponentsInChildren<TMP_Text>().Where(t=>t.isActiveAndEnabled&&!string.IsNullOrEmpty(t.text)).ToArray();
        var rows=texts.Select(t=>{t.ForceMeshUpdate();var b=t.GetComponent<TypographyRoleBinding>();return new{path=Font09Migration.PathOf(t.transform),t.text,font=t.font?.name,material=t.fontSharedMaterial?.name,role=b?.Role.ToString(),t.fontSize,t.isTextOverflowing,overflow=t.overflowMode.ToString(),input=t.GetComponentInParent<TMP_InputField>()!=null,masked=t.GetComponentInParent<UnityEngine.UI.RectMask2D>()!=null||t.GetComponentInParent<UnityEngine.UI.Mask>()!=null,lines=t.textInfo.lineCount,rect=new[]{t.rectTransform.rect.width,t.rectTransform.rect.height},nullFont=t.font==null||t.fontSharedMaterial==null,wrongFace=b==null||t.font!=GameTypography.Resolve(b.Role).Font,synthetic=(t.fontStyle&FontStyles.Bold)!=0,autosize=t.enableAutoSizing,scale=t.rectTransform.localScale.ToString()};}).ToArray();
        Require(rows.All(r=>!r.nullFont&&!r.wrongFace&&!r.synthetic&&!r.autosize),"Runtime font contract failed at "+name);
        matrix.Add(new{suite=SuiteNames[suite],name,path,width=Screen.width,height=Screen.height,frame=Time.frameCount,state=GameManager.Instance.StateMachine.CurrentState.ToString(),safeArea=Screen.width==2340?new[]{90,24,2160,1056}:new[]{0,0,Screen.width,Screen.height},simulation=Screen.width==2340,texts=rows});
        Write("capture-matrix"+Suffix,matrix);
    }
    static string Report=>UiValidationContext.OutputRoot+"/"+(suite==1?"playmode-supplemental":suite==3?"playmode-events":"playmode")+".json";
    static void StartSuite()
    {
        UiValidationContext.OutputRoot=Output+"/"+SuiteNames[suite];Directory.CreateDirectory(UiValidationContext.OutputRoot);UiValidationContext.Viewports=suite==3&&SessionState.GetInt(Key+"NativeStartSuite",0)==3?Sizes.Skip(3).ToArray():Sizes;UiValidationContext.OnCapture=Capture;
        if(File.Exists(Report))File.Delete(Report);Write("progress",new{suite=SuiteNames[suite],frame=Time.frameCount});
        if(suite==0)UI19NavigationValidation.Run();else if(suite==1)UI19NavigationValidation.RunSupplement();else if(suite==2)UI20ScreenStateValidation.Run();else if(suite==3)UI12EventValidation.Run();else if(suite==4)UI17FeedbackValidation.Run();else if(suite==5)UI18DialogValidation.Run();else Font10StressValidation.Run();
    }
    static void Tick()
    {
        if(!EditorApplication.isPlaying)return;Application.runInBackground=true;EditorApplication.QueuePlayerLoopUpdate();EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Repaint();
        if(Time.frameCount<12||Time.frameCount==lastFrame)return;lastFrame=Time.frameCount;
        try{
            if(EditorApplication.timeSinceStartup>deadline)throw new TimeoutException("FONT-10 deadline");
            if(stage==0){if(GameManager.Instance==null||DatabaseInitializer.DatabaseService?.Connection?.DatabasePath!=Environment.GetEnvironmentVariable("COGNA_TEST_DATABASE_PATH"))return;UI01Validation.RequireIsolatedStorage();SceneManager.LoadScene("GameScene");stage=-1;return;}
            if(stage==-1){suite=SessionState.GetInt(Key+"StartSuite",0)>0?SessionState.GetInt(Key+"StartSuite",0):SessionState.GetInt(Key+"NativeStartSuite",0);StartSuite();stage=1;return;}
            if(!File.Exists(Report))return;var report=JObject.Parse(File.ReadAllText(Report));Require((bool)report["completed"]&&((int?)report["failures"]??0)==0,"Suite failed "+Report);
            if(++suite<SuiteNames.Length){StartSuite();return;}Finish(null);
        }catch(Exception e){Finish(e.ToString());}
    }
    static void Finish(string error)
    {
        for(int i=0;i<SuiteTypes.Length;i++){var m=SuiteTypes[i].GetMethod("Tick",BindingFlags.Static|BindingFlags.NonPublic);EditorApplication.update-=(EditorApplication.CallbackFunction)Delegate.CreateDelegate(typeof(EditorApplication.CallbackFunction),m);}
        EditorApplication.update-=Tick;Canvas.willRenderCanvases-=Safe;Application.logMessageReceived-=Log;
        Write("console"+Suffix,logs);Write("capture-matrix"+Suffix,matrix);SessionState.SetString(Key+"Error",error??"");SessionState.SetBool(Key+"Playing",false);SessionState.SetBool(Key+"Restoring",true);
        UiValidationContext.OutputRoot=null;UiValidationContext.OnCapture=null;UiValidationContext.Viewports=null;EditorApplication.isPlaying=false;EditorApplication.delayCall+=Restore;
    }
    static double restoreDue;
    static void Restore()
    {
        var s=SceneManager.GetSceneByPath("Assets/Scenes/GameScene.unity");
        if(EditorApplication.isPlaying||EditorApplication.isPlayingOrWillChangePlaymode||!s.isLoaded||s.rootCount!=7){EditorApplication.QueuePlayerLoopUpdate();EditorApplication.delayCall+=Restore;return;}
        UI01Validation.SetGameView(SessionState.GetInt(Key+"Width",873),SessionState.GetInt(Key+"Height",491));
        restoreDue=EditorApplication.timeSinceStartup+1;EditorApplication.update-=FinishRestore;EditorApplication.update+=FinishRestore;
    }
    static void FinishRestore()
    {
        EditorApplication.QueuePlayerLoopUpdate();EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Repaint();
        if(EditorApplication.isPlaying||EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.timeSinceStartup<restoreDue)return;
        EditorApplication.update-=FinishRestore;
        Environment.SetEnvironmentVariable("COGNA_TEST_DATABASE_PATH",SessionState.GetString(Key+"OldDb",""));Environment.SetEnvironmentVariable("COGNA_TEST_IDENTIFICATION_KEY",SessionState.GetString(Key+"OldKey",""));PlayerPrefs.DeleteKey(SessionState.GetString(Key+"TestKey",""));
        var scene=SceneManager.GetSceneByPath("Assets/Scenes/GameScene.unity");EditorSceneManager.SaveScene(scene,Output+"/live-session-after.unity",true);
        bool preserved=File.ReadAllBytes(Output+"/live-session-after.unity").SequenceEqual(File.ReadAllBytes(Output+"/live-session-before.unity"));SessionState.SetBool(Key+"Restoring",false);
        Write("execution"+Suffix,new{passed=preserved&&SessionState.GetString(Key+"Error","")=="",error=SessionState.GetString(Key+"Error",""),sessionPreserved=preserved,viewport=new[]{SessionState.GetInt(Key+"Width",873),SessionState.GetInt(Key+"Height",491)},unity=Application.unityVersion});
    }
    static void Build(bool android,bool release=false)
    {
        Require(!EditorApplication.isPlayingOrWillChangePlaymode,"Use Edit mode");
        if(!SessionState.GetBool(Key+"BuildOriginalSaved",false)){SessionState.SetBool(Key+"BuildOriginalSaved",true);SessionState.SetString(Key+"OriginalProfile",AssetDatabase.GetAssetPath(BuildProfile.GetActiveBuildProfile())??"");SessionState.SetInt(Key+"OriginalTarget",(int)EditorUserBuildSettings.activeBuildTarget);}
        if(android&&EditorUserBuildSettings.activeBuildTarget!=BuildTarget.Android){SessionState.SetBool(Key+"PendingAndroid",true);Require(EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android,BuildTarget.Android),"Android target switch failed");return;}
        string before=Snapshot();var profile=AssetDatabase.LoadAssetAtPath<BuildProfile>(android?"Assets/Settings/Build Profiles/Android™.asset":"Assets/Settings/Build Profiles/New Windows Profile.asset");Require(profile!=null,"Existing profile missing");
        string target=android?"android":"windows",path=Path.GetFullPath(android?"Builds/FONT-10/Android/Cogna.apk":"Builds/FONT-10/Windows/Cogna.exe");Directory.CreateDirectory(Path.GetDirectoryName(path));
        var scenes=EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToArray();
        var originalDefines=profile.scriptingDefines;var originalHasDefines=new SerializedObject(profile).FindProperty("m_HasScriptingDefines").boolValue;
        try
        {
            if(release){profile.scriptingDefines=originalDefines.Concat(new[]{"FONT10_QA_PLAYER"}).Distinct().ToArray();var defineState=new SerializedObject(profile);defineState.FindProperty("m_HasScriptingDefines").boolValue=true;defineState.ApplyModifiedPropertiesWithoutUndo();}
            BuildProfile.SetActiveBuildProfile(profile);
            var report=BuildPipeline.BuildPlayer(new BuildPlayerWithProfileOptions{buildProfile=profile,locationPathName=path,options=release?BuildOptions.None:BuildOptions.Development});
            EditorSceneManager.SaveScene(SceneManager.GetSceneByPath("Assets/Scenes/GameScene.unity"),Output+"/build-"+target+"-scene.unity",true);
            bool contentPreserved=File.ReadAllBytes(Output+"/build-"+target+"-scene.unity").SequenceEqual(File.ReadAllBytes(Output+"/live-session-before.unity"));
            Write("build-"+target,new{passed=report.summary.result==BuildResult.Succeeded,result=report.summary.result.ToString(),target=report.summary.platform.ToString(),profile=AssetDatabase.GetAssetPath(profile),profileSubtarget=new SerializedObject(profile).FindProperty("m_Subtarget").intValue,effectiveSubtarget=android?0:(int)StandaloneBuildSubtarget.Player,profileUsed=true,release,qaDefine=release?"FONT10_QA_PLAYER":"DEVELOPMENT_BUILD",options=report.summary.options.ToString(),note="Existing profile used directly; opt-in validation instrumentation enabled by a temporary in-memory define for the release proof. Profile files remain unchanged. Scene content comparison uses saved-copy bytes; native object IDs may be recreated by the build.",output=path,scenes,errors=report.summary.totalErrors,warnings=report.summary.totalWarnings,bytes=report.summary.totalSize,seconds=report.summary.totalTime.TotalSeconds,sessionPreserved=contentPreserved,nativeIdentityPreserved=before==Snapshot(),steps=report.steps.Select(s=>new{s.name,messages=s.messages.Select(m=>new{type=m.type.ToString(),m.content})})});
        }
        finally
        {
            if(release){profile.scriptingDefines=originalDefines;var definitionState=new SerializedObject(profile);definitionState.FindProperty("m_HasScriptingDefines").boolValue=originalHasDefines;definitionState.ApplyModifiedPropertiesWithoutUndo();}
            var original=SessionState.GetString(Key+"OriginalProfile","");BuildProfile.SetActiveBuildProfile(string.IsNullOrEmpty(original)?null:AssetDatabase.LoadAssetAtPath<BuildProfile>(original));
            if(android&&EditorUserBuildSettings.activeBuildTarget!=(BuildTarget)SessionState.GetInt(Key+"OriginalTarget",(int)BuildTarget.StandaloneWindows64))EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone,(BuildTarget)SessionState.GetInt(Key+"OriginalTarget",(int)BuildTarget.StandaloneWindows64));
        }
    }
}
