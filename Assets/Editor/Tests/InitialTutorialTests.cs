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
    [Test]
    public void UIs_salvas_na_cena_tem_referencias_e_nao_criam_objetos_ao_alternar_modo()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity", OpenSceneMode.Additive);
        try
        {
            var registry = Object.FindObjectsByType<PanelRegistry>(FindObjectsInactive.Include, FindObjectsSortMode.None).First(r => r.gameObject.scene == scene);
            var listener = Object.FindObjectsByType<UIStateListener>(FindObjectsInactive.Include, FindObjectsSortMode.None).First(r => r.gameObject.scene == scene);
            var serializedListener = new SerializedObject(listener);
            Assert.AreEqual(registry.Resolve(PanelId.Team), serializedListener.FindProperty("initialTeamPanel").objectReferenceValue);
            Assert.AreEqual(registry.Resolve(PanelId.EquipmentStore), serializedListener.FindProperty("initialEquipmentPanel").objectReferenceValue);
            Assert.AreEqual(registry.Resolve(PanelId.Financial), serializedListener.FindProperty("initialCapitalPanel").objectReferenceValue);
            foreach (var id in new[] { PanelId.Financial, PanelId.EquipmentStore, PanelId.Team })
            {
                var panel = registry.Resolve(id);
                var count = panel.GetComponentsInChildren<Transform>(true).Length;
                var tutorial = panel.GetComponentInChildren<InitialTutorialView>(true);
                Assert.IsNotNull(tutorial, "Tutorial salvo: " + id);
                Assert.IsNotNull(tutorial.transform.Find("Sheet/MenuIcon").GetComponent<UnityEngine.UI.Image>().sprite);
                InitialTutorialView.Configure(panel.transform, true);
                Assert.IsTrue(tutorial.gameObject.activeSelf);
                tutorial.Dismiss();
                Assert.IsFalse(tutorial.gameObject.activeSelf);
                InitialTutorialView.Configure(panel.transform, false);
                Assert.AreEqual(count, panel.GetComponentsInChildren<Transform>(true).Length);
            }
            var team = new SerializedObject(registry.Resolve(PanelId.Team).GetComponent<TeamScreenController>());
            Assert.AreEqual(Resources.LoadAll<RoleData>("Roles").Length, team.FindProperty("roleCards").arraySize);
            var store = new SerializedObject(registry.Resolve(PanelId.EquipmentStore).GetComponent<EquipmentScreenView>());
            Assert.GreaterOrEqual(store.FindProperty("preparedCards").arraySize, Resources.LoadAll<EquipmentData>("Equipments").Length);
            Assert.IsNotNull(store.FindProperty("cartModal").objectReferenceValue);
        }
        finally { EditorSceneManager.CloseScene(scene, true); }
    }

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
