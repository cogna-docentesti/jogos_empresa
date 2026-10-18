using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>Reads the current manifest; ART-36 is deliberately not an import source.</summary>
    public sealed class SharedUiImportRules : AssetPostprocessor
    {
        [Serializable] public sealed class Rule
        {
            public string id, path, kind, display_fit;
            public int[] actual_size_px;
            public float[] nine_slice_lbrt_px, pivot;
        }
        [Serializable] private sealed class Manifest { public Rule[] assets; }
        public static Rule[] Read() => JsonUtility.FromJson<Manifest>(File.ReadAllText("docs/art/manifest.json")).assets;
        private void OnPreprocessTexture()
        {
            if(!assetPath.StartsWith("Assets/Art/new/",StringComparison.Ordinal) || !File.Exists("docs/art/manifest.json")) return;
            var rule=Read().FirstOrDefault(r=>"Assets/Art/new/"+r.path==assetPath);
            if(rule!=null) Configure((TextureImporter)assetImporter,rule);
        }
        public static void Configure(TextureImporter importer,Rule rule)
        {
            // Single has already been migrated. A bad incoming mode is an error, never a new slicing pass.
            if(importer.spriteImportMode!=SpriteImportMode.Single)
                throw new InvalidOperationException("Expected existing Single sprite: "+importer.assetPath);
            importer.alphaIsTransparency=true; importer.mipmapEnabled=false; importer.sRGBTexture=true;
            importer.filterMode=FilterMode.Bilinear; importer.wrapMode=TextureWrapMode.Clamp;
            importer.spritePixelsPerUnit=100; importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.crunchedCompression=false;
            int dimension=rule.actual_size_px.Max();
            importer.maxTextureSize=Mathf.Max(32,Mathf.NextPowerOfTwo(dimension));
            var settings=new TextureImporterSettings(); importer.ReadTextureSettings(settings);
            if(rule.kind=="componente" || rule.kind=="icone" || rule.path.StartsWith("shared/")) settings.spriteMeshType=SpriteMeshType.FullRect;
            if(rule.pivot?.Length==2) { settings.spriteAlignment=(int)SpriteAlignment.Custom; settings.spritePivot=new Vector2(rule.pivot[0],rule.pivot[1]); }
            if(rule.nine_slice_lbrt_px?.Length==4) settings.spriteBorder=new Vector4(rule.nine_slice_lbrt_px[0],rule.nine_slice_lbrt_px[1],rule.nine_slice_lbrt_px[2],rule.nine_slice_lbrt_px[3]);
            importer.SetTextureSettings(settings);
            foreach(string platform in new[]{"Standalone","Android","iPhone","WebGL"})
            {
                var p=importer.GetPlatformTextureSettings(platform);
                if(!p.overridden) continue;
                p.maxTextureSize=importer.maxTextureSize; p.textureCompression=TextureImporterCompression.Uncompressed;
                p.crunchedCompression=false; p.format=TextureImporterFormat.RGBA32; importer.SetPlatformTextureSettings(p);
            }
        }
        public static void ApplyManifest()
        {
            foreach(var rule in Read())
            {
                var importer=AssetImporter.GetAtPath("Assets/Art/new/"+rule.path) as TextureImporter;
                if(importer==null) throw new FileNotFoundException(rule.path);
                string before=EditorJsonUtility.ToJson(importer); Configure(importer,rule);
                if(before!=EditorJsonUtility.ToJson(importer)) importer.SaveAndReimport();
            }
        }
    }
}
