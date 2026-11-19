using System.Linq;
using Game.Adapter.In.Controllers;
using NUnit.Framework;
using UnityEngine;
using Game.Adapter.In.UI;
using Game.Adapter.In.UI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;

public class InitialTutorialTests
{
    [TestCase(RestaurantType.PODRAO, 65000)]
    [TestCase(RestaurantType.JAPONES, 69000)]
    [TestCase(RestaurantType.FRANCES, 72000)]
    public void Capital_proprio_cobre_kit_e_equipe_minima_existe(RestaurantType type, int capital)
    {
        var basic = Resources.LoadAll<EquipmentData>("Equipments").Where(e => e.category == EquipmentCategory.BASIC).ToArray();
        Assert.IsNotEmpty(basic);
        Assert.LessOrEqual(basic.Sum(e => e.cost), capital);
        var restaurant = Resources.LoadAll<RestaurantData>("Restaurants").Single(r => r.type == type);
        var roles = Resources.LoadAll<RoleData>("Roles");
        var team = new TeamSelectionData();
        Assert.IsFalse(TeamScreenController.HasMinimum(restaurant, team, roles));
        foreach (var requirement in restaurant.requiredRoles)
            TeamSelectionHelper.AddMember(team, requirement.roleId, requirement.quantity);
        Assert.IsTrue(TeamScreenController.HasMinimum(restaurant, team, roles));
        TeamSelectionHelper.RemoveMember(team, restaurant.requiredRoles[0].roleId);
        Assert.IsFalse(TeamScreenController.HasMinimum(restaurant, team, roles));
    }

    [Test]
    public void Retomar_preparacao_na_etapa_pendente_sem_recomprar_kit()
    {
        var session = new GameSessionEntity { currentRound = 1, status = GameSessionStatus.IN_PROGRESS, restaurantType = RestaurantType.JAPONES };
        Assert.AreEqual(GameState.Initial_Capital, GameManager.ResumeOperationalSetup(session));
        session.creditLineId = "no_financing";
        Assert.AreEqual(GameState.Initial_Equipment, GameManager.ResumeOperationalSetup(session));
        var equipment = new EquipmentSelectionData { equipmentIds = Resources.LoadAll<EquipmentData>("Equipments")
            .Where(e => e.category == EquipmentCategory.BASIC).Select(e => e.id).ToList() };
        session.equipmentJson = EquipmentSelectionHelper.ToJson(equipment);
        Assert.AreEqual(GameState.Initial_Team, GameManager.ResumeOperationalSetup(session));
        var team = new TeamSelectionData();
        foreach (var role in Resources.LoadAll<RestaurantData>("Restaurants").Single(r => r.type == session.restaurantType).requiredRoles)
            TeamSelectionHelper.AddMember(team, role.roleId, role.quantity);
        session.teamJson = TeamSelectionHelper.ToJson(team);
        Assert.AreEqual(GameState.Management_Hub, GameManager.ResumeOperationalSetup(session));
    }

    [Test]
    public void Partida_em_andamento_nao_volta_ao_tutorial()
    {
        Assert.AreEqual(GameState.Management_Hub, GameManager.ResumeOperationalSetup(new GameSessionEntity
            { currentRound = 2, status = GameSessionStatus.IN_PROGRESS }));
        Assert.AreEqual(3, LoanService.ContractTerm(1));
        Assert.AreEqual(2, LoanService.ContractTerm(2));
        Assert.AreEqual(0, LoanService.ContractTerm(3));
    }
}
