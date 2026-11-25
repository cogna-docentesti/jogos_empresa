// Assets/Editor/Tests/Simulation/EventRecordTests.cs
// Duas ocorrências do mesmo evento no mesmo dia (duas unidades paradas) guardam cada uma a sua reação.
// Antes, a reação da primeira ia para o registro da segunda e a primeira ficava sem reação, o que fazia a
// conferência da partida jogada (Game > Simulação > Conferir partida jogada) reproduzir o mês errado.
using System.Linq;
using Game.Domain.Simulation;
using NUnit.Framework;

public class EventRecordTests
{
    [Test]
    public void DuasUnidadesParadasNoMesmoDia_CadaRegistroGuardaASuaReacao()
    {
        var cat = SampleCatalog.Build();
        var setup = MotorTestKit.Lanches();
        var start = InitialStateFactory.Create(cat, setup);
        var first = start.Equipment[0];
        var second = start.Equipment[1];
        foreach (var u in new[] { first, second }) { u.IsBroken = true; u.BrokenSinceAbsDay = 0; }   // lembrete no dia 10

        var sim = new MonthSimulation(cat, setup, start, new MonthPolicies());
        bool seen = false;
        while (!sim.IsFinished)
        {
            sim.AdvanceDay();
            while (sim.IsAwaitingDecision)
            {
                var occ = sim.PendingDecisions[0];
                if (occ.Definition.Id == EventIds.EquipmentStillDown && sim.Day == 10)
                {
                    seen = true;
                    sim.Choose(occ.OccurrenceId, occ.UnitId == first.UnitId ? "repair" : "postpone");
                }
                else MotorTestKit.PickFree(sim);
            }
            if (sim.Day == 10) break;
        }

        Assert.IsTrue(seen, "o cenário não gerou os lembretes do dia 10");
        var day10 = sim.State.EventHistory.Where(e => e.EventId == EventIds.EquipmentStillDown && e.AbsDay == 10).ToList();
        Assert.AreEqual(2, day10.Count);
        CollectionAssert.AreEquivalent(new[] { "repair", "postpone" }, day10.Select(e => e.ReactionId).ToArray());
        Assert.Greater(day10.Single(e => e.ReactionId == "repair").Cost, 0, "o conserto tem custo gravado");
    }
}
