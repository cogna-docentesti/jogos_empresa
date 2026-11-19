using System.Linq;
using System.Reflection;
using Game.Adapter.In.UI;
using Game.Adapter.In.Controllers;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class UI06RestaurantTests
{
    private Scene scene; private bool opened; private RestaurantScreenView view;
    private T Field<T>(string name)=>(T)typeof(RestaurantScreenView).GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(view);
    [SetUp] public void Open()
    {
        scene=SceneManager.GetSceneByPath("Assets/Scenes/GameScene.unity"); opened=!scene.isLoaded;
        if(opened) scene=EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity",OpenSceneMode.Additive);
        view=scene.GetRootGameObjects().First(r=>r.name=="Canvas").transform.Find("Panel_Restaurant").GetComponent<RestaurantScreenView>();
    }
    [TearDown] public void Close() { if(opened) EditorSceneManager.CloseScene(scene,true); }
    [Test] public void PanelPreservesOneControllerAndRetiresLegacyPresentation()
    {
        Assert.AreEqual(1,view.GetComponentsInChildren<RestaurantScreenController>(true).Count(c=>c.gameObject.activeSelf));
        Assert.AreEqual(Vector2.zero,((RectTransform)view.transform).anchorMin); Assert.AreEqual(Vector2.one,((RectTransform)view.transform).anchorMax);
        foreach(var n in new[]{"Header","MainArea","SidePanel","Footer"}) Assert.IsFalse(view.transform.Find(n).gameObject.activeSelf,n);
        Assert.That(view.transform.Find("DecisionHeader/Step").GetComponent<TMPro.TMP_Text>().text,Does.Contain("3 DE 7"));
    }
    [Test] public void FacadesAreWholeNeutralSingleSpritesAndCatalogEconomicsRemainUnchanged()
    {
        var data=Resources.LoadAll<RestaurantData>("Restaurants").OrderBy(d=>(int)d.type).ToArray();
        CollectionAssert.AreEqual(new[]{"REST_LANCHES","REST_JAPONES","REST_FRANCES"},data.Select(d=>d.id));
        CollectionAssert.AreEqual(new[]{1500,1500,2000},data.Select(d=>d.baseMonthlyCost));
        CollectionAssert.AreEqual(new[]{35,71,84},data.Select(d=>d.ticketMin)); CollectionAssert.AreEqual(new[]{95,210,260},data.Select(d=>d.ticketMax));
        var fields=new[]{"cardPodrao","cardJapones","cardFrances"};
        for(int i=0;i<3;i++)
        {
            Assert.AreEqual(3,data[i].products.Length); var image=Field<UnityEngine.UI.Button>(fields[i]).transform.Find("Facade").GetComponent<UnityEngine.UI.Image>();
            Assert.AreSame(data[i].facade,image.sprite); Assert.AreEqual(Color.white,image.color); Assert.IsTrue(image.preserveAspect);
            Assert.AreEqual(new Rect(0,0,1448,1086),image.sprite.rect); Assert.AreEqual(UnityEngine.UI.Image.Type.Simple,image.type);
            var importer=(TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(image.sprite)); Assert.AreEqual(SpriteImportMode.Single,importer.spriteImportMode);
        }
    }
    [Test] public void FiveAllowedCombinationsArePreserved()
    {
        var data=Resources.LoadAll<RestaurantData>("Restaurants");
        CollectionAssert.AreEqual(new[]{Segment.LOW,Segment.MEDIUM},data.Single(d=>d.type==RestaurantType.PODRAO).allowedSegments);
        CollectionAssert.AreEqual(new[]{Segment.MEDIUM,Segment.HIGH},data.Single(d=>d.type==RestaurantType.JAPONES).allowedSegments);
        CollectionAssert.AreEqual(new[]{Segment.HIGH},data.Single(d=>d.type==RestaurantType.FRANCES).allowedSegments);
    }
    [Test] public void EveryControlUsesSharedStateAppearanceAndNativeText()
    {
        foreach(var field in new[]{"cardPodrao","cardJapones","cardFrances","btnLow","btnMedium","btnHigh","confirmButton","backButton"})
        {
            var b=Field<UnityEngine.UI.Button>(field); Assert.IsNotNull(b.GetComponent<SharedControlAppearance>(),field);
            Assert.IsTrue(b.targetGraphic.raycastTarget,field);
        }
        foreach(var field in new[]{"btnLow","btnMedium","btnHigh"})
        {
            var b=Field<UnityEngine.UI.Button>(field); Assert.IsNotNull(b.transform.Find("Radio").GetComponent<UnityEngine.UI.Image>().sprite);
            Assert.IsNotNull(b.transform.Find("SegmentLabel").GetComponent<TMPro.TMP_Text>());
        }
    }
    [Test] public void LongCopyIsMaskedLayoutDrivenAndActionsStayOutsideScrolls()
    {
        var scrolls=view.GetComponentsInChildren<UnityEngine.UI.ScrollRect>(true).Where(s=>s.name=="CardDetails" || s.name=="Compatibility").ToArray(); Assert.AreEqual(4,scrolls.Length);
        foreach(var s in scrolls)
        {
            Assert.IsNotNull(s.viewport.GetComponent<UnityEngine.UI.Mask>()); Assert.IsNotNull(s.content.GetComponent<UnityEngine.UI.VerticalLayoutGroup>());
            Assert.AreEqual(UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize,s.content.GetComponent<UnityEngine.UI.ContentSizeFitter>().verticalFit);
            Assert.AreEqual(Vector2.zero,s.verticalScrollbar.handleRect.sizeDelta); Assert.IsFalse(s.horizontal);
            Assert.IsFalse(Field<UnityEngine.UI.Button>("confirmButton").transform.IsChildOf(s.transform));
        }
    }
}
