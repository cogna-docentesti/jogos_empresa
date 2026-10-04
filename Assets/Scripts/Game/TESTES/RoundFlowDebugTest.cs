using System;
using System.Collections;
using System.Linq;
using UnityEngine;

public class RoundFlowDebugTest : MonoBehaviour
{
    private IEnumerator Start()
    {
        yield return null;
        RunQuarterTests();
    }

    [ContextMenu("Testar ciclo trimestral")]
    public void RunQuarterTests()
    {
        RunTests();
    }

    public static void RunTests()
    {
        if (GameManager.Instance == null || DatabaseInitializer.DatabaseService?.Connection == null)
            throw new InvalidOperationException("Os testes exigem GameManager e banco inicializados.");

        var db = DatabaseInitializer.DatabaseService.Connection;
        if (db.IsInTransaction)
            throw new InvalidOperationException("Execute os testes fora de uma transacao existente.");

        var previousSession = GameSessionState.Current;
        var previousIncompatibleSession = GameSessionState.IncompatibleSession;
        var previousEstablishmentId = Game.Infrastructure.Session.PlayerSession.SelectedEstablishmentId;
        var previousEstablishmentName = Game.Infrastructure.Session.PlayerSession.SelectedEstablishmentName;
        var sm = GameManager.Instance.StateMachine;
        var previousState = sm.CurrentState;
        var repository = new RoundResultRepository(db);
        var service = new GameSessionService("quarter_test_" + Guid.NewGuid(), "quarter_test_professor");
        int passed = 0;
        Action<bool, string> check = (condition, name) =>
        {
            if (!condition)
                throw new InvalidOperationException("[Trimestre] FALHOU: " + name);
            passed++;
            Debug.Log("[Trimestre] PASSOU: " + name);
        };

        db.BeginTransaction();
        try
        {
            GameSessionState.Clear();
            var session = NewSavedSession(service);
            check(session.currentRound == 1 && session.status == GameSessionStatus.IN_PROGRESS,
                "Nova sessao ativa comeca na rodada 1");

            for (int round = 1; round <= 3; round++)
            {
                sm.ForceState(GameState.Management_Hub);
                service.StartRound();
                check(sm.CurrentState == GameState.Round_Sales, "Inicio valido do mes " + round);
                var result = service.ProcessCurrentRound();
                check(result != null && result.round == round, "Resultado do mes " + round);
                check(result != null && result.thirteenthSalary == 0f, "13o salario zero no mes " + round);
                check(session.currentRound == (round < 3 ? round + 1 : 3), "Avanco/limite apos mes " + round);
                check(session.status == (round < 3 ? GameSessionStatus.IN_PROGRESS : GameSessionStatus.COMPLETED),
                    "Status apos mes " + round);
                check(sm.CurrentState == (round < 3 ? GameState.Management_Hub : GameState.FinalReport),
                    "Estado externo apos mes " + round);
            }

            var results = repository.GetBySessionId(session.sessionId);
            check(results.Count == 3 && results.Select(r => r.round).SequenceEqual(new[] { 1, 2, 3 }),
                "Exatamente tres resultados: 1, 2 e 3");
            check(results.All(r => r.thirteenthSalary == 0f), "13o salario persistido zero em todo trimestre");
            check(repository.GetBySessionAndRound(session.sessionId, 4) == null, "Nenhum resultado da rodada 4");

            Action<string> blocked = label =>
            {
                int originalRound = session.currentRound;
                var originalStatus = session.status;
                float originalCash = session.currentCash;
                long? originalCompletedAt = session.completedAt;
                int originalNegativeRounds = session.consecutiveNegativeRounds;
                int originalCount = repository.GetBySessionId(session.sessionId).Count;
                sm.ForceState(GameState.Management_Hub);
                service.StartRound();
                check(sm.CurrentState == GameState.Management_Hub, label + ": StartRound bloqueado");
                check(new RoundService().ProcessRound() == null, label + ": ProcessRound retorna null");
                sm.ForceState(GameState.Round_Sales);
                check(service.ProcessCurrentRound() == null && sm.CurrentState == GameState.Round_Event,
                    label + ": fluxo externo nao chega ao resumo");
                GameSessionState.AdvanceRound(false);
                check(session.currentRound == originalRound && session.status == originalStatus
                    && session.currentCash == originalCash && session.completedAt == originalCompletedAt
                    && session.consecutiveNegativeRounds == originalNegativeRounds,
                    label + ": rodada, status e dados financeiros preservados");
                check(repository.GetBySessionId(session.sessionId).Count == originalCount,
                    label + ": nenhum resultado adicional persistido");
            };

            blocked("Sessao COMPLETED");

            session.status = GameSessionStatus.IN_PROGRESS;
            session.completedAt = null;
            session.currentRound = 3;
            GameSessionState.AdvanceRound(false);
            check(session.currentRound == 3, "AdvanceRound ativo nao avanca de 3 para 4");

            foreach (int invalidRound in new[] { 0, 4 })
            {
                session.currentRound = invalidRound;
                blocked("Rodada invalida " + invalidRound);
            }
            check(repository.GetBySessionAndRound(session.sessionId, 4) == null, "Rodada 4 continua sem resultado");

            GameSessionState.Clear();
            session = NewSavedSession(service);
            // Os valores financeiros atuais produzem lucro fixo. Simula perdas
            // apenas no fixture e usa o mesmo encerramento chamado por ProcessRound.
            for (int month = 1; month <= 3; month++)
            {
                var loss = new RoundResultEntity
                {
                    roundResultId = Guid.NewGuid().ToString(),
                    sessionId = session.sessionId,
                    round = month,
                    openingCash = session.currentCash,
                    netResult = -100f,
                    closingCash = session.currentCash - 100f,
                    thirteenthSalary = 0f
                };
                repository.Insert(loss);
                GameSessionState.SetCash(loss.closingCash, false);
                GameSessionState.RegisterNegativeRound(true, false);
                RoundService.EvaluateSessionEnd();
                check(session.consecutiveNegativeRounds == month, "Prejuizos consecutivos no mes " + month);
                check(session.currentRound == (month < 3 ? month + 1 : 3), "Limite no trimestre com prejuizos: mes " + month);
                check(session.status == (month < 3 ? GameSessionStatus.IN_PROGRESS : GameSessionStatus.BANKRUPT),
                    "Precedencia de falencia no mes " + month);
            }
            check(session.currentRound == 3 && session.status == GameSessionStatus.BANKRUPT
                && session.completedAt.HasValue, "BANKRUPT prevalece sobre COMPLETED no mes 3");
            sm.ForceState(GameState.Round_Summary);
            typeof(GameSessionService).GetMethod("EvaluateRoundEnd", System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.NonPublic).Invoke(service,
                    new object[] { repository.GetLastRoundBySessionId(session.sessionId) });
            check(sm.CurrentState == GameState.GameOver_Bankruptcy, "Fluxo externo encaminha falencia ao GameOver");
            results = repository.GetBySessionId(session.sessionId);
            check(results.Count == 3 && results.Select(r => r.round).SequenceEqual(new[] { 1, 2, 3 }),
                "Trimestre falido tem somente resultados 1, 2 e 3");
            blocked("Sessao BANKRUPT");
            check(repository.GetBySessionAndRound(session.sessionId, 4) == null,
                "Nenhum resultado 4 depois da falencia");

            GameSessionState.Clear();
            sm.ForceState(GameState.Management_Hub);
            service.StartRound();
            check(sm.CurrentState == GameState.Management_Hub, "Sem sessao: inicio bloqueado");
            check(new RoundService().ProcessRound() == null, "Sem sessao: processamento bloqueado");
            check(service.ProcessCurrentRound() == null, "Sem sessao: fluxo externo retorna null");
            GameSessionState.AdvanceRound(false);
            check(GameSessionState.Current == null, "Sem sessao: AdvanceRound nao causa excecao");
            RunCreditTests(check);
            RunSaveCompatibilityTests(check);
            Debug.Log("[Trimestre] TODOS OS " + passed + " CHECKS PASSARAM.");
        }
        finally
        {
            db.Rollback();
            if (previousIncompatibleSession != null)
                GameSessionState.LoadActiveSession(previousIncompatibleSession.userId);
            else
                GameSessionState.Set(previousSession);
            Game.Infrastructure.Session.PlayerSession.SaveSelectedEstablishment(
                previousEstablishmentId, previousEstablishmentName);
            sm.ForceState(previousState);
        }
    }

    /// <summary>
    /// Sessao de teste no estado "depois do confirm da D3": tem cardapio e ja
    /// existe no banco. Desde a E-03 uma sessao nova nasce so em memoria e o
    /// Save() e ignorado ate o primeiro Persist(); estes testes verificam o que
    /// acontece com partidas ja gravadas, entao gravam explicitamente aqui.
    /// </summary>
    private static GameSessionEntity NewSavedSession(GameSessionService service)
    {
        var session = service.CreateNewSession();
        session.menuPricingJson = "{\"items\":[{\"productId\":\"TESTE\",\"selectedPrice\":10.0}]}";
        if (!GameSessionState.Persist())
            throw new InvalidOperationException("[Trimestre] Nao foi possivel gravar a sessao de teste.");
        return session;
    }

    private static void RunCreditTests(Action<bool, string> check)
    {
        var lines = Resources.LoadAll<CreditLineData>("CreditLines").Where(line => line.maxAmount > 0f).ToArray();
        check(lines.Length == 3, "Credito: tres linhas oficiais disponiveis");
        var expected = new[]
        {
            (id: "microcredito_empresarial", amount: 50000f, rate: 0.02f),
            (id: "credito_empresarial_padrao", amount: 120000f, rate: 0.03f),
            (id: "credito_de_investimento", amount: 250000f, rate: 0.04f)
        };
        var db = DatabaseInitializer.DatabaseService.Connection;
        var repository = new RoundResultRepository(db);
        foreach (var line in lines)
        {
            var official = expected.Single(item => item.id == line.id);
            check(line.maxAmount == official.amount && line.monthlyInterestRate == official.rate,
                "Credito: valor e juros preservados em " + line.id);
            foreach (int startMonth in new[] { 1, 2 })
            {
                GameSessionState.Clear();
                var service = new GameSessionService("credit_test_" + Guid.NewGuid(), "test_professor");
                var session = NewSavedSession(service);
                session.currentRound = startMonth;
                float originalCash = session.currentCash;
                check(LoanService.ContractTerm(startMonth) == 4 - startMonth,
                    line.id + ": prazo " + (4 - startMonth) + " no mes " + startMonth);
                check(LoanService.TryContract(line) && session.loanBalance == line.maxAmount
                    && session.currentCash == originalCash + line.maxAmount,
                    line.id + ": contratacao credita caixa e registra principal");
                check(!LoanService.TryContract(line) && session.currentCash == originalCash + line.maxAmount,
                    line.id + ": confirmacao repetida nao duplica emprestimo");
                for (int month = startMonth; month <= 3; month++)
                {
                    float openingBalance = session.loanBalance;
                    float principal = openingBalance / (4 - month);
                    float expectedPayment = principal + openingBalance * line.monthlyInterestRate;
                    check(Mathf.Abs(Game.Domain.Service.EstablishmentSummaryService.Build().LoanInstallment - expectedPayment) < 0.05f,
                        line.id + ": resumo usa parcela trimestral no mes " + month);
                    GameManager.Instance.StateMachine.ForceState(GameState.Management_Hub);
                    service.StartRound();
                    var result = service.ProcessCurrentRound();
                    check(result != null && Mathf.Abs(result.loanPayment - expectedPayment) < 0.05f,
                        line.id + ": parcela processada no mes " + month);
                    check(Mathf.Abs(session.loanBalance - (openingBalance - principal)) < 0.05f
                        && Mathf.Abs(new GameSessionRepository(db).GetById(session.sessionId).loanBalance - session.loanBalance) < 0.05f,
                        line.id + ": amortizacao persistida no mes " + month);
                }
                check(session.currentRound == 3 && session.loanBalance == 0f && !GameSessionState.HasActiveSession,
                    line.id + ": emprestimo quitado dentro do trimestre");
                check(repository.GetBySessionId(session.sessionId).Select(item => item.round)
                    .SequenceEqual(Enumerable.Range(startMonth, 4 - startMonth))
                    && repository.GetBySessionAndRound(session.sessionId, 4) == null
                    && LoanService.RemainingPaymentMonths(4) == 0
                    && LoanService.Installment(session.loanBalance, line, 4) == 0f,
                    line.id + ": nenhuma parcela ou resultado do mes 4");
            }
            GameSessionState.Clear();
            var lastMonthService = new GameSessionService("credit_block_test_" + Guid.NewGuid(), "test_professor");
            var lastMonthSession = NewSavedSession(lastMonthService);
            lastMonthSession.currentRound = 3;
            float lastMonthCash = lastMonthSession.currentCash;
            check(LoanService.ContractTerm(3) == 0 && !LoanService.TryContract(line)
                && lastMonthSession.loanBalance == 0f && lastMonthSession.currentCash == lastMonthCash
                && lastMonthSession.creditLineId == null,
                line.id + ": mes 3 bloqueia contratacao sem alterar caixa ou divida");
        }
#if UNITY_EDITOR
        if (!Application.isPlaying)
            RunCreditInterfaceTests(check, lines[0]);
#endif
    }

    private static void RunSaveCompatibilityTests(Action<bool, string> check)
    {
        var db = DatabaseInitializer.DatabaseService.Connection;
        var sessions = new GameSessionRepository(db);
        var rounds = new RoundResultRepository(db);
        var history = new SessionEventHistoryRepository(db);
        foreach (var sample in new[]
        {
            new[] { 4, 0, 0, 1 }, new[] { 12, 0, 0, 1 },
            new[] { 3, 3, 0, 0 }, new[] { 2, 4, 0, 1 },
            new[] { 2, 0, 4, 1 }, new[] { 1, 0, 0, 0 }
        })
        {
            GameSessionState.Clear();
            var user = "save_test_" + Guid.NewGuid();
            var service = new GameSessionService(user, "test_professor");
            var session = NewSavedSession(service);
            session.currentRound = sample[0];
            GameSessionState.Save();
            for (int round = 1; round <= sample[1]; round++)
                rounds.Insert(new RoundResultEntity
                {
                    roundResultId = Guid.NewGuid().ToString(), sessionId = session.sessionId, round = round
                });
            var eventHistory = new SessionEventHistoryEntity
            {
                historyId = Guid.NewGuid().ToString(), sessionId = session.sessionId,
                round = sample[2] > 0 ? sample[2] : 1, eventId = "test_event"
            };
            history.Insert(eventHistory);
            int count = rounds.GetBySessionId(session.sessionId).Count;
            GameSessionState.Clear();
            GameManager.Instance.StateMachine.ForceState(GameState.Bootstrap);
            GameManager.Instance.PrepareSession(user, "test_professor");
            bool incompatible = sample[3] == 1;
            string label = "Save rodada " + sample[0] + ", resultado max " + sample[1] + ", historico " + sample[2];
            check(GameSessionState.HasIncompatibleSave == incompatible, label + ": classificacao correta");
            check(GameManager.Instance.StateMachine.CurrentState == (incompatible ? GameState.MainMenu : GameState.Management_Hub),
                label + ": encaminhamento correto");
            GameSessionState.Save();
            check(sessions.GetById(session.sessionId)?.currentRound == sample[0]
                && rounds.GetBySessionId(session.sessionId).Count == count
                && history.GetBySession(session.sessionId).Count == 1,
                label + ": deteccao preserva sessao, resultados e historico");
            if (incompatible)
            {
                check(!GameSessionState.HasActiveSession && GameSessionState.Current == null,
                    label + ": save isolado da sessao jogavel");
                bool creationBlocked = false;
                try { service.CreateNewSession(); }
                catch (Exception) { creationBlocked = true; }
                check(creationBlocked, label + ": nova sessao exige confirmar reinicio");
                var replacement = service.RestartSession();
                check(sessions.GetById(session.sessionId) == null
                    && rounds.GetBySessionId(session.sessionId).Count == 0
                    && history.GetBySession(session.sessionId).Count == 0,
                    label + ": reinicio remove todos os dados relacionados");
                check(replacement.sessionId != session.sessionId && replacement.currentRound == 1
                    && replacement.status == GameSessionStatus.IN_PROGRESS && !GameSessionState.HasIncompatibleSave
                    && sessions.GetById(replacement.sessionId) == null && !GameSessionState.IsPersisted,
                    label + ": nova sessao ativa so em memoria (grava no confirm da D3)");
            }
        }
#if UNITY_EDITOR
        if (!Application.isPlaying)
            RunSaveInterfaceTest(check);
#endif
    }

#if UNITY_EDITOR
    private static void RunCreditInterfaceTests(Action<bool, string> check, CreditLineData line)
    {
        var root = new GameObject("CreditInterfaceTest", typeof(RectTransform));
        root.SetActive(false);
        try
        {
            var card = root.AddComponent<Game.Adapter.In.UI.BankCardView>();
            var labelObject = new GameObject("Term", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
            labelObject.transform.SetParent(root.transform, false);
            var label = labelObject.GetComponent<TMPro.TextMeshProUGUI>();
            var button = root.AddComponent<UnityEngine.UI.Button>();
            var fields = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            card.GetType().GetField("termText", fields).SetValue(card, label);
            card.GetType().GetField("selectButton", fields).SetValue(card, button);
            GameSessionState.Clear();
            var session = new GameSessionService("credit_ui_test_" + Guid.NewGuid(), "test_professor").CreateNewSession();
            foreach (int month in new[] { 1, 2, 3 })
            {
                session.currentRound = month;
                card.Setup(line);
                check(label.text == (month < 3 ? (4 - month) + " meses" : "Novas contratações indisponíveis")
                    && button.interactable == (month < 3), "Credito UI: prazo e disponibilidade no mes " + month);
            }
            var view = root.AddComponent<Game.Adapter.In.UI.FinancialScreenView>();
            var controller = root.AddComponent<Game.Adapter.In.Controllers.FinancialScreenController>();
            view.GetType().GetField("hintText", fields).SetValue(view, label);
            view.GetType().GetField("confirmButton", fields).SetValue(view, button);
            controller.GetType().GetField("view", fields).SetValue(controller, view);
            controller.GetType().GetField("_selectedCreditLine", fields).SetValue(controller, line);
            float cash = session.currentCash;
            controller.GetType().GetMethod("OnConfirm", fields).Invoke(controller, null);
            check(label.text == LoanService.LastMonthMessage && !button.interactable
                && session.loanBalance == 0f && session.currentCash == cash && session.creditLineId == null,
                "Credito UI: confirmacao no mes 3 bloqueada com mensagem clara");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void RunSaveInterfaceTest(Action<bool, string> check)
    {
        var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity",
            UnityEditor.SceneManagement.OpenSceneMode.Additive);
        try
        {
            GameSessionState.Clear();
            var service = new GameSessionService("ui_save_test_" + Guid.NewGuid(), "test_professor");
            var old = NewSavedSession(service);
            old.currentRound = 4;
            GameSessionState.Save();
            GameManager.Instance.StateMachine.ForceState(GameState.Bootstrap);
            GameManager.Instance.PrepareSession(old.userId, old.professorId);
            var listener = UnityEngine.Object.FindObjectsByType<UIStateListener>(FindObjectsInactive.Include,
                FindObjectsSortMode.None).First(item => item.gameObject.scene == scene);
            typeof(UIStateListener).GetMethod("HandleStateChanged", System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.NonPublic).Invoke(listener, new object[] { GameState.MainMenu });
            var view = UnityEngine.Object.FindObjectsByType<Game.Adapter.In.UI.EstablishmentSummaryView>(
                FindObjectsInactive.Include, FindObjectsSortMode.None).First(item => item.gameObject.scene == scene);
            var fields = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var warning = (GameObject)view.GetType().GetField("resetWarning", fields).GetValue(view);
            var confirm = (UnityEngine.UI.Button)view.GetType().GetField("confirmResetButton", fields).GetValue(view);
            var cancel = (UnityEngine.UI.Button)view.GetType().GetField("cancelResetButton", fields).GetValue(view);
            var hub = (GameObject)typeof(UIStateListener).GetField("managementHubPanel", fields).GetValue(listener);
            check(warning.activeInHierarchy && !hub.activeInHierarchy,
                "UI: modal existente visivel sem abrir hub");
            check(warning.GetComponentsInChildren<TMPro.TextMeshProUGUI>().Any(label =>
                label.text.Contains("versão anterior") && label.text.Contains("3 meses")),
                "UI: mensagem explica incompatibilidade trimestral");
            check(confirm.GetComponentInChildren<TMPro.TextMeshProUGUI>().text == "Iniciar nova sessão"
                && !cancel.gameObject.activeSelf, "UI: acao de reinicio clara sem liberar save bloqueado");
            check(new GameSessionRepository(DatabaseInitializer.DatabaseService.Connection).GetById(old.sessionId) != null,
                "UI: exibir modal nao apaga save");
            confirm.onClick.Invoke();
            check(!warning.activeSelf && !GameSessionState.HasIncompatibleSave
                && GameSessionState.Current.currentRound == 1
                && GameManager.Instance.StateMachine.CurrentState == GameState.Config_Location,
                "UI: confirmar reinicio fecha modal e inicia configuracao");
            check(new GameSessionRepository(DatabaseInitializer.DatabaseService.Connection).GetById(old.sessionId) == null,
                "UI: save anterior removido somente apos confirmacao");
        }
        finally
        {
            UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true);
        }
    }

    // Entrada batch: SQLite em memoria, sem abrir ou modificar saves de jogadores.
    public static void RunBatch()
    {
        var databaseObject = new GameObject("QuarterTestDatabase");
        var databaseService = databaseObject.AddComponent<DatabaseService>();
        var connection = new SQLite4Unity3d.SQLiteConnection(":memory:");
        typeof(DatabaseService).GetProperty("Connection").SetValue(databaseService, connection);
        typeof(DatabaseInitializer).GetProperty("DatabaseService").SetValue(null, databaseService);
        connection.CreateTable<GameSessionEntity>();
        connection.CreateTable<RoundResultEntity>();
        connection.CreateTable<SessionEventHistoryEntity>();
        var managerObject = new GameObject("QuarterTestManager");
        var manager = managerObject.AddComponent<GameManager>();
        // Inicializa as dependencias sem executar o ciclo de vida de Play Mode.
        typeof(GameManager).GetProperty("Instance").SetValue(null, manager);
        typeof(GameManager).GetProperty("StateMachine").SetValue(manager, new GameStateMachine());
        try
        {
            RunTests();
        }
        finally
        {
            connection.Close();
            UnityEngine.Object.DestroyImmediate(managerObject);
            UnityEngine.Object.DestroyImmediate(databaseObject);
            typeof(DatabaseInitializer).GetProperty("DatabaseService").SetValue(null, null);
            typeof(GameManager).GetProperty("Instance").SetValue(null, null);
        }
    }
#endif
}
