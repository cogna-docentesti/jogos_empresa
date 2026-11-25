// Assets/Scripts/Game/Validation/MotorMonthQA.cs
// Passos comuns das validações em Play (UI-12, UI-19 e, por elas, FONT-10) para o mês do motor novo:
// hub > Planejar mês > Confirmar e iniciar > mês rodando com popups > resultado gravado uma única vez.
// Substitui o fluxo antigo (MonthlySettlementController, Panel_Event, MonthlyEventPlanEntity, RoundService).
// Fica no runtime (como o Font10CampaignValidation) para servir também à build de QA do FONT-10.
#if DEVELOPMENT_BUILD || UNITY_EDITOR || FONT10_QA_PLAYER
using System;
using System.Linq;
using System.Reflection;
using Game.Adapter.In.Controllers;
using UnityEngine;
using UnityEngine.UI;

public static class MotorMonthQA
{
    private static T F<T>(object o, string n)
        => (T)o.GetType().GetField(n, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(o);

    public static SystemicRoundFlow Flow
        => UnityEngine.Object.FindFirstObjectByType<SystemicRoundFlow>(FindObjectsInactive.Include)
           ?? throw new InvalidOperationException("GameScene sem SimulationFlow (rode Game > Simulação > Montar painéis na GameScene)");
    public static MonthSimulationController Controller => F<MonthSimulationController>(Flow, "monthController");
    public static ManagementPoliciesView Planning => F<ManagementPoliciesView>(Flow, "planningView");
    public static GameObject MonthPanel => F<GameObject>(Flow, "monthPanel");
    public static EventDecisionPopupView Popup => F<EventDecisionPopupView>(Controller, "popupView");
    public static Button PlanningConfirm => F<Button>(Planning, "confirmButton");
    public static MonthSimulationView MonthView => F<MonthSimulationView>(Controller, "monthView");
    public static Button AbandonButton => MonthPanel.transform.Find("AbandonButton").GetComponent<Button>();
    public static Button AbandonConfirm => F<Button>(MonthView, "abandonConfirmButton");
    public static Button AbandonCancel => F<Button>(MonthView, "abandonCancelButton");
    public static float Speed => F<float>(Controller, "_speed");

    public static bool PopupOpen
    {
        get { var root = F<GameObject>(Popup, "root"); return root != null && root.activeInHierarchy; }
    }

    public static string PopupTitle => PopupOpen ? F<TMPro.TextMeshProUGUI>(Popup, "titleText").text : null;

    public static Button FirstReaction
        => F<Button[]>(Popup, "reactionButtons").FirstOrDefault(b => b != null && b.gameObject.activeInHierarchy && b.interactable);

    private static GameState State => GameManager.Instance.StateMachine.CurrentState;

    /// Tutoriais da configuração (banco, loja e equipe) dão a partida como vistos, e o kit básico como comprado,
    /// para a campanha de QA atravessar a configuração sem os cartões do tutorial na frente dos botões.
    /// Os tutoriais têm os próprios testes (EquipmentTutorialTests, InitialTutorialTests).
    /// withMinimumTeam: a equipe mínima do restaurante também já vem contratada (para suítes que só confirmam a tela).
    public static void SkipSetupTutorials(bool withMinimumTeam = false)
    {
        var s = GameSessionState.Current;
        if (s == null) throw new InvalidOperationException("Sem sessão para marcar os tutoriais");
        PlayerPrefs.SetInt("loan_tutorial_completed:" + s.sessionId, 1);
        PlayerPrefs.Save();
        s.equipmentTutorialStep = Game.Adapter.In.UI.EquipmentTutorial.CompletedStep;
        s.equipmentTutorialVersion = Game.Adapter.In.UI.EquipmentTutorial.Version;
        s.teamTutorialStep = 3;
        var owned = EquipmentSelectionHelper.FromJson(s.equipmentJson) ?? new EquipmentSelectionData();
        var basic = Resources.LoadAll<EquipmentData>("Equipments").Where(e => e.category == EquipmentCategory.BASIC).Select(e => e.id);
        EquipmentSelectionHelper.AddUniqueIds(owned, basic.ToArray());
        s.equipmentJson = EquipmentSelectionHelper.ToJson(owned);
        if (withMinimumTeam)
        {
            var restaurant = Resources.LoadAll<RestaurantData>("Restaurants").FirstOrDefault(r => r.type == s.restaurantType);
            var team = TeamSelectionHelper.FromJson(s.teamJson) ?? new TeamSelectionData();
            if (restaurant != null)
                foreach (var req in restaurant.requiredRoles) TeamSelectionHelper.SetMemberQuantity(team, req.roleId, req.quantity);
            s.teamJson = TeamSelectionHelper.ToJson(team);
        }
        if (!GameSessionState.TrySave(out string error)) throw new InvalidOperationException("Tutoriais: " + error);
        foreach (var t in UnityEngine.Object.FindObjectsByType<Game.Adapter.In.UI.LoanTutorial>(FindObjectsInactive.Include, FindObjectsSortMode.None)) t.Hide();
        foreach (var t in UnityEngine.Object.FindObjectsByType<Game.Adapter.In.UI.EquipmentTutorial>(FindObjectsInactive.Include, FindObjectsSortMode.None)) t.Hide();
    }

    /// Corre o mês até o próximo popup ou até o fim (botão Pular).
    public static void AdvanceToPopupOrEnd()
    {
        if (State != GameState.Round_Sales) throw new InvalidOperationException("O mês não está rodando: " + State);
        if (!PopupOpen) Controller.SkipToEnd();
    }

    /// Roda o mês até o resumo escolhendo a primeira reação habilitada de cada popup. Devolve quantos popups houve.
    public static int RunToSummary(Action<Button> click, int guard = 300)
    {
        int popups = 0;
        while (State == GameState.Round_Sales && guard-- > 0)
        {
            if (PopupOpen)
            {
                // o popup seguinte pode abrir no mesmo quadro, com outro número de reações: refaz o layout antes do clique
                var root = F<GameObject>(Popup, "root");
                foreach (var group in root.GetComponentsInChildren<LayoutGroup>()) LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)group.transform);
                Canvas.ForceUpdateCanvases();
                click(FirstReaction ?? throw new InvalidOperationException("Popup sem reação habilitada: " + PopupTitle));
                popups++;
            }
            else Controller.SkipToEnd();
        }
        if (State != GameState.Round_Summary) throw new InvalidOperationException("O mês não chegou ao resumo: " + State);
        return popups;
    }
}
#endif
