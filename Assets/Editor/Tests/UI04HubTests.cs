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

public sealed class UI04HubTests
{
    private Scene scene;
    private bool opened;
    private Transform canvas,master;
    [SetUp] public void Open()
    {
        scene=SceneManager.GetSceneByPath("Assets/Scenes/GameScene.unity"); opened=!scene.isLoaded;
        if(opened) scene=EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity",OpenSceneMode.Additive);
        canvas=scene.GetRootGameObjects().First(r=>r.name=="Canvas").transform;
        master=canvas.Find("Panel_Menu/MapMaster"); Assert.IsNotNull(master);
    }
    [TearDown] public void Close() { if(opened) EditorSceneManager.CloseScene(scene,true); }
    [Test] public void EveryCompactLayerUsesItsAuthoritativeBoundsAndOrder()
    {
        var layout=JObject.Parse(File.ReadAllText("docs/art/layouts/map-layout.json"));
        int previous=-1;
        foreach(var layer in layout["layers"].OrderBy(l=>(int)l["order"]))
        {
            string id=(string)layer["asset_id"]; var rect=(RectTransform)master.Find(id);
            Assert.IsNotNull(rect,id); Assert.AreEqual(new Vector2(0,1),rect.pivot,id);
            Assert.AreEqual(new Vector2((float)layer["position_px"][0],-(float)layer["position_px"][1]),rect.anchoredPosition,id);
            var image=rect.GetComponent<UnityEngine.UI.Image>(); Assert.IsFalse(image.raycastTarget,id); Assert.AreEqual(Color.white,image.color,id);
            Assert.AreEqual(image.sprite.rect.size,rect.sizeDelta,id); Assert.Greater(rect.GetSiblingIndex(),previous,id); previous=rect.GetSiblingIndex();
            string path=AssetDatabase.GetAssetPath(image.sprite);
            if(id.StartsWith("MAP_") && new[]{"MAP_UNIVERSITY","MAP_BANK","MAP_STORE","MAP_CONDOMINIUM","MAP_MARKETING"}.Contains(id)) Assert.That(path,Does.Contain("/compact/"));
        }
    }
    [Test] public void PolygonsAndMarkersShareTheMasterWithNoIndependentScaling()
    {
        var rect=(RectTransform)master; Assert.AreEqual(new Vector2(24,-176),rect.anchoredPosition);
        Assert.AreEqual(Vector3.one*(2f/3f),rect.localScale); Assert.AreEqual(new Vector2(1920,1080),rect.sizeDelta);
        var layout=JObject.Parse(File.ReadAllText("docs/art/layouts/map-layout.json"));
        foreach(var node in master.GetComponentsInChildren<MapMenuNode>(true))
        {
            var area=node.GetComponentInChildren<MapPolygonHitArea>(true);
            var source=string.IsNullOrEmpty(node.LocationId)?layout["central_hit_area"]["polygon_px"]:layout["zones"].First(z=>(string)z["catalog_id"]==node.LocationId)["polygon_px"];
            CollectionAssert.AreEqual(source.Select(p=>new Vector2((float)p[0],(float)p[1])).ToArray(),area.Polygon);
            for(var t=area.transform;t!=master;t=t.parent) Assert.AreEqual(Vector3.one,t.localScale,t.name);
        }
    }
    [TestCase(1920,1080)] [TestCase(1366,768)] [TestCase(1280,720)]
    public void PolygonRaycastsFollowTheSameAffineTransformAtEachResolution(int width,int height)
    {
        var go=new GameObject("UI04 polygon",typeof(RectTransform),typeof(MapPolygonHitArea));
        try
        {
            var r=(RectTransform)go.transform; r.pivot=new Vector2(0,1); r.sizeDelta=new Vector2(1920,1080);
            r.position=new Vector3(24*height/1080f,height-176*height/1080f); r.localScale=Vector3.one*(height/1080f)*(2f/3f);
            var hit=go.GetComponent<MapPolygonHitArea>(); hit.Configure(new[]{new Vector2(1062,242),new Vector2(1236,315),new Vector2(1121,387),new Vector2(947,315)});
            foreach(var point in new[]{new Vector2(1100,315),new Vector2(1000,315),new Vector2(1200,200),new Vector2(900,500)})
            {
                var screen=RectTransformUtility.WorldToScreenPoint(null,r.TransformPoint(new Vector3(point.x,-point.y)));
                Assert.AreEqual(hit.Contains(point),hit.IsRaycastLocationValid(screen,null),$"{width}x{height}: {point}");
            }
        }
        finally { Object.DestroyImmediate(go); }
    }
    [Test] public void ExistingDestinationsAndNativePanelReferencesArePreserved()
    {
        var registry=canvas.Find("UiNavigation").GetComponent<PanelRegistry>();
        var expected=new[]{(PanelId.Menu,"Panel_Menu"),(PanelId.Establishment,"Panel_Establishment"),(PanelId.Financial,"Panel_Financial"),(PanelId.MenuPricing,"Panel_MenuPricing"),(PanelId.Team,"Panel_Team"),(PanelId.EquipmentStore,"Panel_Equipment")};
        foreach(var pair in expected) Assert.AreEqual(canvas.Find(pair.Item2).gameObject,registry.Resolve(pair.Item1));
        var nodes=master.GetComponentsInChildren<MapMenuNode>(true); Assert.AreEqual(6,nodes.Length);
        CollectionAssert.AreEquivalent(new[]{"university","bank","store","condominium","marketing"},nodes.Select(n=>n.LocationId).Where(id=>!string.IsNullOrEmpty(id)));
        Assert.AreEqual(PanelId.Financial,nodes.First(n=>n.LocationId=="bank").Destination);
        Assert.AreEqual(PanelId.Team,nodes.First(n=>n.LocationId=="university").Destination);
        Assert.AreEqual(PanelId.EquipmentStore,nodes.First(n=>n.LocationId=="store").Destination);
        Assert.AreEqual(PanelId.MenuPricing,nodes.First(n=>n.LocationId=="marketing").Destination);
        Assert.AreEqual(PanelId.Establishment,nodes.First(n=>n.LocationId=="").Destination);
        Assert.AreEqual(5,Resources.LoadAll<LocationData>("Locations").Length);
    }
    [Test] public void EveryEnabledGraphicRaycastIsAnExplicitHitAreaOrButton()
    {
        foreach(var image in master.GetComponentsInChildren<UnityEngine.UI.Image>(true).Where(i=>i.enabled && i.raycastTarget))
            Assert.IsTrue(image.GetComponent<MapPolygonHitArea>()!=null || image.name=="MarkerTouchTarget",image.name);
        foreach(var node in master.GetComponentsInChildren<MapMenuNode>(true))
        {
            var button=node.GetComponent<UnityEngine.UI.Button>();
            Assert.IsNotNull(node.GetComponent<PanelNavButton>()); Assert.AreEqual(UnityEngine.UI.Navigation.Mode.Explicit,button.navigation.mode);
            Assert.IsNotNull(button.navigation.selectOnRight); Assert.IsNotNull(button.navigation.selectOnLeft);
            var target=(RectTransform)node.transform.Find("MarkerTouchTarget"); Assert.GreaterOrEqual(target.rect.height*2f/3f*720/1080f,44);
        }
    }
    [Test] public void HudAndHubAreScopedToTheMenuAndPreviewStaysOff()
    {
        var panel=canvas.Find("Panel_Menu"); Assert.IsFalse(panel.gameObject.activeSelf);
        Assert.AreEqual(1,panel.GetComponentsInChildren<MenuHudView>(true).Length);
        Assert.IsNotNull(panel.GetComponent<HubMapView>());
        Assert.IsFalse(canvas.Find("SharedComponentsPreview").gameObject.activeSelf);
        Assert.IsFalse(canvas.Find("HubHistory/Back").gameObject.activeSelf);
    }
}
