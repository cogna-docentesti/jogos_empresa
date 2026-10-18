using System.Linq;
using System.Reflection;
using Game.Adapter.In.Controllers;
using Game.Adapter.In.UI;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;

public sealed class UI10EquipmentTests
{
    private GameObject instance;
    private static T Field<T>(object o, string n) => (T)o.GetType().GetField(n, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(o);
    private EquipmentCardView Card(string id)
    {
        GameSessionState.Set(new GameSessionEntity { status = GameSessionStatus.IN_PROGRESS });
        instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Card_equipment.prefab"));
        var card = instance.GetComponent<EquipmentCardView>();
        typeof(EquipmentCardView).GetMethod("OnEnable",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(card,null);
        card.Setup(Resources.LoadAll<EquipmentData>("Equipments").Single(e => e.id == id)); card.SetCartAvailable(true); return card;
    }
    [TearDown] public void Clean() { if (instance != null) Object.DestroyImmediate(instance); GameSessionState.Clear(); }
    [TestCase("bancada_inox_preparo",1500)][TestCase("conjunto_panelas_industriais",1200)]
    [TestCase("fogao_industrial_6_bocas",2500)][TestCase("pia_industrial_inox",1800)]
    [TestCase("freezer_horizontal",3000)][TestCase("geladeira_comercial_dupla",4500)]
    [TestCase("mobiliario_basico",6000)][TestCase("sistema_pdv_computador",3500)]
    [TestCase("chapa_profissional_podrao",1500)][TestCase("fritadeira_eletrica_dupla_podrao",1800)]
    [TestCase("balcao_refrigerado_sushi",5500)][TestCase("facas_especiais_japones",1200)]
    [TestCase("panela_eletrica_arroz",800)][TestCase("adega_climatizada",4500)]
    [TestCase("forno_combinado_inteligente",18000)][TestCase("mobiliario_luxo",12000)]
    public void WholeArtworkIsMappedByCatalogIdWithOriginalPrice(string id, int price)
    {
        var card = Card(id); var data = card.Equipment; Assert.AreEqual(price, data.cost); Assert.AreEqual(0, data.capacityBonus); Assert.AreEqual(0, data.qualityBonus);
        var image = Field<UnityEngine.UI.Image>(card,"equipmentImage"); Assert.AreSame(data.icon,image.sprite); Assert.AreEqual(Color.white,image.color); Assert.IsTrue(image.preserveAspect); Assert.AreEqual(UnityEngine.UI.Image.Type.Simple,image.type);
        string path = AssetDatabase.GetAssetPath(data.icon); Assert.That(path, Does.StartWith("Assets/Art/new/equipment/")); Assert.That(path, Does.EndWith("/"+id+".png"));
        Assert.AreEqual(new Rect(0,0,512,512),data.icon.rect); Assert.AreEqual(SpriteImportMode.Single,((TextureImporter)AssetImporter.GetAtPath(path)).spriteImportMode);
    }
    [TestCase(RestaurantType.PODRAO,10)][TestCase(RestaurantType.JAPONES,11)][TestCase(RestaurantType.FRANCES,11)]
    public void RestaurantCompatibilityKeepsAllEightBasicsAndOnlyApplicableSpecifics(RestaurantType type,int count)
    {
        var method=typeof(EquipmentScreenController).GetMethod("IsVisibleForRestaurant",BindingFlags.Static|BindingFlags.NonPublic);
        var all=Resources.LoadAll<EquipmentData>("Equipments"); Assert.AreEqual(16,all.Length); Assert.AreEqual(16,all.Select(e=>e.id).Distinct().Count());
        var visible=all.Where(e=>(bool)method.Invoke(null,new object[]{e,type})).ToArray(); Assert.AreEqual(count,visible.Length); Assert.AreEqual(8,visible.Count(e=>e.category==EquipmentCategory.BASIC));
        Assert.IsTrue(visible.Where(e=>e.category==EquipmentCategory.SPECIFIC).All(e=>e.applicableTypes.Contains(type)));
    }
    [Test] public void QuantityIsZeroOrOneAndOwnedItemsCannotBePurchasedAgain()
    {
        var c=Card("sistema_pdv_computador"); Assert.AreEqual("0",Field<TMP_Text>(c,"quantityText").text); int calls=0; c.SelectionRequested+=_=>calls++;
        Field<UnityEngine.UI.Button>(c,"buyButton").onClick.Invoke(); Assert.AreEqual(1,calls);
        c.SetInCart(true); Assert.AreEqual("1",Field<TMP_Text>(c,"quantityText").text); Assert.IsFalse(Field<UnityEngine.UI.Button>(c,"buyButton").interactable); Assert.IsTrue(Field<UnityEngine.UI.Button>(c,"removeButton").interactable);
        c.SetOwned(true); c.SetInCart(true); Assert.IsFalse(c.IsInCart); Assert.IsTrue(c.IsOwned); Assert.AreEqual("1",Field<TMP_Text>(c,"quantityText").text);
        Field<UnityEngine.UI.Button>(c,"buyButton").onClick.Invoke(); Assert.AreEqual(1,calls); Assert.AreEqual("Adquirido",Field<TMP_Text>(c,"statusText").text);
    }
    [Test] public void BlockedSpecificsCannotRequestAcquisitionAndExplainPrerequisite()
    {
        var c=Card("facas_especiais_japones"); c.SetCartAvailable(false); int calls=0; c.SelectionRequested+=_=>calls++;
        Field<UnityEngine.UI.Button>(c,"buyButton").onClick.Invoke(); Assert.AreEqual(0,calls); Assert.IsFalse(c.GetComponent<UnityEngine.UI.Button>().interactable); Assert.That(Field<TMP_Text>(c,"statusText").text,Does.Contain("8 básicos"));
    }
    [Test] public void MissingSessionDisablesQuantityActionsWithoutChangingData()
    {
        var c=Card("sistema_pdv_computador"); GameSessionState.Clear(); c.SetCartAvailable(false);
        Assert.That(Field<TMP_Text>(c,"statusText").text,Does.Contain("indisponível")); Assert.IsFalse(Field<UnityEngine.UI.Button>(c,"buyButton").interactable);
    }
    [Test] public void LongCatalogNamesExpandCardInsteadOfShrinkingOrCroppingText()
    {
        var c=Card("mobiliario_basico"); var text=Field<TMP_Text>(c,"nameText"); text.text=string.Join(" ",Enumerable.Repeat("Mobiliário de restaurante com nome extenso",5));
        typeof(EquipmentCardView).GetMethod("LateUpdate",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(c,null); text.ForceMeshUpdate(); Assert.Greater(c.RequiredHeight,360); Assert.IsFalse(text.isTextOverflowing); Assert.IsFalse(text.enableAutoSizing);
    }
    [Test] public void SerializationKeepsUniqueIdsAndPurchaseCapacityCannotBecomeQuantity()
    {
        var d=EquipmentSelectionHelper.FromJson("{\"equipmentIds\":[\"sistema_pdv_computador\"]}");
        EquipmentSelectionHelper.AddUniqueIds(d,new[]{"sistema_pdv_computador","facas_especiais_japones"});
        Assert.AreEqual(2,d.equipmentIds.Count); Assert.IsTrue(EquipmentSelectionHelper.ContainsId(EquipmentSelectionHelper.FromJson(EquipmentSelectionHelper.ToJson(d)),"facas_especiais_japones"));
    }
}
