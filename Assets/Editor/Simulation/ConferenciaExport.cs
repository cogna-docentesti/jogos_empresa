// Assets/Editor/Simulation/ConferenciaExport.cs
// Menu Game > Simulação > Exportar conferência (CSV para a planilha).
//
// Lê <projeto>/Conferencia/cenarios.json (decisões D1 a D6 e políticas de cada cenário; se não existir,
// cria um com 4 cenários de exemplo), roda cada cenário pelo serviço com o catálogo REAL do jogo
// (SimulationConfig + ScriptableObjects), passa cada mês pela Conferencia e grava
// <projeto>/Conferencia/C<n>_<nome>.csv no formato da aba "C<n> Dados" da planilha Conferencia_Motor.xlsx.
// O CSV usa ponto e vírgula e vírgula decimal (Excel em português abre direto).
//
// Layout de cada mês (bloco de 190 linhas; o mês m começa na linha 1 + 190*(m-1)):
//   linha +0          MÊS | m
//   linhas +1..+95    parâmetros (A nome, B valor, C explicação)   | decisões (E, F) | equipamentos (H..N) | equipe (P..R)
//   linha +100        cabeçalho da tabela de dias; linhas +101..+130 dias 1 a 30
//   linhas +135..     números do fechamento calculados pelo motor (A nome, B valor)
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Game.Adapter.Out.Simulation;
using Game.Domain.Simulation;
using UnityEditor;
using UnityEngine;

public static class ConferenciaExport
{
    public const int BlockHeight = 210, DayHeaderOffset = 100, MonthValuesOffset = 135;
    private static readonly CultureInfo PtBr = new CultureInfo("pt-BR");

    // ------------------------------------------------------------------ cenários (cenarios.json)
    [Serializable] public class PratoPreco { public string prato; public double preco; }
    [Serializable] public class Cargo { public string cargo; public int quantidade; }
    [Serializable]
    public class Cenario
    {
        public string nome;
        public int semente = 4242;
        public string regiao;                 // id do LocationData: bank, store, marketing, university, condominium
        public string restaurante;            // PODRAO, JAPONES, FRANCES
        public string publico;                // LOW, MEDIUM, HIGH
        public List<PratoPreco> precos = new List<PratoPreco>();   // prato sem preço usa o de referência
        public List<Cargo> equipe = new List<Cargo>();             // ids de Resources/Roles
        public List<string> equipamentos = new List<string>();     // ids de Resources/Equipments
        public string linhaDeCredito;         // id de Resources/CreditLines ou vazio
        public double emprestimo;
        public int meses = 3;
        public MonthPolicies politicas = new MonthPolicies();
    }
    [Serializable] public class ArquivoDeCenarios { public List<Cenario> cenarios = new List<Cenario>(); }

    public static string Folder => Path.Combine(Directory.GetParent(UnityEngine.Application.dataPath).FullName, "Conferencia");

    [MenuItem("Game/Simulação/Exportar conferência (CSV para a planilha)")]
    public static void ExportFromMenu()
    {
        var config = AssetDatabase.LoadAssetAtPath<SimulationConfigAsset>(SimulationConfigBuilder.AssetPath);
        if (config == null) { Debug.LogError("[Conferência] Crie o SimulationConfig antes (Game > Simulação > Criar ou atualizar SimulationConfig)."); return; }
        var provider = new UnitySimulationCatalogProvider(config);
        var catalog = provider.Build();
        var summary = ExportAll(catalog, provider.RoleTypeById, Folder);
        Debug.Log("[Conferência] " + summary + " Pasta: " + Folder);
        EditorUtility.RevealInFinder(Folder);
    }

    /// Roda todos os cenários de <pasta>/cenarios.json e grava um CSV por cenário. Devolve um resumo.
    public static string ExportAll(SimulationCatalog catalog, IReadOnlyDictionary<string, RoleType> roleIds, string folder)
    {
        Directory.CreateDirectory(folder);
        string jsonPath = Path.Combine(folder, "cenarios.json");
        if (!File.Exists(jsonPath)) File.WriteAllText(jsonPath, JsonUtility.ToJson(DefaultScenarios(), true), new UTF8Encoding(false));
        var file = JsonUtility.FromJson<ArquivoDeCenarios>(File.ReadAllText(jsonPath));

        var lines = new List<string>();
        for (int i = 0; i < file.cenarios.Count; i++)
        {
            var c = file.cenarios[i];
            var setup = ToSetup(catalog, c, roleIds, i + 1);
            var (errors, _) = SetupRules.Validate(catalog, setup);
            if (errors.Count > 0) { lines.Add($"C{i + 1} {c.nome}: decisões inválidas ({string.Join(" ", errors)})"); continue; }
            var runs = ConferenciaRunner.Play(catalog, setup, c.politicas ?? new MonthPolicies(), Math.Max(1, Math.Min(3, c.meses)));
            string name = $"C{i + 1}_{Slug(c.nome)}.csv";
            File.WriteAllText(Path.Combine(folder, name), Csv(catalog, c, setup, runs), new UTF8Encoding(true));
            int checks = runs.Sum(r => r.Checks.Count), bad = runs.Sum(r => r.Checks.Count(x => !x.Ok));
            lines.Add($"C{i + 1} {c.nome}: {runs.Count} meses, {checks} conferências, {bad} divergências → {name}");
        }
        return string.Join(" | ", lines);
    }

    public static ArquivoDeCenarios DefaultScenarios()
    {
        Cenario C(string nome, int seed, string region, string rest, string seg, string[] team, string[] extraEquipment, string credit, double loan, MonthPolicies p)
        {
            var c = new Cenario { nome = nome, semente = seed, regiao = region, restaurante = rest, publico = seg, linhaDeCredito = credit, emprestimo = loan, politicas = p };
            foreach (var g in team.GroupBy(x => x)) c.equipe.Add(new Cargo { cargo = g.Key, quantidade = g.Count() });
            c.equipamentos.AddRange(new[] { "bancada_inox_preparo", "conjunto_panelas_industriais", "fogao_industrial_6_bocas", "freezer_horizontal",
                                            "geladeira_comercial_dupla", "mobiliario_basico", "pia_industrial_inox", "sistema_pdv_computador" });
            c.equipamentos.AddRange(extraEquipment);
            return c;
        }
        var file = new ArquivoDeCenarios();
        file.cenarios.Add(C("Lanches na Educacional", 4242, "university", "PODRAO", "LOW", new[] { "attendant", "attendant", "grill_cook" },
            new[] { "chapa_profissional_podrao", "fritadeira_eletrica_dupla_podrao" }, "microcredito_empresarial", 50000, new MonthPolicies()));
        file.cenarios.Add(C("Japones na Comercial economico", 99, "store", "JAPONES", "MEDIUM", new[] { "attendant", "attendant", "sushi_chef" },
            new[] { "balcao_refrigerado_sushi", "facas_especiais_japones", "panela_eletrica_arroz" }, "", 0,
            new MonthPolicies { Marketing = "low", Supplier = "economy", Stock = "low", Pay = "below", Maintenance = "intensive", Training = "basic" }));
        file.cenarios.Add(C("Frances na Financeira premium", 777, "bank", "FRANCES", "HIGH", new[] { "attendant", "attendant", "head_chef", "manager" },
            new[] { "adega_climatizada", "forno_combinado_inteligente", "mobiliario_luxo" }, "credito_empresarial_padrao", 80000,
            new MonthPolicies { Marketing = "high", Loyalty = "program", Supplier = "premium", SupplierPayment = "installments", Maintenance = "preventive", Stock = "high", Pay = "above", Training = "advanced" }));
        var stress = C("Frances na Educacional caro", 31337, "university", "FRANCES", "HIGH", new[] { "attendant", "attendant", "head_chef" },
            new[] { "adega_climatizada", "forno_combinado_inteligente", "mobiliario_luxo" }, "credito_de_investimento", 20000,
            new MonthPolicies { Marketing = "high", Training = "advanced", Pay = "above" });
        stress.precos.Add(new PratoPreco { prato = "FRA_PRATO_EXECUTIVO", preco = 130 });
        stress.precos.Add(new PratoPreco { prato = "FRA_MENU_CLASSICO", preco = 180 });
        stress.precos.Add(new PratoPreco { prato = "FRA_MENU_PREMIUM", preco = 260 });
        file.cenarios.Add(stress);
        return file;
    }

    public static SessionSetup ToSetup(SimulationCatalog cat, Cenario c, IReadOnlyDictionary<string, RoleType> roleIds, int index)
    {
        var setup = new SessionSetup
        {
            SessionId = "conferencia-" + index, Seed = c.semente, RegionId = c.regiao,
            Restaurant = (RestaurantType)Enum.Parse(typeof(RestaurantType), c.restaurante),
            TargetSegment = (Segment)Enum.Parse(typeof(Segment), c.publico)
        };
        foreach (var p in cat.Restaurants[setup.Restaurant].Products)
        {
            var chosen = c.precos?.FirstOrDefault(x => x.prato == p.Id);
            setup.Menu.Add(new MenuPrice(p.Id, chosen != null ? chosen.preco : p.ReferencePrice));
        }
        foreach (var m in c.equipe ?? new List<Cargo>())
            if (roleIds.TryGetValue(m.cargo, out var role)) for (int i = 0; i < m.quantidade; i++) setup.Team.Add(role);
        setup.EquipmentIds.AddRange((c.equipamentos ?? new List<string>()).Where(cat.Equipment.ContainsKey));
        if (!string.IsNullOrEmpty(c.linhaDeCredito) && cat.CreditLines.ContainsKey(c.linhaDeCredito))
        {
            setup.CreditLineId = c.linhaDeCredito;
            setup.LoanAmount = c.emprestimo;
        }
        return setup;
    }

    // ------------------------------------------------------------------ CSV no layout da aba "Dados"
    public static readonly string[] DayColumns =
    {
        // entradas registradas pelo motor (DayTrace)
        "dia", "moral", "experiencia", "tem_gerente", "treinamento_prod", "atendentes_presentes", "especialistas_presentes",
        "bonus_cozinha", "bonus_salao", "disp_atendimento", "disp_cozinha", "disp_salao", "fator_cap_atendimento", "fator_cap_cozinha",
        "pressao_concorrencia", "fator_eventos", "ruido", "alcance_marketing", "ganho_max_marketing", "lembranca_marca",
        "forca_fidelizacao", "qualidade_politicas", "fator_custo_insumos", "parcelado", "dias_cobertura", "fator_entrega",
        "fator_erros_treinamento", "evento_negativo_aberto", "bonus_premium_gerente", "refrigeracao_parada",
        "caixa_antes_operacao", "perda_falha_equipamento",
        // resultados do motor
        "m_reputacao_antes", "m_satisfacao_antes", "m_memoria_demanda", "m_capacidade_recente", "m_estoque_antes", "m_qualidade_equip",
        "m_qualidade", "m_encaixe", "m_ticket", "m_f_concorrencia", "m_f_reputacao", "m_f_fidelizacao", "m_f_marketing", "m_f_calendario",
        "m_demanda", "m_produtividade", "m_cap_atendimento", "m_cap_cozinha", "m_cap_salao", "m_custo_unit_estoque", "m_compra",
        "m_estoque_entrou", "m_atendidos", "m_perdidos_capacidade", "m_perdidos_estoque", "m_receita", "m_cmv_vendas", "m_perda_validade",
        "m_estoque_fim", "m_ocupacao", "m_fator_tempo", "m_taxa_erros", "m_nps", "m_reputacao_fim", "m_satisfacao_fim",
        "m_caixa_antes_juros", "m_juros_cheque", "m_caixa_fim"
    };

    public static string Csv(SimulationCatalog cat, Cenario c, SessionSetup setup, List<ConferenciaRunner.MonthRun> runs)
    {
        var grid = new SortedDictionary<int, SortedDictionary<int, string>>();
        void Put(int row, int col, object value)
        {
            if (!grid.TryGetValue(row, out var line)) grid[row] = line = new SortedDictionary<int, string>();
            line[col] = Format(value);
        }

        var k = cat.Constants;
        var region = cat.Regions[setup.RegionId];
        var rest = cat.Restaurants[setup.Restaurant];
        foreach (var run in runs)
        {
            int r0 = 1 + BlockHeight * (run.Month - 1);
            var sim = run.Sim;
            var start = run.Start;
            var options = setup == null ? null : sim.Policies.All().Select(p => cat.PolicyOption(p.policy, p.option)).ToList();
            Put(r0, 1, "MÊS"); Put(r0, 2, run.Month); Put(r0, 4, "Cenário"); Put(r0, 5, c.nome);

            // parâmetros: A nome, B valor, C explicação (a ordem não pode mudar: a planilha aponta para as linhas)
            var p = new List<(string key, object value, string what)>
            {
                ("demanda_base", region.BaseDemandPerDay, "pedidos/dia da região (LocationData)"),
                ("capacidade_fisica", region.PhysicalCapacityPerDay, "salão da região"),
                ("aluguel", region.Rent, "R$/mês"),
                ("fator_preco_regiao", region.PriceFactor, "desloca o gasto de referência"),
                ("indice_regional", region.RegionalIndex, "calendário"),
                ("perfil_seg", region.WeekdayProfile[0], "dia da semana 1 (dia 1 do mês = segunda)"),
                ("perfil_ter", region.WeekdayProfile[1], ""), ("perfil_qua", region.WeekdayProfile[2], ""), ("perfil_qui", region.WeekdayProfile[3], ""),
                ("perfil_sex", region.WeekdayProfile[4], ""), ("perfil_sab", region.WeekdayProfile[5], ""), ("perfil_dom", region.WeekdayProfile[6], ""),
                ("sazonalidade_mes", k.MonthSeasonality[(run.Month - 1) % k.MonthSeasonality.Length], ""),
            };
            foreach (var seg in new[] { Segment.LOW, Segment.MEDIUM, Segment.HIGH })
            {
                var sp = cat.Segments[seg];
                string n = seg == Segment.LOW ? "C" : seg == Segment.MEDIUM ? "B" : "A";
                p.Add(($"T_{n}", sp.ReferenceSpend, $"gasto de referência da classe {n}"));
                p.Add(($"sens_{n}", sp.Sensitivity, "sensibilidade a preço"));
                p.Add(($"cidade_{n}", sp.CityShare, "participação na cidade"));
                p.Add(($"mix_{n}", region.Mix.TryGetValue(seg, out var mix) ? mix : 0, "participação na região"));
                p.Add(($"atrat_{n}", rest.Attractiveness[seg], "atratividade da categoria"));
                p.Add(($"ajuste_{n}", seg == setup.TargetSegment ? k.TargetSegmentBonus : k.OtherSegmentPenalty, "público-alvo +15%, outros -5%"));
            }
            for (int i = 0; i < 3; i++)
            {
                var prod = rest.Products[i];
                p.Add(($"preco_{i + 1}", setup.PriceOf(prod.Id), prod.Name + " (D3)"));
                p.Add(($"preco_ref_{i + 1}", prod.ReferencePrice, "preço de referência"));
                p.Add(($"custo_{i + 1}", prod.UnitCost, "custo unitário (ficha técnica)"));
            }
            p.AddRange(new (string, object, string)[]
            {
                ("cap_atendente", rest.AttendantCapacityPerDay, "pedidos/dia por atendente"),
                ("cap_especialista", cat.Roles[rest.SpecialistRole].KitchenCapacityPerDay, "pedidos/dia por especialista"),
                ("perecibilidade", rest.PerishabilityPerDay, "perda por dia"),
                ("cmv_min", rest.CmvMin, ""), ("cmv_max", rest.CmvMax, ""),
                ("encaixe_referencia", k.FitReference, "divisor do encaixe (0,45)"),
                ("fluxo_cidade", k.CityReachFlow, "alcance do marketing traz a cidade (x2)"),
                ("imposto", k.TaxRate, "sobre a receita"), ("desconto_a_vista", k.CashDiscountRate, ""),
                ("encargos", k.PayrollCharges, "x salários"), ("juros_cheque_mes", k.OverdraftMonthlyRate, ""),
                ("dias_mes", k.DaysPerMonth, ""), ("memoria_satisfacao", k.SatisfactionMemory, ""), ("atraso_reputacao", k.ReputationLagPerDay, ""),
                ("gerente_produtividade", k.ManagerProductivity, ""),
                ("energia_fixa", k.EnergyBase, ""), ("energia_kwh", k.EnergyPerKwh, "R$/kWh"), ("energia_pedido", k.EnergyPerOrder, ""),
                ("agua_fixa", k.WaterBase, ""), ("agua_pedido", k.WaterPerOrder, ""), ("limpeza_fixa", k.CleaningBase, ""),
                ("limpeza_pedido", k.CleaningPerOrder, ""), ("taxas_adm", k.AdminFees, ""),
                ("reputacao_inicio", start.Reputation, "estado no dia 1"), ("satisfacao_inicio", start.Satisfaction, ""),
                ("memoria_demanda_inicio", start.RecentDemand, ""), ("capacidade_recente_inicio", start.RecentCapacity, ""),
                ("estoque_inicio", start.InventoryValue, ""), ("caixa_inicio", sim.CashAtStart, ""), ("capital", start.Capital, ""),
                ("lucros_acumulados_inicio", start.RetainedEarnings, ""), ("emprestimo_inicio", start.LoanBalance, ""),
                ("fornecedores_inicio", start.SupplierPayables, ""),
                ("marketing_fixo", options.Where(o => o.PolicyId == "marketing").Sum(o => o.FixedMonthlyCost), "política de marketing"),
                ("fidelizacao_fixo", options.Where(o => o.PolicyId == "loyalty").Sum(o => o.FixedMonthlyCost), ""),
                ("fidelizacao_por_pedido", options.Sum(o => o.CostPerOrder), ""),
                ("treinamento_fixo", options.Where(o => o.PolicyId == "training").Sum(o => o.FixedMonthlyCost), ""),
                ("manutencao_taxa", options.Sum(o => o.AssetValueRate), "x valor de compra dos equipamentos"),
                ("fator_folha", sim.Trace.PayrollFactor, "remuneração"),
                ("juros_emprestimo", sim.Trace.LoanInterest, "pago no dia 20 (taxa x saldo)"),
                ("taxa_emprestimo", sim.Trace.LoanRate, ""), ("saldo_antes_parcela", sim.Trace.LoanBalanceBeforePayment, ""),
                ("amortizacao", sim.Trace.LoanAmortization, "saldo / meses restantes"),
                ("parcelas_fornecedor_pagas", sim.Trace.SupplierInstallmentsPaid, "compras parceladas de meses anteriores"),
                ("reacoes_reparo", sim.Trace.Reactions.Where(x => x.line == DreLine.MaintenanceAndLosses).Sum(x => x.amount), "custos de reação"),
                ("reacoes_outras", sim.Trace.Reactions.Where(x => x.line == DreLine.OtherExpenses).Sum(x => x.amount), ""),
                ("reacoes_ganho", sim.Trace.Reactions.Where(x => x.line == DreLine.OtherIncome).Sum(x => x.amount), ""),
                ("reacoes_juros", sim.Trace.Reactions.Where(x => x.line == DreLine.Interest).Sum(x => x.amount), "tarifa de renegociação"),
                ("conta_atrasada", sim.Occurrences.Any(o => o.Definition.Id == EventIds.CashShortfall) ? 1 : 0, "1 = houve Caixa insuficiente"),
            });
            if (p.Count > 95) throw new InvalidOperationException("Parâmetros demais para o bloco.");
            for (int i = 0; i < p.Count; i++)
            {
                Put(r0 + 1 + i, 1, p[i].key); Put(r0 + 1 + i, 2, p[i].value); Put(r0 + 1 + i, 3, p[i].what);
            }

            // decisões (texto, para leitura)
            var decisions = new List<(string, string)>
            {
                ("Região (D1)", region.Name), ("Restaurante (D2)", rest.Name), ("Público-alvo (D2)", setup.TargetSegment.ToString()),
                ("Preços (D3)", string.Join(" / ", rest.Products.Select(x => $"{x.Name} {setup.PriceOf(x.Id):N2}"))),
                ("Crédito (D4)", setup.CreditLineId == null ? "sem empréstimo" : $"{setup.CreditLineId} R$ {setup.LoanAmount:N0}"),
                ("Equipamentos (D5)", setup.EquipmentIds.Count + " itens"),
                ("Equipe (D6)", string.Join(", ", setup.Team.GroupBy(x => x).Select(g => $"{g.Count()}x {g.Key}"))),
            };
            decisions.AddRange(sim.Policies.All().Select(x => ("Política " + x.policy, cat.PolicyOption(x.policy, x.option).Label)));
            decisions.Add(("Semente", setup.Seed.ToString()));
            decisions.Add(("Reações", "rodízio entre as reações habilitadas (dia + mês)"));
            for (int i = 0; i < decisions.Count; i++) { Put(r0 + 1 + i, 5, decisions[i].Item1); Put(r0 + 1 + i, 6, decisions[i].Item2); }

            // equipamentos no início do mês (H..N) e equipe no dia do salário (P..R)
            string[] eqHead = { "unidade", "preço de compra", "kWh/mês", "fator energia", "vida (meses)", "valor contábil no início", "qualidade do nível" };
            for (int j = 0; j < eqHead.Length; j++) Put(r0 + 1, 8 + j, eqHead[j]);
            for (int i = 0; i < start.Equipment.Count && i < 30; i++)
            {
                var u = start.Equipment[i];
                var lv = cat.Levels[u.Level];
                object[] v = { u.ProfileId, u.PurchasePrice, cat.Equipment[u.ProfileId].KwhPerMonth, lv.EnergyFactor, lv.LifeMonths, u.BookValue, lv.QualityDelta };
                for (int j = 0; j < v.Length; j++) Put(r0 + 2 + i, 8 + j, v[j]);
            }
            Put(r0 + 1, 16, "cargo"); Put(r0 + 1, 17, "pessoas no dia 27"); Put(r0 + 1, 18, "salário");
            int s = 0;
            foreach (var kv in sim.Trace.StaffOnSalaryDay.OrderBy(x => x.Key.ToString()))
            {
                Put(r0 + 2 + s, 16, kv.Key.ToString()); Put(r0 + 2 + s, 17, kv.Value); Put(r0 + 2 + s, 18, cat.Roles[kv.Key].Salary);
                s++;
            }

            // tabela de dias
            int hr = r0 + DayHeaderOffset;
            for (int j = 0; j < DayColumns.Length; j++) Put(hr, 1 + j, DayColumns[j]);
            foreach (var d in sim.DayResults)
            {
                var t = d.Trace;
                object[] v =
                {
                    d.Day, t.Morale, t.Experience, B(t.HasManager), t.TrainingAdd, t.AttendantsPresent, t.SpecialistsPresent,
                    t.KitchenBonus, t.DiningBonus, t.ServiceAvailability, t.KitchenAvailability, t.DiningAvailability, t.ServiceCapFactor, t.KitchenCapFactor,
                    t.CompetitionPressure, d.Demand.Events, d.Demand.Noise, t.MarketingReach, t.MarketingMaxGain, t.BrandRecall,
                    t.LoyaltyStrength, t.QualityLever, t.SupplierCostFactor, B(t.Installments), t.CoverDays, t.DeliveryFactor,
                    t.ErrorsFactor, B(t.UnresolvedNegative), B(t.PremiumBonus), B(t.RefrigerationDown),
                    t.CashBeforeOperations, t.FailureStockLoss,
                    t.ReputationBefore, t.SatisfactionBefore, t.RecentDemandBefore, t.RecentCapacityBefore, t.InventoryBefore, t.EquipmentQualityDelta,
                    d.Quality, d.Demand.Fit, d.AvgTicket, d.Demand.Competition, d.Demand.Reputation, d.Demand.Loyalty, d.Demand.Marketing, d.Demand.Calendar,
                    d.Demand.Potential, t.Productivity, d.Capacity.Service, d.Capacity.Kitchen, d.Capacity.Dining, t.StockUnitCost, d.Purchases,
                    t.InventoryAdded, d.Serve.Served, d.Serve.LostCapacity, d.Serve.LostStock, d.Revenue, t.CmvSold, d.Spoilage,
                    d.InventoryEnd, d.Occupancy, d.ServiceTimeFactor, d.ErrorRate, d.Nps.Total, d.Reputation, d.Satisfaction,
                    t.CashBeforeOverdraftInterest, t.OverdraftInterest, d.CashEnd
                };
                if (v.Length != DayColumns.Length) throw new InvalidOperationException("Colunas do dia fora do cabeçalho.");
                for (int j = 0; j < v.Length; j++) Put(hr + d.Day, 1 + j, v[j]);
            }

            // números do fechamento calculados pelo motor
            var rep = run.Report;
            var values = new List<(string, object)>();
            values.AddRange(rep.Dre.Select(x => ("dre_" + x.Name, (object)x.Value)));
            values.Add(("lucro_liquido", rep.NetIncome));
            values.AddRange(rep.CashFlow.Select(x => ("caixa_" + x.Name, (object)x.Value)));
            values.Add(("resultado_caixa", rep.CashResult));
            values.Add(("caixa_fim_mes", sim.State.Cash));
            values.AddRange(rep.Balance.Select(x => ("balanco_" + x.Name, (object)x.Value)));
            values.Add(("diferenca_balanco", rep.BalanceDifference));
            values.Add(("pedidos", rep.Orders)); values.Add(("perdidos_fracao", rep.LostRatio)); values.Add(("nps_medio", rep.NpsAverage));
            values.Add(("cmv_receita", rep.CmvRatio)); values.Add(("ticket_medio", rep.AvgTicket)); values.Add(("liquidez", rep.CurrentLiquidity));
            values.Add(("pontuacao", rep.Score)); values.Add(("classificacao", rep.Classification));
            // tela de resultado do mês do layout novo (RoundResultEntity no modelo de caixa, gravado em float)
            var screen = RoundResultMapper.From(rep, sim.State.Cash, run.Sim.Setup.SessionId, null, 0);
            values.Add(("tela_receita", (double)screen.grossRevenue)); values.Add(("tela_insumos", (double)screen.supplyCost));
            values.Add(("tela_aluguel", (double)screen.rent)); values.Add(("tela_folha", (double)screen.salaries));
            values.Add(("tela_despesas_operacionais", (double)screen.utilities)); values.Add(("tela_13", (double)screen.thirteenthSalary));
            values.Add(("tela_parcela", (double)screen.loanPayment)); values.Add(("tela_principal", (double)screen.loanPrincipalPayment));
            values.Add(("tela_saidas", (double)screen.TotalCosts)); values.Add(("tela_eventos", (double)screen.eventCashImpact));
            values.Add(("tela_resultado", (double)screen.netResult)); values.Add(("tela_caixa_inicio", (double)screen.openingCash));
            values.Add(("tela_caixa_fim", (double)screen.closingCash));
            values.Add(("divergencias_no_unity", run.Checks.Count(x => !x.Ok)));
            if (MonthValuesOffset + values.Count > BlockHeight) throw new InvalidOperationException("Valores do mês não cabem no bloco.");
            for (int i = 0; i < values.Count; i++) { Put(r0 + MonthValuesOffset + i, 1, values[i].Item1); Put(r0 + MonthValuesOffset + i, 2, values[i].Item2); }
        }

        var sb = new StringBuilder();
        int lastRow = grid.Keys.Max();
        for (int row = 1; row <= lastRow; row++)
        {
            if (grid.TryGetValue(row, out var line))
            {
                int lastCol = line.Keys.Max();
                for (int col = 1; col <= lastCol; col++)
                {
                    if (col > 1) sb.Append(';');
                    if (line.TryGetValue(col, out var cell)) sb.Append(cell);
                }
            }
            sb.Append("\r\n");
        }
        return sb.ToString();
    }

    private static int B(bool b) => b ? 1 : 0;

    private static string Format(object value)
    {
        switch (value)
        {
            case null: return "";
            case double d: return double.IsNaN(d) || double.IsInfinity(d) ? "" : d.ToString("G17", PtBr);
            case float f: return ((double)f).ToString("G17", PtBr);
            case int i: return i.ToString(PtBr);
            default:
                string s = value.ToString();
                return s.Contains(";") || s.Contains("\"") ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
        }
    }

    private static string Slug(string name)
        => new string((name ?? "cenario").Select(ch => char.IsLetterOrDigit(ch) ? ch : '_').ToArray());
}
