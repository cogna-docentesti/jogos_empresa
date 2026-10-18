using System;
using System.Linq;
using System.IO;
using Game.Adapter.In.UI;
using Game.Adapter.In.UI.Theme;
using Game.EditorTools;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class Font04TypographyTests
{
    [TestCase(TypographyRole.Brand,"Roboto-Bold",700,36)]
    [TestCase(TypographyRole.ScreenTitle,"Roboto-Bold",700,44)]
    [TestCase(TypographyRole.SectionTitle,"RobotoCondensed-Bold",700,32)]
    [TestCase(TypographyRole.Body,"Roboto-Regular",400,24)]
    [TestCase(TypographyRole.Input,"Roboto-Regular",400,24)]
    [TestCase(TypographyRole.Table,"Roboto-Regular",400,22)]
    [TestCase(TypographyRole.TableTotal,"Roboto-Bold",700,22)]
    [TestCase(TypographyRole.Kpi,"Roboto-Bold",700,42)]
    [TestCase(TypographyRole.Button,"RobotoCondensed-SemiBold",600,24)]
    [TestCase(TypographyRole.Navigation,"RobotoCondensed-Medium",500,22)]
    [TestCase(TypographyRole.Label,"RobotoCondensed-Medium",500,22)]
    [TestCase(TypographyRole.Caption,"RobotoCondensed-Regular",400,20)]
    [TestCase(TypographyRole.MapMarker,"RobotoCondensed-SemiBold",600,24)]
    public void RoleUsesRealFaceAndNeutralMaterial(TypographyRole role,string face,int weight,int size)
    {
        var style=GameTypography.Resolve(role);
        Assert.AreEqual(face,style.Font.name); Assert.AreEqual(weight,style.Weight); Assert.AreEqual(size,style.Size);
        Assert.AreSame(style.Font.material,style.Material);
        foreach(var property in new[]{"_FaceDilate","_WeightNormal","_WeightBold","_OutlineWidth"}) Assert.AreEqual(0,style.Material.GetFloat(property));
        Assert.AreEqual(0,style.Font.boldStyle); Assert.AreEqual(1,style.Font.faceInfo.scale);
    }

    [Test]
    public void ExplicitFactoryRoleOverridesLegacyBoldAndSurvivesCloneAndReentry()
    {
        var root=new GameObject("FONT04 unrelated object name",typeof(RectTransform));
        GameObject clone=null; var overwrite=UiFactory.OverwriteExisting; UiFactory.BeginRun(true);
        try
        {
            var rect=(RectTransform)root.transform; rect.sizeDelta=new Vector2(600,80);
            var text=UiFactory.Text(rect,"Continuar",new TypographyToken(TypographyRole.Button,30),FontStyles.Bold,GamePalette.Ink);
            var position=rect.anchoredPosition; var bounds=rect.sizeDelta; var content=text.text;
            for(int i=0;i<3;i++) GameTypography.Apply(text,null,30,true);
            Assert.AreEqual("RobotoCondensed-SemiBold",text.font.name); Assert.AreEqual(FontWeight.Regular,text.fontWeight);
            Assert.AreEqual(FontStyles.Normal,text.fontStyle&FontStyles.Bold); Assert.AreEqual(1,root.GetComponents<TypographyRoleBinding>().Length);
            clone=UnityEngine.Object.Instantiate(root); clone.SetActive(false); clone.SetActive(true);
            var other=clone.GetComponent<TMP_Text>();
            Assert.AreEqual(text.font,other.font); Assert.AreSame(text.font.material,other.fontSharedMaterial);
            Assert.AreEqual(30,other.fontSize); Assert.AreEqual(content,other.text);
            Assert.AreEqual(position,other.rectTransform.anchoredPosition); Assert.AreEqual(bounds,other.rectTransform.sizeDelta);
            Assert.AreEqual(Vector3.one,other.rectTransform.localScale);
        }
        finally { if(clone!=null)UnityEngine.Object.DestroyImmediate(clone); UnityEngine.Object.DestroyImmediate(root); UiFactory.BeginRun(overwrite); }
    }

    [Test]
    public void ControlSkinKeepsTheExplicitTextRoleOnBindAndEnable()
    {
        var go=new GameObject("FONT04 explicit control role",typeof(RectTransform),typeof(UnityEngine.UI.Image),typeof(UnityEngine.UI.Button));
        var label=new GameObject("Unrelated label name",typeof(RectTransform),typeof(TextMeshProUGUI)); label.transform.SetParent(go.transform,false);
        try
        {
            var text=label.GetComponent<TMP_Text>(); GameTypography.Apply(text,TypographyRole.Navigation,23);
            SharedUiStyles.Bind(go.GetComponent<UnityEngine.UI.Button>(),SharedControlKind.Primary);
            go.SetActive(false); go.SetActive(true);
            Assert.AreEqual(TypographyRole.Navigation,text.GetComponent<TypographyRoleBinding>().Role);
            Assert.AreEqual("RobotoCondensed-Medium",text.font.name); Assert.AreEqual(23,text.fontSize);
            Assert.AreEqual(FontStyles.Normal,text.fontStyle&FontStyles.Bold);
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }

    [Test]
    public void ApplyingRoleKeepsContentRichTextAndExistingDimensions()
    {
        var canvas=new GameObject("FONT04 mesh canvas",typeof(RectTransform),typeof(Canvas));
        var root=new GameObject("FONT04 rich text",typeof(RectTransform),typeof(TextMeshProUGUI)); root.transform.SetParent(canvas.transform,false);
        try
        {
            var text=root.GetComponent<TMP_Text>(); text.rectTransform.sizeDelta=new Vector2(1100,100); text.fontSize=27;
            text.text="<b>Ação</b> <i>←</i> <color=#FF0000>R$ -12.345,67</color>"; text.richText=true;
            var content=text.text; var bounds=text.rectTransform.sizeDelta;
            for(int i=0;i<3;i++) GameTypography.KeepSize(text,TypographyRole.Body);
            text.ForceMeshUpdate(true,true);
            Assert.AreEqual(content,text.text); Assert.AreEqual(bounds,text.rectTransform.sizeDelta); Assert.AreEqual(27,text.fontSize);
            Assert.AreEqual("Roboto-Bold",text.textInfo.characterInfo[0].fontAsset.name);
            Assert.AreEqual(FontStyles.Normal,text.fontStyle&FontStyles.Bold); Assert.AreEqual(FontWeight.Regular,text.fontWeight);
            Assert.IsTrue(text.textInfo.characterInfo.Take(text.textInfo.characterCount).All(c=>c.textElement!=null));
            Assert.IsFalse(text.isTextOverflowing); Assert.IsFalse(text.enableAutoSizing);
        }
        finally { UnityEngine.Object.DestroyImmediate(canvas); }
    }

    [Test]
    public void RoleBindingsSurviveSavingAndReopeningAnAdditiveScene()
    {
        const string path="Assets/Font04TemporaryRoundTrip.unity";
        Assert.IsFalse(System.IO.File.Exists(path),"Never overwrite an existing scene.");
        // Unity can persist TMP material ratios and dynamic caches while saving a test scene.
        // Preserve the exact files that belonged to the caller before this round trip.
        var fonts=Resources.FindObjectsOfTypeAll<TMP_FontAsset>().Select(AssetDatabase.GetAssetPath).Where(p=>!string.IsNullOrEmpty(p)).Distinct().ToDictionary(p=>p,File.ReadAllBytes);
        var original=SceneManager.GetActiveScene(); var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
        try
        {
            foreach(var role in new[]{TypographyRole.ScreenTitle,TypographyRole.Body,TypographyRole.Button,TypographyRole.Kpi})
            {
                var go=new GameObject(role.ToString(),typeof(RectTransform),typeof(Canvas),typeof(TextMeshProUGUI));
                SceneManager.MoveGameObjectToScene(go,scene); GameTypography.Apply(go.GetComponent<TMP_Text>(),role,29);
            }
            Assert.IsTrue(EditorSceneManager.SaveScene(scene,path)); EditorSceneManager.CloseScene(scene,true);
            scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
            Assert.AreEqual(4,scene.rootCount);
            foreach(var go in scene.GetRootGameObjects())
            {
                var binding=go.GetComponent<TypographyRoleBinding>(); binding.Reapply();
                var text=go.GetComponent<TMP_Text>(); Assert.AreSame(GameTypography.Resolve(binding.Role).Font,text.font);
                Assert.AreEqual(text.font.material.GetInstanceID(),text.fontSharedMaterial.GetInstanceID()); Assert.AreEqual(29,text.fontSize);
            }
        }
        finally
        {
            if(scene.IsValid()&&scene.isLoaded) EditorSceneManager.CloseScene(scene,true);
            SceneManager.SetActiveScene(original); AssetDatabase.DeleteAsset(path);
            foreach(var pair in fonts)
                if(!File.ReadAllBytes(pair.Key).SequenceEqual(pair.Value))
                {
                    string temporary=pair.Key+".font04-temporary";
                    File.WriteAllBytes(temporary,pair.Value); File.Replace(temporary,pair.Key,null);
                }
        }
    }
}
