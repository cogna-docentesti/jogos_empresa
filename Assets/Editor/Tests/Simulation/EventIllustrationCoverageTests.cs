using Game.Adapter.In.UI;
using Game.EditorTools;
using NUnit.Framework;
using UnityEngine;

public sealed class EventIllustrationCoverageTests
{
    [Test]
    public void EveryConfiguredAndLegacyEventHasAnImportedIllustration()
    {
        Assert.IsEmpty(EventArtCoverageMigration.Sync());
    }

    [TestCase("staff_absent")]
    [TestCase("staff_resigned")]
    [TestCase("kitchen_equipment_failure")]
    [TestCase("equipment_still_down")]
    [TestCase("cash_shortfall")]
    [TestCase("quality_complaint")]
    public void NewEngineEventsResolveArtworkWithoutReserializingTheScene(string id)
    {
        EventArtCoverageMigration.Sync();
        var root = new GameObject("EventArtCoverageTest");
        try { Assert.IsNotNull(root.AddComponent<EventScreenView>().Illustration(id), id); }
        finally { Object.DestroyImmediate(root); }
    }
}
