using System;
using System.Linq;
using Game.Adapter.In.UI;
using Game.Adapter.In.UI.Navigation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class UI14QuarterlyResultTests
{
    private static RoundResultEntity Saved(int month) => new()
    {
        roundResultId = "m" + month, sessionId = "quarter", round = month,
        grossRevenue = 10000 * month, supplyCost = 1000 * month, rent = 500, salaries = 2000, utilities = 200,
        loanPayment = 330, loanPrincipalPayment = 300, eventCashImpact = month == 2 ? -500 : 100,
        netResult = 6070 * month, openingCash = 10000 * month, closingCash = 16070 * month,
        customers = 100 * month, averageTicket = 10 * month, baseDemand = 200 * month,
        potentialDemand = 250 * month, serviceCapacity = 300 * month,
        alignmentScore = 60 + month, alignmentClassification = month == 3 ? "HIGH" : "ADEQUATE",
        coherenceFactor = month * .25f, reputationAtStart = 40 + month, reputationAtEnd = 44 + month, reputationDelta = 4
    };
    [TestCase(1)] [TestCase(2)] [TestCase(3)]
    public void SavedMonthsAreOrderedAndAbsentMonthsNeverBecomeZero(int count)
    {
        using var db = new SQLite4Unity3d.SQLiteConnection(":memory:"); db.CreateTable<RoundResultEntity>();
        for (int m = count; m > 0; m--) db.Insert(Saved(m));
        var report = QuarterlyResultPresentation.Build("quarter", new RoundResultRepository(db).GetBySessionId("quarter"));
        Assert.AreEqual(count, report.Count); Assert.AreEqual(count == 3, report.Complete); Assert.AreEqual(25, report.Rows.Count);
        Assert.AreEqual(10000 * count * (count + 1) / 2, report.Revenue);
        Assert.AreEqual(10000, report.Opening); Assert.AreEqual(16070 * count, report.Closing);
        for (int m = 0; m < 3; m++)
            Assert.AreEqual(m < count ? QuarterlyResultPresentation.SignedMoney(10000 * (m + 1)) : "Não salvo", report.Rows[0].Months[m].Text);
        for (int i = 0; i < 3; i++) QuarterlyResultPresentation.Build("quarter", new RoundResultRepository(db).GetBySessionId("quarter"));
        Assert.AreEqual(count, db.Table<RoundResultEntity>().Count()); Assert.AreEqual(16070, db.Find<RoundResultEntity>("m1").closingCash);
    }
    [Test]
    public void AggregationFollowsEachMetricAndNeverAddsBalancesOrLoanDetailsAgain()
    {
        var r = QuarterlyResultPresentation.Build("quarter", new[] { Saved(3), Saved(1), Saved(2) });
        double[] expected = { 60000,-6000,-1500,-6000,-600,0,-990,-15090,-300,36420,10000,48210,-900,-90,600,1200,1500,1800,14000d/600,62 };
        for (int i = 0; i < expected.Length; i++) Assert.That(r.Rows[i].Aggregate.Amount, Is.EqualTo(expected[i]).Within(.001), "row " + i);
        Assert.AreEqual("Alto", r.Rows[20].Aggregate.Text); Assert.AreEqual(.5, r.Rows[21].Aggregate.Amount);
        Assert.AreEqual(41, r.Rows[22].Aggregate.Amount); Assert.AreEqual(47, r.Rows[23].Aggregate.Amount); Assert.AreEqual(12, r.Rows[24].Aggregate.Amount);
        Assert.AreEqual(20000, r.Rows[0].Change.Amount); Assert.AreEqual(20000, r.Rows[10].Change.Amount);
        Assert.AreEqual("Δ M3 − M1", r.ChangeLabel); Assert.AreEqual("Não se aplica", r.Rows[20].Change.Text);
    }
    [Test] public void MissingMiddleMonthIsExplicitAndOnlySavedMonthsContribute()
    {
        var r=QuarterlyResultPresentation.Build("quarter",new[]{Saved(1),Saved(3)});
        Assert.AreEqual(2,r.Count);Assert.IsFalse(r.Complete);Assert.AreEqual("Não salvo",r.Rows[0].Months[1].Text);
        Assert.AreEqual(40000,r.Revenue);Assert.AreEqual(10000,r.Opening);Assert.AreEqual(48210,r.Closing);
    }
    [Test] public void OneMonthCannotImplyVariation()
    {
        var r=QuarterlyResultPresentation.Build("quarter",new[]{Saved(2)});
        Assert.AreEqual("Requer 2 meses",r.Rows[0].Change.Text);Assert.IsNull(r.Rows[0].Change.Amount);
        Assert.AreEqual(20000,r.Opening);Assert.AreEqual(32140,r.Closing);
    }
    [Test] public void ZeroCustomersHaveNoInventedWeightedTicket()
    {
        var m=Saved(1);m.customers=0;var r=QuarterlyResultPresentation.Build("quarter",new[]{m});
        Assert.AreEqual("Sem clientes",r.Rows[18].Aggregate.Text);Assert.IsNull(r.Rows[18].Aggregate.Amount);
    }
    [Test] public void EmptyHistoryIsEmpty()
    { var r=QuarterlyResultPresentation.Build("quarter",Array.Empty<RoundResultEntity>());Assert.AreEqual(0,r.Count);Assert.IsEmpty(r.Rows); }
    [Test] public void ForeignCampaignDuplicateAndOldCycleAreRejected()
    {
        var other=Saved(1);other.sessionId="other";
        foreach(var rows in new[]{new[]{other},new[]{Saved(1),Saved(1)},new[]{Saved(4)}})
            Assert.Throws<InvalidOperationException>(()=>QuarterlyResultPresentation.Build("quarter",rows));
    }
    [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)] [TestCase(float.NegativeInfinity)]
    public void CorruptStoredDataHasNoZeroFallback(float value)
    { var m=Saved(1);m.netResult=value;Assert.Throws<InvalidOperationException>(()=>QuarterlyResultPresentation.Build("quarter",new[]{m})); }
    [TestCase(-500)] [TestCase(0)] [TestCase(500)]
    public void SignedEventsAndNegativeOrZeroBalancesArePreserved(float amount)
    {
        var m=Saved(1);m.eventCashImpact=amount;m.openingCash=amount;m.closingCash=amount;m.netResult=amount;
        var r=QuarterlyResultPresentation.Build("quarter",new[]{m});Assert.AreEqual(amount,r.Rows[8].Aggregate.Amount);
        Assert.AreEqual(amount,r.Opening);Assert.AreEqual(amount,r.Closing);Assert.AreEqual(amount,r.Net);
    }
    [Test] public void ClassificationDoesNotMutateHistoricalStrings()
    { var m=Saved(1);m.alignmentClassification="HIGH";Assert.AreEqual("Alto",QuarterlyResultPresentation.Build("quarter",new[]{m}).Rows[20].Months[0].Text);Assert.AreEqual("HIGH",m.alignmentClassification); }
    [Test] public void ExistingPanelIdsRemainStable()
    { Assert.AreEqual(11,(int)PanelId.MonthlyResults);Assert.AreEqual(12,(int)PanelId.QuarterlyResults);Assert.AreEqual(1,(int)PanelId.Menu); }
    [Test] public void QuarterlyTotalsSumDisplayedCentsFromLargePersistedFloats()
    {
        var months=new[]{Saved(1),Saved(2),Saved(3)};
        foreach(var m in months)m.grossRevenue=m.netResult=158533.33f;
        var r=QuarterlyResultPresentation.Build("quarter",months);
        Assert.AreEqual(QuarterlyResultPresentation.Money(475599.99),r.Rows[0].Aggregate.Text.TrimStart('+'));
        Assert.That(r.Revenue,Is.EqualTo(475599.99).Within(.000001));Assert.That(r.Net,Is.EqualTo(475599.99).Within(.000001));
        Assert.AreEqual(158533.33f,months[0].grossRevenue);
    }
    [Test] public void QuarterlyCompositionAndHandAnchorsMatchApprovedContract()
    {
        var scene=UnityEngine.SceneManagement.SceneManager.GetSceneByPath("Assets/Scenes/GameScene.unity");
        bool opened=!scene.isLoaded;
        if(opened)scene=UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity",UnityEditor.SceneManagement.OpenSceneMode.Additive);
        try
        {
            var view=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<QuarterlyResultView>(true)).Single();
            var art=(RectTransform)view.transform.Find("QuarterlyIllustration");Assert.AreEqual(new Vector2(1312,-100),art.anchoredPosition);Assert.AreEqual(new Vector2(560,420),art.sizeDelta);Assert.AreEqual(Vector3.one*(584f/560f),art.localScale);Assert.NotNull(art.GetComponent<UnityEngine.UI.RectMask2D>());
            var spec=Newtonsoft.Json.Linq.JObject.Parse(System.IO.File.ReadAllText("docs/art/layouts/dre-layout.json"))["quarterly"];int index=0;
            foreach(var layer in spec["layers"])
            {
                var image=art.Find((string)layer["asset_id"]).GetComponent<UnityEngine.UI.Image>();var r=image.rectTransform;float s=(float)layer["scale"];
                Assert.AreEqual(index++,r.GetSiblingIndex());Assert.IsTrue(image.preserveAspect);Assert.AreEqual(Color.white,image.color);Assert.IsFalse(image.raycastTarget);
                var size=new Vector2(image.sprite.rect.width*s,image.sprite.rect.height*s);Assert.AreEqual(size,r.sizeDelta);
                Assert.AreEqual(new Vector2((float)layer["position_px"][0]-size.x*.5f-1320,-((float)layer["position_px"][1]-size.y*.5f-120)),r.anchoredPosition);
            }
            foreach(var anchor in ((Newtonsoft.Json.Linq.JObject)spec["anchor_definitions"]).Properties())
            {
                var def=anchor.Value;var r=(RectTransform)art.Find((string)def["asset_id"]);var image=r.GetComponent<UnityEngine.UI.Image>();var native=def["native_px"];
                float x=1320+r.anchoredPosition.x+(float)native[0]*r.sizeDelta.x/image.sprite.rect.width;
                float y=120-r.anchoredPosition.y+(float)native[1]*r.sizeDelta.y/image.sprite.rect.height;
                Assert.That(x,Is.EqualTo((float)spec["anchors"][anchor.Name][0]).Within(.001));Assert.That(y,Is.EqualTo((float)spec["anchors"][anchor.Name][1]).Within(.001));
            }
            Assert.AreEqual(25,view.Rows.Count);Assert.AreEqual(3,view.Bars.Count);Assert.NotNull(view.TableScroll.viewport.GetComponent<UnityEngine.UI.Mask>());
        }
        finally { if(opened)UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene,true); }
    }
}
