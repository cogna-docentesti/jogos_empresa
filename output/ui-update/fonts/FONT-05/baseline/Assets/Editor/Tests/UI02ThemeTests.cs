using System;
using System.Reflection;
using Game.Adapter.In.UI.Layout;
using Game.Adapter.In.UI.Theme;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;

public sealed class UI02ThemeTests
{
    [TestCase(GamePalette.HexIvory, GamePalette.HexInk)]
    [TestCase(GamePalette.HexViolet, GamePalette.HexIvory)]
    [TestCase(GamePalette.HexPlum, GamePalette.HexIvory)]
    [TestCase(GamePalette.HexLavender, GamePalette.HexPlum)]
    [TestCase(GamePalette.HexWarn, GamePalette.HexPlum)]
    [TestCase(GamePalette.HexIvory, GamePalette.HexErrorText)]
    public void TextSurfacePairsHaveReadableContrast(string background,string foreground)
    {
        float L(Color c) { float Channel(float x)=>x<=.04045f?x/12.92f:Mathf.Pow((x+.055f)/1.055f,2.4f); return .2126f*Channel(c.r)+.7152f*Channel(c.g)+.0722f*Channel(c.b); }
        float a=L(GamePalette.Parse(background)),b=L(GamePalette.Parse(foreground));
        Assert.GreaterOrEqual((Mathf.Max(a,b)+.05f)/(Mathf.Min(a,b)+.05f),4.5f);
    }

    [Test]
    public void FontMaterialsAndPortugueseCurrencyGlyphsAreAvailable()
    {
        foreach(TypographyRole role in System.Enum.GetValues(typeof(TypographyRole)))
        {
            var font=GameTypography.Resolve(role).Font;
            foreach(char c in "ÁÉÍÓÚáéíóúÂÊÔâêôÃÕãõÇç R$ -12.345,67") Assert.IsTrue(font.HasCharacter(c),role+" / "+c);
            Assert.IsFalse(font.material.IsKeywordEnabled("UNDERLAY_ON"));
            Assert.AreEqual(0,font.material.GetFloat("_OutlineWidth"));
        }
    }

    [Test]
    public void SafeAreaPreservesAnchorsAndDoesNotAccumulateOnRevisit()
    {
        var canvas=new GameObject("UI02 safe-area test",typeof(RectTransform),typeof(Canvas));
        canvas.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
        var child=new GameObject("Nested panel",typeof(RectTransform)); child.transform.SetParent(canvas.transform,false);
        var rect=(RectTransform)child.transform; rect.anchorMin=Vector2.zero; rect.anchorMax=Vector2.one;
        rect.offsetMin=new Vector2(24,16); rect.offsetMax=new Vector2(-24,-16);
        var fitter=child.AddComponent<SafeAreaFitter>();
        var apply=typeof(SafeAreaFitter).GetMethod("Apply",BindingFlags.NonPublic|BindingFlags.Instance,null,new[]{typeof(Rect),typeof(Vector2Int),typeof(bool)},null);
        var resolution=new Vector2Int(Screen.width,Screen.height);
        try
        {
            apply.Invoke(fitter,new object[]{new Rect(48,0,Screen.width-48,Screen.height),resolution,true});
            float first=rect.offsetMin.x; Assert.Greater(first,24);
            apply.Invoke(fitter,new object[]{new Rect(48,0,Screen.width-48,Screen.height),resolution,true});
            Assert.AreEqual(first,rect.offsetMin.x,.01f); Assert.AreEqual(Vector2.zero,rect.anchorMin); Assert.AreEqual(Vector2.one,rect.anchorMax);
            apply.Invoke(fitter,new object[]{new Rect(0,0,Screen.width,Screen.height),resolution,true});
            Assert.AreEqual(24,rect.offsetMin.x,.01f); Assert.AreEqual(-24,rect.offsetMax.x,.01f);
        }
        finally { UnityEngine.Object.DestroyImmediate(canvas); }
    }
}
