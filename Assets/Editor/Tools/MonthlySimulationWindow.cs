using System;
using Game.Adapter.In.Controllers;
using UnityEditor;
using UnityEngine;

/// <summary>Preview, settle and inspect the real active session during Play Mode.</summary>
public sealed class MonthlySimulationWindow : EditorWindow
{
    private RoundResultEntity _preview;
    private string _error;
    private Vector2 _scroll;

    [MenuItem("Game/Monthly Simulation")]
    public static void Open() => GetWindow<MonthlySimulationWindow>("Monthly Simulation");

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Monthly Simulation", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Preview is read-only. Settle Current Month saves the active player's month and advances the quarter.", MessageType.Info);
        if (!Application.isPlaying || !GameSessionState.HasSession)
        {
            EditorGUILayout.LabelField("Enter Play Mode and finish the initial decisions first.");
            return;
        }
        var session = GameSessionState.Current;
        EditorGUILayout.LabelField("Session", session.sessionId);
        EditorGUILayout.LabelField("Month / Status", session.currentRound + " / " + session.status);
        using (new EditorGUI.DisabledScope(!GameSessionState.HasActiveSession || !GameSessionState.IsPersisted))
        {
            if (GUILayout.Button("Preview Current Month")) Run(() =>
            {
                var db = DatabaseInitializer.DatabaseService?.Connection;
                var history = new SessionEventHistoryRepository(db).GetBySessionAndRound(session.sessionId, session.currentRound);
                _preview = MonthlySimulationEngine.Calculate(MonthlySimulationCatalog.Load(session, history));
            });
            if (GUILayout.Button("Settle Current Month")) Run(() =>
            {
                _preview = MonthlySettlementController.SettleCurrentMonth();
                if (_preview == null) throw new InvalidOperationException("Settlement is unavailable. Open the management hub and check the Console.");
            });
        }
        if (!string.IsNullOrWhiteSpace(_error)) EditorGUILayout.HelpBox(_error, MessageType.Error);
        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        if (_preview != null) DrawResult("Preview / Last Settlement", _preview);
        var connection = DatabaseInitializer.DatabaseService?.Connection;
        if (connection != null)
            foreach (var result in new RoundResultRepository(connection).GetBySessionId(session.sessionId))
                DrawResult("Saved Month " + result.round, result);
        EditorGUILayout.EndScrollView();
    }

    private void Run(Action action)
    {
        _error = null;
        try { action(); }
        catch (Exception exception) { _error = exception.Message; }
    }

    private static void DrawResult(string title, RoundResultEntity result)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Demand / Capacity / Customers", $"{result.potentialDemand} / {result.serviceCapacity} / {result.customers}");
        EditorGUILayout.LabelField("Average Ticket", result.averageTicket.ToString("F2"));
        EditorGUILayout.LabelField("Revenue / Supplies", $"{result.grossRevenue:F2} / {result.supplyCost:F2}");
        EditorGUILayout.LabelField("Rent / Utilities / Payroll", $"{result.rent:F2} / {result.utilities:F2} / {result.Payroll:F2}");
        EditorGUILayout.LabelField("Loan Payment / Event Cash", $"{result.loanPayment:F2} / {result.eventCashImpact:F2}");
        EditorGUILayout.LabelField("Net Result", result.netResult.ToString("F2"));
        EditorGUILayout.LabelField("Opening / Closing Cash", $"{result.openingCash:F2} / {result.closingCash:F2}");
        EditorGUILayout.LabelField("Reputation", $"{result.reputationAtStart} -> {result.reputationAtEnd} ({result.reputationDelta:+0;-0;0})");
    }
}
