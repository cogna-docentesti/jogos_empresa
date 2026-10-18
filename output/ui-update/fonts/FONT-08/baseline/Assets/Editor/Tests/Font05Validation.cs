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
public static class Font05Validation
{
    const string Output = Font05Migration.Output;
    const string Key = "Cogna.Font05.";
    static readonly List<string> failures = new();
    static readonly List<object> captures = new(), checks = new();
    static readonly Queue<(string, Action)> steps = new();
    static double due;
    static int stage, lastFrame;
    static string resolution;
    static int suppressedExtras;
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
    [MenuItem("Tools/Jogos de Empresa/FONT-05/Validate saved consumers and live entry %&F7")]
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
        foreach (var type in new[] { typeof(Font04TypographyTests), typeof(UI01IdentificationTests), typeof(UI03SharedComponentsTests), typeof(UI04HubTests), typeof(UI05LocationTests) })
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
        var copy = "Assets/Scenes/Font05ReadOnlyCheck.unity";
        var active = SceneManager.GetActiveScene();
        File.Copy(production, copy); AssetDatabase.ImportAsset(copy, ImportAssetOptions.ForceSynchronousImport);
        var scene = EditorSceneManager.OpenScene(copy, OpenSceneMode.Additive);
        try { ValidateRoots(scene.GetRootGameObjects(), production, rows); }
        finally { EditorSceneManager.CloseScene(scene, true); SceneManager.SetActiveScene(active); AssetDatabase.DeleteAsset(copy); }
        foreach (var path in Font05Migration.Consumers.Select(c => (string)c["asset"]).Where(p => p.EndsWith(".prefab")).Distinct())
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try { ValidateRoots(new[] { root }, path, rows); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        Write("saved-verification", new { passed = true, count = rows.Count, rows });
    }
    [MenuItem("Tools/Jogos de Empresa/FONT-05/Validate scoped commit scene %&F8")]
    public static void ValidateCommitScene()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Use Edit mode.");
        var before=LiveState(); var original=SceneManager.GetActiveScene(); const string copy="Assets/Scenes/Font05CommitCheck.unity";
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
        Expect(before==LiveState(),"Commit-scene audit changed the live session");Write("commit-scene-verification",new{passed=true,serializedConsumers=rows.Count,liveSessionPreserved=true});
        Debug.Log("FONT-05 scoped commit scene PASS.");
    }
    static void ValidateRoots(GameObject[] roots, string asset, List<object> rows)
    {
        var texts = roots.SelectMany(r => r.GetComponentsInChildren<TMP_Text>(true)).ToLookup(t => PathOf(t.transform));
        foreach (var consumer in Font05Migration.Consumers.Where(c => (string)c["asset"] == asset))
        {
            var path = (string)consumer["hierarchy_key"];
            var matches=texts[path].ToArray(); Expect(matches.Length==1, "Missing/ambiguous frozen consumer " + path);
            var t=matches[0];
            Role(t, FontRoleMigration.ParseRole((string)consumer["target"]["role"]));
            float expectedSize=(float?)(consumer["font05_layout"]?["size"]) ?? (float)consumer["target"]["start_size"];
            Expect(Mathf.Abs(t.fontSize-expectedSize)<.01f,"Saved size: "+path);
            rows.Add(new { id = (string)consumer["id"], asset, path, font = t.font.name, role = t.GetComponent<TypographyRoleBinding>().Role.ToString(), size = t.fontSize });
        }
    }
    [InitializeOnLoadMethod] static void Reload()
    {
        if (SessionState.GetBool(Key + "Restoring", false)) { EditorApplication.delayCall += Restore; return; }
        if (!SessionState.GetBool(Key + "Playing", false)) return;
        stage = 0; lastFrame = -1; due = 0; suppressedExtras=0; failures.Clear(); captures.Clear(); checks.Clear();
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
    }
    static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;
    public static void Role(TMP_Text t, TypographyRole role)
    {
        Expect(t != null, "Missing text for " + role);
        var style = GameTypography.Resolve(role);
        Expect(t.GetComponent<TypographyRoleBinding>()?.Role == role && t.font == style.Font && t.fontSharedMaterial == style.Material, "Role/font/material: " + PathOf(t.transform));
        Expect(!t.enableAutoSizing && t.fontWeight == FontWeight.Regular && (t.fontStyle & FontStyles.Bold) == 0, "Synthetic weight/autosize: " + PathOf(t.transform));
    }
    static void Capture(string name)
    {
        foreach(var panel in new[]{Root.Find("Panel_Location"),Root.Find("Panel_Menu")}.Where(p=>p.gameObject.activeInHierarchy))
        {
            Rect Bounds(string path) { var r=(RectTransform)panel.Find(path);var corners=new Vector3[4];r.GetWorldCorners(corners);return Rect.MinMaxRect(corners[0].x,corners[0].y,corners[2].x,corners[2].y); }
            Expect(!Bounds("MapMaster/Node_Bank/Card/MapLabel").Overlaps(Bounds("MapMaster/Node_Store/Card/MapLabel")),"Bank/equipment marker labels overlap");
            var bank=panel.Find("MapMaster/Node_Bank/Card").GetComponent<MapMenuNode>();
            var target=(RectTransform)bank.transform.Find("MarkerTouchTarget");
            var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=target.TransformPoint(target.rect.center)},hits);
            Expect(hits.Count>0 && hits[0].gameObject.GetComponentInParent<MapMenuNode>()==bank,"Bank marker raycast blocked");
        }
        var relevant = new[] { "Panel_Identification", "Panel_Location", "Panel_Menu" }.Select(n => Root.Find(n)).Where(p => p.gameObject.activeInHierarchy).SelectMany(p => p.GetComponentsInChildren<TMP_Text>()).Where(t => t.enabled && !string.IsNullOrEmpty(t.text)).ToArray();
        foreach (var t in relevant)
        {
            var binding = t.GetComponent<TypographyRoleBinding>();
            Expect(binding != null, "Active scope text lacks role: " + PathOf(t.transform)); Role(t, binding.Role); t.ForceMeshUpdate();
            bool input = t.GetComponentInParent<TMP_InputField>() != null;
            Expect(!t.raycastTarget,"Text blocks control raycasts: "+PathOf(t.transform));
            Expect(input || !t.isTextOverflowing, "Text overflow: " + PathOf(t.transform) + " / " + t.text);
            Expect(input || (t.overflowMode != TextOverflowModes.Truncate && t.overflowMode != TextOverflowModes.Ellipsis), "Truncation: " + PathOf(t.transform));
        }
        Canvas.ForceUpdateCanvases();
        string path = UI01Validation.Capture("../fonts/FONT-05/" + name + "-" + resolution);
        captures.Add(new { name, path, resolution, frame = Time.frameCount, activeScene=SceneManager.GetActiveScene().path,
            inputs=Root.GetComponentsInChildren<TMP_InputField>().Select(i=>new{path=PathOf(i.transform),i.isFocused,i.stringPosition,i.selectionStringAnchorPosition,i.selectionStringFocusPosition,scrollX=i.textComponent.rectTransform.anchoredPosition.x,mask=i.textViewport.GetComponent<UnityEngine.UI.RectMask2D>()!=null}).ToArray(),
            texts = relevant.Select(t => new { path = PathOf(t.transform), t.text, font = t.font.name, role = t.GetComponent<TypographyRoleBinding>().Role.ToString(), size = t.fontSize, overflow = t.isTextOverflowing, rect = new[] { t.rectTransform.rect.width, t.rectTransform.rect.height } }).ToArray() });
        Write("captures", captures);
    }
    static void Fresh()
    {
        MenuNavigator.Instance.CloseAll(); GameSessionState.Clear(); PlayerSession.Clear(); PlayerSession.ClearSavedIdentification();
        new GameSessionService("font05-" + Guid.NewGuid(), "qa").CreateNewSession(); GameManager.Instance.BeginNewIdentification();
    }
    static void Plan()
    {
        steps.Clear();
        foreach (var size in new[] { new Vector2Int(1920,1080), new Vector2Int(1280,720), new Vector2Int(1600,1000) })
        {
            var s = size;
            Add("Viewport " + s, () => { resolution = s.x + "x" + s.y; UI01Validation.SetGameView(s.x, s.y); Fresh(); });
            Add("Placeholder and disabled registration", () => { Expect(!Field<Button>(Form,"buttonRegister").interactable,"Empty form enabled"); Capture("identification-placeholder"); });
            Add("Invalid accented form", () => { Name.text="João"; RA.text="123"; Form.Register(); });
            Add("Error and focus", () => { Expect(Name.GetComponent<IdentificationInputAppearance>().IsInvalid,"Missing invalid appearance"); Capture("identification-error"); });
            Add("Long accented name and RA", () => { Name.text="Ágata Gonçalves de São João Çéu Pág gyqp Ágata Souza".Substring(0,50); RA.text="1234567890"; Name.Select(); Name.ActivateInputField(); });
            Add("Caret and horizontal end", () => { Name.MoveTextEnd(false); Name.ForceLabelUpdate(); });
            Add("Observe caret", () => { Expect(Name.text.Length==50 && Name.isFocused && Name.stringPosition==Name.text.Length,"Caret did not reach end"); Expect(Name.textViewport!=null && Name.textViewport.GetComponent<UnityEngine.UI.RectMask2D>()!=null,"Input mask missing"); Expect(Name.textComponent.rectTransform.anchoredPosition.x<0,"Long input did not scroll horizontally"); Capture("identification-caret-end"); });
            Add("Select accented text", () => { Name.selectionStringAnchorPosition=4; Name.selectionStringFocusPosition=Name.text.Length; Name.ForceLabelUpdate(); });
            Add("Observe selection", () => { Expect(Name.selectionStringAnchorPosition!=Name.selectionStringFocusPosition,"No selection"); Capture("identification-selection"); Name.MoveTextStart(false); });
            Add("Horizontal start and register", () => { Capture("identification-caret-start"); Name.DeactivateInputField(); Field<Button>(Form,"buttonRegister").onClick.Invoke(); });
            Add("Location empty", () => { Expect(States.CurrentState==GameState.Config_Location,"Register did not navigate"); Capture("location-empty"); });
            Add("Select educational area", () => { var node=Location.Zones.Single(z=>z.id=="university").marker; ExecuteEvents.Execute(node.gameObject,new PointerEventData(EventSystem.current),ExecuteEvents.pointerClickHandler); });
            Add("Location selected and accented labels", () => { Capture("location-selected"); });
            Add("Back to retained identification", () => { Field<Button>(Location,"backButton").onClick.Invoke(); });
            Add("Identification reentry", () => { Expect(Name.text.StartsWith("Ágata") && RA.text=="1234567890","Identity lost"); Capture("identification-reentry"); Field<Button>(Form,"buttonRegister").onClick.Invoke(); });
            Add("Live hub fixture", () => {
                var session=GameSessionState.Current; session.restaurantType=RestaurantType.PODRAO; session.targetSegment=Segment.MEDIUM; session.locationZone=LocationZone.Educacao;
                session.currentRound=1; session.currentCash=25000; session.initialCapital=25000; session.companyName=PlayerSession.RestaurantName; session.studentName=PlayerSession.StudentName; session.studentRA=PlayerSession.StudentRA;
                var restaurant=Resources.LoadAll<RestaurantData>("Restaurants").Single(r=>r.type==RestaurantType.PODRAO);
                session.menuPricingJson=MenuPricingHelper.ToJson(MenuPricingHelper.FromProducts(restaurant.products)); PlayerSession.LoadDecisionsFrom(session); Expect(GameSessionState.Persist(),"Hub fixture persistence"); States.ForceState(GameState.Management_Hub);
            });
            Add("Hub typography and map", () => { var m=Root.Find("Panel_Menu/MapMaster"); Expect(m.localScale==Vector3.one*(896f/1080f) && ((RectTransform)m).anchoredPosition==new Vector2(-104,-88),"Map affine transform changed"); Capture("hub"); });
            foreach (var state in new[] { "hover", "pressed", "selected", "disabled", "normal" })
            {
                var st=state;
                Add("Navbar " + st, () => { var b=Root.Find("Panel_Menu/HubNavbar/NavTeam").GetComponent<Button>(); var a=b.GetComponent<SharedControlAppearance>(); b.interactable=st!="disabled"; SetState(a,st); Capture("navbar-"+st); b.interactable=true; SetState(a,"normal"); });
            }
            Add("HUD changed month cash and score", () => { var session=GameSessionState.Current; session.currentRound=3; session.currentCash=-12345.67f; session.reputationScore=72; session.alignmentScore=78; Root.Find("Panel_Menu").gameObject.SetActive(false); Root.Find("Panel_Menu").gameObject.SetActive(true); });
            Add("HUD refresh preserves roles", () => { foreach(var h in Root.GetComponentsInChildren<ConceptHudView>()) h.SendMessage("Refresh"); Root.Find("Panel_Menu").GetComponentInChildren<MenuHudView>(true).Refresh(); Capture("hub-negative-reentry"); });
        }
    }
    static void SetState(SharedControlAppearance a, string state)
    {
        var p=new PointerEventData(EventSystem.current){pointerId=-1,button=PointerEventData.InputButton.Left};
        a.OnPointerUp(p); a.OnPointerExit(p); a.SetSelected(state=="selected");
        if(state=="hover") a.OnPointerEnter(p);
        if(state=="pressed") a.OnPointerDown(p);
        a.Refresh(); Expect(a.CurrentState==state,"Control state "+state+" was "+a.CurrentState);
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying) return;
        Application.runInBackground=true; EditorApplication.QueuePlayerLoopUpdate();
        // The Editor can reload its unsaved scene snapshot in Play mode. This exact FONT-01 extra is absent from the production file.
        // Hide only its renderer in the disposable player, never the user's Edit-mode object or data.
        var extra=GameObject.Find("Canvas/Text (TMP)")?.GetComponent<TMP_Text>();
        if(extra!=null && extra.enabled && extra.text=="Restaurante") {extra.enabled=false;suppressedExtras++;}
        Canvas.ForceUpdateCanvases(); EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Repaint();
        if(Time.frameCount<12 || Time.frameCount==lastFrame || EditorApplication.timeSinceStartup<due) return;
        lastFrame=Time.frameCount; due=EditorApplication.timeSinceStartup+.22;
        try
        {
            UI01Validation.RequireIsolatedStorage();
            // Reload the saved production scene inside the disposable player; the user's extra in-memory TMP is restored when Play mode ends.
            if(stage==0) { SceneManager.LoadScene("GameScene"); stage=-1; due=EditorApplication.timeSinceStartup+.5; return; }
            if(stage==-1) { Plan(); stage=1; }
            if(stage==1 && steps.Count>0)
            {
                var step=steps.Dequeue(); try {step.Item2();checks.Add(new{name=step.Item1,passed=true,frame=Time.frameCount});}
                catch(Exception e) { failures.Add(step.Item1+": "+e);checks.Add(new{name=step.Item1,passed=false,error=e.ToString(),frame=Time.frameCount}); }
                Write("focused-checks",new{completed=steps.Count==0,count=checks.Count,checks,failures});return;
            }
            if(stage==1)
            {
                UI01Validation.SetGameView(1920,1080); resolution="1920x1080";
                UiValidationContext.OutputRoot=Output+"/campaigns"; Directory.CreateDirectory(UiValidationContext.OutputRoot);
                UiValidationContext.Viewports=new[]{new Vector2Int(1920,1080)}; UiValidationContext.OnCapture=(n,p)=> {
                    foreach(var t in Root.GetComponentsInChildren<TMP_Text>().Where(t=>t.GetComponent<TypographyRoleBinding>()!=null)) Role(t,t.GetComponent<TypographyRoleBinding>().Role);
                };
                var p=Output+"/campaigns/playmode.json"; if(File.Exists(p))File.Delete(p);
                UI19NavigationValidation.Run(); stage=2; due=EditorApplication.timeSinceStartup+1;return;
            }
            var reportPath=Output+"/campaigns/playmode.json"; if(!File.Exists(reportPath))return;
            var report=JObject.Parse(File.ReadAllText(reportPath)); Expect((bool)report["completed"] && (int)report["failures"]==0,"Existing campaign validation failed"); Finish();
        }
        catch(Exception e) { failures.Add(e.ToString()); Finish(); }
    }
    static void Finish()
    {
        EditorApplication.update-=Tick; UiValidationContext.OnCapture=null; UiValidationContext.Viewports=null; UiValidationContext.OutputRoot=null;
        SessionState.SetString(Key+"Failures",JsonConvert.SerializeObject(failures)); SessionState.SetBool(Key+"Playing",false); SessionState.SetBool(Key+"Restoring",true);
        EditorApplication.isPlaying=false; EditorApplication.delayCall+=Restore;
    }
    static void Restore()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode) {EditorApplication.delayCall+=Restore;return;}
        if(!SessionState.GetBool(Key+"Restoring",false))return;
        var s=SceneManager.GetSceneByPath("Assets/Scenes/GameScene.unity"); if(!s.IsValid()||s.rootCount!=7) {EditorApplication.delayCall+=Restore;return;}
        SessionState.SetBool(Key+"Restoring",false); failures.Clear(); failures.AddRange(JsonConvert.DeserializeObject<List<string>>(SessionState.GetString(Key+"Failures","[]")));
        Environment.SetEnvironmentVariable("COGNA_TEST_DATABASE_PATH",SessionState.GetString(Key+"OldDb","")); Environment.SetEnvironmentVariable("COGNA_TEST_IDENTIFICATION_KEY",SessionState.GetString(Key+"OldKey","")); Complete();
    }
    static void Complete()
    {
        bool preserved=SessionState.GetString(Key+"Before","")==LiveState(); if(!preserved)failures.Add("Open dirty scene changed during validation.");
        var scene=SceneManager.GetSceneByPath("Assets/Scenes/GameScene.unity"); EditorSceneManager.SaveScene(scene,Output+"/live-session-after.unity",true);
        object[] counts={0,0,0}; typeof(Editor).Assembly.GetType("UnityEditor.LogEntries").GetMethod("GetCountsByType",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,counts);
        if((int)counts[0]>0)failures.Add("Console errors: "+counts[0]);
        Write("execution",new{task="FONT-05",passed=failures.Count==0,failures,scenePreserved=preserved,scene.isDirty,scene.rootCount,consoleErrors=counts[0],consoleWarnings=counts[1],unity=Application.unityVersion,
            transientEditorExtraSuppression="Canvas/Text (TMP), Restaurante; renderer only in disposable Play mode; Edit-mode delta retained"});
        Debug.Log("FONT-05 "+(failures.Count==0?"PASS":"FAIL")+"; "+Output+"/execution.json");
    }
}
