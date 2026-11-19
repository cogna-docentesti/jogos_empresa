using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Game.Adapter.In.UI;
using Game.Adapter.In.UI.Layout;
using Game.Adapter.In.UI.Navigation;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Final production-scene QA. Run in an isolated Editor process using RunAndExit.</summary>
public static class UI20FinalValidation
{
    const string Root = "output/ui-update/UI-20";
    const string Running = "Cogna.UI20.Running";
    static string suite;
    static int stage, lastFrame;
    static double deadline;
    static readonly List<object> matrix = new(), logs = new();
    static readonly Vector2Int[] sizes = { new(1920,1080), new(1366,768), new(1280,720), new(1600,1000), new(2340,1080) };
    static void Write(string name, object value) => File.WriteAllText(Root + "/" + name + ".json", Newtonsoft.Json.JsonConvert.SerializeObject(value, Newtonsoft.Json.Formatting.Indented));
    static void Require(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
    static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;
    static float[] Box(Rect r) => new[] { r.x, r.y, r.width, r.height };

    public static void RunAndExit()
    {
        Require(Application.dataPath.Replace('\\','/') == "D:/Cogna/jogos_empresa/Assets", "Wrong project");
        Require(!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("COGNA_TEST_DATABASE_PATH")), "Isolated database required before entering Play Mode");
        Directory.CreateDirectory(Root);
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity");
        Write("editor-inventory", new { project = Application.dataPath, unity = Application.unityVersion,
            scenes = EditorBuildSettings.scenes.Select(s => new {s.path,s.enabled}),
            temporaryPipeline = File.ReadAllText("Packages/manifest.json").Contains("com.unity.pipeline"),
            androidSupported = BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android),
            buildTarget = EditorUserBuildSettings.activeBuildTarget.ToString() });
        SessionState.SetBool(Running, true);
        EditorApplication.isPlaying = true;
    }
    public static void ApplyAndRun()
    {
        Require(Application.dataPath.Replace('\\','/') == "D:/Cogna/jogos_empresa/Assets", "Wrong project");
        var scene=UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity");
        var canvas=scene.GetRootGameObjects().Single(g=>g.name=="Canvas");
        if(canvas.GetComponent<ProductionCanvasLayout>()==null) canvas.AddComponent<ProductionCanvasLayout>();
        canvas.GetComponent<UnityEngine.UI.CanvasScaler>().screenMatchMode=UnityEngine.UI.CanvasScaler.ScreenMatchMode.Expand;
        var camera=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Camera>(true)).Single(c=>c.CompareTag("MainCamera"));
        camera.clearFlags=CameraClearFlags.SolidColor;
        camera.backgroundColor=Game.Adapter.In.UI.Theme.GamePalette.Background;
        var legacyBackdrop=canvas.transform.Find("GameObject");
        Require(legacyBackdrop!=null && legacyBackdrop.GetComponent<UnityEngine.UI.Image>()!=null,"Expected legacy oversized backdrop");
        legacyBackdrop.gameObject.SetActive(false);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
        RunAndExit();
    }
    [InitializeOnLoadMethod] static void Reload()
    {
        if (!SessionState.GetBool(Running, false)) return;
        stage = -1; deadline = EditorApplication.timeSinceStartup + 1800;
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
        Application.logMessageReceived += Log;
        Canvas.willRenderCanvases += SimulateSafeArea;
    }
    static void Log(string message, string stack, LogType type)
    {
        if (type is LogType.Warning or LogType.Error or LogType.Exception or LogType.Assert)
            logs.Add(new { type = type.ToString(), message, stack, frame = Time.frameCount });
    }
    static void SimulateSafeArea()
    {
        if (Screen.width != 2340 || Screen.height != 1080) return;
        var safe = new Rect(90, 24, 2160, 1056);
        UnityEngine.Object.FindFirstObjectByType<ProductionCanvasLayout>()?.Apply(safe);
        foreach (var fitter in UnityEngine.Object.FindObjectsByType<SafeAreaFitter>(FindObjectsSortMode.None).Where(f=>f.isActiveAndEnabled))
            typeof(SafeAreaFitter).GetMethod("Apply", BindingFlags.Instance | BindingFlags.NonPublic, null,
                new[] { typeof(Rect), typeof(Vector2Int), typeof(bool) }, null).Invoke(fitter, new object[] { safe, new Vector2Int(2340,1080), true });
    }
    static void CaptureAudit(string name, string path)
    {
        var canvas = GameObject.Find("Canvas");
        var texts = canvas.GetComponentsInChildren<TMP_Text>().Where(t=>t.isActiveAndEnabled && !string.IsNullOrEmpty(t.text)).ToArray();
        foreach(var t in texts) t.ForceMeshUpdate();
        var images = canvas.GetComponentsInChildren<UnityEngine.UI.Image>().Where(i=>i.isActiveAndEnabled && i.sprite != null).ToArray();
        matrix.Add(new { suite, name, path, width=Screen.width, height=Screen.height, frame=Time.frameCount,
            state=GameManager.Instance.StateMachine.CurrentState.ToString(), panel=UIStateListener.Instance.CurrentPanel?.name,
            navigator=MenuNavigator.Instance.Current.ToString(), modal=SharedModalFocus.Active?.name,
            safeArea=Box(Screen.width==2340 ? new Rect(90,24,2160,1056) : Screen.safeArea),
            evidenceKind=Screen.width==2340 ? "Editor Android landscape safe-area simulation; not a device test" : "Editor Play Mode",
            advancing=Time.frameCount>lastFrame,
            texts=texts.Select(t=>new {path=PathOf(t.transform),t.text,font=t.font?.name,t.fontSize,color=ColorUtility.ToHtmlStringRGBA(t.color),
                style=t.fontStyle.ToString(),t.enableAutoSizing,t.isTextOverflowing,overflow=t.overflowMode.ToString(),
                masked=t.GetComponentInParent<UnityEngine.UI.RectMask2D>()!=null || t.GetComponentInParent<UnityEngine.UI.Mask>()!=null,
                rect=Box(((RectTransform)t.transform).rect)}).ToArray(),
            images=images.Select(i=>new {path=PathOf(i.transform),asset=AssetDatabase.GetAssetPath(i.sprite),i.preserveAspect,i.raycastTarget}).ToArray(),
            buttons=canvas.GetComponentsInChildren<UnityEngine.UI.Button>().Where(b=>b.IsActive()).Select(b=>new {path=PathOf(b.transform),b.interactable,
                state=b.GetComponent<SharedControlAppearance>()?.CurrentState}).ToArray(),
            scrolls=canvas.GetComponentsInChildren<UnityEngine.UI.ScrollRect>().Select(s=>new {path=PathOf(s.transform),s.vertical,s.horizontal,s.verticalNormalizedPosition,
                content=s.content!=null?Box(s.content.rect):null,viewport=s.viewport!=null?Box(s.viewport.rect):null}).ToArray() });
        lastFrame=Time.frameCount;
    }
    static void StartSuite(string name, Action run)
    {
        suite=name; UiValidationContext.OutputRoot=Root+"/"+name; Directory.CreateDirectory(UiValidationContext.OutputRoot);
        var requested=Environment.GetEnvironmentVariable("COGNA_UI20_VIEWPORTS");
        UiValidationContext.Viewports=string.IsNullOrEmpty(requested)?sizes:sizes.Where(s=>requested.Split(',').Contains(s.x+"x"+s.y)).ToArray();
        Require(UiValidationContext.Viewports.Length>0,"No matching QA viewport");
        UiValidationContext.OnCapture=CaptureAudit;
        var report=UiValidationContext.OutputRoot+"/"+(name=="supplemental"?"playmode-supplemental":"playmode")+".json";
        if(File.Exists(report)) File.Delete(report);
        Write("progress",new {suite,stage,frame=Time.frameCount});
        run();
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying) return;
        Application.runInBackground=true; EditorApplication.QueuePlayerLoopUpdate();
        EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Repaint();
        if(Time.frameCount<6) return;
        try
        {
            if(EditorApplication.timeSinceStartup>deadline) throw new TimeoutException("Final QA timed out in "+suite);
            if(stage==-1)
            {
                UI01Validation.RequireIsolatedStorage();
                stage=0;
                string requested=Environment.GetEnvironmentVariable("COGNA_UI20_SUITE");
                if(requested=="supplemental") {stage=1;StartSuite("supplemental",UI19NavigationValidation.RunSupplement);}
                else if(requested=="feedback") {stage=2;StartSuite("feedback",UI17FeedbackValidation.Run);}
                else if(requested=="dialogs") {stage=3;StartSuite("dialogs",UI18DialogValidation.Run);}
                else if(requested=="states") {stage=4;StartSuite("states",UI20ScreenStateValidation.Run);}
                else StartSuite("campaigns",UI19NavigationValidation.Run);
                return;
            }
            string report=UiValidationContext.OutputRoot+"/"+(suite=="supplemental"?"playmode-supplemental":"playmode")+".json";
            if(!File.Exists(report)) return;
            var result=Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(report));
            Require((bool)result["completed"], "Suite failed: "+suite+"; inspect "+report);
            if(!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("COGNA_UI20_SUITE"))) { Finish(true,null);return; }
            stage++;
            if(stage==1) StartSuite("supplemental",UI19NavigationValidation.RunSupplement);
            else if(stage==2) StartSuite("feedback",UI17FeedbackValidation.Run);
            else if(stage==3) StartSuite("dialogs",UI18DialogValidation.Run);
            else Finish(true,null);
        }
        catch(Exception e) { Finish(false,e.ToString()); }
    }
    static void Finish(bool passed,string error)
    {
        Write("execution-"+(suite??"startup"),new {passed,error,frame=Time.frameCount,project=Application.dataPath,unity=Application.unityVersion,
            database=DatabaseInitializer.DatabaseService?.Connection?.DatabasePath,key=Environment.GetEnvironmentVariable("COGNA_TEST_IDENTIFICATION_KEY"),
            captures=matrix.Count,sizes=UiValidationContext.Resolutions.Select(s=>new[] {s.x,s.y}),deviceTest=false});
        SessionState.SetBool(Running,false); EditorApplication.update-=Tick;
        Canvas.willRenderCanvases-=SimulateSafeArea; Application.logMessageReceived-=Log;
        Write("console-"+(suite??"startup"),logs); Write("matrix-"+(suite??"startup"),matrix);
        EditorApplication.Exit(passed?0:1);
    }
}
