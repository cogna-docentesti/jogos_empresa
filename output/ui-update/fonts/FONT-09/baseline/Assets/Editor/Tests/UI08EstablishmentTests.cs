using System;
using System.Linq;
using System.Reflection;
using Game.Domain.Service;
using Game.Infrastructure.Session;
using NUnit.Framework;
using UnityEngine;

public sealed class UI08EstablishmentTests
{
    private string locationId,locationName;private LocationZone? zone;private RestaurantType? type,menuType;private Segment? segment;private MenuPricingData menu;
    [SetUp] public void SaveDraft(){locationId=PlayerSession.SelectedEstablishmentId;locationName=PlayerSession.SelectedEstablishmentName;zone=PlayerSession.SelectedZone;type=PlayerSession.SelectedRestaurantType;segment=PlayerSession.SelectedTargetSegment;menuType=PlayerSession.MenuRestaurantType;menu=PlayerSession.GetMenu();PlayerSession.ClearDecisions();}
    [TearDown] public void RestoreDraft(){PlayerSession.ClearDecisions();if(zone.HasValue)PlayerSession.SaveLocation(locationId,locationName,zone.Value);if(type.HasValue && segment.HasValue)PlayerSession.SaveRestaurant(type.Value,segment.Value);if(menuType.HasValue)PlayerSession.SetMenu(menuType.Value,menu);}
    public static GameSessionEntity Fixture(RestaurantType type)
    {
        var r=Resources.LoadAll<RestaurantData>("Restaurants").Single(r=>r.type==type);
        var pricing=new MenuPricingData();foreach(var p in r.products)pricing.items.Add(new MenuPricingItem{productId=p.id,selectedPrice=p.price});
        var team=new TeamSelectionData();foreach(var req in r.requiredRoles)TeamSelectionHelper.SetMemberQuantity(team,req.roleId,req.quantity);
        TeamSelectionHelper.SetMemberQuantity(team,"manager",1);
        return new GameSessionEntity {sessionId="ui08-isolated",userId="qa",professorId="qa",cityId="city_01",companyName="Restaurante da turma",status=GameSessionStatus.IN_PROGRESS,currentRound=1,restaurantType=type,locationZone=LocationZone.Financas,targetSegment=r.allowedSegments[0],currentCash=65000,reputationScore=50,menuPricingJson=MenuPricingHelper.ToJson(pricing),teamJson=TeamSelectionHelper.ToJson(team),equipmentJson=EquipmentSelectionHelper.ToJson(new EquipmentSelectionData()),creditLineId="sem_credito",coherenceRating="ADEQUATE"};
    }
    [Test] public void EmptyDraftNeverShowsSessionDefaultsOrInventedIndicators()
    {
        var s=EstablishmentSummaryService.BuildDraft();Assert.IsFalse(s.HasChoices);Assert.IsFalse(s.HasSession);Assert.IsFalse(s.HasMonthlyEstimate);Assert.IsFalse(s.HasScore);Assert.IsNull(s.Facade);Assert.IsTrue(s.Decisions.All(d=>!d.Ready));Assert.That(s.LocationLabel,Does.Contain("pendente"));
    }
    [TestCase(RestaurantType.PODRAO)][TestCase(RestaurantType.FRANCES)][TestCase(RestaurantType.JAPONES)]
    public void ConfirmedSummaryUsesTheExactCatalogAndCalculatorWithoutMutatingItsInput(RestaurantType type)
    {
        var session=Fixture(type);string before=Newtonsoft.Json.JsonConvert.SerializeObject(session);
        var s=EstablishmentSummaryService.Build(session);var expected=MonthlySimulationEngine.Calculate(MonthlySimulationCatalog.Load(session));
        Assert.IsTrue(s.HasSession && s.HasMonthlyEstimate && s.IsReady);Assert.AreEqual(expected.grossRevenue,s.EstimatedRevenue);Assert.AreEqual(expected.netResult,s.MonthlyResult);Assert.AreEqual(expected.alignmentScore,s.Score);Assert.AreEqual(80,s.ScoreMax);
        Assert.AreSame(Resources.LoadAll<RestaurantData>("Restaurants").Single(r=>r.type==type).facade,s.Facade);Assert.AreEqual(before,Newtonsoft.Json.JsonConvert.SerializeObject(session));
    }
    [Test] public void DraftChangesRestaurantLocationAndPricesWithoutBorrowingSessionMoney()
    {
        var session=Fixture(RestaurantType.JAPONES);PlayerSession.LoadDecisionsFrom(session);
        var s=EstablishmentSummaryService.BuildDraft();Assert.IsFalse(s.HasSession || s.HasMonthlyEstimate);Assert.IsTrue(s.Decisions.Take(3).All(d=>d.Ready));Assert.That(s.MenuDetails,Does.Contain("R$ 90,00"));
        PlayerSession.SaveLocation("university","Área educacional",LocationZone.Educacao);Assert.AreNotEqual(s.LocationLabel,EstablishmentSummaryService.BuildDraft().LocationLabel);
        PlayerSession.SaveRestaurant(RestaurantType.FRANCES,Segment.HIGH);var changed=EstablishmentSummaryService.BuildDraft();Assert.AreNotEqual(s.Facade,changed.Facade);Assert.IsFalse(changed.Decisions[2].Ready);Assert.That(changed.MenuDetails,Does.Contain("Preço pendente"));
    }
    [Test] public void RebuildReadsEachChoiceAgainAndLoansUseTheExistingFormula()
    {
        var session=Fixture(RestaurantType.PODRAO);var old=EstablishmentSummaryService.Build(session);
        session.locationZone=LocationZone.Educacao;Assert.AreNotEqual(old.LocationLabel,EstablishmentSummaryService.Build(session).LocationLabel);
        var menu=MenuPricingHelper.FromJson(session.menuPricingJson);menu.items[0].selectedPrice+=1;session.menuPricingJson=MenuPricingHelper.ToJson(menu);Assert.AreNotEqual(old.MenuDetails,EstablishmentSummaryService.Build(session).MenuDetails);
        var credit=Resources.LoadAll<CreditLineData>("CreditLines").First(c=>c.maxAmount>0);session.creditLineId=credit.id;session.loanBalance=credit.maxAmount;
        var loan=EstablishmentSummaryService.Build(session);Assert.That(loan.CreditLabel,Does.Contain(credit.displayName));Assert.AreEqual(LoanService.Installment(session.loanBalance,credit,1),loan.LoanInstallment,.01f);
        var equipment=new EquipmentSelectionData();EquipmentSelectionHelper.AddUniqueIds(equipment,new[]{Resources.LoadAll<EquipmentData>("Equipments").First().id});session.equipmentJson=EquipmentSelectionHelper.ToJson(equipment);Assert.AreNotEqual(old.EquipmentDetails,EstablishmentSummaryService.Build(session).EquipmentDetails);
        session.teamJson="{}";var empty=EstablishmentSummaryService.Build(session);Assert.IsFalse(empty.Decisions[5].Ready);Assert.IsEmpty(empty.Members);Assert.IsFalse(empty.IsReady);
    }
    [Test] public void MissingPricesArePendingAndNeverReplacedWithCatalogDefaults()
    {
        var session=Fixture(RestaurantType.PODRAO);session.menuPricingJson="{}";session.alignmentClassification="HIGH";session.alignmentScore=80;var s=EstablishmentSummaryService.Build(session);Assert.IsFalse(s.HasMonthlyEstimate || s.HasScore || s.Decisions[2].Ready);Assert.That(s.MenuDetails,Does.Contain("Preço pendente"));Assert.AreEqual("A definir",s.PriceLabel);
    }
    [Test] public void UnavailableAndCorruptDataRemainReadableAndDoNotCreateFakePeople()
    {
        var session=Fixture(RestaurantType.PODRAO);session.restaurantType=(RestaurantType)999;session.locationZone=(LocationZone)999;session.teamJson="broken";session.equipmentJson="broken";session.menuPricingJson="broken";var s=EstablishmentSummaryService.Build(session);Assert.IsFalse(s.HasMonthlyEstimate || s.IsReady);Assert.IsNull(s.Facade);Assert.IsEmpty(s.Members);
    }
    [Test] public void NoWaiterRoleOrEconomicExtensionWasIntroduced()
    {CollectionAssert.AreEquivalent(new[]{"attendant","grill_cook","head_chef","manager","sushi_chef"},Resources.LoadAll<RoleData>("Roles").Select(r=>r.id));}
    [Test] public void CurrencyKeepsRealCentsAndBrazilianSeparators()
    {Assert.AreEqual("R$ -1.234,50",EstablishmentSummaryService.Brl(-1234.5f));}
}
