using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Read-only typography census and fresh Game View evidence. Never saves production assets.</summary>
public static class Font01InventoryValidation
{
    const string Output = "output/ui-update/fonts/FONT-01";
    const string Running = "Cogna.Font01.Running";
    static int stage = -1, lastFrame;
    static double deadline;
    static readonly List<object> captures = new(), errors = new();
    static readonly Dictionary<string, JObject> runtime = new();
    static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;
    static string KeyOf(Transform t)
    {
        string part=t.name;
        if(t.parent!=null && t.parent.Cast<Transform>().Count(x=>x.name==t.name)>1)
            part+="["+t.GetSiblingIndex()+"]";
        return t.parent==null?part:KeyOf(t.parent)+"/"+part;
    }
    static void Write(string name, object data) => File.WriteAllText(Output + "/" + name + ".json", JsonConvert.SerializeObject(data, Formatting.Indented));
    static string Asset(UnityEngine.Object value) => value == null ? null : AssetDatabase.GetAssetPath(value);

    static Dictionary<int, List<object>> Bindings(GameObject[] roots)
    {
        var result = new Dictionary<int, List<object>>();
        foreach (var mb in roots.SelectMany(r => r.GetComponentsInChildren<MonoBehaviour>(true)).Where(m => m != null && m is not TMP_Text))
        {
            var so = new SerializedObject(mb); var p = so.GetIterator();
            while (p.Next(true))
            {
                if (p.propertyType != SerializedPropertyType.ObjectReference || p.objectReferenceValue is not TMP_Text text) continue;
                if (!result.TryGetValue(text.GetInstanceID(), out var list)) result[text.GetInstanceID()] = list = new();
                list.Add(new { component = mb.GetType().FullName, owner = PathOf(mb.transform), field = p.propertyPath });
            }
        }
        return result;
    }
    static JObject Text(TMP_Text t, string asset, string scope, Dictionary<int,List<object>> bindings)
    {
        var input = t.GetComponentInParent<TMP_InputField>(true);
        var rect = t.rectTransform; var corners = new Vector3[4]; rect.GetWorldCorners(corners);
        var screen = corners.Select(v => RectTransformUtility.WorldToScreenPoint(null, v)).ToArray();
        var mat = t.fontSharedMaterial;
        return JObject.FromObject(new {
            asset, scope, path = PathOf(t.transform), hierarchy_key=KeyOf(t.transform), component = t.GetType().FullName,
            global_id = GlobalObjectId.GetGlobalObjectIdSlow(t).ToString(), active_self = t.gameObject.activeSelf,
            active_in_hierarchy = t.gameObject.activeInHierarchy, enabled = t.enabled,
            text = t.text, font_asset = Asset(t.font), font_family = t.font?.faceInfo.familyName,
            font_face = t.font?.faceInfo.styleName, font_weight = (int)t.fontWeight, style = t.fontStyle.ToString(),
            material = Asset(mat), material_name = mat?.name, face_dilate = mat != null && mat.HasProperty("_FaceDilate") ? mat.GetFloat("_FaceDilate") : 0,
            font_size = t.fontSize, autosize = t.enableAutoSizing, min_size = t.fontSizeMin, max_size = t.fontSizeMax,
            rich_text = t.richText, overflow = t.overflowMode.ToString(), wrapping = t.textWrappingMode.ToString(),
            local_scale = new [] {rect.localScale.x, rect.localScale.y, rect.localScale.z},
            size = new [] {rect.rect.width, rect.rect.height},
            screen_rect = new [] {screen.Min(v=>v.x), Screen.height-screen.Max(v=>v.y),screen.Max(v=>v.x)-screen.Min(v=>v.x),screen.Max(v=>v.y)-screen.Min(v=>v.y)},
            input_slot = input == null ? null : input.placeholder == t ? "placeholder" : input.textComponent == t ? "editable" : "input-label",
            input_type = input?.contentType.ToString(), input_character_limit = input?.characterLimit,
            bindings = bindings.TryGetValue(t.GetInstanceID(),out var found) ? found : new List<object>()
        });
    }
    static void Census()
    {
        var dependencies = AssetDatabase.GetDependencies("Assets/Scenes/GameScene.unity", true);
        Write("production-dependencies",dependencies);
        var rows = new List<JObject>(); var assets = new List<object>();
        foreach (var path in AssetDatabase.FindAssets("t:Scene", new[]{"Assets/Scenes", "Assets/Settings/Scenes"}).Select(AssetDatabase.GUIDToAssetPath).OrderBy(x=>x))
        {
            var scene = EditorSceneManager.OpenPreviewScene(path); var roots = scene.GetRootGameObjects();
            var binding = Bindings(roots); var texts = roots.SelectMany(r=>r.GetComponentsInChildren<TMP_Text>(true)).ToArray();
            string scope = EditorBuildSettings.scenes.Any(s=>s.enabled && s.path==path) ? "production-scene" : "scene-outside-build";
            rows.AddRange(texts.Select(t=>Text(t,path,scope,binding))); assets.Add(new {path,scope,text_count=texts.Length});
            EditorSceneManager.ClosePreviewScene(scene);
        }
        foreach (var path in AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/Prefabs", "Assets/Scripts/Game/TESTES"}).Select(AssetDatabase.GUIDToAssetPath).OrderBy(x=>x))
        {
            var prefab = PrefabUtility.LoadPrefabContents(path);
            try {
                string scope = path.StartsWith("Assets/Scripts/Game/TESTES") ? "test-prefab" : "project-prefab";
                var texts=prefab.GetComponentsInChildren<TMP_Text>(true); var binding=Bindings(new[]{prefab});
                rows.AddRange(texts.Select(t=>Text(t,path,scope,binding)));
                assets.Add(new{path,scope,text_count=texts.Length,referenced_by_build_scene=dependencies.Contains(path)});
            } finally {PrefabUtility.UnloadPrefabContents(prefab);}
        }
        Write("serialized-texts",rows);Write("scanned-assets",assets);
        Write("font-assets",AssetDatabase.FindAssets("t:TMP_FontAsset").Select(AssetDatabase.GUIDToAssetPath).OrderBy(x=>x).Select(path=>{
            var f=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            return new{path,guid=AssetDatabase.AssetPathToGUID(path),family=f.faceInfo.familyName,face=f.faceInfo.styleName,
                source=Asset(f.sourceFontFile),population=f.atlasPopulationMode.ToString(),f.atlasWidth,f.atlasHeight,f.atlasPadding,f.boldStyle,f.boldSpacing,
                f.atlasRenderMode,scale=f.faceInfo.scale,characters=f.characterTable.Select(c=>c.unicode).ToArray(),
                fallbacks=f.fallbackFontAssetTable.Where(x=>x!=null).Select(Asset).ToArray(),material=Asset(f.material)};
        }).ToArray());
    }
    static JObject LiveState()
    {
        var scene=SceneManager.GetActiveScene();var roots=scene.GetRootGameObjects();var bindings=Bindings(roots);
        return JObject.FromObject(new{scene=scene.path,dirty=scene.isDirty,texts=roots.SelectMany(r=>r.GetComponentsInChildren<TMP_Text>(true)).Select(t=>Text(t,scene.path,"live-editor",bindings)).ToArray()});
    }
    [MenuItem("Tools/Jogos de Empresa/FONT-01/Inventory and capture in open Editor %&F1")]
    public static void RunInOpenEditor()
    {
        if(EditorApplication.isPlaying || SceneManager.GetActiveScene().path!="Assets/Scenes/GameScene.unity")throw new InvalidOperationException("Open GameScene outside Play Mode.");
        Directory.CreateDirectory(Output);Write("live-before",LiveState());
        SessionState.SetString("Cogna.Font01.OldDb",Environment.GetEnvironmentVariable("COGNA_TEST_DATABASE_PATH")??"");
        SessionState.SetString("Cogna.Font01.OldKey",Environment.GetEnvironmentVariable("COGNA_TEST_IDENTIFICATION_KEY")??"");
        Environment.SetEnvironmentVariable("COGNA_TEST_DATABASE_PATH",Path.GetFullPath(Output+"/qa-game.db"));
        Environment.SetEnvironmentVariable("COGNA_TEST_IDENTIFICATION_KEY","Cogna.Font01.QA.Identification");
        SessionState.SetBool("Cogna.Font01.Live",true);Census();SessionState.SetBool(Running,true);EditorApplication.isPlaying=true;
    }
    public static void RunAndExit()
    {
        if(string.IsNullOrEmpty(Environment.GetEnvironmentVariable("COGNA_TEST_DATABASE_PATH")))throw new InvalidOperationException("Isolated QA database required.");
        Directory.CreateDirectory(Output); Census();EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity");SessionState.SetBool(Running,true);EditorApplication.isPlaying=true;
    }
    [InitializeOnLoadMethod] static void Reload()
    {
        if(!SessionState.GetBool(Running,false)){
            if(SessionState.GetBool("Cogna.Font01.Restore",false))EditorApplication.delayCall+=RestoreLive;
            return;
        }
        stage=-1;deadline=EditorApplication.timeSinceStartup+900;EditorApplication.update-=Tick;EditorApplication.update+=Tick;
        Application.logMessageReceived+=Log;
    }
    static void Log(string message,string stack,LogType type)
    { if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(new{type=type.ToString(),message,stack}); }
    static void Capture(string name,string path)
    {
        var roots=SceneManager.GetActiveScene().GetRootGameObjects();var binding=Bindings(roots);
        foreach(var text in roots.SelectMany(r=>r.GetComponentsInChildren<TMP_Text>(true)))
        {
            var row=Text(text,"Assets/Scenes/GameScene.unity","runtime",binding);
            string key=(string)row["hierarchy_key"];
            if(!runtime.TryGetValue(key,out var entry)){runtime[key]=entry=row;entry["samples"]=new JArray();entry["observed_at"]=new JArray();}
            var samples=(JArray)entry["samples"];if(!samples.Values<string>().Contains(text.text))samples.Add(text.text);
            var observations=(JArray)entry["observed_at"];
            if(text.isActiveAndEnabled)observations.Add(new JObject{{"capture",name},{"frame",Time.frameCount},{"text",text.text},{"screen_rect",row["screen_rect"].DeepClone()}});
        }
        captures.Add(new{name,path,frame=Time.frameCount,advancing=Time.frameCount>lastFrame,width=Screen.width,height=Screen.height,captured_utc=DateTime.UtcNow.ToString("o")});lastFrame=Time.frameCount;
        Write("runtime-texts",runtime.Values);Write("captures",captures);
    }
    static void Start(string name,Action action)
    {
        UiValidationContext.OutputRoot=Output+"/"+name;Directory.CreateDirectory(UiValidationContext.OutputRoot);
        UiValidationContext.Viewports=new[]{new Vector2Int(1920,1080)};UiValidationContext.OnCapture=Capture;
        var report=UiValidationContext.OutputRoot+"/playmode.json";if(File.Exists(report))File.Delete(report);action();
    }
    static void Tick()
    {
        if(!EditorApplication.isPlaying)return;
        Application.runInBackground=true;EditorApplication.QueuePlayerLoopUpdate();
        EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Repaint();if(Time.frameCount<8)return;
        try {
            if(EditorApplication.timeSinceStartup>deadline)throw new TimeoutException("FONT-01 QA timed out.");
            if(stage==-1){UI01Validation.RequireIsolatedStorage();stage=0;Start("campaigns",UI19NavigationValidation.Run);return;}
            var path=UiValidationContext.OutputRoot+"/playmode.json";if(!File.Exists(path))return;
            var report=JObject.Parse(File.ReadAllText(path));if(!(bool)report["completed"])throw new InvalidOperationException("Native flow failed. See "+path);
            stage++;if(stage==1)Start("dialogs",UI18DialogValidation.Run);else Finish(true,null);
        } catch(Exception e){Finish(false,e.ToString());}
    }
    static void Finish(bool passed,string error)
    {
        SessionState.SetBool(Running,false);EditorApplication.update-=Tick;Application.logMessageReceived-=Log;
        Write("execution",new{passed,error,unity=Application.unityVersion,frame=Time.frameCount,captures=captures.Count,
            database=DatabaseInitializer.DatabaseService?.Connection?.DatabasePath,key=Environment.GetEnvironmentVariable("COGNA_TEST_IDENTIFICATION_KEY"),
            production_assets_saved=false,viewport=new[]{1920,1080},finished_utc=DateTime.UtcNow.ToString("o")});
        Write("console-errors",errors);
        if(SessionState.GetBool("Cogna.Font01.Live",false)){
            SessionState.SetBool("Cogna.Font01.Restore",true);EditorApplication.isPlaying=false;
            EditorApplication.delayCall+=RestoreLive;
        }else EditorApplication.Exit(passed?0:1);
    }
    static void RestoreLive()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode){EditorApplication.delayCall+=RestoreLive;return;}
        if(!SessionState.GetBool("Cogna.Font01.Restore",false))return;
        SessionState.SetBool("Cogna.Font01.Restore",false);SessionState.SetBool("Cogna.Font01.Live",false);
        Environment.SetEnvironmentVariable("COGNA_TEST_DATABASE_PATH",SessionState.GetString("Cogna.Font01.OldDb",""));
        Environment.SetEnvironmentVariable("COGNA_TEST_IDENTIFICATION_KEY",SessionState.GetString("Cogna.Font01.OldKey",""));
        Write("live-after",LiveState());Debug.Log("FONT-01 finished; original edit-mode scene restored without saving.");
    }
}
