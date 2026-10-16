using System.IO;
using System.Linq;
using Game.Adapter.In.UI;
using Game.Adapter.In.UI.Navigation;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class UI05LocationTests
{
    private Scene scene; private bool opened; private Transform panel,master; private LocationScreenView view;
    [SetUp] public void Open()
    {
        scene=SceneManager.GetSceneByPath("Assets/Scenes/GameScene.unity"); opened=!scene.isLoaded;
        if(opened) scene=EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity",OpenSceneMode.Additive);
        panel=scene.GetRootGameObjects().First(r=>r.name=="Canvas").transform.Find("Panel_Location");
        master=panel.Find("MapMaster"); view=panel.GetComponent<LocationScreenView>();
    }
    [TearDown] public void Close() { if(opened) EditorSceneManager.CloseScene(scene,true); }
    [Test] public void OneActiveControllerAndOnePresentationOwnThePanel()
    {
        Assert.IsTrue(view.enabled);
        Assert.AreEqual(1,panel.GetComponentsInChildren<Game.Adapter.In.Controllers.LocationScreenController>(true).Count(c=>c.gameObject.activeSelf));
        Assert.IsEmpty(panel.GetComponentsInChildren<LocationScreenV2View>(true).Where(v=>v.enabled));
        foreach(var n in new[]{"MapArea","SidePanel","Header","BottomBar","LocationScreenContext"})
            Assert.IsFalse(panel.Find(n).gameObject.activeSelf,n);
        foreach(var graphic in panel.Find("MapArea").GetComponentsInChildren<UnityEngine.UI.Graphic>(true)) Assert.IsFalse(graphic.raycastTarget);
    }
    [Test] public void DecisionUsesTheExactSharedMapTransformAndCompactLayerBounds()
    {
        Assert.AreEqual(new Vector2(24,-176),((RectTransform)master).anchoredPosition);
        Assert.AreEqual(Vector3.one*(2f/3f),master.localScale);
        Assert.AreEqual(Vector2.zero,((RectTransform)panel).anchorMin); Assert.AreEqual(Vector2.one,((RectTransform)panel).anchorMax);
        var source=JObject.Parse(File.ReadAllText("docs/art/layouts/map-layout.json"));
        foreach(var layer in source["layers"])
        {
            string id=(string)layer["asset_id"]; var r=(RectTransform)master.Find(id); var image=r.GetComponent<UnityEngine.UI.Image>();
            Assert.AreEqual(new Vector2((float)layer["position_px"][0],-(float)layer["position_px"][1]),r.anchoredPosition,id);
            Assert.AreEqual(image.sprite.rect.size,r.sizeDelta,id); Assert.IsFalse(image.raycastTarget,id);
            if(new[]{"MAP_BANK","MAP_CONDOMINIUM","MAP_MARKETING","MAP_STORE","MAP_UNIVERSITY"}.Contains(id))
                Assert.That(AssetDatabase.GetAssetPath(image.sprite),Does.Contain("/compact/"));
        }
    }
    [Test] public void OnlyFiveCatalogIdsAreInteractiveAndNoHubDestinationCanLeak()
    {
        CollectionAssert.AreEquivalent(new[]{"university","bank","store","condominium","marketing"},view.Zones.Select(z=>z.id));
        Assert.IsEmpty(master.GetComponentsInChildren<PanelNavButton>());
        foreach(var zone in view.Zones)
        {
            Assert.AreEqual(zone.id,zone.marker.LocationId);
            Assert.AreEqual("selection_"+zone.id,zone.overlay.sprite.name);
            Assert.AreEqual("vignette_"+zone.id,zone.vignette.name);
            Assert.AreEqual(0,zone.marker.GetComponent<UnityEngine.UI.Button>().onClick.GetPersistentEventCount());
        }
    }
    [Test] public void EveryHitPolygonMatchesTheAuthoritativeMasterCoordinates()
    {
        var layout=JObject.Parse(File.ReadAllText("docs/art/layouts/map-layout.json"));
        foreach(var zone in view.Zones)
        {
            var source=layout["zones"].First(z=>(string)z["catalog_id"]==zone.id)["polygon_px"];
            CollectionAssert.AreEqual(source.Select(p=>new Vector2((float)p[0],(float)p[1])).ToArray(),zone.marker.GetComponentInChildren<MapPolygonHitArea>().Polygon);
        }
    }
    [Test] public void LongCopyHasALayoutDrivenMaskedSidebarAndFixedActions()
    {
        var scroll=panel.Find("DecisionSidebar/Details").GetComponent<UnityEngine.UI.ScrollRect>();
        Assert.IsNotNull(scroll.viewport.GetComponent<UnityEngine.UI.Mask>());
        Assert.IsNotNull(scroll.content.GetComponent<UnityEngine.UI.VerticalLayoutGroup>());
        Assert.AreEqual(UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize,scroll.content.GetComponent<UnityEngine.UI.ContentSizeFitter>().verticalFit);
        Assert.IsFalse(scroll.horizontal); Assert.IsTrue(scroll.vertical);
        Assert.IsFalse(scroll.transform.IsChildOf(master));
        Assert.IsFalse(panel.Find("DecisionFooter").IsChildOf(scroll.transform));
        Assert.IsNotNull(scroll.verticalScrollbar.handleRect.GetComponent<UnityEngine.UI.Image>().sprite);
        Assert.AreEqual(Vector2.zero,scroll.verticalScrollbar.handleRect.sizeDelta,"Scrollbar controls size through anchors only");
    }
}
