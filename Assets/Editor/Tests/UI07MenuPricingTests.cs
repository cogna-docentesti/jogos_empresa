using System.Linq;
using System.Reflection;
using Game.Adapter.In.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class UI07MenuPricingTests
{
    private static T Field<T>(object o,string n)=>(T)o.GetType().GetField(n,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(o);
    [Test] public void AllNineProductIdsMapToTheApprovedWholeImagesWithoutEconomicChanges()
    {
        var products=Resources.LoadAll<ProductData>("Products").OrderBy(p=>p.id).ToArray();
        var expectedIds=new[]{"FRA_MENU_CLASSICO","FRA_MENU_PREMIUM","FRA_PRATO_EXECUTIVO","JAP_COMBINADO_MEDIO","JAP_COMBINADO_PREMIUM","JAP_SUSHI_EXECUTIVO","LAN_COMBO_COMPLETO","LAN_COMBO_ESPECIAL","LAN_COMBO_SIMPLES"};
        CollectionAssert.AreEqual(expectedIds,products.Select(p=>p.id));
        CollectionAssert.AreEqual(new[]{150,215,110,130,175,90,58,80,42},products.Select(p=>p.price));
        CollectionAssert.AreEqual(new[]{122.5f,173.5f,84.5f,107,140,71,47.5f,67.5f,35},products.Select(p=>p.minPrice));
        CollectionAssert.AreEqual(new[]{180f,260,130,160,210,110,70,95,52},products.Select(p=>p.maxPrice));
        for(int i=0;i<products.Length;i++)
        {
            var p=products[i];var path="Assets/Art/new/menu_pricing/products/"+p.id.Substring(0,3).ToLowerInvariant()+"/"+p.id.ToLowerInvariant()+".png";
            Assert.AreSame(AssetDatabase.LoadAssetAtPath<Sprite>(path),p.productSprite,p.id);
            Assert.AreEqual(SpriteImportMode.Single,((TextureImporter)AssetImporter.GetAtPath(path)).spriteImportMode);
            Assert.AreEqual(new Rect(0,0,512,512),p.productSprite.rect);
        }
        foreach(var r in Resources.LoadAll<RestaurantData>("Restaurants"))
            Assert.IsTrue(r.products.All(p=>p.id.StartsWith(r.type==RestaurantType.PODRAO?"LAN_":r.type==RestaurantType.JAPONES?"JAP_":"FRA_")));
    }
    [Test] public void InclusiveLimitsAndBothDecimalSeparatorsAreAcceptedForEveryProduct()
    {
        foreach(var p in Resources.LoadAll<ProductData>("Products"))
        foreach(var price in new[]{p.minPrice,p.maxPrice,(float)p.price})
        foreach(var separator in new[]{".",","})
        {
            var raw=price.ToString("F2",System.Globalization.CultureInfo.InvariantCulture).Replace(".",separator);
            Assert.IsTrue(ProductPricingItemView.TryParsePrice(raw,p,out float parsed),p.id+" "+raw);
            Assert.AreEqual(price,parsed,.001f);
        }
    }
    [TestCase("")][TestCase(" ")][TestCase("NaN")][TestCase("Infinity")][TestCase("-42")][TestCase("+42")]
    [TestCase("42,123")][TestCase("42.123")][TestCase("4e1")][TestCase("R$ 42")][TestCase("1.000,00")]
    [TestCase("34,99")][TestCase("52,01")][TestCase("9999999999999999")][TestCase("42,")]
    public void InvalidTextCannotBecomeAConfirmedPrice(string raw)
    {Assert.IsFalse(ProductPricingItemView.TryParsePrice(raw,Resources.LoadAll<ProductData>("Products").Single(p=>p.id=="LAN_COMBO_SIMPLES"),out _));}
    [Test] public void InvalidInputKeepsLastValidValueAndReadOnlyControlsCannotMutateIt()
    {
        var instance=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Card_ProductItem.prefab"));
        try
        {
            var item=instance.GetComponent<ProductPricingItemView>();var p=Resources.LoadAll<ProductData>("Products").First();item.Setup(p,p.price);
            item.RestoreDraftInput("inválido");Assert.IsFalse(item.IsPriceValid);Assert.AreEqual(p.price,item.SelectedPrice);Assert.AreEqual("inválido",item.DraftInput);
            item.RestoreDraftInput(p.maxPrice.ToString(System.Globalization.CultureInfo.InvariantCulture));Assert.IsTrue(item.IsPriceValid);Assert.AreEqual(p.maxPrice,item.SelectedPrice);
            Assert.IsFalse(Field<UnityEngine.UI.Button>(item,"increaseButton").interactable);
            item.SetReadOnly(true);
            foreach (var button in item.GetComponentsInChildren<UnityEngine.UI.Button>())
            {
                Assert.IsFalse(button.interactable, button.name);
                button.onClick.Invoke();
            }
            Assert.AreEqual(p.maxPrice,item.SelectedPrice);
        }
        finally{Object.DestroyImmediate(instance);}
    }
    [Test]
    public void AdjustmentsPreserveOffGridPricesAndRejectOutOfRangeDeltas()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Card_ProductItem.prefab");
        var instance = Object.Instantiate(prefab);
        try
        {
            var item = instance.GetComponent<ProductPricingItemView>();
            var product = Resources.LoadAll<ProductData>("Products").Single(p => p.id == "LAN_COMBO_SIMPLES");
            item.Setup(product, 42.37f);
            var decrease = Field<UnityEngine.UI.Button>(item, "decreaseButton");
            var decreaseHalf = Field<UnityEngine.UI.Button>(item, "decreaseHalfButton");
            var increaseHalf = Field<UnityEngine.UI.Button>(item, "increaseHalfButton");
            var increase = Field<UnityEngine.UI.Button>(item, "increaseButton");
            Assert.IsEmpty(item.GetComponentsInChildren<UnityEngine.UI.Slider>(true));
            Assert.AreEqual(42.37f, item.SelectedPrice);
            foreach (var (button, expected) in new[] {
                (decrease, 41.37f), (decreaseHalf, 40.87f), (increaseHalf, 41.37f), (increase, 42.37f)
            })
            {
                button.onClick.Invoke();
                Assert.AreEqual(expected, item.SelectedPrice);
            }
            for (int click = 0; click < 50; click++)
            {
                increaseHalf.onClick.Invoke();
                decreaseHalf.onClick.Invoke();
            }
            Assert.AreEqual(42.37f, item.SelectedPrice);
            Assert.AreEqual("42,37", item.DraftInput);

            item.RestoreDraftInput("35,50");
            Assert.IsFalse(decrease.interactable);
            Assert.IsTrue(decreaseHalf.interactable);
            decrease.onClick.Invoke();
            Assert.AreEqual(35.50f, item.SelectedPrice);
            decreaseHalf.onClick.Invoke();
            Assert.AreEqual(product.minPrice, item.SelectedPrice);
            Assert.IsFalse(decreaseHalf.interactable);

            item.RestoreDraftInput("51.25");
            Assert.IsFalse(increase.interactable);
            Assert.IsTrue(increaseHalf.interactable);
            increaseHalf.onClick.Invoke();
            Assert.AreEqual(51.75f, item.SelectedPrice);
            Assert.IsFalse(increaseHalf.interactable);
        }
        finally { Object.DestroyImmediate(instance); }
    }
    [Test] public void CurrencyUsesBrazilianDecimalsAndThousands()
    {Assert.AreEqual("R$ 1.234,50",ProductPricingItemView.Money(1234.5f));Assert.AreEqual("R$ 1.234.567,50",ProductPricingItemView.Money(1234567.5f));}
    [TestCase(0f)][TestCase(.35f)][TestCase(1f)]
    public void SolidCoherenceFillUsesProportionalGeometry(float value)
    {
        var root=new GameObject("Coherence QA",typeof(RectTransform));root.SetActive(false);
        try
        {
            var view=root.AddComponent<MenuPricingScreenView>();var track=new GameObject("Track",typeof(RectTransform));track.transform.SetParent(root.transform,false);
            ((RectTransform)track.transform).sizeDelta=new Vector2(1000,16);
            var fill=new GameObject("Fill",typeof(RectTransform),typeof(UnityEngine.UI.Image));fill.transform.SetParent(track.transform,false);
            typeof(MenuPricingScreenView).GetField("priceCoherenceBarFill",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(view,fill.GetComponent<UnityEngine.UI.Image>());
            view.UpdatePriceCoherence(value,"QA");
            Assert.AreEqual(value,((RectTransform)fill.transform).anchorMax.x,.0001f);
            Assert.AreEqual(1000*value,((RectTransform)fill.transform).rect.width,.01f);
        }
        finally{Object.DestroyImmediate(root);}
    }
}
