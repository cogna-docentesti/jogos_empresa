using System.Linq;
using System.Reflection;
using Game.Adapter.In.UI;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;

public sealed class UI11TeamTests
{
    private GameObject instance;
    private static T Field<T>(object o, string n) => (T)o.GetType().GetField(n, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(o);
    private TeamRoleCardView Card(string id)
    {
        instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/TeamRoleCard.prefab"));
        var card = instance.GetComponent<TeamRoleCardView>();
        typeof(TeamRoleCardView).GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(card, null);
        card.Setup(Resources.LoadAll<RoleData>("Roles").Single(r => r.id == id), AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/new/team/portraits/" + id + ".png"), null, 0);
        card.SetQuantity(0, true); return card;
    }
    [TearDown] public void Clean() { if (instance != null) Object.DestroyImmediate(instance); GameSessionState.Clear(); }
    [TestCase("attendant",2200,900)][TestCase("grill_cook",2500,750)][TestCase("head_chef",8000,450)][TestCase("manager",5000,0)][TestCase("sushi_chef",4500,600)]
    public void FiveRealRolesKeepSalaryCapacityAndWholePortrait(string id, int salary, int capacity)
    {
        var c = Card(id); Assert.AreEqual(salary, c.Role.salary); Assert.AreEqual(capacity, c.Role.maxClientsSupported);
        var face = Field<UnityEngine.UI.Image>(c,"portrait"); Assert.AreEqual(Color.white,face.color); Assert.IsTrue(face.preserveAspect);
        Assert.AreEqual(UnityEngine.UI.Image.Type.Simple,face.type); Assert.AreEqual(new Rect(0,0,512,512),face.sprite.rect);
        Assert.That(Field<TMP_Text>(c,"salaryText").text,Does.Contain(FinancialScreenView.Money(salary)));
        Assert.That(Field<TMP_Text>(c,"capacityText").text,Does.Contain(capacity.ToString("N0")));
    }
    [Test] public void ZeroCannotBeDecrementedAndAddRequestsOne()
    {
        var c=Card("attendant"); int quantity=-1; c.QuantityRequested+=(_,q)=>quantity=q;
        Field<UnityEngine.UI.Button>(c,"removeButton").onClick.Invoke(); Assert.AreEqual(-1,quantity);
        Field<UnityEngine.UI.Button>(c,"addButton").onClick.Invoke(); Assert.AreEqual(1,quantity);
        Assert.IsFalse(Field<UnityEngine.UI.Button>(c,"removeButton").interactable);
    }
    [Test] public void QuantitiesAboveOneRemainSupportedAndSelected()
    {
        var c=Card("attendant"); c.SetQuantity(3,true); int quantity=-1; c.QuantityRequested+=(_,q)=>quantity=q;
        Assert.IsTrue(c.GetComponent<SharedControlAppearance>().IsSelected);
        Field<UnityEngine.UI.Button>(c,"removeButton").onClick.Invoke(); Assert.AreEqual(2,quantity);
        Field<UnityEngine.UI.Button>(c,"addButton").onClick.Invoke(); Assert.AreEqual(4,quantity);
    }
    [Test] public void UnavailableControlsCannotChangeQuantity()
    {
        var c=Card("manager"); c.SetQuantity(2,false); int count=0; c.QuantityRequested+=(_,_)=>count++;
        Field<UnityEngine.UI.Button>(c,"removeButton").onClick.Invoke(); Field<UnityEngine.UI.Button>(c,"addButton").onClick.Invoke();
        Assert.AreEqual(0,count); Assert.AreEqual("2",Field<TMP_Text>(c,"quantityText").text);
    }
    [TestCase(RestaurantType.PODRAO,"grill_cook",6900,2550)][TestCase(RestaurantType.JAPONES,"sushi_chef",8900,2400)][TestCase(RestaurantType.FRANCES,"head_chef",12400,2250)]
    public void CompleteTeamUsesRestaurantRequirementsAndExistingAlignment(RestaurantType type,string specialist,int salary,int capacity)
    {
        var restaurant=Resources.LoadAll<RestaurantData>("Restaurants").Single(r=>r.type==type); var roles=Resources.LoadAll<RoleData>("Roles").ToList();
        var team=new TeamSelectionData(); TeamSelectionHelper.SetMemberQuantity(team,"attendant",2); TeamSelectionHelper.SetMemberQuantity(team,specialist,1);
        Assert.AreEqual(salary,team.members.Sum(m=>roles.Single(r=>r.id==m.roleId).salary*m.quantity));
        Assert.AreEqual(capacity,team.members.Sum(m=>roles.Single(r=>r.id==m.roleId).maxClientsSupported*m.quantity));
        Assert.AreEqual(20,AlignmentEngine.Calculate(type,Segment.LOW,LocationZone.Comercio,restaurant,new MenuPricingData(),team,roles).restaurantTeamScore);
        TeamSelectionHelper.RemoveMember(team,"attendant");
        Assert.AreEqual(5,AlignmentEngine.Calculate(type,Segment.LOW,LocationZone.Comercio,restaurant,new MenuPricingData(),team,roles).restaurantTeamScore);
        Assert.AreEqual(5,AlignmentEngine.Calculate(type,Segment.LOW,LocationZone.Comercio,restaurant,new MenuPricingData(),new TeamSelectionData(),roles).restaurantTeamScore);
    }
    [Test] public void NoWaiterRoleIsCreatedAndJsonRoundTripKeepsQuantities()
    {
        var roles=Resources.LoadAll<RoleData>("Roles"); Assert.AreEqual(5,roles.Length); Assert.IsFalse(roles.Any(r=>r.id=="waiter"));
        var team=new TeamSelectionData(); TeamSelectionHelper.SetMemberQuantity(team,"attendant",4); TeamSelectionHelper.SetMemberQuantity(team,"manager",1);
        team=TeamSelectionHelper.FromJson(TeamSelectionHelper.ToJson(team)); Assert.AreEqual(4,TeamSelectionHelper.GetMemberQuantity(team,"attendant"));
        TeamSelectionHelper.SetMemberQuantity(team,"manager",0); Assert.AreEqual(1,team.members.Count);
    }
    [Test] public void AlignmentSaveCannotCreateFirstInsertBeforeD3()
    { GameSessionState.Set(new GameSessionEntity{status=GameSessionStatus.IN_PROGRESS}); Assert.IsFalse(new GameSessionService("u","p").SaveTeamAlignment(Resources.LoadAll<RoleData>("Roles").ToList())); Assert.IsFalse(GameSessionState.IsPersisted); }
    [Test] public void LongNamesExpandWithoutReducingTypeSize()
    {
        var c=Card("attendant"); var text=Field<TMP_Text>(c,"nameText"); text.text=string.Join(" ",Enumerable.Repeat("Função com descrição muito extensa",4));
        typeof(TeamRoleCardView).GetMethod("LateUpdate",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(c,null); text.ForceMeshUpdate();
        Assert.Greater(c.RequiredHeight,368); Assert.IsFalse(text.isTextOverflowing); Assert.IsFalse(text.enableAutoSizing);
    }
}
