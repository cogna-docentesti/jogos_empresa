using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Game.Adapter.In.UI.Theme;

/// <summary>FONT-03 acceptance in the existing Editor. Temporary live-screen font swaps are restored; no scene is saved.</summary>
public static class Font03AssetValidation
{
    const string Output = ConceptFontAssetGenerator.Output;
    static readonly List<string> failures = new List<string>();
    static readonly List<object> captures = new List<object>();
    static readonly List<object> checks = new List<object>();
    static string before;
    static TMP_Text[] live;
    static Dictionary<TMP_Text, string> originals;
    static GameObject sheet;
    static double due;
    static int stage;
    static double started;
    const string Playing = "Cogna.Font03.Playing";
    const string Restoring = "Cogna.Font03.Restoring";
    static int lastFrame;
    static string Hash(byte[] data) { using var sha = SHA256.Create(); return BitConverter.ToString(sha.ComputeHash(data)).Replace("-", "").ToLowerInvariant(); }
    static void Write(string name, object data) => File.WriteAllText(Output + "/" + name + ".json", JsonConvert.SerializeObject(data, Formatting.Indented) + "\n");
    static TMP_Text[] SceneTexts() => Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt).SelectMany(s => s.GetRootGameObjects()).SelectMany(r => r.GetComponentsInChildren<TMP_Text>(true)).OrderBy(t => t.GetInstanceID()).ToArray();
    static string SceneState()
    {
        var texts = SceneTexts();
        return JsonConvert.SerializeObject(new { scenes = Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt).Select(s => new {s.path, s.isDirty, s.rootCount}),
            texts = texts.Length, fingerprint = Hash(Encoding.UTF8.GetBytes(string.Join("\n", texts.Select(t => JsonUtility.ToJson(t))))),
            unsavedRestaurant = texts.Count(t => t.name == "Text (TMP)" && t.text == "Restaurante") });
    }
    static string Signature()
    {
        return JsonConvert.SerializeObject(ConceptFontAssetGenerator.Faces.Concat(ConceptFontAssetGenerator.Fallbacks).Select(name => {
            var f = ConceptFontAssetGenerator.Load(name);
            return new {name, font = ConceptFontAssetGenerator.Identity(f), material = ConceptFontAssetGenerator.Identity(f.material), atlas = ConceptFontAssetGenerator.Identity(f.atlasTexture),
                pixels = Hash(f.atlasTexture.GetRawTextureData<byte>().ToArray()), characters = f.characterTable.Select(c => new {c.unicode, c.glyphIndex}),
                glyphs = f.glyphTable.Select(g => new {g.index, g.metrics, g.glyphRect, g.scale, g.atlasIndex})};
        }));
    }
    static void Require(bool value, string message) { if (!value) failures.Add(message); }
    [MenuItem("Tools/Jogos de Empresa/FONT-03/Generate and validate assets %&F3")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().path != "Assets/Scenes/GameScene.unity")
            throw new InvalidOperationException("Use the existing GameScene in Edit mode.");
        failures.Clear(); captures.Clear(); checks.Clear(); Directory.CreateDirectory(Output); before = SceneState(); started = EditorApplication.timeSinceStartup;
        Write("live-before", JObject.Parse(before));
        try
        {
            ConceptFontAssetGenerator.GenerateAll();
            string first = Signature();
            File.Copy(Output + "/generation.json", Output + "/generation-first.json", true);
            ConceptFontAssetGenerator.GenerateAll();
            string regenerated = Signature();
            Require(first == regenerated, "Regeneration changed object identities, metrics or atlas pixels.");
            foreach (var name in ConceptFontAssetGenerator.Faces.Concat(ConceptFontAssetGenerator.Fallbacks))
                AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(ConceptFontAssetGenerator.Load(name)), ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            string reimported = Signature();
            Require(regenerated == reimported, "Reimport changed object identities, metrics or atlas pixels.");
            Write("regeneration", new {passed = first == regenerated && regenerated == reimported, first = Hash(Encoding.UTF8.GetBytes(first)), regenerated = Hash(Encoding.UTF8.GetBytes(regenerated)), reimported = Hash(Encoding.UTF8.GetBytes(reimported)), fontMaterialAtlasLocalIdsStable = first == regenerated, dataStable = regenerated == reimported});
            Validate();
            Require(before == SceneState(), "Generation/validation changed the open scene.");
            StartCapture();
        }
        catch (Exception error) { failures.Add(error.ToString()); Finish(); }
    }
    [MenuItem("Tools/Jogos de Empresa/FONT-03/Validate existing assets and capture in Play %&F4")]
    public static void RunExisting()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().path != "Assets/Scenes/GameScene.unity") throw new InvalidOperationException("Use the existing GameScene in Edit mode.");
        failures.Clear(); captures.Clear(); checks.Clear(); before=SceneState(); started=EditorApplication.timeSinceStartup;
        Write("live-before",JObject.Parse(before));
        try {Validate(); Require(before==SceneState(),"Validation changed the open scene."); StartCapture();}
        catch(Exception error) {failures.Add(error.ToString()); Finish();}
    }
    static void StartCapture()
    {
        SessionState.SetString("Cogna.Font03.Before",before);
        SessionState.SetString("Cogna.Font03.Failures",JsonConvert.SerializeObject(failures));
        SessionState.SetString("Cogna.Font03.OldDb",Environment.GetEnvironmentVariable("COGNA_TEST_DATABASE_PATH")??"");
        SessionState.SetString("Cogna.Font03.OldKey",Environment.GetEnvironmentVariable("COGNA_TEST_IDENTIFICATION_KEY")??"");
        SessionState.SetString("Cogna.Font03.Started",started.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Environment.SetEnvironmentVariable("COGNA_TEST_DATABASE_PATH",Path.GetFullPath(Output+"/qa-game.db"));
        Environment.SetEnvironmentVariable("COGNA_TEST_IDENTIFICATION_KEY","Cogna.Font03.QA.Identification");
        SessionState.SetBool(Playing,true); EditorApplication.isPlaying=true;
    }
    [InitializeOnLoadMethod] static void Reload()
    {
        if(SessionState.GetBool(Restoring,false)) {EditorApplication.delayCall+=RestoreAfterPlay; return;}
        if(!SessionState.GetBool(Playing,false)) return;
        before=SessionState.GetString("Cogna.Font03.Before","");
        failures.Clear(); failures.AddRange(JsonConvert.DeserializeObject<List<string>>(SessionState.GetString("Cogna.Font03.Failures","[]")));
        captures.Clear(); stage=-2; lastFrame=-1;
        started=double.Parse(SessionState.GetString("Cogna.Font03.Started","0"),System.Globalization.CultureInfo.InvariantCulture);
        EditorApplication.update-=Tick; EditorApplication.update+=Tick;
    }
    static void Validate()
    {
        uint[] corpus = ConceptFontAssetGenerator.Corpus;
        var sourceGaps = JObject.Parse(File.ReadAllText(Output + "/fallback-binaries.json"));
        var gap = new HashSet<uint>(sourceGaps["codepoints"].Values<uint>());
        var symbols = new HashSet<uint>(sourceGaps["symbols_codepoints"].Values<uint>());
        foreach (string name in ConceptFontAssetGenerator.Faces)
        {
            var font = ConceptFontAssetGenerator.Load(name);
            var resolution = new List<object>();
            foreach (uint code in corpus)
            {
                var c = TMP_FontAssetUtilities.GetCharacterFromFontAsset(code, font, true, FontStyles.Normal, FontWeight.Regular, out bool alternate);
                string resolved = c?.textAsset?.name;
                string expected = symbols.Contains(code) ? "NotoSansMath-Arrows" : gap.Contains(code) ? (name.EndsWith("Bold") ? "NotoSans-Bold" : "NotoSans-Regular") : name;
                Require(resolved == expected, name + ": " + code.ToString("X4") + " resolved " + resolved + "; expected " + expected);
                resolution.Add(new {codepoint = "U+" + code.ToString("X4"), resolved, expected, alternate});
            }
            Require(font.atlasPopulationMode == AtlasPopulationMode.Static && font.atlasTextureCount == 1 && !font.isMultiAtlasTexturesEnabled, name + ": unbounded population");
            Require(font.faceInfo.scale == 1 && font.boldStyle == 0 && font.boldSpacing == 0 && font.normalStyle == 0, name + ": synthetic style");
            Require(font.sourceFontFile == null && font.material.mainTexture == font.atlasTexture && font.atlasTexture.width == font.atlasWidth, name + ": incomplete baked asset");
            Require(font.fallbackFontAssetTable.Count == 2 && font.fallbackFontAssetTable.All(f => f.fallbackFontAssetTable.Count == 0), name + ": fallback cycle or unexpected chain");
            foreach (var property in new[] {"_FaceDilate", "_OutlineWidth", "_WeightNormal", "_WeightBold"}) Require(font.material.GetFloat(property) == 0, name + ": " + property);
            Require(!font.material.IsKeywordEnabled("UNDERLAY_ON") && font.material.GetColor("_FaceColor") == Color.white, name + ": non-neutral material");
            checks.Add(new {name, requested = corpus.Length, main = corpus.Length-gap.Count, fallback = gap.Count, resolution});
        }
        foreach (string name in ConceptFontAssetGenerator.Fallbacks)
        {
            var f = ConceptFontAssetGenerator.Load(name);
            var expected = (name == "NotoSansMath-Arrows" ? sourceGaps["symbols_codepoints"] : sourceGaps["latin_codepoints"]).Values<uint>().OrderBy(c => c).ToArray();
            Require(f.characterTable.Select(c => c.unicode).OrderBy(c => c).SequenceEqual(expected), name + ": fallback is not restricted to declared gap");
        }
        var registry = Resources.Load<ConceptFontSet>("UI/ConceptFontSet");
        Require(registry != null && new[] {registry.robotoRegular, registry.robotoBold, registry.condensedRegular, registry.condensedMedium, registry.condensedSemiBold, registry.condensedBold}.All(f => f != null && f.atlasTexture != null && f.material != null), "Player-safe resource references missing");
        MeshChecks(); Write("coverage", new {passed = failures.Count == 0, checks, failures});
    }
    static void MeshChecks()
    {
        var go = new GameObject("FONT03 transient mesh validation", typeof(RectTransform), typeof(Canvas)); go.hideFlags = HideFlags.HideAndDontSave;
        var child = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI)); child.transform.SetParent(go.transform, false);
        var text = child.GetComponent<TextMeshProUGUI>(); text.fontSize = 24; text.rectTransform.sizeDelta = new Vector2(1800, 900); text.enableAutoSizing = false;
        var records = new List<object>();
        try
        {
            foreach (string name in ConceptFontAssetGenerator.Faces)
            {
                text.font = ConceptFontAssetGenerator.Load(name); text.fontSharedMaterial = text.font.material;
                foreach (var variant in new[] {"plain", "fontWeight700", "FontStyles.Bold", "rich-b", "weight500", "weight600"})
                {
                    text.fontStyle = variant == "FontStyles.Bold" ? FontStyles.Bold : FontStyles.Normal;
                    text.fontWeight = variant == "fontWeight700" ? FontWeight.Bold : FontWeight.Regular;
                    text.richText = true;
                    text.text = variant == "rich-b" ? "<b>Ação ←</b>" : variant == "weight500" ? "<font-weight=500>Ação ←</font-weight>" : variant == "weight600" ? "<font-weight=600>Ação ←</font-weight>" : "Ação ←";
                    text.ForceMeshUpdate(true, true);
                    bool bold = variant == "fontWeight700" || variant == "FontStyles.Bold" || variant == "rich-b";
                    string expected = bold ? (name.StartsWith("RobotoCondensed") ? "RobotoCondensed-Bold" : "Roboto-Bold") :
                        variant == "weight500" ? (name.StartsWith("RobotoCondensed") ? "RobotoCondensed-Medium" : "Roboto-Regular") :
                        variant == "weight600" ? (name.StartsWith("RobotoCondensed") ? "RobotoCondensed-SemiBold" : "Roboto-Bold") : name;
                    string actual = text.textInfo.characterInfo[0].fontAsset?.name;
                    Require(expected == actual, name + "/" + variant + ": actual face " + actual + "; expected " + expected);
                    Require(text.textInfo.characterInfo.Take(text.textInfo.characterCount).All(c => c.textElement != null), name + "/" + variant + ": missing mesh character");
                    records.Add(new {name, variant, expected, actual, alternate = text.textInfo.characterInfo[0].isUsingAlternateTypeface, material = text.fontSharedMaterial.name, text.textInfo.materialCount});
                }
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
        Write("weight-resolution", new {passed = failures.Count == 0, cases = records, failures});
    }
    static void Refresh()
    {
        Canvas.ForceUpdateCanvases();
        var type = typeof(Editor).Assembly.GetType("UnityEditor.GameView");
        EditorWindow.GetWindow(type).Repaint(); EditorApplication.QueuePlayerLoopUpdate();
    }
    static string Capture(string name)
    {
        var type = typeof(Editor).Assembly.GetType("UnityEditor.PlayModeView");
        var view = type.GetMethod("GetMainPlayModeView", BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic).Invoke(null, null);
        var source = (RenderTexture)type.GetField("m_TargetTexture", BindingFlags.Instance|BindingFlags.NonPublic).GetValue(view);
        if (source == null || source.width != 1920 || source.height != 1080) throw new InvalidOperationException("Keep the existing 1920x1080 Game View for FONT-03.");
        var target = RenderTexture.GetTemporary(1920, 1080, 0, RenderTextureFormat.ARGB32); var previous = RenderTexture.active;
        var texture = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
        try
        {
            if (SystemInfo.graphicsUVStartsAtTop) Graphics.Blit(source, target, new Vector2(1,-1), new Vector2(0,1)); else Graphics.Blit(source, target);
            RenderTexture.active = target; texture.ReadPixels(new Rect(0,0,1920,1080),0,0); texture.Apply();
            string path = Output + "/" + name + ".png"; File.WriteAllBytes(path, texture.EncodeToPNG());
            Require(Time.frameCount>lastFrame,"Capture did not advance: "+name);
            captures.Add(new {name, path, width=1920, height=1080, utc=DateTime.UtcNow.ToString("o"), scene=SceneManager.GetActiveScene().path, editMode=false, frame=Time.frameCount, advancing=Time.frameCount>lastFrame});
            lastFrame=Time.frameCount;
            return path;
        }
        finally {RenderTexture.active=previous; RenderTexture.ReleaseTemporary(target); UnityEngine.Object.DestroyImmediate(texture);}
    }
    static void Tick()
    {
        if(!EditorApplication.isPlaying) return;
        Application.runInBackground=true;
        if(stage==-2)
        {
            Refresh(); if(Time.frameCount<12) return;
            try {UI01Validation.RequireIsolatedStorage(); live=SceneTexts().Where(t=>t.isActiveAndEnabled && t.GetComponentInParent<Canvas>()!=null).ToArray(); originals=live.ToDictionary(t=>t,t=>EditorJsonUtility.ToJson(t)); stage=-1; due=EditorApplication.timeSinceStartup+0.8;}
            catch(Exception error) {failures.Add(error.ToString()); Finish();}
            return;
        }
        if (EditorApplication.timeSinceStartup < due) {Refresh(); return;}
        try
        {
            if (stage == -1) Capture("identification-baseline");
            else if (stage < 6) Capture("identification-" + ConceptFontAssetGenerator.Faces[stage]);
            else if (stage == 6) Capture("all-faces-specimen");
            stage++;
            if (stage < 6)
            {
                var font = ConceptFontAssetGenerator.Load(ConceptFontAssetGenerator.Faces[stage]);
                foreach (var t in live) {t.font=font; t.fontSharedMaterial=font.material; t.fontStyle=FontStyles.Normal; t.fontWeight=FontWeight.Regular; t.ForceMeshUpdate(true,true);}
            }
            else if (stage == 6) {RestoreTexts(); MakeSheet();}
            else {Finish(); return;}
            Refresh(); due = EditorApplication.timeSinceStartup + 0.7;
        }
        catch (Exception error) {failures.Add(error.ToString()); Finish();}
    }
    static void RestoreTexts()
    {
        if (originals == null) return;
        foreach (var pair in originals) if (pair.Key != null) {EditorJsonUtility.FromJsonOverwrite(pair.Value, pair.Key); pair.Key.ForceMeshUpdate(true,true);}
    }
    static void Label(Transform parent, string content, TMP_FontAsset font, float x, float y, float width, float height, float size)
    {
        var go = new GameObject("FONT03 " + font.name, typeof(RectTransform), typeof(TextMeshProUGUI)); go.transform.SetParent(parent,false);
        var t = go.GetComponent<TextMeshProUGUI>(); var r=t.rectTransform; r.anchorMin=r.anchorMax=new Vector2(0,1); r.pivot=new Vector2(0,1); r.anchoredPosition=new Vector2(x,-y); r.sizeDelta=new Vector2(width,height);
        t.font=font; t.fontSharedMaterial=font.material; t.fontSize=size; t.fontStyle=FontStyles.Normal; t.fontWeight=FontWeight.Regular; t.color=new Color32(36,17,61,255); t.enableAutoSizing=false; t.textWrappingMode=TextWrappingModes.NoWrap; t.overflowMode=TextOverflowModes.Overflow; t.text=content;
    }
    static void MakeSheet()
    {
        var canvas = SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Canvas>(true)).First(c=>c.isActiveAndEnabled && c.isRootCanvas);
        sheet = new GameObject("FONT03 temporary specimen",typeof(RectTransform),typeof(UnityEngine.UI.Image)); sheet.hideFlags=HideFlags.DontSave; sheet.transform.SetParent(canvas.transform,false); sheet.transform.SetAsLastSibling();
        var rect=(RectTransform)sheet.transform; rect.anchorMin=Vector2.zero; rect.anchorMax=Vector2.one; rect.offsetMin=rect.offsetMax=Vector2.zero;
        sheet.GetComponent<UnityEngine.UI.Image>().color=new Color32(244,241,233,255);
        var regular=ConceptFontAssetGenerator.Load("Roboto-Regular");
        Label(sheet.transform,"FONT-03 · atlas TMP reais · 1920×1080 · escala 1",regular,48,22,1800,52,32);
        for(int i=0;i<6;i++)
        {
            var f=ConceptFontAssetGenerator.Load(ConceptFontAssetGenerator.Faces[i]); float top=85+i*145;
            Label(sheet.transform, f.name+"   36 px   Restaurante · Ação · 0123456789",f,48,top,1800,46,36);
            Label(sheet.transform,"24 px  ÁÉÍÓÚ ÂÊÔ ÃÕ Ç áéíóú âêô ãõ ç   R$ -12.345,67   72 / 100   + − %  ← ↑ → ↓",f,48,top+48,1800,36,24);
            Label(sheet.transform,"20 px  Informação secundária · Formação de preços · Equipamentos · Fluxo de caixa · São Paulo",f,48,top+87,1800,32,20);
        }
        Label(sheet.transform,"Fallback latino restrito: ƀ Ɓ Ɔ Ɖ Ǝ Ɛ Ƣ Ʃ Ǆ ǅ ǆ Ǽ Ȁ Ȣ ȴ Ɏ ɏ     Setas Noto Sans Math: ← ↑ → ↓",regular,48,976,1800,40,24);
    }
    static void Finish()
    {
        EditorApplication.update-=Tick;
        if(sheet!=null) UnityEngine.Object.DestroyImmediate(sheet); sheet=null;
        RestoreTexts(); originals=null;
        if(EditorApplication.isPlaying)
        {
            Write("captures",captures);
            SessionState.SetString("Cogna.Font03.Failures",JsonConvert.SerializeObject(failures));
            SessionState.SetBool(Playing,false); SessionState.SetBool(Restoring,true);
            EditorApplication.isPlaying=false; EditorApplication.delayCall+=RestoreAfterPlay; return;
        }
        Complete();
    }
    static void RestoreAfterPlay()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode) {EditorApplication.delayCall+=RestoreAfterPlay; return;}
        if(!SessionState.GetBool(Restoring,false)) return;
        // isPlaying becomes false before Unity has restored the edit-mode backup scene.
        int expectedRoots=(int)JObject.Parse(SessionState.GetString("Cogna.Font03.Before","{}"))["scenes"][0]["rootCount"];
        if(SceneManager.GetActiveScene().rootCount!=expectedRoots) {EditorApplication.delayCall+=RestoreAfterPlay; return;}
        SessionState.SetBool(Restoring,false);
        before=SessionState.GetString("Cogna.Font03.Before",""); failures.Clear(); failures.AddRange(JsonConvert.DeserializeObject<List<string>>(SessionState.GetString("Cogna.Font03.Failures","[]")));
        started=double.Parse(SessionState.GetString("Cogna.Font03.Started","0"),System.Globalization.CultureInfo.InvariantCulture);
        Environment.SetEnvironmentVariable("COGNA_TEST_DATABASE_PATH",SessionState.GetString("Cogna.Font03.OldDb",""));
        Environment.SetEnvironmentVariable("COGNA_TEST_IDENTIFICATION_KEY",SessionState.GetString("Cogna.Font03.OldKey",""));
        Complete();
    }
    [MenuItem("Tools/Jogos de Empresa/FONT-03/Check restored scene")]
    public static void CheckRestoredScene()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Wait for Edit mode.");
        SessionState.SetBool(Restoring,true); RestoreAfterPlay();
    }
    static void Complete()
    {
        string after=SceneState(); Require(before==after,"Open scene differs after temporary screen validation.");
        var logEntries=typeof(Editor).Assembly.GetType("UnityEditor.LogEntries"); object[] counts={0,0,0};
        logEntries.GetMethod("GetCountsByType",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,counts);
        Require((int)counts[0]==0,"Console has errors: "+counts[0]);
        var savedCaptures=File.Exists(Output+"/captures.json")?JArray.Parse(File.ReadAllText(Output+"/captures.json")):new JArray();
        Require(savedCaptures.Count==8 && savedCaptures.All(c=>(bool?)c["advancing"]==true),"Eight advancing Play-mode captures required.");
        Write("live-after",JObject.Parse(after));
        Write("execution",new {task="FONT-03",passed=failures.Count==0,failures,unity=Application.unityVersion,consoleErrors=counts[0],consoleWarnings=counts[1],before=JObject.Parse(before),after=JObject.Parse(after),scenePreserved=before==after,sceneSaved=false,captures=savedCaptures.Count,playMode=true,isolatedDatabase=Path.GetFullPath(Output+"/qa-game.db"),seconds=EditorApplication.timeSinceStartup-started});
        Refresh(); Debug.Log("FONT-03 "+(failures.Count==0?"PASS":"FAIL")+"; "+Output+"/execution.json");
    }
}
