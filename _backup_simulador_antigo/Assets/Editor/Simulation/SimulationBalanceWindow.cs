using System;
using System.IO;
using System.Linq;
using Game.Adapter.Out.Simulation;
using Game.Domain.Simulation;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Janela Game > Simulacao > Balanceamento. Roda o Monte Carlo (25 contextos x 5 estrategias)
/// com a configuracao atual e mostra o resumo: lucro medio, falencias, eventos por mes,
/// estrategia vencedora por contexto e avisos (estrategia dominante, eventos frequentes demais).
/// Tambem simula um mes isolado para conferir numeros.
/// </summary>
public sealed class SimulationBalanceWindow : EditorWindow
{
    private int _seeds = 10;
    private string _summary;
    private BalanceReport _report;
    private Vector2 _scroll;

    private int _restaurant;
    private int _location;
    private int _segment;
    private int _strategy;
    private int _seed = 1;
    private string _preview;

    private static readonly string[] Restaurants = { "PODRAO", "JAPONES", "FRANCES" };
    private static readonly string[] Locations = { "bank", "condominium", "marketing", "store", "university" };
    private static readonly string[] Segments = { "LOW", "MEDIUM", "HIGH" };

    [MenuItem("Game/Simulacao/Balanceamento", priority = 20)]
    public static void Open() => GetWindow<SimulationBalanceWindow>("Balanceamento");

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Monte Carlo do simulador", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Usa os assets e o SimulationConfig atuais. Nao grava nada no banco.", MessageType.Info);
        _seeds = EditorGUILayout.IntSlider("Sementes por celula", _seeds, 2, 60);
        if (GUILayout.Button("Rodar balanceamento")) RunBalance();
        using (new EditorGUI.DisabledScope(_report == null))
            if (GUILayout.Button("Exportar CSV")) ExportCsv();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Um mes isolado", EditorStyles.boldLabel);
        _restaurant = EditorGUILayout.Popup("Restaurante", _restaurant, Restaurants);
        _location = EditorGUILayout.Popup("Localizacao", _location, Locations);
        _segment = EditorGUILayout.Popup("Publico-alvo", _segment, Segments);
        var strategies = StrategyProfile.Archetypes();
        _strategy = EditorGUILayout.Popup("Estrategia", _strategy, strategies.Select(s => s.Name).ToArray());
        _seed = EditorGUILayout.IntField("Semente", _seed);
        if (GUILayout.Button("Simular mes 1")) Preview(strategies[_strategy]);

        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        if (!string.IsNullOrEmpty(_preview)) EditorGUILayout.TextArea(_preview, GUILayout.ExpandHeight(false));
        if (!string.IsNullOrEmpty(_summary)) EditorGUILayout.TextArea(_summary, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
    }

    private static SimulationCatalog LoadCatalog()
    {
        var provider = new UnitySimulationCatalogProvider();
        var catalog = provider.Load();
        var problems = catalog.Validate();
        if (problems.Count > 0) throw new InvalidOperationException(string.Join("\n", problems));
        return catalog;
    }

    private void RunBalance()
    {
        try
        {
            var catalog = LoadCatalog();
            _report = BalanceSimulator.Run(catalog, _seeds, null, message => EditorUtility.DisplayProgressBar("Balanceamento", message, 0.5f));
            _summary = _report.Summary();
        }
        catch (Exception exception)
        {
            _summary = "Erro: " + exception.Message;
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    private void ExportCsv()
    {
        string path = EditorUtility.SaveFilePanel("Exportar balanceamento", Application.dataPath, "balanceamento", "csv");
        if (!string.IsNullOrEmpty(path)) File.WriteAllText(path, _report.ToCsv());
    }

    private void Preview(StrategyProfile strategy)
    {
        try
        {
            var catalog = LoadCatalog();
            var inputs = BalanceSimulator.BuildInputs(catalog, Restaurants[_restaurant], Locations[_location], Segments[_segment], strategy, (ulong)Math.Max(1, _seed));
            var report = new MonthSimulation(catalog, inputs, strategy.Reactions).RunToEnd().Report;
            var i = report.income;
            _preview = "Receita bruta " + SimFormat.Money(i.grossRevenue) + " | CMV " + SimFormat.Money(i.cmv) + " | Lucro liquido " + SimFormat.Money(i.netIncome)
                       + "\nClientes " + report.cashResult.customers + " | Ticket " + SimFormat.Money(report.indicators.averageTicket)
                       + " | NPS " + SimFormat.Number(report.indicators.npsAverage) + " | Pontuacao " + report.scorecard.total + " (" + report.scorecard.classification + ")"
                       + "\nEventos: " + string.Join(", ", report.events.Select(e => "d" + e.day + " " + e.eventId + (string.IsNullOrEmpty(e.reactionId) ? string.Empty : "=" + e.reactionId)))
                       + "\n" + string.Join("\n", report.feedback.Select(f => "- " + f.text));
        }
        catch (Exception exception)
        {
            _preview = "Erro: " + exception.Message;
        }
    }
}
