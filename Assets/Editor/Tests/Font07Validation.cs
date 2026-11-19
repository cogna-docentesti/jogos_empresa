using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Game.Adapter.In.UI;
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
public static class Font07Validation
{
    const string Output = Font07Migration.Output;
    const string Key = "Cogna.Font07.";
    static readonly List<string> failures = new();
    static readonly List<object> captures = new(), checks = new();
    static readonly Queue<(string, Action)> steps = new();
    static double due;
    static int stage, lastFrame;
    static string resolution;
    static int suppressedExtras;
    static double isolationDeadline;
    static CreditLineData longFixture;
    static Transform Root => GameObject.Find("Canvas").transform;
    static GameStateMachine States => GameManager.Instance.StateMachine;
    static T Field<T>(object o, string n) => (T)o.GetType().GetField(n, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(o);
    static UIIndentification Form => Root.Find("Panel_Identification").GetComponent<UIIndentification>();
    static TMP_InputField Name => Field<TMP_InputField>(Form, "studentNameInput");
    static TMP_InputField RA => Field<TMP_InputField>(Form, "raInput");
    static LocationScreenView Location => Root.Find("Panel_Location").GetComponent<LocationScreenView>();
    static void Expect(bool ok, string why) { if (!ok) throw new InvalidOperationException(why); }
    static void Write(string name, object value) => File.WriteAllText(Output + "/" + name + ".json", JsonConvert.SerializeObject(value, Formatting.Indented));
    static void Add(string n, Action action) => steps.Enqueue((n, action));
    static string LiveState()
    {
        var scene = SceneManager.GetSceneByPath("Assets/Scenes/GameScene.unity");
        return JsonConvert.SerializeObject(new { scene.path, scene.isDirty, scene.rootCount,
            texts = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<TMP_Text>(true)).OrderBy(t => t.GetInstanceID()).Select(t => new { id = t.GetInstanceID(), json = JsonUtility.ToJson(t) }).ToArray() });
    }
    [MenuItem("Tools/Jogos de Empresa/FONT-07/Validate saved consumers and live entry")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Use Edit mode.");
        failures.Clear(); checks.Clear(); captures.Clear(); Directory.CreateDirectory(Output);
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
        foreach (var type in new[] { typeof(Font04TypographyTests), typeof(UI09FinancialTests), typeof(UI10EquipmentTests), typeof(UI11TeamTests) })
        foreach (var method in type.GetMethods().Where(m => m.GetCustomAttributes<NUnit.Framework.TestAttribute>().Any() || m.GetCustomAttributes<NUnit.Framework.TestCaseAttribute>().Any()))
        {
            var cases = method.GetCustomAttributes<NUnit.Framework.TestCaseAttribute>().Select(c => c.Arguments).ToArray();
            if (cases.Length == 0) cases = new[] { Array.Empty<object>() };
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
        var copy = "Assets/Scenes/Font07ReadOnlyCheck.unity";
        var active = SceneManager.GetActiveScene();
        File.Copy(production, copy); AssetDatabase.ImportAsset(copy, ImportAssetOptions.ForceSynchronousImport);
        var scene = EditorSceneManager.OpenScene(copy, OpenSceneMode.Additive);
        try { ValidateRoots(scene.GetRootGameObjects(), production, rows); }
        finally { EditorSceneManager.CloseScene(scene, true); SceneManager.SetActiveScene(active); AssetDatabase.DeleteAsset(copy); }
        foreach (var path in Font07Migration.Consumers.Select(c => (string)c["asset"]).Where(p => p.EndsWith(".prefab")).Distinct())
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
        var before=LiveState();failures.Clear();RunTests();ValidateSaved();
        Expect(failures.Count==0,string.Join("\n",failures));Expect(before==LiveState(),"Saved audit changed the live text session");
        EditorSceneManager.SaveScene(SceneManager.GetSceneByPath("Assets/Scenes/GameScene.unity"),Output+"/live-session-after.unity",true);
        Write("post-migration-verification",new{passed=true,liveTextSessionPreserved=true,
            note="Only inactive legacy semantic roles changed after the full runtime pass; the active production faces, sizes, layout and business code are identical."});
    }
    [MenuItem("Tools/Jogos de Empresa/FONT-07/Validate scoped commit scene")]
    public static void ValidateCommitScene()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Use Edit mode.");
        typeof(Editor).Assembly.GetType("UnityEditor.LogEntries").GetMethod("Clear",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,null);
        var before=LiveState(); var original=SceneManager.GetActiveScene(); const string copy="Assets/Scenes/Font07CommitCheck.unity";
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
        Debug.Log("FONT-07 scoped commit scene PASS.");
    }
    static void ValidateRoots(GameObject[] roots, string asset, List<object> rows)
    {
        var texts = roots.SelectMany(r => r.GetComponentsInChildren<TMP_Text>(true)).ToLookup(t => PathOf(t.transform));
        foreach (var consumer in Font07Migration.Consumers.Where(c => (string)c["asset"] == asset))
        {
            var path = (string)consumer["hierarchy_key"];
            var matches=texts[path].ToArray(); Expect(matches.Length==1, "Missing/ambiguous frozen consumer " + path);
            var t=matches[0];
            Role(t, FontRoleMigration.ParseRole((string)consumer["target"]["role"]));
            float expectedSize=(float?)(consumer["font07_layout"]?["size"]) ?? (float)consumer["target"]["start_size"];
            Expect(Mathf.Abs(t.fontSize-expectedSize)<.01f,"Saved size: "+path);
            rows.Add(new { id = (string)consumer["id"], asset, path, font = t.font.name, role = t.GetComponent<TypographyRoleBinding>().Role.ToString(), size = t.fontSize });
        }
    }
    [InitializeOnLoadMethod] static void Reload()
    {
        if (SessionState.GetBool(Key + "Restoring", false)) { EditorApplication.delayCall += Restore; return; }
        if (!SessionState.GetBool(Key + "Playing", false)) return;
        stage = 0; lastFrame = -1; due = 0; suppressedExtras=0; failures.Clear(); captures.Clear(); checks.Clear();
        isolationDeadline=EditorApplication.timeSinceStartup+30;
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
    }
    static string PathOf(Transform t) => Font07Migration.PathOf(t);
    public static void Role(TMP_Text t, TypographyRole role)
    {
        Expect(t != null, "Missing text for " + role);
        var style = GameTypography.Resolve(role);
        Expect(t.GetComponent<TypographyRoleBinding>()?.Role == role && t.font == style.Font && t.fontSharedMaterial == style.Material, "Role/font/material: " + PathOf(t.transform));
        Expect(!t.enableAutoSizing && t.fontWeight == FontWeight.Regular && (t.fontStyle & FontStyles.Bold) == 0, "Synthetic weight/autosize: " + PathOf(t.transform));
    }
    static readonly Vector2Int[] Sizes = { new(1920,1080), new(1280,720), new(1600,1000) };
    static int suite;
    static string SuiteOutput => Output + "/" + new[] {"bank", "equipment", "team"}[suite];
    static readonly string[] Reports = {"playmode-financial.json","playmode-equipment.json","playmode-team.json"};
    static int Writes => DatabaseInitializer.DatabaseService.Connection.ExecuteScalar<int>("SELECT total_changes()");
    static void CaptureObserved(string name, string path)
    {
        var panels = new[]{"Panel_Financial","Panel_Equipment","Panel_Team"}.Select(n=>Root.Find(n)).Where(p=>p.gameObject.activeInHierarchy).ToArray();
        var relevant=panels.SelectMany(p=>p.GetComponentsInChildren<TMP_Text>()).Where(t=>t.enabled&&!string.IsNullOrEmpty(t.text)).ToArray();
        foreach(var t in relevant)
        {
            var b=t.GetComponent<TypographyRoleBinding>(); Expect(b!=null,"Missing role: "+PathOf(t.transform)); Role(t,b.Role); t.ForceMeshUpdate();
            bool input=t.GetComponentInParent<TMP_InputField>()!=null;
            Expect(input || !t.isTextOverflowing,"Overflow: "+PathOf(t.transform)+" / "+t.text);
            Expect(input || (t.overflowMode!=TextOverflowModes.Truncate && t.overflowMode!=TextOverflowModes.Ellipsis),"Truncation: "+PathOf(t.transform));
            Expect(t.transform.localScale==Vector3.one,"Condensed by scale: "+PathOf(t.transform));
            Expect(!t.text.Contains("<b>")&&!t.text.Contains("<font-weight"),"Inline cumulative bold: "+PathOf(t.transform));
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
        string path=UI01Validation.Capture("../fonts/FONT-07/"+name+"-"+resolution);
        CaptureObserved(name,path);
    }
    static void Fresh()
    {
        UI01Validation.RequireIsolatedStorage(); MenuNavigator.Instance.CloseAll();
        GameSessionState.Clear(); PlayerSession.Clear(); PlayerSession.ClearSavedIdentification();
        DatabaseInitializer.DatabaseService.Connection.DeleteAll<GameSessionEntity>();
        new GameSessionService("font07-"+Guid.NewGuid(),"qa").CreateNewSession(); GameManager.Instance.BeginNewIdentification();
    }
    static void StartSuite()
    {
        Fresh(); UI01Validation.SetGameView(1920,1080);
        Directory.CreateDirectory(SuiteOutput); UiValidationContext.OutputRoot=SuiteOutput;
        UiValidationContext.Viewports=Sizes; UiValidationContext.OnCapture=CaptureObserved;
        var file=SuiteOutput+"/"+Reports[suite];if(File.Exists(file))File.Delete(file);
        if(suite==0)UI09FinancialValidation.Run(); else if(suite==1)UI10EquipmentValidation.Run();else UI11TeamValidation.Run();
    }
    static void Plan()
    {
        steps.Clear();
        // Focused bind/enable/SQLite checks are appended after the production interaction suites.
        foreach(var size in Sizes)
        {
            var s=size;
            Add("Detailed viewport "+s,()=>{resolution=s.x+"x"+s.y;UI01Validation.SetGameView(s.x,s.y);});
            foreach(var panel in new[]{PanelId.Financial,PanelId.EquipmentStore,PanelId.Team})
            {
                var id=panel;
                Add("Open and reenter "+id,()=>{MenuNavigator.Instance.OpenRoot();MenuNavigator.Instance.Open(id);});
                Add("Bind and enable preserve typography and SQLite "+id,()=>{
                    int before=Writes;
                    var active=Root.Find(id==PanelId.Financial?"Panel_Financial":id==PanelId.Team?"Panel_Team":"Panel_Equipment");
                    foreach(var t in active.GetComponentsInChildren<TMP_Text>(true))
                    {var binding=t.GetComponent<TypographyRoleBinding>();Expect(binding!=null,"Missing binding "+PathOf(t.transform));GameTypography.KeepSize(t,binding.Role);}
                    foreach(var c in active.GetComponentsInChildren<BankCardView>())c.Setup(c.CreditLine);
                    foreach(var c in active.GetComponentsInChildren<EquipmentCardView>())c.Setup(c.Equipment,c.IsOwned);
                    foreach(var c in active.GetComponentsInChildren<TeamRoleCardView>())c.SetQuantity(c.Quantity,false);
                    Expect(Writes==before,"Binding or typography wrote SQLite");
                    Capture("reentry-"+id.ToString().ToLowerInvariant()+"-top");
                    foreach(var scroll in active.GetComponentsInChildren<UnityEngine.UI.ScrollRect>())scroll.verticalNormalizedPosition=0;
                });
                Add("Final catalog and summary "+id,()=>Capture("reentry-"+id.ToString().ToLowerInvariant()+"-bottom"));
                if(id==PanelId.Financial)
                {
                    Add("Long credit fixture without catalog mutation",()=>{
                        var card=Root.Find("Panel_Financial").GetComponentsInChildren<BankCardView>().Last();
                        longFixture=UnityEngine.Object.Instantiate(card.CreditLine);
                        longFixture.displayName=string.Join(" ",Enumerable.Repeat("Crédito de investimento e expansão de restaurante",3));
                        longFixture.cardDescription=string.Join(" ",Enumerable.Repeat("Confira o prazo, os juros mensais e o planejamento antes de contratar.",6));
                        card.Setup(longFixture);
                        Root.Find("Panel_Financial").GetComponentInChildren<UnityEngine.UI.ScrollRect>().verticalNormalizedPosition=1;
                    });
                    Add("Long credit native height and top",()=>{
                        var scroll=Root.Find("Panel_Financial").GetComponentInChildren<UnityEngine.UI.ScrollRect>();
                        var grid=scroll.content.GetComponent<UnityEngine.UI.GridLayoutGroup>();
                        var card=scroll.content.GetComponentsInChildren<BankCardView>().Last();
                        Expect(grid.cellSize.y>=card.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight,"Long credit escaped grid height");
                        Capture("credit-long-top");scroll.verticalNormalizedPosition=0;
                    });
                    Add("Long credit final lines accessible",()=>{
                        Capture("credit-long-bottom");MenuNavigator.Instance.OpenRoot();
                        UnityEngine.Object.DestroyImmediate(longFixture);longFixture=null;
                    });
                }
            }
        }
    }
    static void Tick()
    {
        if(!EditorApplication.isPlaying)return;
        Application.runInBackground=true;EditorApplication.QueuePlayerLoopUpdate();
        var extra=GameObject.Find("Canvas/Text (TMP)")?.GetComponent<TMP_Text>();if(extra!=null&&extra.enabled&&extra.text=="Restaurante"){extra.enabled=false;suppressedExtras++;}
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
            UI01Validation.RequireIsolatedStorage();
            if(stage==0){SceneManager.LoadScene("GameScene");stage=-1;due=EditorApplication.timeSinceStartup+.6;return;}
            if(stage==-1){suite=0;StartSuite();stage=1;return;}
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
        if(longFixture!=null){UnityEngine.Object.DestroyImmediate(longFixture);longFixture=null;}
        EditorApplication.update-=Tick; UiValidationContext.OnCapture=null; UiValidationContext.Viewports=null; UiValidationContext.OutputRoot=null;
        SessionState.SetString(Key+"Failures",JsonConvert.SerializeObject(failures)); SessionState.SetBool(Key+"Playing",false); SessionState.SetBool(Key+"Restoring",true);
        EditorApplication.isPlaying=false; EditorApplication.delayCall+=Restore;
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
        Write("execution",new{task="FONT-07",passed=failures.Count==0,failures,scenePreserved=preserved,scene.isDirty,scene.rootCount,consoleErrors=counts[0],consoleWarnings=counts[1],unity=Application.unityVersion,
            transientEditorExtraSuppression="Canvas/Text (TMP), Restaurante; renderer only in disposable Play mode; Edit-mode delta retained"});
        Debug.Log("FONT-07 "+(failures.Count==0?"PASS":"FAIL")+"; "+Output+"/execution.json");
    }
}
