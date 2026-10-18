#if DEVELOPMENT_BUILD || UNITY_EDITOR || FONT10_QA_PLAYER
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Adapter.In.UI;
using Game.Adapter.In.UI.Theme;
using Newtonsoft.Json;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Font10PlayerQa
{
    // Narrow adapters allow the unchanged campaign assertions to execute in a graphical Player.
    static class EditorApplication
    {
        public static bool isPlaying=>Application.isPlaying;
        public static double timeSinceStartup=>Time.realtimeSinceStartupAsDouble;
        public static event Action update;
        public static void QueuePlayerLoopUpdate(){}
        public static void Tick()=>update?.Invoke();
    }
    static class UiValidationContext
    {
        public static string OutputRoot;
        public static Vector2Int[] Resolutions={new(1920,1080)};
        public static string DirectoryFor(string task)=>OutputRoot;
        public static string FileFor(string task,string name)=>Path.Combine(OutputRoot,name);
        public static string CaptureName(string task,string name)=>Path.Combine(OutputRoot,name);
        public static Action<string,string> OnCapture;
    }
    static class UI01Validation
    {
        public static void RequireIsolatedStorage()
        {
            string path=Environment.GetEnvironmentVariable("COGNA_TEST_DATABASE_PATH"),key=Environment.GetEnvironmentVariable("COGNA_TEST_IDENTIFICATION_KEY");
            if(string.IsNullOrWhiteSpace(path)||string.IsNullOrWhiteSpace(key)||key=="PlayerSession.Identification"||Path.GetFullPath(path)==Path.Combine(Application.persistentDataPath,"game.db")||DatabaseInitializer.DatabaseService?.Connection?.DatabasePath!=path)throw new InvalidOperationException("Isolated Player storage required");
        }
        public static void SetGameView(int width,int height)=>Screen.SetResolution(width,height,FullScreenMode.Windowed);
        public static string Capture(string name)
        {
            RequireIsolatedStorage();string path=Path.GetFullPath(name+".png");Directory.CreateDirectory(Path.GetDirectoryName(path));
            var texture=ScreenCapture.CaptureScreenshotAsTexture();try{File.WriteAllBytes(path,texture.EncodeToPNG());}finally{UnityEngine.Object.Destroy(texture);}return path;
        }
    }
    /// <summary>Opt-in final Player proof. Dormant unless explicitly launched with --font10-qa.</summary>
    public sealed class Font10PlayerValidation:MonoBehaviour
    {
        static readonly List<object> matrix=new(),logs=new();
        static string output;
        static bool active;
        double deadline;
        int stage;
        static void Write(string name,object data)=>File.WriteAllText(Path.Combine(output,name+".json"),JsonConvert.SerializeObject(data,Formatting.Indented));
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]static void Init()
        {
            if(Application.isEditor||!Environment.GetCommandLineArgs().Contains("--font10-qa"))return;
            output=Environment.GetEnvironmentVariable("COGNA_FONT10_OUTPUT");if(string.IsNullOrWhiteSpace(output))throw new InvalidOperationException("FONT-10 output directory required");
            Directory.CreateDirectory(output);active=true;Application.runInBackground=true;
            var go=new GameObject("FONT-10 isolated Player validation");DontDestroyOnLoad(go);go.AddComponent<Font10PlayerValidation>();
        }
        void Awake(){deadline=Time.realtimeSinceStartupAsDouble+1800;Application.logMessageReceived+=Log;StartCoroutine(Loop());}
        static void Log(string message,string stack,LogType type){if(type is LogType.Warning or LogType.Error or LogType.Assert or LogType.Exception)logs.Add(new{type=type.ToString(),message,stack,frame=Time.frameCount});}
        IEnumerator Loop()
        {
            while(active){yield return new WaitForEndOfFrame();try{Tick();}catch(Exception e){Finish(e.ToString());}}
        }
        void Tick()
        {
            if(Time.realtimeSinceStartupAsDouble>deadline)throw new TimeoutException("Player validation timeout");
            if(stage==0)
            {
                if(Time.frameCount<20||Time.realtimeSinceStartupAsDouble<5||GameManager.Instance==null||DatabaseInitializer.DatabaseService?.Connection==null)return;
                if(DatabaseInitializer.DatabaseService.Connection.DatabasePath!=Environment.GetEnvironmentVariable("COGNA_TEST_DATABASE_PATH"))throw new InvalidOperationException("Player isolation was not compiled; no QA action may run.");
                UI01Validation.RequireIsolatedStorage();Assets();UiValidationContext.OutputRoot=Path.Combine(output,"campaigns");Directory.CreateDirectory(UiValidationContext.OutputRoot);UiValidationContext.OnCapture=Capture;CampaignValidation.Run();stage=1;
            }
            EditorApplication.Tick();
            string report=Path.Combine(UiValidationContext.OutputRoot,stage==1?"playmode.json":"playmode-supplemental.json");
            if(!File.Exists(report))return;var r=Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(report));if(!(bool)r["completed"]||(int)r["failures"]!=0)throw new InvalidOperationException("Player campaign assertions failed: "+report);
            if(stage==1){UiValidationContext.OutputRoot=Path.Combine(output,"supplemental");Directory.CreateDirectory(UiValidationContext.OutputRoot);CampaignValidation.RunSupplement();stage=2;}else Finish(null);
        }
        static string PathOf(Transform t)=>t.parent==null?t.name:PathOf(t.parent)+"/"+t.name;
        static void Capture(string name,string path)
        {
            var texts=GameObject.Find("Canvas").GetComponentsInChildren<TMP_Text>().Where(t=>t.isActiveAndEnabled&&!string.IsNullOrEmpty(t.text)).ToArray();
            var rows=texts.Select(t=>{t.ForceMeshUpdate();var b=t.GetComponent<TypographyRoleBinding>();return new{path=PathOf(t.transform),t.text,font=t.font?.name,material=t.fontSharedMaterial?.name,role=b?.Role.ToString(),t.fontSize,t.isTextOverflowing,masked=t.GetComponentInParent<UnityEngine.UI.RectMask2D>()!=null||t.GetComponentInParent<UnityEngine.UI.Mask>()!=null,glyphFonts=t.textInfo.characterInfo.Take(t.textInfo.characterCount).Where(c=>c.isVisible&&c.elementType==TMP_TextElementType.Character).Select(c=>c.fontAsset?.name).Distinct().ToArray(),valid=b!=null&&t.font==GameTypography.Resolve(b.Role).Font&&t.fontSharedMaterial==t.font.material&&(t.fontStyle&FontStyles.Bold)==0&&!t.enableAutoSizing};}).ToArray();
            if(rows.Any(t=>!t.valid||t.glyphFonts.Any(f=>f==null||!new[]{"Roboto-Regular","Roboto-Bold","RobotoCondensed-Regular","RobotoCondensed-Medium","RobotoCondensed-SemiBold","RobotoCondensed-Bold","NotoSans-Regular","NotoSans-Bold","NotoSansMath-Arrows"}.Contains(f))))throw new InvalidOperationException("Player typography invalid at "+name);
            matrix.Add(new{name,path,width=Screen.width,height=Screen.height,frame=Time.frameCount,state=GameManager.Instance.StateMachine.CurrentState.ToString(),texts=rows});Write("capture-matrix",matrix);
        }
        static void Assets()
        {
            var set=Resources.Load<ConceptFontSet>("UI/ConceptFontSet");if(set==null)throw new Exception("Missing Player font set");
            var fonts=new[]{set.robotoRegular,set.robotoBold,set.condensedRegular,set.condensedMedium,set.condensedSemiBold,set.condensedBold};var corpus=Environment.GetEnvironmentVariable("COGNA_FONT10_CORPUS");if(string.IsNullOrEmpty(corpus))throw new Exception("Corpus required");
            var rows=fonts.Select(f=>new{name=f?.name,family=f?.faceInfo.familyName,face=f?.faceInfo.styleName,sourceFontAbsent=f!=null&&f.sourceFontFile==null,neutral=f!=null&&f.faceInfo.scale==1&&f.boldStyle==0&&f.material.mainTexture==f.atlasTexture&&new[]{"_FaceDilate","_WeightNormal","_WeightBold","_OutlineWidth"}.All(p=>f.material.GetFloat(p)==0),staticAtlas=f!=null&&f.atlasPopulationMode==AtlasPopulationMode.Static,atlas=f?.atlasTexture?.name,shader=f?.material?.shader?.name,missing=corpus.Where(c=>!f.HasCharacter(c,true,false)).Select(c=>(int)c).Distinct().ToArray(),fallbacks=f?.fallbackFontAssetTable.Select(x=>new{x.name,sourceFontAbsent=x.sourceFontFile==null,population=x.atlasPopulationMode.ToString(),atlas=x.atlasTexture?.name,shader=x.material?.shader?.name,bound=x.material!=null&&x.material.mainTexture==x.atlasTexture}).ToArray()}).ToArray();
            if(rows.Any(r=>!r.sourceFontAbsent||!r.staticAtlas||!r.neutral||r.atlas==null||r.shader==null||r.missing.Length>0||r.fallbacks.Any(f=>!f.sourceFontAbsent||f.population!="Static"||f.atlas==null||f.shader==null||!f.bound)))throw new Exception("Player atlas/corpus missing");
            Write("font-assets",new{passed=true,fonts=rows,codepoints=corpus.Distinct().Count(),systemFonts=Font.GetOSInstalledFontNames().Where(n=>n.Contains("Roboto")||n.Contains("Noto")).ToArray(),fontSource="Bundled static TMP atlases; HasCharacter(..., searchFallbacks:true, tryAddCharacter:false). No OS font lookup by the production resolver."});
        }
        void Finish(string error)
        {
            active=false;Write("console",logs);Write("capture-matrix",matrix);
            bool logErrors=Newtonsoft.Json.Linq.JArray.FromObject(logs).Any(x=>new[]{"Error","Assert","Exception"}.Contains((string)x["type"]));
            Write("execution",new{passed=error==null&&!logErrors,error,unity=Application.unityVersion,platform=Application.platform.ToString(),frame=Time.frameCount,captures=matrix.Count,database=DatabaseInitializer.DatabaseService?.Connection?.DatabasePath,key=Environment.GetEnvironmentVariable("COGNA_TEST_IDENTIFICATION_KEY"),actualPlayer=!Application.isEditor,developmentBuild=Debug.isDebugBuild});
            PlayerPrefs.DeleteKey(Environment.GetEnvironmentVariable("COGNA_TEST_IDENTIFICATION_KEY"));PlayerPrefs.Save();StartCoroutine(Shutdown(error==null&&!logErrors?0:1));
        }
        static IEnumerator Shutdown(int code)
        {
            // Screenshot textures are destroyed at frame boundaries. Collect their
            // temporary resources while the graphics device still exists.
            yield return null;yield return null;
            yield return Resources.UnloadUnusedAssets();
            GC.Collect();GC.WaitForPendingFinalizers();
            yield return null;
            Application.logMessageReceived-=Log;Write("console",logs);
            Write("shutdown",new{resourcesCollected=true,exitCode=code,frame=Time.frameCount});
            Application.Quit(code);
        }
    }
}
#endif
