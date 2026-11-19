using System.Linq;
using Game.Domain.Simulation;
using NUnit.Framework;

namespace Game.Tests.Simulation
{
    public class EventEngineTests
    {
        [Test]
        public void RefrigerationFailureRequiresRefrigerationEquipment()
        {
            var catalog = SimulationTestKit.Catalog(c =>
            {
                c.events.RemoveAll(e => e.scope != EventScope.EquipmentUnit);
                c.EquipmentCategory(EquipmentCategoryId.Refrigeration).baseFailuresPerMonth = 30d;
            });
            var without = SimulationTestKit.Inputs(catalog);
            without.EquipmentOwned.RemoveAll(id => catalog.Equipment[id].CategoryId == EquipmentCategoryId.Refrigeration);
            var report = SimulationTestKit.RunMonth(catalog, without).Report;
            Assert.IsFalse(report.events.Any(e => e.eventId == "freezer_breakdown"));

            var with = SimulationTestKit.Inputs(catalog);
            Assert.IsTrue(SimulationTestKit.RunMonth(catalog, with).Report.events.Any(e => e.eventId == "freezer_breakdown"));
        }

        [Test]
        public void ConditionalEventsOnlyOccurWhenConditionsHold()
        {
            var catalog = SimulationTestKit.Catalog(c =>
            {
                var loyalty = c.Event("loyalty_program_success");
                loyalty.baseRatePerMonth = 300d;
                loyalty.conditions.RemoveAll(x => x.indicator == Indicator.Satisfaction);
                c.events.RemoveAll(e => e.id != "loyalty_program_success");
            });
            var off = SimulationTestKit.Inputs(catalog);
            off.Policies["loyalty"] = "off";
            Assert.IsFalse(SimulationTestKit.RunMonth(catalog, off).Report.events.Any());
            var on = SimulationTestKit.Inputs(catalog);
            on.Policies["loyalty"] = "on";
            Assert.IsTrue(SimulationTestKit.RunMonth(catalog, on).Report.events.Any(e => e.eventId == "loyalty_program_success"));
        }

        [Test]
        public void TemporaryEffectsStopAfterConfiguredDuration()
        {
            var forced = SimulationTestKit.ForcedEvent("forced_drop", 5, null, new Modifier(Lever.DemandFactor, 0.5d));
            forced.baseEffect.durationDays = 3;
            var catalog = SimulationTestKit.QuietCatalog(c => c.events.Add(forced));
            var report = SimulationTestKit.RunMonth(catalog, SimulationTestKit.Inputs(catalog)).Report;
            Assert.AreEqual(1d, report.days[3].fEvents, 1e-9);
            Assert.AreEqual(0.5d, report.days[4].fEvents, 1e-9);
            Assert.AreEqual(0.5d, report.days[6].fEvents, 1e-9);
            Assert.AreEqual(1d, report.days[7].fEvents, 1e-9);
        }

        [Test]
        public void PersistentEffectsLastUntilEndConditionIncludingNextMonth()
        {
            var forced = SimulationTestKit.ForcedEvent("forced_competitor", 10, null, new Modifier(Lever.CompetitionDelta, 0.4d));
            forced.baseEffect.end = EffectEnd.GameEnd;
            forced.canRecur = false;
            var catalog = SimulationTestKit.QuietCatalog(c => c.events.Add(forced));
            var campaign = CampaignRunner.Run(catalog, SimulationTestKit.Inputs(catalog), new DefaultReactionPolicy());
            var month1 = campaign.Months[0];
            Assert.Greater(month1.days[8].fCompetition, month1.days[10].fCompetition);
            Assert.AreEqual(month1.days[10].fCompetition, campaign.Months[1].days[15].fCompetition, 1e-6);
            Assert.AreEqual(1, campaign.Months.Sum(m => m.events.Count(e => e.eventId == "forced_competitor")));
        }

        [Test]
        public void UnitRepairedEffectEndsWhenEquipmentIsFixed()
        {
            var catalog = SimulationTestKit.Catalog(c =>
            {
                c.events.RemoveAll(e => e.scope != EventScope.EquipmentUnit || e.id == MonthSimulation.PreventiveSaveEventId);
                c.EquipmentCategory(EquipmentCategoryId.Refrigeration).baseFailuresPerMonth = 3000d;
                foreach (var cat in c.equipmentCategories.Where(x => x.id != EquipmentCategoryId.Refrigeration)) cat.baseFailuresPerMonth = 0d;
                c.reliability.brokenReminderDays = 5;
            });
            var policy = new FixedReactionPolicy("postpone_repair");
            var sim = new MonthSimulation(catalog, SimulationTestKit.Inputs(catalog), null);
            // Dia 1: as duas unidades de refrigeracao falham; adiar o conserto.
            sim.Advance();
            Assert.AreEqual(SimulationStatus.AwaitingDecision, sim.Status);
            while (sim.Status == SimulationStatus.AwaitingDecision) sim.Resolve(sim.Pending.OccurrenceId, "postpone_repair");
            sim.Advance();
            Assert.Greater(sim.CurrentIndicators[Indicator.EquipmentBroken(EquipmentCategoryId.Refrigeration)], 0d);
            // Avanca ate o lembrete e conserta.
            int guard = 0;
            while (sim.Status != SimulationStatus.Finished && guard++ < 200)
            {
                sim.Advance();
                while (sim.Status == SimulationStatus.AwaitingDecision)
                {
                    var pending = sim.Pending;
                    var repair = pending.Options.FirstOrDefault(o => o.ReactionId == "repair_now" && o.Enabled);
                    sim.Resolve(pending.OccurrenceId, (repair ?? pending.Options.First(o => o.Enabled)).ReactionId);
                }
            }
            var outcome = sim.Close();
            Assert.IsFalse(outcome.NextState.effects.Any(e => e.end == EffectEnd.UnitRepaired && outcome.NextState.units.Any(u => u.unitKey == e.unitKey && !u.broken)));
            Assert.IsTrue(outcome.Report.events.Any(e => e.eventId == MonthSimulation.ReminderEventId));
        }

        [Test]
        public void MutuallyExclusiveEventsDoNotOccurTogether()
        {
            var a = SimulationTestKit.ForcedEvent("spike_a", 3, "DEMAND_SPIKE", new Modifier(Lever.DemandFactor, 1.2d));
            var b = SimulationTestKit.ForcedEvent("spike_b", 3, "DEMAND_SPIKE", new Modifier(Lever.DemandFactor, 1.3d));
            a.kind = EventKind.Probabilistic; a.baseRatePerMonth = 1e6d; a.impact = EventImpact.Positive;
            b.kind = EventKind.Probabilistic; b.baseRatePerMonth = 1e6d; b.impact = EventImpact.Positive;
            var catalog = SimulationTestKit.QuietCatalog(c => { c.events.Add(a); c.events.Add(b); });
            var report = SimulationTestKit.RunMonth(catalog, SimulationTestKit.Inputs(catalog)).Report;
            Assert.AreEqual(1, report.events.Count(e => e.day == 3));
        }

        [Test]
        public void CooldownIsRespected()
        {
            var catalog = SimulationTestKit.QuietCatalog(c =>
            {
                var rain = DefaultSimulationConfig.Create().Event("heavy_rain");
                rain.baseRatePerMonth = 1e6d;
                rain.cooldownDays = 7;
                c.events.Add(rain);
                c.eventEngine.openingGraceDays = 0;
            });
            var report = SimulationTestKit.RunMonth(catalog, SimulationTestKit.Inputs(catalog)).Report;
            var days = report.events.Where(e => e.eventId == "heavy_rain").Select(e => e.day).ToList();
            Assert.Greater(days.Count, 1);
            for (int i = 1; i < days.Count; i++) Assert.GreaterOrEqual(days[i] - days[i - 1], 7);
        }

        [Test]
        public void MercyDampingReducesButNeverEliminatesNegativeRisk()
        {
            var p = new EventEngineParameters();
            var calm = new SimulationState();
            var stormy = new SimulationState();
            for (int d = 1; d <= 8; d++) stormy.recentNegatives.Add(new RecentEventState { absDay = d, severity = 2d });
            double calmFactor = EventEngine.MercyFactor(calm, 9, p);
            double stormyFactor = EventEngine.MercyFactor(stormy, 9, p);
            Assert.AreEqual(1d, calmFactor, 1e-9);
            Assert.Less(stormyFactor, calmFactor);
            Assert.GreaterOrEqual(stormyFactor, p.mercyMinimumFactor);
            Assert.Greater(stormyFactor, 0d);
        }

        [Test]
        public void RelatedEventsDoNotChargeTwice()
        {
            var catalog = SimulationTestKit.Catalog(c =>
            {
                c.events.RemoveAll(e => e.scope != EventScope.EquipmentUnit || e.id == MonthSimulation.PreventiveSaveEventId || e.id == MonthSimulation.ReminderEventId);
                c.EquipmentCategory(EquipmentCategoryId.Refrigeration).baseFailuresPerMonth = 3000d;
                foreach (var cat in c.equipmentCategories.Where(x => x.id != EquipmentCategoryId.Refrigeration)) cat.baseFailuresPerMonth = 0d;
            });
            var inputs = SimulationTestKit.Inputs(catalog);
            int refrigerationUnits = inputs.EquipmentOwned.Count(id => catalog.Equipment[id].CategoryId == EquipmentCategoryId.Refrigeration);
            var report = SimulationTestKit.RunMonth(catalog, inputs, new FixedReactionPolicy("postpone_repair")).Report;
            // Unidade parada nao falha de novo: no maximo uma falha por unidade enquanto o conserto e adiado.
            Assert.AreEqual(refrigerationUnits, report.events.Count(e => e.eventId == "freezer_breakdown"));
            // O caixa dos eventos e exatamente a soma das reacoes registradas.
            Assert.AreEqual(report.events.Sum(e => e.cashImpact), report.cashResult.eventCashImpact, 0.05d);
        }

        [Test]
        public void UnaffordableReactionsAreDisabled()
        {
            var catalog = SimulationTestKit.Catalog(c =>
            {
                c.finance.overdraftLimit = 0d;
                c.events.RemoveAll(e => e.scope != EventScope.EquipmentUnit || e.id == MonthSimulation.PreventiveSaveEventId);
                c.EquipmentCategory(EquipmentCategoryId.Refrigeration).baseFailuresPerMonth = 3000d;
            });
            var inputs = SimulationTestKit.Inputs(catalog);
            inputs.OpeningCash = 100d;
            var sim = new MonthSimulation(catalog, inputs, null);
            sim.Advance();
            Assert.AreEqual(SimulationStatus.AwaitingDecision, sim.Status);
            var options = sim.Pending.Options;
            Assert.IsFalse(options.First(o => o.ReactionId == "replace_premium").Enabled);
            Assert.IsTrue(options.First(o => o.ReactionId == "postpone_repair").Enabled);
            Assert.Throws<System.InvalidOperationException>(() => sim.Resolve(sim.Pending.OccurrenceId, "replace_premium"));
        }

        [Test]
        public void SupplierAndMaintenanceChangeInspectionRisk()
        {
            var config = SimulationTestKit.Catalog().Config;
            var inspection = config.Event("health_inspection");
            double Probability(params (string category, string option)[] policies)
            {
                var levers = new LeverSet();
                foreach (var (category, option) in policies)
                    levers.ApplyAll(config.Policy(category).Option(option).modifiers, category);
                var ctx = new EventContext { Config = config, Indicators = new IndicatorSet(), State = new SimulationState(), Levers = levers, Month = 1, Day = 10, AbsDay = 10 };
                return EventEngine.Evaluate(inspection, ctx).Probability;
            }
            double reference = Probability(("supplier", "standard"), ("maintenance", "corrective"));
            Assert.Greater(Probability(("supplier", "economic"), ("maintenance", "corrective")), reference);
            Assert.Less(Probability(("supplier", "premium"), ("maintenance", "intensive")), reference);
        }

        [Test]
        public void EveryHazardLeverPointsToAnExistingEvent()
        {
            // Uma chave ".hr" que nenhum evento le nao muda nada no jogo (era o caso de "sanitary.hr").
            var config = SimulationTestKit.Catalog().Config;
            var engineKeys = new[] { Lever.EquipmentHazard, Lever.UnitHazard };
            var modifiers = config.policies.SelectMany(p => p.options).SelectMany(o => o.modifiers)
                .Concat(config.events.SelectMany(e => e.reactions.SelectMany(r => r.effect.modifiers).Concat(e.baseEffect.modifiers)));
            foreach (var key in modifiers.Select(m => m.key).Where(k => k.EndsWith(".hr")).Distinct())
            {
                if (engineKeys.Contains(key)) continue;
                Assert.IsTrue(key.StartsWith(Lever.EventHazardPrefix), "Chave de risco sem evento: " + key);
                string eventId = key.Substring(Lever.EventHazardPrefix.Length, key.Length - Lever.EventHazardPrefix.Length - Lever.EventHazardSuffix.Length);
                Assert.IsNotNull(config.Event(eventId), "Chave de risco aponta para evento inexistente: " + key);
            }
        }
    }
}
