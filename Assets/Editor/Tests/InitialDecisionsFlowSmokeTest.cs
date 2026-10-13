using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Adapter.In.UI;
using Game.Infrastructure.Session;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Teste de fumaca do fluxo D1 -> D2 -> D3 dentro do Play Mode.
///
/// Aperta os BOTOES REAIS da GameScene (Button.onClick), na ordem que um
/// jogador apertaria, e confere a cada passo: estado da state machine,
/// painel visivel, rascunho em memoria e quantidade de linhas no SQLite.
///
/// Como usar:
///   1. Feche o jogo, apague (ou renomeie) o game.db para comecar sem partida salva.
///   2. Abra a GameScene e aperte Play. Espere a D1 (mapa) aparecer.
///   3. Menu Tools > Jogos de Empresa > Teste de fumaca D1-D3.
///   4. Acompanhe o Console: cada passo imprime [Fumaca] OK ou [Fumaca] FALHOU.
///
/// Atencao: o teste confirma a D3 de verdade, entao no fim existe UMA sessao
/// gravada no game.db. Apague o arquivo de novo se quiser repetir.
/// </summary>
public static class InitialDecisionsFlowSmokeTest
{
    private const string Tag = "[Fumaca] ";

    private static readonly List<(string name, Action action)> Steps = new();
    private static int _index;
    private static double _nextTime;
    private static int _failures;
    private static float _changedPrice;

    private static double _stepDelay = 0.4;

    [MenuItem("Tools/Jogos de Empresa/Teste de fumaca D1-D3")]
    public static void Run()
    {
        if (!EditorApplication.isPlaying)
        {
            Debug.LogError(Tag + "Entre no Play Mode na GameScene antes de rodar.");
            return;
        }

        BuildSteps();
        Start(0.4);
    }

    /// <summary>
    /// Mesmo teste, mas comecando pela cena 0_Identification e passando pelo
    /// Voltar da D1 (que volta para a identificacao). Confere que nome, RA e
    /// nome do restaurante chegam na linha gravada.
    /// Como usar: game.db apagado, abrir a cena 0_Identification, Play, e rodar.
    /// </summary>
    [MenuItem("Tools/Jogos de Empresa/Teste de fumaca Identificacao + D1-D3")]
    public static void RunWithIdentification()
    {
        if (!EditorApplication.isPlaying)
        {
            Debug.LogError(Tag + "Entre no Play Mode na cena 0_Identification antes de rodar.");
            return;
        }

        BuildIdentificationSteps();
        Start(1.2);   // troca de cena leva mais tempo que troca de painel
    }

    private static void Start(double stepDelay)
    {
        _stepDelay = stepDelay;
        _index = 0;
        _failures = 0;
        _nextTime = EditorApplication.timeSinceStartup + 0.3;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        Debug.Log(Tag + $"Iniciando {Steps.Count} passos.");
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying)
        {
            EditorApplication.update -= Tick;
            return;
        }

        if (EditorApplication.timeSinceStartup < _nextTime)
            return;

        if (_index >= Steps.Count)
        {
            EditorApplication.update -= Tick;
            if (_failures == 0)
                Debug.Log(Tag + "TERMINOU: todos os passos passaram.");
            else
                Debug.LogError(Tag + $"TERMINOU com {_failures} falha(s).");
            return;
        }

        var (name, action) = Steps[_index++];
        try
        {
            action();
            Debug.Log(Tag + "OK      " + name);
        }
        catch (Exception e)
        {
            _failures++;
            Debug.LogError(Tag + "FALHOU  " + name + "\n" + e.Message);
            // Sem estado confiavel, os passos seguintes nao fazem sentido.
            _index = Steps.Count;
        }

        // Da tempo para Start/OnEnable/Destroy rodarem antes do proximo passo.
        _nextTime = EditorApplication.timeSinceStartup + _stepDelay;
    }

    // ─────────────────────────────────────────────────────────────

    private const string StudentName = "Aluna Teste";
    private const string StudentRA = "123456";
    private const string FirstCompanyName = "Sushi Bom";
    private const string SecondCompanyName = "Sushi Mar";

    private static void BuildIdentificationSteps()
    {
        Steps.Clear();

        Steps.Add(("Identificacao: preenche e Cadastrar abre o jogo na D1", () =>
        {
            Expect(ActiveScene == Game.Infrastructure.SceneNames.Identification,
                "o teste precisa comecar na cena 0_Identification (e sem cadastro salvo). Cena atual: " + ActiveScene);
            var ui = Identification;
            Input(ui, "studentNameInput").text = StudentName;
            Input(ui, "raInput").text = StudentRA;
            Input(ui, "restaurantNameInput").text = FirstCompanyName;
            ui.Register();
        }));

        Steps.Add(("GameScene: D1 vazia, banco sem sessao", () =>
        {
            Expect(ActiveScene == Game.Infrastructure.SceneNames.Game, "deveria estar na GameScene, esta em " + ActiveScene);
            Expect(State == GameState.Config_Location, "estado deveria ser Config_Location, e " + State);
            Expect(SessionRows() == 0, $"esperava 0 linhas no banco, ha {SessionRows()}");
        }));

        Steps.Add(("D1: escolhe a Area Financeira e aperta Voltar", () =>
        {
            Click(LocationView, "bankButton");
            Click(LocationView, "backButton");
        }));

        Steps.Add(("Identificacao reaberta com os dados preenchidos (sem pular sozinha); troca o nome do restaurante", () =>
        {
            Expect(ActiveScene == Game.Infrastructure.SceneNames.Identification,
                "Voltar da D1 deveria abrir a identificacao e ficar nela. Cena atual: " + ActiveScene);
            var ui = Identification;
            Expect(Input(ui, "studentNameInput").text == StudentName, "nome do aluno deveria vir preenchido");
            Expect(Input(ui, "restaurantNameInput").text == FirstCompanyName, "nome do restaurante deveria vir preenchido");
            Input(ui, "restaurantNameInput").text = SecondCompanyName;
            ui.Register();
        }));

        Steps.Add(("D1 reidratada com a Area Financeira depois da troca de cena", () =>
        {
            Expect(ActiveScene == Game.Infrastructure.SceneNames.Game, "deveria estar na GameScene, esta em " + ActiveScene);
            Expect(State == GameState.Config_Location, "estado deveria continuar Config_Location, e " + State);
            Expect(PlayerSession.SelectedZone == LocationZone.Financas, "rascunho deveria manter Financas");
            Expect(PrivateField<GameObject>(LocationView, "detailsPanel").activeSelf, "DetailsPanel deveria vir aberto");
            Expect(ButtonOf(LocationView, "confirmButton").interactable, "Confirmar deveria vir habilitado");
            Expect(SessionRows() == 0, $"esperava 0 linhas no banco, ha {SessionRows()}");
        }));

        Steps.Add(("D1 -> D2 (Frances)", () =>
        {
            Click(LocationView, "confirmButton");
            Click(RestaurantView, "cardFrances");
            Click(RestaurantView, "confirmButton");
            Expect(State == GameState.Config_TargetSegment, "estado deveria ser D3, e " + State);
        }));

        Steps.Add(("D3: Confirmar grava nome, RA e nome do restaurante novo", () =>
        {
            Click(MenuView, "confirmButton");
            Expect(SessionRows() == 1, $"esperava 1 linha no banco, ha {SessionRows()}");
            var row = Db.Table<GameSessionEntity>().Single();
            Expect(row.studentName == StudentName, "studentName gravado: " + row.studentName);
            Expect(row.studentRA == StudentRA, "studentRA gravado: " + row.studentRA);
            Expect(row.companyName == SecondCompanyName, "companyName deveria ser o nome corrigido, e " + row.companyName);
            Expect(row.locationZone == LocationZone.Financas, "zona gravada: " + row.locationZone);
            Expect(row.restaurantType == RestaurantType.FRANCES, "restaurante gravado: " + row.restaurantType);
            Debug.Log(Tag + $"linha gravada: aluno='{row.studentName}' RA='{row.studentRA}' empresa='{row.companyName}' zona={row.locationZone} restaurante={row.restaurantType} segmento={row.targetSegment}");
        }));

        Steps.Add(("Limpeza: apaga o cadastro de teste do PlayerPrefs", RemoveTestRegistration));
    }

    /// <summary>
    /// O teste salva "Aluna Teste" no PlayerPrefs (e o que o botao Cadastrar faz).
    /// Sem limpar, a cena de identificacao passaria a pular sozinha para o jogo
    /// com esse cadastro falso. So apaga se o cadastro salvo for o do teste.
    /// </summary>
    [MenuItem("Tools/Jogos de Empresa/Apagar cadastro de teste (PlayerPrefs)")]
    public static void RemoveTestRegistration()
    {
        const string key = "PlayerSession.Identification";
        string saved = PlayerPrefs.GetString(key, string.Empty);

        if (saved.Contains("\"" + StudentName + "\""))
        {
            PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
            Debug.Log(Tag + "Cadastro de teste removido do PlayerPrefs.");
        }
        else
        {
            Debug.Log(Tag + "Nenhum cadastro de teste no PlayerPrefs. Nada foi apagado.");
        }
    }

    private static string ActiveScene => UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

    private static UIIndentification Identification
    {
        get
        {
            var ui = UnityEngine.Object.FindFirstObjectByType<UIIndentification>();
            Expect(ui != null, "UIIndentification nao encontrado na cena");
            return ui;
        }
    }

    private static TMPro.TMP_InputField Input(UIIndentification ui, string field) =>
        PrivateField<TMPro.TMP_InputField>(ui, field);

    // ─────────────────────────────────────────────────────────────

    private static void BuildSteps()
    {
        Steps.Clear();

        Steps.Add(("D1 aparece vazia e o banco nao tem sessao", () =>
        {
            Expect(State == GameState.Config_Location, "estado deveria ser Config_Location, e " + State);
            Expect(Panel("Panel_Location").activeInHierarchy, "Panel_Location nao esta visivel");
            Expect(!PlayerSession.HasLocation, "rascunho deveria comecar sem localizacao");
            Expect(SessionRows() == 0, $"esperava 0 linhas no banco, ha {SessionRows()}");
        }));

        Steps.Add(("D1: clica na Area Comercial e o Confirmar habilita", () =>
        {
            Click(LocationView, "storeButton");
            Expect(ButtonOf(LocationView, "confirmButton").interactable, "Confirmar deveria habilitar");
            Expect(PrivateField<GameObject>(LocationView, "detailsPanel").activeSelf, "DetailsPanel deveria aparecer");
        }));

        Steps.Add(("D1: Confirmar leva para a D2 sem gravar no banco", () =>
        {
            Click(LocationView, "confirmButton");
            Expect(State == GameState.Config_Restaurant, "estado deveria ser Config_Restaurant, e " + State);
            Expect(Panel("Panel_Restaurant").activeInHierarchy, "Panel_Restaurant nao esta visivel");
            Expect(PlayerSession.SelectedZone == LocationZone.Comercio, "rascunho deveria ter Comercio");
            Expect(SessionRows() == 0, $"esperava 0 linhas no banco, ha {SessionRows()}");
        }));

        Steps.Add(("D2: escolhe Japones e Confirmar leva para a D3 sem gravar", () =>
        {
            Click(RestaurantView, "cardJapones");
            Expect(ButtonOf(RestaurantView, "confirmButton").interactable, "Confirmar da D2 deveria habilitar");
            Click(RestaurantView, "confirmButton");
            Expect(State == GameState.Config_TargetSegment, "estado deveria ser Config_TargetSegment (D3), e " + State);
            Expect(Panel("Panel_MenuPricing").activeInHierarchy, "Panel_MenuPricing nao esta visivel");
            Expect(PlayerSession.SelectedRestaurantType == RestaurantType.JAPONES, "rascunho deveria ter JAPONES");
            Expect(SessionRows() == 0, $"esperava 0 linhas no banco, ha {SessionRows()}");
        }));

        Steps.Add(("D3: muda o preco do primeiro prato", () =>
        {
            var items = ProductItems();
            Expect(items.Count > 0, "D3 deveria listar pratos");
            var slider = PrivateField<Slider>(items[0], "priceSlider");
            slider.value = slider.maxValue;
            _changedPrice = slider.value;
        }));

        Steps.Add(("D3: Voltar leva para a D2 com o Japones marcado e o cardapio guardado", () =>
        {
            Click(MenuView, "backButton");
            Expect(State == GameState.Config_Restaurant, "estado deveria ser Config_Restaurant, e " + State);
            Expect(PlayerSession.SelectedRestaurantType == RestaurantType.JAPONES, "D2 deveria manter JAPONES");
            Expect(PlayerSession.HasMenu, "cardapio da D3 deveria ter ido para o rascunho");
            var selectedIcon = PrivateField<Button>(RestaurantView, "cardJapones")
                .GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "selectedIcon");
            if (selectedIcon != null)
                Expect(selectedIcon.gameObject.activeSelf, "card Japones deveria aparecer selecionado");
        }));

        Steps.Add(("D2: Voltar leva para a D1 com a Area Comercial reidratada", () =>
        {
            Click(RestaurantView, "backButton");
            Expect(State == GameState.Config_Location, "estado deveria ser Config_Location, e " + State);
            Expect(ButtonOf(LocationView, "confirmButton").interactable, "Confirmar da D1 deveria vir habilitado (reidratado)");
            Expect(PrivateField<GameObject>(LocationView, "detailsPanel").activeSelf, "DetailsPanel deveria vir aberto (reidratado)");
        }));

        Steps.Add(("D1: troca para a Area Financeira e avanca", () =>
        {
            Click(LocationView, "bankButton");
            Click(LocationView, "confirmButton");
            Expect(State == GameState.Config_Restaurant, "estado deveria ser Config_Restaurant, e " + State);
            Expect(PlayerSession.SelectedZone == LocationZone.Financas, "rascunho deveria ter trocado para Financas");
            Expect(PlayerSession.SelectedRestaurantType == RestaurantType.JAPONES, "D2 deveria continuar com JAPONES");
        }));

        Steps.Add(("D2 -> D3: o preco alterado volta na tela", () =>
        {
            Click(RestaurantView, "confirmButton");
            Expect(State == GameState.Config_TargetSegment, "estado deveria ser D3, e " + State);
        }));

        Steps.Add(("D3: confere o preco reidratado", () =>
        {
            var items = ProductItems();
            Expect(items.Count > 0, "D3 deveria listar pratos");
            Expect(Mathf.Approximately(items[0].SelectedPrice, _changedPrice),
                $"preco do 1o prato deveria ser {_changedPrice}, e {items[0].SelectedPrice}");
            Expect(SessionRows() == 0, $"ainda nao confirmou a D3: esperava 0 linhas, ha {SessionRows()}");
        }));

        Steps.Add(("D3: Confirmar grava UMA linha com as escolhas novas e segue para o Banco", () =>
        {
            Click(MenuView, "confirmButton");
            Expect(SessionRows() == 1, $"esperava 1 linha no banco, ha {SessionRows()}");
            Expect(GameSessionState.IsPersisted, "sessao deveria estar marcada como gravada");
            Expect(State == InitialDecisionFlow.AfterCommit, "estado deveria ser " + InitialDecisionFlow.AfterCommit + ", e " + State);

            var row = Db.Table<GameSessionEntity>().Single();
            Expect(row.locationZone == LocationZone.Financas, "linha deveria ter Financas (a escolha nova), tem " + row.locationZone);
            Expect(row.restaurantType == RestaurantType.JAPONES, "linha deveria ter JAPONES");
            Expect(MenuPricingHelper.FromJson(row.menuPricingJson).items.Count > 0, "linha deveria ter o cardapio");
            Expect(MenuPricingHelper.TryGetPrice(MenuPricingHelper.FromJson(row.menuPricingJson),
                    ProductIdOfFirstItem, out var price) && Mathf.Approximately(price, _changedPrice),
                "linha deveria ter o preco alterado");
            Debug.Log(Tag + $"linha gravada: sessionId={row.sessionId} aluno='{row.studentName}' RA='{row.studentRA}' empresa='{row.companyName}' zona={row.locationZone} restaurante={row.restaurantType} segmento={row.targetSegment}");
        }));

        Steps.Add(("Gravar de novo continua com UMA linha (InsertOrReplace)", () =>
        {
            var service = new GameSessionService(GameSessionState.Current.userId, GameSessionState.Current.professorId);
            Expect(service.CommitInitialDecisions(), "segundo commit deveria funcionar");
            Expect(SessionRows() == 1, $"esperava 1 linha, ha {SessionRows()}");
        }));
    }

    // ─────────────────────────────────────────────────────────────
    //  Auxiliares
    // ─────────────────────────────────────────────────────────────

    private static string ProductIdOfFirstItem;

    private static GameState State => GameManager.Instance.StateMachine.CurrentState;

    private static SQLite4Unity3d.SQLiteConnection Db => DatabaseInitializer.DatabaseService.Connection;

    private static int SessionRows() => Db.Table<GameSessionEntity>().Count();

    private static LocationScreenView LocationView => Panel("Panel_Location").GetComponent<LocationScreenView>();
    private static RestaurantScreenView RestaurantView => Panel("Panel_Restaurant").GetComponent<RestaurantScreenView>();
    private static MenuPricingScreenView MenuView => Panel("Panel_MenuPricing").GetComponent<MenuPricingScreenView>();

    private static List<ProductPricingItemView> ProductItems()
    {
        var items = Panel("Panel_MenuPricing").GetComponentsInChildren<ProductPricingItemView>(false).ToList();
        ProductIdOfFirstItem = items.Count > 0 && items[0].Product != null ? items[0].Product.id : null;
        return items;
    }

    private static GameObject Panel(string name) => Find(name);

    private static GameObject Find(string path)
    {
        var canvas = GameObject.Find("Canvas");
        Expect(canvas != null, "Canvas nao encontrado. A GameScene esta aberta?");
        var t = canvas.transform.Find(path);
        Expect(t != null, path + " nao encontrado dentro do Canvas");
        return t.gameObject;
    }

    private static Button ButtonOf(object view, string field) => PrivateField<Button>(view, field);

    private static void Click(object view, string field)
    {
        var button = ButtonOf(view, field);
        Expect(button != null, field + " nao esta ligado no Inspector");
        Expect(button.IsActive() && button.interactable, field + " nao esta clicavel (inativo ou desabilitado)");
        button.onClick.Invoke();
    }

    private static T PrivateField<T>(object target, string field) where T : class
    {
        var f = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Expect(f != null, $"campo {field} nao existe em {target.GetType().Name}");
        return f.GetValue(target) as T;
    }

    private static void Expect(bool condition, string message)
    {
        if (!condition)
            throw new Exception(message);
    }
}
