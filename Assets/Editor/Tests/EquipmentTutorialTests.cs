using System.Linq;
using Game.Adapter.In.UI;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;

public sealed class EquipmentTutorialTests
{
    [TestCase(0, false, false, 0)]
    [TestCase(3, false, false, 1)] // A cart is not a purchase and is rebuilt after reload.
    [TestCase(3, false, true, 2)] // A closed cart must be reopened through the real store button.
    [TestCase(0, true, false, 5)] // Never ask to buy the basic equipment twice.
    [TestCase(6, true, false, 6)]
    public void ResumeUsesCommittedPurchasesAndCurrentCart(int saved, bool owned, bool selected, int expected)
        => Assert.AreEqual(expected, EquipmentTutorial.ResumeStep(saved, owned, selected));

    [TestCase(1920, 1080)]
    [TestCase(1366, 768)]
    [TestCase(1280, 720)]
    public void EveryStepFitsItsSurfaceAndNavigationKeepsOneInstance(int width, int height)
    {
        var root = new GameObject("Equipment tutorial layout test", typeof(RectTransform), typeof(Canvas));
        var previewScene = EditorSceneManager.NewPreviewScene();
        SceneManager.MoveGameObjectToScene(root, previewScene);
        root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        var rect = root.GetComponent<RectTransform>(); rect.sizeDelta = new Vector2(width, height);
        try
        {
            var tutorial = EquipmentTutorial.Create(root.transform);
            int objects = tutorial.GetComponentsInChildren<Transform>(true).Length;
            for (int step = 0; step < EquipmentTutorial.CompletedStep; step++)
            {
                tutorial.Show(step); Canvas.ForceUpdateCanvases();
                var card = tutorial.transform.Find("Card").GetComponent<RectTransform>();
                Assert.LessOrEqual(card.rect.width, width - 48);
                Assert.LessOrEqual(card.rect.height, height - 48);
                foreach (var text in tutorial.GetComponentsInChildren<TMP_Text>().Where(t => t.gameObject.activeInHierarchy))
                {
                    text.ForceMeshUpdate();
                    Assert.IsFalse(text.isTextOverflowing, $"Step {step}: {text.name}");
                }
                Assert.AreEqual(objects, tutorial.GetComponentsInChildren<Transform>(true).Length);
                if (width == 1920) Capture(root, previewScene, width, height, step);
            }
            tutorial.Show(EquipmentTutorial.CompletedStep);
            Assert.IsFalse(tutorial.IsVisible);
        }
        finally { Object.DestroyImmediate(root); EditorSceneManager.ClosePreviewScene(previewScene); }
    }

    public static void Capture(GameObject root, Scene scene, int width, int height, int step, RectTransform scrollViewport = null)
    {
        var cameraObject = new GameObject("Tutorial preview camera", typeof(Camera));
        SceneManager.MoveGameObjectToScene(cameraObject, scene);
        var camera = cameraObject.GetComponent<Camera>();
        camera.enabled = false; camera.orthographic = true; camera.orthographicSize = height * .5f;
        camera.transform.position = new Vector3(0, 0, -10); camera.nearClipPlane = .1f; camera.farClipPlane = 100;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Game.Adapter.In.UI.Theme.GamePalette.Plum;
        camera.aspect = (float)width / height; camera.scene = scene;
        root.GetComponent<Canvas>().worldCamera = camera;
        var target = new RenderTexture(width, height, 24);
        var previous = RenderTexture.active;
        var image = new Texture2D(width, height, TextureFormat.RGB24, false);
        try
        {
            target.Create(); Canvas.ForceUpdateCanvases();
            foreach (var guide in root.GetComponentsInChildren<EquipmentTutorial>()) guide.RefreshLayout();
            Canvas.ForceUpdateCanvases();
            foreach (var guide in root.GetComponentsInChildren<EquipmentTutorial>().Where(g => g.ActionTarget != null))
            {
                var eventsRoot = new GameObject("Spotlight raycast test", typeof(UnityEngine.EventSystems.EventSystem));
                try
                {
                    var pointer = new UnityEngine.EventSystems.PointerEventData(eventsRoot.GetComponent<UnityEngine.EventSystems.EventSystem>()) {
                        position = RectTransformUtility.WorldToScreenPoint(camera, guide.ActionTarget.TransformPoint(guide.ActionTarget.rect.center))
                    };
                    var hits = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
                    guide.GetComponent<UnityEngine.UI.GraphicRaycaster>().Raycast(pointer, hits);
                    Assert.IsEmpty(hits, "The tutorial must leave the highlighted control clickable.");
                    if (scrollViewport != null)
                    {
                        pointer.position = RectTransformUtility.WorldToScreenPoint(camera,
                            scrollViewport.TransformPoint(scrollViewport.rect.center));
                        guide.GetComponent<UnityEngine.UI.GraphicRaycaster>().Raycast(pointer, hits);
                        Assert.IsEmpty(hits, "The tutorial must let wheel and drag input reach the cart viewport.");
                    }
                }
                finally { Object.DestroyImmediate(eventsRoot); }
            }
            RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
            System.IO.Directory.CreateDirectory("output/equipment-tutorial");
            System.IO.File.WriteAllBytes($"output/equipment-tutorial/step-{step + 1}.png", image.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous;
            Object.DestroyImmediate(image); Object.DestroyImmediate(target); Object.DestroyImmediate(cameraObject);
        }
    }
}
