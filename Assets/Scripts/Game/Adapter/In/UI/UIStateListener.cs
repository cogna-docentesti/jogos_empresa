using System;
using UnityEngine;

public class UIStateListener : MonoBehaviour
{
    [Header("Configuração inicial")]
    [SerializeField] private GameObject configLocationPanel;
    [SerializeField] private GameObject configRestaurantPanel;
    [SerializeField] private GameObject configTargetSegmentPanel;
    [SerializeField] private GameObject configReviewPanel;

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

        GameManager.Instance.StateMachine.OnStateChanged += HandleStateChanged;

        HandleStateChanged(GameManager.Instance.StateMachine.CurrentState);
    }

    private void OnDestroy()
    {
        if (GameManager.Instance == null || GameManager.Instance.StateMachine == null)
            return;

        GameManager.Instance.StateMachine.OnStateChanged -= HandleStateChanged;
    }

    private void HandleStateChanged(GameState state)
    {
        HideAllPanels();

        switch (state)
        {
            case GameState.Config_Location:
                Show(configLocationPanel);
                break;

            case GameState.Config_Restaurant:
                Show(configRestaurantPanel);
                break;

            case GameState.Config_TargetSegment:
                Show(configTargetSegmentPanel);
                break;

            case GameState.Config_Review:
                Show(configReviewPanel);
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
                Show(managementHubPanel);
                break;

            case GameState.Round_Start:
                Show(roundStartPanel);
                break;

            case GameState.Round_Sales:
                Show(roundSalesPanel);
                break;

            case GameState.Round_Costs:
                Show(roundCostsPanel);
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

    private void HideAllPanels()
    {
        SetActive(configLocationPanel, false);
        SetActive(configRestaurantPanel, false);
        SetActive(configTargetSegmentPanel, false);
        SetActive(configReviewPanel, false);

        SetActive(initialEquipmentPanel, false);
        SetActive(initialTeamPanel, false);
        SetActive(initialCapitalPanel, false);

        SetActive(managementHubPanel, false);

        SetActive(roundStartPanel, false);
        SetActive(roundSalesPanel, false);
        SetActive(roundCostsPanel, false);
        SetActive(roundEventPanel, false);
        SetActive(roundSummaryPanel, false);

        SetActive(finalReportPanel, false);
        SetActive(bankruptcyPanel, false);
    }

    private void Show(GameObject panel)
    {
        SetActive(panel, true);
    }

    private void SetActive(GameObject target, bool active)
    {
        if (target != null)
            target.SetActive(active);
    }
}
