using System;
using UnityEngine;

public class UIStateListener : MonoBehaviour
{
    public static UIStateListener Instance { get; private set; }
    public GameObject CurrentPanel { get; private set; }
    private GameStateMachine subscribedMachine;
    private void Awake() { Instance = this; }

    // These states are synchronous handoffs, never user-facing screens.
    public static bool IsTechnicalState(GameState state) => state == GameState.Bootstrap
        || state == GameState.MainMenu || state == GameState.Config_Review
        || state == GameState.Round_Start || state == GameState.Round_Sales || state == GameState.Round_Costs;

    /// <summary>The sole writer of screen visibility. Navigation and report consultations delegate here.</summary>
    public static void PresentScreen(GameObject target)
    {
        if (Instance == null) { if (target != null) target.SetActive(true); return; }
        Instance.Present(target);
    }

    public static void DismissScreen(GameObject target)
    {
        if (Instance != null && Instance.CurrentPanel == target) Instance.Present(null);
        else if (target != null) target.SetActive(false);
    }

    private void Present(GameObject target)
    {
        foreach (var panel in AllPanels())
            if (panel != null && panel != target && panel.activeSelf) panel.SetActive(false);
        var registry = GetComponentInChildren<Game.Adapter.In.UI.Navigation.PanelRegistry>(true)
            ?? FindFirstObjectByType<Game.Adapter.In.UI.Navigation.PanelRegistry>();
        if (registry != null)
            foreach (var binding in registry.Bindings)
                if (binding.root != null && binding.root != target && binding.root.activeSelf) binding.root.SetActive(false);
        CurrentPanel = target;
        if (target != null && !target.activeSelf) target.SetActive(true);
    }

    private GameObject[] AllPanels() => new[] { identificationPanel, configLocationPanel, configRestaurantPanel,
        configTargetSegmentPanel, initialEquipmentPanel, initialTeamPanel, initialCapitalPanel, managementHubPanel,
        roundStartPanel, roundSalesPanel, roundCostsPanel, roundEventPanel, roundSummaryPanel, finalReportPanel, bankruptcyPanel };

    [Header("Configuração inicial")]
    [SerializeField] private GameObject identificationPanel;
    [SerializeField] private GameObject configLocationPanel;
    [SerializeField] private GameObject configRestaurantPanel;
    [SerializeField] private GameObject configTargetSegmentPanel;

    [Header("Configuração operacional inicial")]
    [SerializeField] private GameObject initialEquipmentPanel;
    [SerializeField] private GameObject initialTeamPanel;
    [SerializeField] private GameObject initialCapitalPanel;

    [Header("Hub")]
    [SerializeField] private GameObject managementHubPanel;

    [Header("Rodada")]
    [SerializeField] private GameObject roundStartPanel;
    [SerializeField] private GameObject roundSalesPanel;
    [SerializeField] private GameObject roundCostsPanel;
    [SerializeField] private GameObject roundEventPanel;
    [SerializeField] private GameObject roundSummaryPanel;

    [Header("Fim")]
    [SerializeField] private GameObject finalReportPanel;
    [SerializeField] private GameObject bankruptcyPanel;

    private void Start()
    {
        if (GameManager.Instance == null || GameManager.Instance.StateMachine == null)
        {
            Debug.LogError("GameManager ou StateMachine não encontrados.");
            return;
        }

        subscribedMachine = GameManager.Instance.StateMachine;
        subscribedMachine.OnStateChanged += HandleStateChanged;

        HandleStateChanged(GameManager.Instance.StateMachine.CurrentState);
    }

    private void OnDestroy()
    {
        if (subscribedMachine != null) subscribedMachine.OnStateChanged -= HandleStateChanged;
        if (Instance == this) Instance = null;
    }

    private void HandleStateChanged(GameState state)
    {
        Game.Adapter.In.UI.SharedModalFocus.CloseForNavigation();
        if (IsTechnicalState(state) && !GameSessionState.HasIncompatibleSave) return;
        if (state != GameState.Management_Hub)
            Game.Adapter.In.UI.Navigation.MenuNavigator.Instance?.CloseAll();
        Present(null);
        var views = FindObjectsByType<Game.Adapter.In.UI.EstablishmentSummaryView>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        if (GameSessionState.HasIncompatibleSave)
        {
            Game.Adapter.In.UI.Navigation.MenuNavigator.Instance?.CloseAll();
            if (views.Length > 0)
            {
                var view = views[0];
                var controller = view.GetComponent<Game.Adapter.In.Controllers.EstablishmentSummaryController>();
                if (controller != null)
                    view.ShowIncompatibleSave(controller.ResetGame);
            }
            return;
        }

        foreach (var view in views)
            view.HideIncompatibleSave();

        switch (state)
        {
            case GameState.Config_Identification:
                Show(identificationPanel);
                break;

            case GameState.Config_Location:
                Show(configLocationPanel);
                break;

            case GameState.Config_Restaurant:
                Show(configRestaurantPanel);
                break;

            case GameState.Config_TargetSegment:
                Show(configTargetSegmentPanel);
                break;


            case GameState.Initial_Equipment:
                Show(initialEquipmentPanel);
                break;

            case GameState.Initial_Team:
                Show(initialTeamPanel);
                break;

            case GameState.Initial_Capital:
                Show(initialCapitalPanel);
                break;

            case GameState.Management_Hub:
                if(Game.Adapter.In.UI.Navigation.MenuNavigator.Instance!=null)
                    Game.Adapter.In.UI.Navigation.MenuNavigator.Instance.OpenRoot();
                else Show(managementHubPanel);
                break;

            case GameState.Round_Event:
                Show(roundEventPanel);
                break;

            case GameState.Round_Summary:
                Show(roundSummaryPanel);
                break;

            case GameState.FinalReport:
                Show(finalReportPanel);
                break;

            case GameState.GameOver_Bankruptcy:
                Show(bankruptcyPanel);
                break;
        }
    }

    private void Show(GameObject panel)
    {
        if (panel == null) Debug.LogError("[UIStateListener] Missing required panel for " + subscribedMachine?.CurrentState);
        PresentScreen(panel);
    }
}
