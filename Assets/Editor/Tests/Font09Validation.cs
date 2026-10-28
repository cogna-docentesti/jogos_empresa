using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Game.Adapter.In.UI;
using Game.Adapter.In.Controllers;
using Game.Adapter.In.UI.Navigation;
using Game.Adapter.In.UI.Theme;
using Game.Domain.Service;
using Game.EditorTools;
using Game.Infrastructure.Session;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using Button = UnityEngine.UI.Button;

/// <summary>Real production controls and isolated persistence; preserves the open dirty Editor session.</summary>
public static class Font09Validation
{
    const string Output = Font09Migration.Output;
    const string Key = "Cogna.Font09.";
    static readonly List<string> failures = new();
    static readonly List<object> captures = new(), checks = new();
    static readonly Queue<(string, Action)> steps = new();
    static double due;
    static int stage, lastFrame;
    static string resolution;
    static int suppressedExtras;
    static double isolationDeadline;
    static Transform Root => GameObject.Find("Canvas").transform;
    static GameStateMachine States => GameManager.Instance.StateMachine;
    static T Field<T>(object o, string n) => (T)o.GetType().GetField(n, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(o);
    static UIIndentification Form => Root.Find("Panel_Identification").GetComponent<UIIndentification>();
    static TMP_InputField Name => Field<TMP_InputField>(Form, "studentNameInput");
    static TMP_InputField RA => Field<TMP_InputField>(Form, "raInput");
    static LocationScreenView Location => Root.Find("Panel_Location").GetComponent<LocationScreenView>();
    static void Expect(bool ok, string why) { if (!ok) throw new InvalidOperationException(why); }
    static void Write(string name, object value) => File.WriteAllText(Output + "/" + name + ".json", JsonConvert.SerializeObject(value, Formatting.Indented));
    public static void Status() => Write("editor-status",new {stage,lastFrame,frame=Time.frameCount,playing=EditorApplication.isPlaying,
        active=SessionState.GetBool(Key+"Playing",false),restoring=SessionState.GetBool(Key+"Restoring",false),due,now=EditorApplication.timeSinceStartup,
        database=DatabaseInitializer.DatabaseService?.Connection?.DatabasePath,expected=Environment.GetEnvironmentVariable("COGNA_TEST_DATABASE_PATH"),steps=steps.Count});
    public static void Abort(){failures.Add("Explicit local validation abort; no acceptance claimed.");Finish();}
    static void Add(string n, Action action) => steps.Enqueue((n, action));
    static string LiveState()
    {
        var scene = SceneManager.GetSceneByPath("Assets/Scenes/GameScene.unity");
        return JsonConvert.SerializeObject(new { scene.path, scene.isDirty, scene.rootCount,
            texts = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<TMP_Text>(true)).OrderBy(t => t.GetInstanceID()).Select(t => new { id = t.GetInstanceID(), json = JsonUtility.ToJson(t) }).ToArray() });
    }
    [MenuItem("Tools/Jogos de Empresa/FONT-09/Validate saved consumers and live entry")]
    public static void Run()
        => Begin(false);
    public static void RunFocused()
        => Begin(true);
    public static void RunFromFeedback() => Begin(false,1);
    public static void FinishPassedSuites() => Begin(false,3);
    static void Begin(bool retainPassedSuites,int startSuite=0)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Use Edit mode.");
        failures.Clear(); checks.Clear(); captures.Clear(); Directory.CreateDirectory(Output);
        SessionState.SetBool(Key+"RetainPassedSuites",retainPassedSuites);
        SessionState.SetInt(Key+"StartSuite",startSuite);
        typeof(Editor).Assembly.GetType("UnityEditor.LogEntries").GetMethod("Clear", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
        SessionState.SetString(Key + "Before", LiveState());
        try
        {
            RunTests();
            typeof(Editor).Assembly.GetType("UnityEditor.LogEntries").GetMethod("Clear", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
            ValidateSaved();
            if (failures.Count > 0) { Complete(); return; }
            SessionState.SetString(Key + "OldDb", Environment.GetEnvironmentVariable("COGNA_TEST_DATABASE_PATH") ?? "");
            SessionState.SetString(Key + "OldKey", Environment.GetEnvironmentVariable("COGNA_TEST_IDENTIFICATION_KEY") ?? "");
            Environment.SetEnvironmentVariable("COGNA_TEST_DATABASE_PATH", Path.GetFullPath(Output + "/qa-" + Guid.NewGuid() + ".db"));
            Environment.SetEnvironmentVariable("COGNA_TEST_IDENTIFICATION_KEY", Key + "Identification");
            PlayerPrefs.DeleteKey(Key + "Identification");
            SessionState.SetBool(Key + "Playing", true); EditorApplication.isPlaying = true;
        }
        catch (Exception e) { failures.Add(e.ToString()); Complete(); }
    }
    static void RunTests()
    {
        var results = new List<object>();
        foreach (var type in new[] { typeof(Font04TypographyTests), typeof(UI12EventTests), typeof(UI18DialogTests) })
        foreach (var method in type.GetMethods().Where(m => m.GetCustomAttributes<NUnit.Framework.TestAttribute>().Any() || m.GetCustomAttributes<NUnit.Framework.TestCaseAttribute>().Any() || m.GetCustomAttributes<NUnit.Framework.TestCaseSourceAttribute>().Any()))
        {
            var cases = method.GetCustomAttributes<NUnit.Framework.TestCaseAttribute>().Select(c => c.Arguments).ToList();
            foreach(var source in method.GetCustomAttributes<NUnit.Framework.TestCaseSourceAttribute>())
            {
                var member=type.GetProperty(source.SourceName,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static);
                var data=(System.Collections.IEnumerable)member.GetValue(null);
                foreach(NUnit.Framework.TestCaseData entry in data)cases.Add(entry.Arguments);
            }
            if (cases.Count == 0) cases.Add(Array.Empty<object>());
            foreach (var args in cases)
            {
                string name = type.Name + "." + method.Name + "(" + string.Join(",", args) + ")";
                var instance = Activator.CreateInstance(type); Exception error = null;
                try
                {
                    foreach (var setup in type.GetMethods().Where(m => m.GetCustomAttributes<NUnit.Framework.SetUpAttribute>().Any())) setup.Invoke(instance, null);
                    method.Invoke(instance, args);
                }
                catch (Exception e) { error = (e as TargetInvocationException)?.InnerException ?? e; }
                finally
                {
                    try { foreach (var teardown in type.GetMethods().Where(m => m.GetCustomAttributes<NUnit.Framework.TearDownAttribute>().Any())) teardown.Invoke(instance, null); }
                    catch (Exception e) { error ??= e; }
                }
                if (error != null) failures.Add(name + ": " + error);
                results.Add(new { name, passed = error == null, error = error?.ToString() });
            }
        }
        Write("edit-tests", new { count = results.Count, passed = failures.Count == 0, results });
    }
    static void ValidateSaved()
    {
        var rows = new List<object>();
        var production = "Assets/Scenes/GameScene.unity";
        var copy = "Assets/Scenes/Font09ReadOnlyCheck.unity";
        var active = SceneManager.GetActiveScene();
        File.Copy(production, copy); AssetDatabase.ImportAsset(copy, ImportAssetOptions.ForceSynchronousImport);
        var scene = EditorSceneManager.OpenScene(copy, OpenSceneMode.Additive);
        try { ValidateRoots(scene.GetRootGameObjects(), production, rows); }
        finally { EditorSceneManager.CloseScene(scene, true); SceneManager.SetActiveScene(active); AssetDatabase.DeleteAsset(copy); }
        foreach (var path in Font09Migration.Consumers.Select(c => (string)c["asset"]).Where(p => p.EndsWith(".prefab")).Distinct())
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try { ValidateRoots(new[] { root }, path, rows); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        Write("saved-verification", new { passed = true, count = rows.Count, rows });
    }
    public static void ValidateSavedOnly()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Use Edit mode.");
        StopSuites();
        var before=LiveState();failures.Clear();RunTests();ValidateSaved();
        Expect(failures.Count==0,string.Join("\n",failures));Expect(before==LiveState(),"Saved audit changed the live text session");
        EditorSceneManager.SaveScene(SceneManager.GetSceneByPath("Assets/Scenes/GameScene.unity"),Output+"/live-session-after.unity",true);
        Write("post-migration-verification",new{passed=true,liveTextSessionPreserved=true,
            note="Saved consumer faces, dimensions, tests and the original dirty text session verified again after the full runtime pass."});
    }
    [MenuItem("Tools/Jogos de Empresa/FONT-09/Validate scoped commit scene")]
    public static void ValidateCommitScene()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Use Edit mode.");
        typeof(Editor).Assembly.GetType("UnityEditor.LogEntries").GetMethod("Clear",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,null);
        var before=LiveState(); var original=SceneManager.GetActiveScene(); const string copy="Assets/Scenes/Font09CommitCheck.unity";
        File.Copy(Output+"/scene-for-index.unity",copy);AssetDatabase.ImportAsset(copy,ImportAssetOptions.ForceSynchronousImport);
        var scene=EditorSceneManager.OpenScene(copy,OpenSceneMode.Additive);var rows=new List<object>();
        try
        {
            ValidateRoots(scene.GetRootGameObjects(),"Assets/Scenes/GameScene.unity",rows);
            var canvas=scene.GetRootGameObjects().First(r=>r.name=="Canvas").transform;
            foreach(var registry in canvas.GetComponentsInChildren<PanelRegistry>(true))
                foreach(var binding in registry.Bindings)Expect(binding.root!=null,"Commit scene has a broken panel reference "+binding.id);
            Expect(!canvas.Find("SharedComponentsPreview").gameObject.activeSelf,"Showcase unexpectedly active");
            Expect(canvas.Find("Text (TMP)")==null,"User's unsaved extra leaked into commit scene");
            foreach(var root in scene.GetRootGameObjects())foreach(var t in root.GetComponentsInChildren<Transform>(true))
                Expect(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)==0,"Missing script "+PathOf(t));
        }
        finally {EditorSceneManager.CloseScene(scene,true);SceneManager.SetActiveScene(original);AssetDatabase.DeleteAsset(copy);}
        Expect(before==LiveState(),"Commit-scene audit changed the live session");
        object[] counts={0,0,0};typeof(Editor).Assembly.GetType("UnityEditor.LogEntries").GetMethod("GetCountsByType",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,counts);
        Expect((int)counts[0]==0 && (int)counts[1]==0,"Commit scene emitted errors or warnings");
        Write("commit-scene-verification",new{passed=true,serializedConsumers=rows.Count,liveSessionPreserved=true,consoleErrors=counts[0],consoleWarnings=counts[1]});
        Debug.Log("FONT-09 scoped commit scene PASS.");
    }
    static void ValidateRoots(GameObject[] roots, string asset, List<object> rows)
    {
        var texts = roots.SelectMany(r => r.GetComponentsInChildren<TMP_Text>(true)).ToLookup(t => PathOf(t.transform));
        foreach (var consumer in Font09Migration.Consumers.Where(c => (string)c["asset"] == asset))
        {
            var path = (string)consumer["hierarchy_key"];
            var matches=texts[path].ToArray(); Expect(matches.Length==1, "Missing/ambiguous frozen consumer " + path);
            var t=matches[0];
            Role(t, FontRoleMigration.ParseRole((string)consumer["target"]["role"]));
            float expectedSize=(float?)(consumer["font09_layout"]?["size"]) ?? (float?)(consumer["target"]["final_size"]) ?? (float)consumer["target"]["start_size"];
            Expect(Mathf.Abs(t.fontSize-expectedSize)<.01f,"Saved size: "+path);
            if(consumer["font09_layout"]?["width"]!=null)Expect(Mathf.Abs(t.rectTransform.rect.width-(float)consumer["font09_layout"]["width"])<.01f,"Saved width: "+path);
            rows.Add(new { id = (string)consumer["id"], asset, path, font = t.font.name, role = t.GetComponent<TypographyRoleBinding>().Role.ToString(), size = t.fontSize });
        }
    }
    [InitializeOnLoadMethod] static void Reload()
    {
        if (SessionState.GetBool(Key + "Restoring", false)) { EditorApplication.delayCall += Restore; return; }
        if (!SessionState.GetBool(Key + "Playing", false)) return;
        stage = 0; lastFrame = -1; due = 0; suppressedExtras=0; failures.Clear(); captures.Clear(); checks.Clear();
        if(SessionState.GetInt(Key+"StartSuite",0)>0)captures.AddRange(JArray.Parse(File.ReadAllText(Output+"/retained-captures.json")).Cast<object>());
        isolationDeadline=EditorApplication.timeSinceStartup+30;
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
    }
    static string PathOf(Transform t) => Font09Migration.PathOf(t);
    public static void Role(TMP_Text t, TypographyRole role)
    {
        Expect(t != null, "Missing text for " + role);
        var style = GameTypography.Resolve(role);
        Expect(t.GetComponent<TypographyRoleBinding>()?.Role == role && t.font == style.Font && t.fontSharedMaterial != null && t.fontSharedMaterial.GetInstanceID() == style.Material.GetInstanceID(), "Role/font/material: " + PathOf(t.transform));
        Expect(!t.enableAutoSizing && t.fontWeight == FontWeight.Regular && (t.fontStyle & FontStyles.Bold) == 0, "Synthetic weight/autosize: " + PathOf(t.transform));
    }
    static readonly Vector2Int[] Sizes = { new(1920,1080), new(1280,720), new(1600,1000) };
    static int suite;
    static string SuiteOutput => Output + "/" + new[] {"events", "feedback", "dialogs"}[suite];
    static readonly string[] Reports = {"playmode-events.json","playmode.json","playmode.json"};
    static int Writes => DatabaseInitializer.DatabaseService.Connection.ExecuteScalar<int>("SELECT total_changes()");
    static void CaptureObserved(string name, string path)
    {
        var owned=Font09Migration.Consumers.Where(c=>(string)c["asset"]=="Assets/Scenes/GameScene.unity").Select(c=>(string)c["hierarchy_key"]).ToHashSet();
        var relevant=Root.GetComponentsInChildren<TMP_Text>().Where(t=>t.enabled&&!string.IsNullOrEmpty(t.text) &&
            (owned.Contains(PathOf(t.transform)) || t.GetComponentInParent<EventOptionCardView>()!=null ||
             (SharedModalFocus.Active!=null && t.transform.IsChildOf(SharedModalFocus.Active.transform)))).ToArray();
        foreach(var t in relevant)
        {
            var b=t.GetComponent<TypographyRoleBinding>(); Expect(b!=null,"Missing role: "+PathOf(t.transform)); Role(t,b.Role); t.ForceMeshUpdate();
            bool input=t.GetComponentInParent<TMP_InputField>()!=null;
            Expect(input || !t.isTextOverflowing,"Overflow: "+PathOf(t.transform)+" / "+t.text);
            Expect(input || (t.overflowMode!=TextOverflowModes.Truncate && t.overflowMode!=TextOverflowModes.Ellipsis),"Truncation: "+PathOf(t.transform));
            Expect(t.transform.localScale==Vector3.one,"Condensed by scale: "+PathOf(t.transform));
            Expect(!t.text.Contains("<b>")&&!t.text.Contains("<font-weight"),"Inline cumulative bold: "+PathOf(t.transform));
            var pathOfText=PathOf(t.transform);
            if(pathOfText.EndsWith("/CreditTooltip/Message") || pathOfText.Contains("/ContextualHelp/Message") || pathOfText.EndsWith("/Feedback/Toast/Message"))
            {
                var surface=(RectTransform)t.transform.parent;
                var corners=new Vector3[4];t.rectTransform.GetWorldCorners(corners);
                foreach(var corner in corners)
                {
                    var point=surface.InverseTransformPoint(corner);
                    Expect(surface.rect.xMin-.5f<=point.x && point.x<=surface.rect.xMax+.5f && surface.rect.yMin-.5f<=point.y && point.y<=surface.rect.yMax+.5f,"Text escaped transient surface: "+pathOfText);
                }
            }
        }
        captures.Add(new{name,path,resolution=Screen.width+"x"+Screen.height,frame=Time.frameCount,
            texts=relevant.Select(t=>new{path=PathOf(t.transform),t.text,font=t.font.name,role=t.GetComponent<TypographyRoleBinding>().Role.ToString(),size=t.fontSize,overflow=t.isTextOverflowing,
                rect=new[]{t.rectTransform.rect.width,t.rectTransform.rect.height},preferred=new[]{t.preferredWidth,t.preferredHeight},t.textInfo.lineCount,
                baselines=t.textInfo.lineInfo.Take(t.textInfo.lineCount).Select(l=>new[]{l.ascender,l.baseline,l.descender}).ToArray(),
                material=t.fontSharedMaterial.name,t.characterSpacing,t.lineSpacing}).ToArray()});
        Write("captures",captures);
    }
    static void Capture(string name)
    {
        string path=UI01Validation.Capture("../fonts/FONT-09/"+name+"-"+resolution);
        CaptureObserved(name,path);
    }
    static void Fresh()
    {
        UI01Validation.RequireIsolatedStorage(); MenuNavigator.Instance.CloseAll();
        GameSessionState.Clear(); PlayerSession.Clear(); PlayerSession.ClearSavedIdentification();
        DatabaseInitializer.DatabaseService.Connection.DeleteAll<GameSessionEntity>();
        new GameSessionService("font09-"+Guid.NewGuid(),"qa").CreateNewSession(); GameManager.Instance.BeginNewIdentification();
    }
    static void StartSuite()
    {
        Fresh(); UI01Validation.SetGameView(1920,1080);
        Directory.CreateDirectory(SuiteOutput); UiValidationContext.OutputRoot=SuiteOutput;
        UiValidationContext.Viewports=Sizes; UiValidationContext.OnCapture=CaptureObserved;
        var file=SuiteOutput+"/"+Reports[suite];if(File.Exists(file))File.Delete(file);
        if(suite==0)UI12EventValidation.Run(); else if(suite==1)UI17FeedbackValidation.Run();else UI18DialogValidation.Run();
    }
    static void Plan()
    {
        steps.Clear();
        foreach(var size in Sizes)
        {
            var s=size;
            Add("Focused viewport "+s,()=>{resolution=s.x+"x"+s.y;UI01Validation.SetGameView(s.x,s.y);});
            Add("Long tooltip and isolated session",()=>{
                typeof(UI17FeedbackValidation).GetMethod("Fixture",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,null);
                var panel=Root.Find("Panel_Financial/CreditTooltip");
                panel.Find("Message").GetComponent<TMP_Text>().text="O crédito entra no caixa. "+string.Join(" ",Enumerable.Repeat("As parcelas incluem amortização e juros e são pagas no fechamento mensal. Confira as condições antes de confirmar sua escolha.",4));
                Root.Find("Panel_Financial/CreditHelp").GetComponent<Button>().Select();
            });
            Add("Long tooltip remains readable",()=>{Capture("long-tooltip");Root.Find("Panel_Financial/CreditHelp").GetComponent<SharedTooltip>().Dismiss();});
            Add("Long help body",()=>{
                var help=Root.Find("Panel_Financial").GetComponent<ContextualHelp>();
                Root.Find("Panel_Financial/ContextualHelp/Message").GetComponent<TMP_Text>().text=string.Join(" ",Enumerable.Repeat("Selecione uma opção e confira as condições. Você também pode continuar sem crédito; revise o valor das parcelas antes de confirmar.",3));
                help.Refresh(true,false);
            });
            Add("Long help remains readable",()=>{
                Capture("long-help");var help=Root.Find("Panel_Financial").GetComponent<ContextualHelp>();
                var button=Field<Button>(help,"dismiss");int before=Writes;
                typeof(UI17FeedbackValidation).GetMethod("Click",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{button,false});
                Expect(before==Writes && !help.IsVisible,"Long help dismissal changed persistence/visibility");
            });
            Add("Long feedback native face",()=>{FeedbackToast.Show(string.Join(" ",Enumerable.Repeat("Não foi possível concluir a operação. Confira suas escolhas e tente novamente; seu progresso continua preservado.",4)),FeedbackKind.Error);});
            Add("Long feedback close and reentry",()=>{
                Capture("long-toast");int before=Writes;var toast=FeedbackToast.Current;
                var face=GameTypography.Resolve(TypographyRole.Body).Font;int atlasCount=face.atlasTextures.Length;
                int material=Field<TMP_Text>(toast,"message").fontSharedMaterial.GetInstanceID();
                for(int i=0;i<8;i++){toast.Hide();toast.Display("Operação concluída; dados preservados.",FeedbackKind.Success);}
                Expect(face.atlasTextures.Length==atlasCount && Field<TMP_Text>(toast,"message").fontSharedMaterial.GetInstanceID()==material,"Feedback allocated a message material/atlas");
                Expect(before==Writes,"Toast reentry wrote SQLite");Capture("toast-reentry");toast.Hide();
            });
            Add("Actual persisted history",()=>{
                typeof(UI12EventValidation).GetMethod("Fixture",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,null);
                var db=DatabaseInitializer.DatabaseService.Connection;var service=new MonthlyEventDecisionService(db);
                var catalog=Resources.LoadAll<EventData>("Events").OrderBy(e=>e.id).Take(4).ToArray();
                for(int i=0;i<catalog.Length;i++)service.Confirm(GameSessionState.Current.sessionId,1,6+i*6,catalog[i],catalog[i].options[i%3]);
                States.ForceState(GameState.Management_Hub);MenuNavigator.Instance.Open(PanelId.EventHistory);
            });
            Add("Persisted history top and read only reentry",()=>{
                Capture("history-populated-top");var controller=Root.Find("Panel_Event").GetComponent<EventScreenController>();
                int before=Writes;controller.ShowHistory();Expect(before==Writes,"History consultation wrote SQLite");
                controller.GetComponent<EventScreenView>().HistoryScroll.verticalNormalizedPosition=0;
            });
            Add("Persisted history bottom",()=>{Capture("history-populated-bottom");MenuNavigator.Instance.CloseAll();});
            Add("Reparent live reset modal",()=>{
                MenuNavigator.Instance.Open(PanelId.Establishment);
                var view=Root.Find("Panel_Establishment").GetComponent<EstablishmentSummaryView>();
                Field<Button>(view,"resetButton").onClick.Invoke();
                Expect(Field<GameObject>(view,"resetWarning").transform.parent==Root,"Reset modal did not use its real Canvas host");
            });
            Add("Reparented dialog capture and cancel",()=>{
                Capture("reset-reparented");var view=Root.Find("Panel_Establishment").GetComponent<EstablishmentSummaryView>();
                int before=Writes;Field<Button>(view,"cancelResetButton").onClick.Invoke();
                Expect(!view.IsResetOpen && before==Writes,"Reparented dialog cancellation wrote SQLite");
            });
            Add("Inactive bindings and reapply",()=>{
                int before=Writes;
                foreach(var row in Font09Migration.Consumers.Where(c=>(string)c["asset"]=="Assets/Scenes/GameScene.unity"))
                {
                    var path=((string)row["hierarchy_key"]).Substring("Canvas/".Length);var text=Root.Find(path)?.GetComponent<TMP_Text>();
                    // ShowWarning moves this same serialized modal beneath the Canvas;
                    // retain its frozen inventory identity after cancellation/reentry.
                    if(text==null && path.StartsWith("Panel_Establishment/PanelReset/"))text=Root.Find(path.Substring("Panel_Establishment/".Length))?.GetComponent<TMP_Text>();
                    Expect(text!=null,"Missing inactive text "+path);var binding=text.GetComponent<TypographyRoleBinding>();
                    Role(text,FontRoleMigration.ParseRole((string)row["target"]["role"]));binding.Reapply();Role(text,binding.Role);
                }
                Expect(before==Writes,"Inactive typography wrote SQLite");
            });
        }
    }
    static void Tick()
    {
        if(!EditorApplication.isPlaying)return;
        Application.runInBackground=true;EditorApplication.QueuePlayerLoopUpdate();
        var extra=GameObject.Find("Canvas/Text (TMP)")?.GetComponent<TMP_Text>();if(extra!=null&&extra.enabled&&extra.text=="Restaurante"){extra.enabled=false;suppressedExtras++;}
        Canvas.ForceUpdateCanvases();
        GameObject.Find("Canvas")?.GetComponent<Game.Adapter.In.UI.Layout.ProductionCanvasLayout>()?.Apply(Screen.safeArea);
        Canvas.ForceUpdateCanvases();EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Repaint();
        if(Time.frameCount<12 || Time.frameCount==lastFrame || EditorApplication.timeSinceStartup<due)return;
        lastFrame=Time.frameCount;due=EditorApplication.timeSinceStartup+.3;
        try
        {
            // Editor update may resume before the new Player's Awake. Never inspect or mutate the previous connection.
            if(stage==0 && (GameManager.Instance==null || DatabaseInitializer.DatabaseService?.Connection?.DatabasePath!=Environment.GetEnvironmentVariable("COGNA_TEST_DATABASE_PATH")))
            {
                Expect(EditorApplication.timeSinceStartup<isolationDeadline,"Isolated Player services did not initialize within 30 seconds");return;
            }
            // The suites deliberately disconnect their already isolated connection for one
            // frame to exercise the real error/retry controls. Initial Awake/path gating
            // above stays mandatory; this coordinator only waits while that test runs.
            if(DatabaseInitializer.DatabaseService?.Connection==null)
                Expect(stage==1 && Environment.GetEnvironmentVariable("COGNA_TEST_IDENTIFICATION_KEY")==Key+"Identification","Unexpected disconnected storage");
            else UI01Validation.RequireIsolatedStorage();
            if(stage==0){SceneManager.LoadScene("GameScene");stage=-1;due=EditorApplication.timeSinceStartup+.6;return;}
            if(stage==-1){if(SessionState.GetBool(Key+"RetainPassedSuites",false)||SessionState.GetInt(Key+"StartSuite",0)>=3){Fresh();Plan();stage=2;}else{suite=SessionState.GetInt(Key+"StartSuite",0);StartSuite();stage=1;}return;}
            if(stage==1)
            {
                var path=SuiteOutput+"/"+Reports[suite];if(!File.Exists(path))return;
                var report=JObject.Parse(File.ReadAllText(path));Expect((bool)report["completed"]&&(int)report["failures"]==0,"Suite failed: "+path);
                if(++suite<3){StartSuite();return;}UiValidationContext.OnCapture=null;UiValidationContext.OutputRoot=null;UiValidationContext.Viewports=null;
                Plan();stage=2;return;
            }
            if(steps.Count>0)
            {
                var step=steps.Dequeue();step.Item2();checks.Add(new{name=step.Item1,passed=true,frame=Time.frameCount});
                Write("focused-checks",new{completed=steps.Count==0,count=checks.Count,checks,failures});return;
            }
            Finish();
        }
        catch(Exception e){failures.Add(e.ToString());Finish();}
    }

    static void Finish()
    {
        StopSuites();
        EditorApplication.update-=Tick; UiValidationContext.OnCapture=null; UiValidationContext.Viewports=null; UiValidationContext.OutputRoot=null;
        SessionState.SetString(Key+"Failures",JsonConvert.SerializeObject(failures)); SessionState.SetBool(Key+"Playing",false); SessionState.SetBool(Key+"Restoring",true);
        EditorApplication.isPlaying=false; EditorApplication.delayCall+=Restore;
    }
    static void StopSuites()
    {
        // Stop pending callbacks before releasing their scoped output context; an
        // aborted suite must never flush a partial report over historical UI QA.
        foreach(var type in new[]{typeof(UI12EventValidation),typeof(UI17FeedbackValidation),typeof(UI18DialogValidation)})
        {
            var method=type.GetMethod("Tick",BindingFlags.Static|BindingFlags.NonPublic);
            EditorApplication.update-=(EditorApplication.CallbackFunction)Delegate.CreateDelegate(typeof(EditorApplication.CallbackFunction),method);
        }
    }
    static void Restore()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode) {EditorApplication.delayCall+=Restore;return;}
        if(!SessionState.GetBool(Key+"Restoring",false))return;
        var s=SceneManager.GetSceneByPath("Assets/Scenes/GameScene.unity"); if(!s.IsValid()||s.rootCount!=7) {EditorApplication.delayCall+=Restore;return;}
        var viewport=JObject.Parse(File.ReadAllText(Output+"/session-geometry.json"))["viewport"];
        UI01Validation.SetGameView((int)viewport[0],(int)viewport[1]);due=EditorApplication.timeSinceStartup+1;
        EditorApplication.update-=FinishRestoring;EditorApplication.update+=FinishRestoring;
    }
    static void FinishRestoring()
    {
        EditorApplication.QueuePlayerLoopUpdate();EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Repaint();
        if(EditorApplication.timeSinceStartup<due)return;
        EditorApplication.update-=FinishRestoring;SessionState.SetBool(Key+"Restoring",false);
        failures.Clear();failures.AddRange(JsonConvert.DeserializeObject<List<string>>(SessionState.GetString(Key+"Failures","[]")));
        Environment.SetEnvironmentVariable("COGNA_TEST_DATABASE_PATH",SessionState.GetString(Key+"OldDb",""));Environment.SetEnvironmentVariable("COGNA_TEST_IDENTIFICATION_KEY",SessionState.GetString(Key+"OldKey",""));Complete();
    }
    static void Complete()
    {
        bool preserved=SessionState.GetString(Key+"Before","")==LiveState(); if(!preserved)failures.Add("Open dirty scene changed during validation.");
        var scene=SceneManager.GetSceneByPath("Assets/Scenes/GameScene.unity");
        EditorSceneManager.SaveScene(scene,Output+"/live-session-after.unity",true);
        object[] counts={0,0,0}; typeof(Editor).Assembly.GetType("UnityEditor.LogEntries").GetMethod("GetCountsByType",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,counts);
        if((int)counts[0]>0)failures.Add("Console errors: "+counts[0]);
        Write("execution",new{task="FONT-09",passed=failures.Count==0,failures,scenePreserved=preserved,scene.isDirty,scene.rootCount,consoleErrors=counts[0],consoleWarnings=counts[1],unity=Application.unityVersion,
            focusedOnly=SessionState.GetBool(Key+"RetainPassedSuites",false),suppressedExtras,retainedPassedSuites=SessionState.GetInt(Key+"StartSuite",0)>=3?new[]{"events","feedback","dialogs"}:SessionState.GetInt(Key+"StartSuite",0)>0?new[]{"events"}:Array.Empty<string>(),
            transientEditorExtraSuppression="Canvas/Text (TMP), Restaurante; renderer only in disposable Play mode; Edit-mode delta retained"});
        Debug.Log("FONT-09 "+(failures.Count==0?"PASS":"FAIL")+"; "+Output+"/execution.json");
    }
}
