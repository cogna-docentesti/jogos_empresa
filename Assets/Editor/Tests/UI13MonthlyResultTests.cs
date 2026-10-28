using System;
using System.Linq;
using Game.Adapter.In.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class UI13MonthlyResultTests
{
    [TestCase(123456.78f,"123.456,78")] [TestCase(-123456.78f,"123.456,78")] [TestCase(158533.33f,"158.533,33")]
    public void LargeStoredFloatAmountsKeepTheirCentsInPresentation(float amount,string ending)
    { Assert.That(MonthlyResultPresentation.Money(amount),Does.EndWith(ending)); }
    [TestCase("HIGH","Alto")] [TestCase("ADEQUATE","Adequado")] [TestCase("FRAGILE","Frágil")] [TestCase("CRITICAL","Crítico")] [TestCase(null,"Não registrada")]
    public void ClassificationUsesPortuguesePresentationWithoutChangingTheStoredValue(string stored,string label)
    {
        var r=new RoundResultEntity {alignmentScore=74,alignmentClassification=stored};
        Assert.AreEqual("74 • "+label,MonthlyResultPresentation.Build(r)[18].Value);
        Assert.AreEqual(stored,r.alignmentClassification);
    }
    [TestCase(-2500f)] [TestCase(0f)] [TestCase(2500f)]
    public void PersistedCashModelIsPresentedWithoutReapplyingSignedEvents(float impact)
    {
        using var db = new SQLite4Unity3d.SQLiteConnection(":memory:"); db.CreateTable<RoundResultEntity>();
        var r = new RoundResultEntity { roundResultId = "known", sessionId = "isolated", round = 1,
            grossRevenue = 10800, supplyCost = 4680, salaries = 4000, rent = 500, utilities = 100,
            loanPayment = 330, loanPrincipalPayment = 300, eventCashImpact = impact,
            netResult = 1190 + impact, openingCash = 10000, closingCash = 11190 + impact, customers = 360,
            baseDemand = 300, potentialDemand = 360, serviceCapacity = 1500, averageTicket = 30,
            alignmentScore = 90, alignmentClassification = "HIGH", coherenceFactor = 1.2f,
            reputationAtStart = 50, reputationAtEnd = 52, reputationDelta = 2 };
        db.Insert(r); var saved = new RoundResultRepository(db).GetById("known");
        for(int i=0;i<3;i++)
        {
            var rows=MonthlyResultPresentation.Build(saved);
            Assert.AreEqual(MonthlyResultPresentation.SignedMoney(impact),rows[8].Value);
            Assert.AreEqual(MonthlyResultPresentation.SignedMoney(r.netResult),rows[9].Value);
            Assert.AreEqual(MonthlyResultPresentation.SignedMoney(r.closingCash),rows[11].Value);
            Assert.AreEqual(-9610,rows[7].Amount); Assert.AreEqual(-330,rows[6].Amount);
            Assert.AreEqual(-300,rows[12].Amount); Assert.AreEqual(-30,rows[13].Amount);
            Assert.AreEqual(360.ToString(),rows[14].Value); Assert.AreEqual(22,rows.Count);
        }
        Assert.AreEqual(1,db.Table<RoundResultEntity>().Count());
        Assert.AreEqual(r.closingCash,new RoundResultRepository(db).GetById("known").closingCash);
    }
    [TestCase(0f)] [TestCase(100f)] [TestCase(-100f)]
    public void ZeroAndBothSignsHaveDistinctLegibleSemantics(float value)
    {
        var c=MonthlyResultView.Semantic(value);
        Assert.AreEqual(value==0?Game.Adapter.In.UI.Theme.GamePalette.Ink:value>0?Game.Adapter.In.UI.Theme.GamePalette.PositiveReportText:Game.Adapter.In.UI.Theme.GamePalette.NegativeReportText,c);
    }
    [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)] [TestCase(float.NegativeInfinity)]
    public void NonfinitePersistedValuesAreUnavailableInsteadOfFabricatedZero(float value)
    { Assert.Throws<InvalidOperationException>(()=>MonthlyResultPresentation.Build(new RoundResultEntity { openingCash=value })); }
    [Test] public void MissingResultIsNotAZeroStatement()
    { Assert.Throws<ArgumentNullException>(()=>MonthlyResultPresentation.Build(null)); }
    [Test] public void MonthlyCompositionMatchesApprovedPivotsAndOrder()
    {
        var scene=UnityEngine.SceneManagement.SceneManager.GetSceneByPath("Assets/Scenes/GameScene.unity");
        bool opened=!scene.isLoaded;
        if(opened)scene=UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity",UnityEditor.SceneManagement.OpenSceneMode.Additive);
        try
        {
            var view=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MonthlyResultView>(true)).Single();
            var viewport=(RectTransform)view.transform.Find("MonthlyIllustration");Assert.AreEqual(new Vector2(1312,-100),viewport.anchoredPosition);Assert.AreEqual(new Vector2(560,420),viewport.sizeDelta);Assert.AreEqual(Vector3.one*(584f/560f),viewport.localScale);Assert.NotNull(viewport.GetComponent<UnityEngine.UI.RectMask2D>());
            var contract=Newtonsoft.Json.Linq.JObject.Parse(System.IO.File.ReadAllText("docs/art/layouts/dre-layout.json"));int order=0;
            foreach(var layer in contract["layers"])
            {
                var id=(string)layer["asset_id"];var image=viewport.Find(id).GetComponent<UnityEngine.UI.Image>();var r=image.rectTransform;
                Assert.AreEqual(order++,r.GetSiblingIndex());Assert.AreEqual(Color.white,image.color);Assert.IsTrue(image.preserveAspect);Assert.IsFalse(image.raycastTarget);
                var s=(float)layer["scale"];var expected=new Vector2(image.sprite.rect.width*s,image.sprite.rect.height*s);Assert.AreEqual(expected,r.sizeDelta);
                Assert.AreEqual(new Vector2((float)layer["position_px"][0]-expected.x*.5f-1320,-((float)layer["position_px"][1]-expected.y*.5f-120)),r.anchoredPosition);
            }
            Assert.AreEqual(22,view.Rows.Count);Assert.AreEqual(4,view.Bars.Count);Assert.NotNull(view.ContinueButton.GetComponent<SharedControlAppearance>());
        }
        finally { if(opened)UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene,true); }
    }
}
