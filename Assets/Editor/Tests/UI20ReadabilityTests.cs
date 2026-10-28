using System;
using System.Linq;
using System.Reflection;
using Game.Adapter.In.UI;
using Game.Adapter.In.UI.Theme;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;

public class UI20ReadabilityTests
{
    static float L(Color c){float V(float x)=>x<=.04045f?x/12.92f:Mathf.Pow((x+.055f)/1.055f,2.4f);return .2126f*V(c.r)+.7152f*V(c.g)+.0722f*V(c.b);}
    static float Contrast(Color a,Color b)=>(Mathf.Max(L(a),L(b))+.05f)/(Mathf.Min(L(a),L(b))+.05f);
    [TestCase(100f)][TestCase(-100f)][TestCase(0f)]
    public void SmallFinancialValuesRemainReadableOnLavender(float value)
    {
        var texture = new Texture2D(2,2);
        try {
            texture.LoadImage(System.IO.File.ReadAllBytes("Assets/Art/new/dre/components/table_total_column.png"));
            var pixel=texture.GetPixel(texture.width/2,texture.height/2);
            var surface=Color.Lerp(GamePalette.Surface,pixel,pixel.a);
            Assert.That(Contrast(MonthlyResultView.Semantic(value),surface),Is.GreaterThanOrEqualTo(4.5f));
        } finally { UnityEngine.Object.DestroyImmediate(texture); }
        Assert.That(Contrast(MonthlyResultView.Semantic(value),GamePalette.Surface),Is.GreaterThanOrEqualTo(4.5f));
    }
    [TestCase(FeedbackKind.Error)][TestCase(FeedbackKind.Warning)][TestCase(FeedbackKind.Success)]
    public void LongFeedbackFitsItsSurfaceWithoutShrinkingTheFont(FeedbackKind kind)
    {
        var root=new GameObject("Toast QA",typeof(RectTransform),typeof(Canvas));
        try
        {
            var panel=new GameObject("Surface",typeof(RectTransform),typeof(UnityEngine.UI.Image));panel.transform.SetParent(root.transform,false);((RectTransform)panel.transform).sizeDelta=new Vector2(944,72);
            var text=new GameObject("Message",typeof(RectTransform),typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();text.transform.SetParent(panel.transform,false);
            text.rectTransform.sizeDelta=new Vector2(812,52);Game.Adapter.In.UI.Theme.GameTypography.Apply(text,Game.Adapter.In.UI.Theme.TypographyRole.Body,24);
            text.textWrappingMode=TextWrappingModes.Normal;
            var close=new GameObject("Close",typeof(RectTransform),typeof(UnityEngine.UI.Button)).GetComponent<UnityEngine.UI.Button>();close.transform.SetParent(panel.transform,false);
            var toast=root.AddComponent<FeedbackToast>();void Set(string n,object v)=>typeof(FeedbackToast).GetField(n,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(toast,v);
            Set("panel",panel);Set("message",text);Set("surface",panel.GetComponent<UnityEngine.UI.Image>());Set("close",close);
            toast.Display("Não foi possível continuar. Seu resultado está salvo; tente novamente. Confira suas escolhas de localização, restaurante, cardápio, crédito, equipamentos e equipe antes de sair.",kind);
            text.ForceMeshUpdate();Assert.IsTrue(toast.IsVisible);Assert.IsFalse(text.isTextOverflowing);Assert.That(text.fontSize,Is.GreaterThanOrEqualTo(24));
            Assert.That(Contrast(text.color,GamePalette.Surface),Is.GreaterThanOrEqualTo(4.5f));Assert.That(((RectTransform)panel.transform).rect.height,Is.GreaterThan(72));
        }
        finally{UnityEngine.Object.DestroyImmediate(root);}
    }
}
