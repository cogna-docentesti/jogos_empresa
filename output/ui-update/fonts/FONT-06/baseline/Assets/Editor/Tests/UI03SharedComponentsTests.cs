using System;
using System.IO;
using System.Linq;
using Game.Adapter.In.UI;
using Game.Adapter.In.UI.Theme;
using Game.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class UI03SharedComponentsTests
{
    [Test]
    public void All280SpritesRemainSingleAndAllManifestImportsMatchCurrentContract()
    {
        Assert.AreEqual(280,Directory.GetFiles("Assets/Art/new","*.png",SearchOption.AllDirectories).Length);
        foreach(var path in Directory.GetFiles("Assets/Art/new","*.png",SearchOption.AllDirectories))
            Assert.AreEqual(SpriteImportMode.Single,((TextureImporter)AssetImporter.GetAtPath(path.Replace('\\','/'))).spriteImportMode,path);
        foreach(var rule in SharedUiImportRules.Read())
        {
            string path="Assets/Art/new/"+rule.path; var i=(TextureImporter)AssetImporter.GetAtPath(path);
            var s=new TextureImporterSettings(); i.ReadTextureSettings(s);
            Assert.IsFalse(i.mipmapEnabled,path); Assert.IsTrue(i.sRGBTexture,path); Assert.IsTrue(i.alphaIsTransparency,path);
            Assert.AreEqual(FilterMode.Bilinear,i.filterMode,path); Assert.AreEqual(TextureWrapMode.Clamp,i.wrapMode,path);
            Assert.AreEqual(TextureImporterCompression.Uncompressed,i.textureCompression,path);
            Assert.GreaterOrEqual(i.maxTextureSize,rule.actual_size_px.Max(),path);
            Assert.AreEqual(new Vector2(rule.pivot[0],rule.pivot[1]),s.spritePivot,path);
            Assert.AreEqual(new Vector4(rule.nine_slice_lbrt_px[0],rule.nine_slice_lbrt_px[1],rule.nine_slice_lbrt_px[2],rule.nine_slice_lbrt_px[3]),s.spriteBorder,path);
            if(rule.kind=="componente"||rule.kind=="icone"||rule.path.StartsWith("shared/")) Assert.AreEqual(SpriteMeshType.FullRect,s.spriteMeshType,path);
            var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(path);
            Assert.AreEqual(rule.actual_size_px[0],sprite.rect.width,path); Assert.AreEqual(rule.actual_size_px[1],sprite.rect.height,path);
        }
    }
    [TestCase("UI_PRIMARY_VIOLET")][TestCase("UI_SECONDARY_IVORY")][TestCase("UI_DANGER_CORAL")][TestCase("UI_ACCENT_AMBER")]
    [TestCase("UI_ICON_BUTTON")][TestCase("UI_INPUT")][TestCase("UI_TAB")][TestCase("UI_PILL")][TestCase("UI_RADIO")][TestCase("UI_CHECKBOX")][TestCase("UI_TOGGLE")]
    public void StateFamiliesShareBoundsPivotAndBorders(string family)
    {
        var sprites=SharedUiSkin.Current.entries.Where(e=>e.id.StartsWith(family+"_")).Select(e=>e.sprite).ToArray();
        Assert.GreaterOrEqual(sprites.Length,3);
        foreach(var s in sprites) { Assert.AreEqual(sprites[0].rect,s.rect); Assert.AreEqual(sprites[0].pivot,s.pivot); Assert.AreEqual(sprites[0].border,s.border); }
    }
    [Test]
    public void BindingDoesNotReplaceButtonOrDuplicateCallbacks()
    {
        var go=new GameObject("UI03 callback test",typeof(RectTransform),typeof(UnityEngine.UI.Image),typeof(UnityEngine.UI.Button));
        try
        {
            var button=go.GetComponent<UnityEngine.UI.Button>(); int calls=0; button.onClick.AddListener(()=>calls++);
            var first=SharedUiStyles.Bind(button,SharedControlKind.Primary); var second=SharedUiStyles.Bind(button,SharedControlKind.Primary);
            button.onClick.Invoke(); Assert.AreEqual(1,calls); Assert.AreSame(first,second); Assert.AreSame(button,go.GetComponent<UnityEngine.UI.Button>());
            Assert.AreEqual(1,go.GetComponentsInChildren<UnityEngine.UI.Image>(true).Count(i=>i.name=="SharedFocus"));
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }
    [Test]
    public void InactiveTemplatesRetainNativeTextReferencesAndSurfaceContrast()
    {
        var root=new GameObject("UI03 inactive template",typeof(RectTransform)); root.SetActive(false);
        var go=new GameObject("Secondary",typeof(RectTransform),typeof(UnityEngine.UI.Image),typeof(UnityEngine.UI.Button)); go.transform.SetParent(root.transform,false);
        var label=new GameObject("Label",typeof(RectTransform),typeof(TMPro.TextMeshProUGUI)); label.transform.SetParent(go.transform,false);
        try
        {
            var appearance=SharedUiStyles.Bind(go.GetComponent<UnityEngine.UI.Button>(),SharedControlKind.Secondary);
            root.SetActive(true); appearance.Refresh();
            Assert.AreEqual(GamePalette.Ink,label.GetComponent<TMPro.TMP_Text>().color);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }
    [Test]
    public void ProductionEntryAndOptInCompositionRemainInGameScene()
    {
        var scene=UnityEngine.SceneManagement.SceneManager.GetSceneByPath("Assets/Scenes/GameScene.unity");
        bool opened=!scene.isLoaded;
        if(opened) scene=UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity",UnityEditor.SceneManagement.OpenSceneMode.Additive);
        try
        {
            var preview=scene.GetRootGameObjects().First(r=>r.name=="Canvas").transform.Find("SharedComponentsPreview");
            Assert.IsNotNull(preview); Assert.IsFalse(preview.gameObject.activeSelf);
            Assert.IsNotNull(preview.GetComponent<SharedComponentsShowcase>());
            Assert.AreEqual(1,scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<UnityEngine.EventSystems.EventSystem>(true)).Count());
        }
        finally { if(opened) UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene,true); }
    }
}
