using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Game.Adapter.In.UI;
using Game.Adapter.In.UI.Navigation;
using TMPro;
using UnityEditor;
using UnityEngine;

public static class UI20ReferenceAudit
{
    public static void Run()
    {
        if(Application.dataPath.Replace('\\','/')!="D:/Cogna/jogos_empresa/Assets")throw new InvalidOperationException("Wrong project");
        var scene=UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity");
        var all=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();
        int missing=all.Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject));if(missing!=0)throw new InvalidOperationException("Missing scene scripts");
        var registry=UnityEngine.Object.FindFirstObjectByType<PanelRegistry>();if(registry.Bindings.Count!=12||registry.Bindings.Any(b=>b.root==null||registry.Resolve(b.id)!=b.root))throw new InvalidOperationException("Registry bindings");
        var listener=UnityEngine.Object.FindFirstObjectByType<UIStateListener>();
        var fields=typeof(UIStateListener).GetFields(BindingFlags.Instance|BindingFlags.NonPublic).Where(f=>f.FieldType==typeof(GameObject)&&f.IsDefined(typeof(SerializeField),true)).ToArray();
        foreach(var f in fields.Where(f=>!new[]{"roundStartPanel","roundSalesPanel","roundCostsPanel"}.Contains(f.Name)))if((GameObject)f.GetValue(listener)==null)throw new InvalidOperationException("Required panel "+f.Name);
        var prefabs=AssetDatabase.GetDependencies(scene.path,true).Where(p=>p.EndsWith(".prefab")).ToArray();
        foreach(var path in prefabs){var p=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(p.GetComponentsInChildren<Transform>(true).Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject))!=0)throw new InvalidOperationException("Missing prefab script "+path);}
        var events=all.Select(t=>t.GetComponent<EventScreenView>()).First(x=>x!=null);var team=all.Select(t=>t.GetComponent<TeamScreenView>()).First(x=>x!=null);
        var catalog=new List<object>();
        void Entry(string type,string id,Sprite sprite){if(sprite==null||!AssetDatabase.GetAssetPath(sprite).StartsWith("Assets/Art/new/"))throw new InvalidOperationException("Missing/new-art catalog reference "+type+"/"+id);catalog.Add(new{type,id,asset=AssetDatabase.GetAssetPath(sprite)});}
        foreach(var x in Resources.LoadAll<EquipmentData>("Equipments"))Entry("equipment",x.id,x.icon);
        foreach(var x in Resources.LoadAll<ProductData>("Products"))Entry("product",x.id,x.productSprite);
        foreach(var x in Resources.LoadAll<RestaurantData>("Restaurants"))Entry("restaurant",x.id,x.facade);
        foreach(var x in Resources.LoadAll<CreditLineData>("CreditLines"))Entry("credit",x.id,x.icon);
        foreach(var x in Resources.LoadAll<EventData>("Events"))Entry("event",x.id,events.Illustration(x.id));
        foreach(var x in Resources.LoadAll<RoleData>("Roles"))if(!team.HasVisual(x.id)||x.roleType==RoleType.GARCOM)throw new InvalidOperationException("Role art/model mismatch "+x.id);
        var sprites=new List<object>();
        foreach(var p in Directory.GetFiles("Assets/Art/new","*.png",SearchOption.AllDirectories))
        {
            var path=p.Replace('\\','/');var i=(TextureImporter)AssetImporter.GetAtPath(path);if(i.spriteImportMode!=SpriteImportMode.Single)throw new InvalidOperationException("Sprite mode "+path);
            sprites.Add(new{path,guid=AssetDatabase.AssetPathToGUID(path),mode=i.spriteImportMode.ToString(),i.mipmapEnabled,i.alphaIsTransparency,i.maxTextureSize,border=new[]{i.spriteBorder.x,i.spriteBorder.y,i.spriteBorder.z,i.spriteBorder.w},pivot=new[]{i.spritePivot.x,i.spritePivot.y}});
        }
        if(catalog.Count!=52 || Resources.LoadAll<RoleData>("Roles").Length!=5 || Resources.LoadAll<LocationData>("Locations").Length!=5)throw new InvalidOperationException("Catalog count drift");
        var serialized=new List<object>();
        foreach(var component in all.SelectMany(t=>t.GetComponents<MonoBehaviour>()).Where(c=>c!=null))
        {
            var so=new SerializedObject(component);var it=so.GetIterator();while(it.Next(true))if(it.propertyType==SerializedPropertyType.ObjectReference&&it.objectReferenceValue is Sprite sprite)serialized.Add(new{component=component.GetType().Name,objectName=component.name,property=it.propertyPath,asset=AssetDatabase.GetAssetPath(sprite)});
        }
        var canvas=scene.GetRootGameObjects().Single(g=>g.name=="Canvas");
        var legacy=canvas.transform.Find("GameObject");if(legacy==null||legacy.gameObject.activeSelf)throw new InvalidOperationException("Oversized legacy backdrop must be retired");
        var camera=all.Select(t=>t.GetComponent<Camera>()).Single(c=>c!=null&&c.CompareTag("MainCamera"));
        var data=new{passed=true,scene=scene.path,legacyBackdropActive=legacy.gameObject.activeSelf,cameraBackground=ColorUtility.ToHtmlStringRGB(camera.backgroundColor),canvasMatchMode=canvas.GetComponent<UnityEngine.UI.CanvasScaler>().screenMatchMode.ToString(),missingScripts=missing,registryBindings=registry.Bindings.Select(b=>new{id=b.id.ToString(),number=(int)b.id,root=b.root.name}),prefabs,catalog,roles=Resources.LoadAll<RoleData>("Roles").Select(r=>r.id),locations=Resources.LoadAll<LocationData>("Locations").Select(r=>r.id),sprites,serialized,productionFrame=all.Count(t=>t.GetComponent<Game.Adapter.In.UI.Layout.ProductionCanvasLayout>()!=null),packageNewtonsoft=File.ReadAllText("Packages/manifest.json").Contains("\"com.unity.nuget.newtonsoft-json\": \"3.2.2\""),temporaryPipelineRemoved=!File.ReadAllText("Packages/manifest.json").Contains("com.unity.pipeline")};
        // JsonUtility serializes Unity value structs without computed property cycles.
        File.WriteAllText("output/ui-update/UI-20/reference-audit.json",Newtonsoft.Json.JsonConvert.SerializeObject(data,Newtonsoft.Json.Formatting.Indented,new Newtonsoft.Json.JsonSerializerSettings{ReferenceLoopHandling=Newtonsoft.Json.ReferenceLoopHandling.Ignore}));
    }
}
