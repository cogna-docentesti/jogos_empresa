using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using Game.Adapter.In.UI.Theme;

/// <summary>Regenerates baked font data in place, preserving font, material and atlas object identities.</summary>
public static class ConceptFontAssetGenerator
{
    public const string Root = "Assets/TextMesh Pro/Fonts/Concept";
    public const string Output = "output/ui-update/fonts/FONT-03";
    public static readonly string[] Faces = { "Roboto-Regular", "Roboto-Bold", "RobotoCondensed-Regular", "RobotoCondensed-Medium", "RobotoCondensed-SemiBold", "RobotoCondensed-Bold" };
    public static readonly string[] Fallbacks = { "NotoSans-Regular", "NotoSans-Bold", "NotoSansMath-Arrows" };
    public static TMP_FontAsset Load(string name) => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Root + "/SDF/" + name + ".asset");
    public static uint[] Corpus => JObject.Parse(File.ReadAllText("output/ui-update/fonts/FONT-01/glyph-corpus.json"))["primary_codepoints"].Values<uint>().ToArray();
    static void Folder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        Folder(Path.GetDirectoryName(path).Replace('\\', '/'));
        AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\', '/'), Path.GetFileName(path));
    }
    public static object Identity(UnityEngine.Object asset)
    {
        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string guid, out long localId);
        return new { guid, localId };
    }
    static void Neutral(TMP_FontAsset asset)
    {
        asset.normalStyle = asset.boldStyle = asset.boldSpacing = asset.normalSpacingOffset = 0;
        var material = asset.material;
        material.SetColor("_FaceColor", Color.white);
        foreach (var property in new[] { "_FaceDilate", "_OutlineWidth", "_OutlineSoftness", "_WeightNormal", "_WeightBold", "_UnderlayDilate", "_UnderlaySoftness" })
            if (material.HasProperty(property)) material.SetFloat(property, 0);
        material.DisableKeyword("UNDERLAY_ON"); material.DisableKeyword("UNDERLAY_INNER"); material.DisableKeyword("OUTLINE_ON");
        material.SetTexture("_MainTex", asset.atlasTexture);
        material.SetFloat("_TextureWidth", asset.atlasWidth);
        material.SetFloat("_TextureHeight", asset.atlasHeight);
    }
    static object Generate(string name, string sourcePath, uint[] requested, int initialSize)
    {
        var source = AssetDatabase.LoadAssetAtPath<Font>(sourcePath);
        if (source == null) throw new InvalidOperationException("Missing source " + sourcePath);
        if (FontEngine.LoadFontFace(source, 80) != FontEngineError.Success) throw new InvalidOperationException("Cannot load " + sourcePath);
        uint[] supported = requested.Where(c => FontEngine.TryGetGlyphWithUnicodeValue(c, GlyphLoadFlags.LOAD_NO_HINTING, out var glyph) && glyph.index != 0).OrderBy(c => c).ToArray();
        if (supported.Length == 0) throw new InvalidOperationException("Empty coverage " + name);
        string path = Root + "/SDF/" + name + ".asset";
        var asset = Load(name);
        bool created = asset == null;
        if (created) asset = TMP_FontAsset.CreateFontAsset(source, 80, 8, GlyphRenderMode.SDF16, initialSize, initialSize, AtlasPopulationMode.Dynamic, false);
        if (asset == null) throw new InvalidOperationException("TMP creation failed " + name);
        asset.name = name;
        asset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
        asset.isMultiAtlasTexturesEnabled = false;
        var serialized = new SerializedObject(asset);
        serialized.FindProperty("m_SourceFontFile").objectReferenceValue = source;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        var attempts = new List<object>();
        bool complete = false;
        for (int size = initialSize; size <= 2048; size *= 2)
        {
            serialized.Update();
            serialized.FindProperty("m_AtlasWidth").intValue = size;
            serialized.FindProperty("m_AtlasHeight").intValue = size;
            serialized.FindProperty("m_AtlasPadding").intValue = 8;
            serialized.FindProperty("m_AtlasRenderMode").intValue = (int)GlyphRenderMode.SDF16;
            var clearOnBuild = serialized.FindProperty("m_ClearDynamicDataOnBuild");
            if (clearOnBuild != null) clearOnBuild.boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            asset.ClearFontAssetData(true);
            complete = asset.TryAddCharacters(supported, out uint[] missing, true);
            attempts.Add(new { size, requested = supported.Length, packed = supported.Length - (missing?.Length ?? 0), missing = missing ?? Array.Empty<uint>() });
            if (complete) break;
        }
        if (!complete) throw new InvalidOperationException("Atlas capacity exceeded " + name);
        var info = asset.faceInfo; info.scale = 1; asset.faceInfo = info;
        asset.atlasPopulationMode = AtlasPopulationMode.Static;
        // Baked data is sufficient in Player. Editor source GUID/reference is retained by TMP for regeneration.
        asset.fallbackFontAssetTable = new List<TMP_FontAsset>();
        asset.material.name = name + " Material";
        asset.atlasTexture.name = name + " Atlas";
        asset.atlasTexture.filterMode = FilterMode.Bilinear;
        asset.atlasTexture.wrapMode = TextureWrapMode.Clamp;
        Neutral(asset);
        if (created)
        {
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.AddObjectToAsset(asset.material, asset);
            AssetDatabase.AddObjectToAsset(asset.atlasTexture, asset);
        }
        foreach (var item in new UnityEngine.Object[] { asset, asset.material, asset.atlasTexture }) { EditorUtility.SetDirty(item); AssetDatabase.SaveAssetIfDirty(item); }
        return new { path, source = sourcePath, name, family = info.familyName, style = info.styleName, info.scale, sampling = info.pointSize,
            padding = asset.atlasPadding, renderMode = asset.atlasRenderMode.ToString(), atlas = new[] {asset.atlasWidth, asset.atlasHeight}, attempts,
            population = asset.atlasPopulationMode.ToString(), characters = asset.characterTable.Count, glyphs = asset.glyphTable.Count,
            glyphAreaRatio = asset.glyphTable.Sum(g => (double)g.glyphRect.width * g.glyphRect.height) / (asset.atlasWidth * asset.atlasHeight),
            fontIdentity = Identity(asset), materialIdentity = Identity(asset.material), atlasIdentity = Identity(asset.atlasTexture) };
    }
    static void Weights(TMP_FontAsset font, TMP_FontAsset regular, TMP_FontAsset medium, TMP_FontAsset semi, TMP_FontAsset bold)
    {
        for (int index = 0; index < font.fontWeightTable.Length; index++)
            font.fontWeightTable[index] = new TMP_FontWeightPair { regularTypeface = index >= 7 ? bold : index == 6 ? semi : index == 5 ? medium : regular, italicTypeface = null };
        EditorUtility.SetDirty(font); AssetDatabase.SaveAssetIfDirty(font);
    }
    public static void GenerateAll()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Generate fonts in Edit mode.");
        Directory.CreateDirectory(Output);
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        Folder(Root + "/SDF");
        FontEngine.InitializeFontEngine();
        var rows = new List<object>();
        uint[] corpus = Corpus;
        var gap = JObject.Parse(File.ReadAllText(Output + "/fallback-binaries.json"));
        foreach (string name in Faces) rows.Add(Generate(name, Root + "/Static/" + name + ".ttf", corpus, 1024));
        foreach (string name in Fallbacks.Take(2)) rows.Add(Generate(name, Root + "/Static/" + name + ".ttf", gap["latin_codepoints"].Values<uint>().ToArray(), 1024));
        rows.Add(Generate(Fallbacks[2], Root + "/Sources/NotoSansMath/NotoSansMath-Regular.ttf", gap["symbols_codepoints"].Values<uint>().ToArray(), 512));
        var regular = Load(Faces[0]); var bold = Load(Faces[1]);
        var cr = Load(Faces[2]); var cm = Load(Faces[3]); var cs = Load(Faces[4]); var cb = Load(Faces[5]);
        var nr = Load(Fallbacks[0]); var nb = Load(Fallbacks[1]); var arrows = Load(Fallbacks[2]);
        foreach (var font in new[] {regular, bold}) Weights(font, regular, regular, bold, bold);
        foreach (var font in new[] {cr, cm, cs, cb}) Weights(font, cr, cm, cs, cb);
        foreach (var font in new[] {nr, nb}) Weights(font, nr, nr, nb, nb);
        Weights(arrows, arrows, arrows, arrows, arrows);
        foreach (string name in Faces)
        {
            var font = Load(name);
            font.fallbackFontAssetTable = new List<TMP_FontAsset> { name.EndsWith("Bold") ? nb : nr, arrows };
            EditorUtility.SetDirty(font); AssetDatabase.SaveAssetIfDirty(font);
        }
        Folder("Assets/Resources/UI");
        const string registryPath = "Assets/Resources/UI/ConceptFontSet.asset";
        var registry = AssetDatabase.LoadAssetAtPath<ConceptFontSet>(registryPath);
        if (registry == null) { registry = ScriptableObject.CreateInstance<ConceptFontSet>(); AssetDatabase.CreateAsset(registry, registryPath); }
        registry.robotoRegular = regular; registry.robotoBold = bold; registry.condensedRegular = cr;
        registry.condensedMedium = cm; registry.condensedSemiBold = cs; registry.condensedBold = cb;
        EditorUtility.SetDirty(registry); AssetDatabase.SaveAssetIfDirty(registry);
        File.WriteAllText(Output + "/generation.json", JsonConvert.SerializeObject(new {unity = Application.unityVersion, assets = rows, registry = registryPath}, Formatting.Indented) + "\n");
    }
}
