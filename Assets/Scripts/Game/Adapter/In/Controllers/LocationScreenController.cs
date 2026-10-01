using System.Linq;
using Game.Adapter.Out.Persistence;
using Game.Domain.Entities;
using Game.Application.Ports.Out;
using Game.Infrastructure;
using Game.Infrastructure.Session;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Adapter.In.Controllers
{
    /// <summary>
    /// D1 Localizacao.
    ///
    /// Le e grava SO no rascunho em memoria (PlayerSession). O banco so e
    /// tocado no confirm da D3.
    ///
    /// Este componente fica num filho do Panel_Location. O UIStateListener liga
    /// e desliga o painel inteiro; cada vez que o painel reaparece, o Unity
    /// chama OnEnable aqui, e e nesse momento que a tela e reidratada.
    /// </summary>
    public sealed class LocationScreenController : MonoBehaviour
    {
        [SerializeField] private Game.Adapter.In.UI.LocationScreenView view;

        // D1 e a primeira decisao. Voltar daqui leva para o cadastro do aluno,
        // que e outra cena. Avancar leva para a D2, que e outro estado.
        private const string PreviousScene = SceneNames.Identification;
        private const GameState NextState  = InitialDecisionFlow.Restaurant;

        private ILocationRepository _repository;
        private LocationScreenData  _data;
        private Establishment       _selectedEstablishment;
        private bool                _initialized;

        // ── Ciclo de vida ─────────────────────────────────────────────────

        private void Awake()
        {
            // Composicao manual. Trocar por SqliteLocationRepository na E-06.
            _repository = new StaticLocationRepository();
        }

        private void Start()
        {
            // Start roda uma vez so, na primeira vez que o painel aparece.
            // Aqui fica o que so precisa ser feito uma vez: carregar dados,
            // pintar as areas do mapa e ligar os botoes.
            _data = _repository.GetLocationScreenData("university_region");

            InitializeAreaVisuals();
            BindActions();

            _initialized = true;
            ShowCurrentSelection();
        }

        private void OnEnable()
        {
            // Na primeira ativacao o OnEnable roda ANTES do Start, quando os
            // dados ainda nao foram carregados. O Start cuida desse caso.
            // Nas ativacoes seguintes (voltando da D2) o Start nao roda de novo,
            // entao a reidratacao precisa acontecer aqui.
            if (_initialized)
                ShowCurrentSelection();
        }

        // ── Inicializacao ─────────────────────────────────────────────────

        private void InitializeAreaVisuals()
        {
            InitArea("bank",        view.InitializeBankArea);
            InitArea("store",       view.InitializeStoreArea);
            InitArea("marketing",   view.InitializeMarketingArea);
            InitArea("university",  view.InitializeUniversityArea);
            InitArea("condominium", view.InitializeCondominiumArea);
        }

        private void InitArea(
            string id,
            System.Action<string, string, string, Sprite> initMethod)
        {
            var raw = Resources.Load<LocationData>($"Locations/LOC_{id}");

            if (raw == null)
            {
                Debug.LogWarning($"[LocationScreenController] ScriptableObject não encontrado para: {id}");
                return;
            }

            var est = _data.Establishments.FirstOrDefault(e => e.Id == id);
            if (est == null) return;

            initMethod(est.Name, est.Segment, raw.colorHex, raw.pinIcon);
        }

        private void BindActions()
        {
            view.BindBankAction(()        => OnLocationSelected("bank"));
            view.BindUniversityAction(()  => OnLocationSelected("university"));
            view.BindMarketingAction(()   => OnLocationSelected("marketing"));
            view.BindStoreAction(()       => OnLocationSelected("store"));
            view.BindCondominiumAction(() => OnLocationSelected("condominium"));

            view.BindConfirmAction(OnConfirm);
            view.BindBackAction(OnBack);
        }

        // ── Renderizacao ──────────────────────────────────────────────────

        /// <summary>
        /// Desenha a tela "limpa" e, se o rascunho ja tiver uma localizacao,
        /// reaplica a escolha por cima. A ordem importa: Render() chama
        /// ShowEmptyState(), que esconde os detalhes e desabilita o Confirmar.
        /// Se a reidratacao viesse antes, o Render() desfaria tudo.
        /// </summary>
        private void ShowCurrentSelection()
        {
            _selectedEstablishment = null;
            Render();
            RestorePreviousSelection();
        }

        private void Render()
        {
            view.SetRegionName(_data.RegionName);
            view.SetHint("Selecione uma área no mapa para continuar.");
            view.ShowEmptyState();

            view.SetBankVisible(IsAvailable("bank"));
            view.SetUniversityVisible(IsAvailable("university"));
            view.SetStoreVisible(IsAvailable("store"));
            view.SetMarketingVisible(IsAvailable("marketing"));
            view.SetCondominiumVisible(IsAvailable("condominium"));
        }

        private void RestorePreviousSelection()
        {
            if (!PlayerSession.HasLocation)
                return;

            string id = PlayerSession.SelectedEstablishmentId;

            if (!LocationZoneMap.IsKnownId(id))
                id = LocationZoneMap.ToId(PlayerSession.SelectedZone.Value);

            if (string.IsNullOrEmpty(id))
                return;

            // Reaproveita o mesmo caminho do clique: seleciona a area,
            // preenche o painel lateral e habilita o Confirmar.
            OnLocationSelected(id);
        }

        private bool IsAvailable(string id) =>
            _data.Establishments.Any(e => e.Id == id && e.IsUnlocked);

        // ── Acoes ─────────────────────────────────────────────────────────

        private void OnLocationSelected(string id)
        {
            var e = _data.Establishments.FirstOrDefault(x => x.Id == id);
            if (e == null) return;

            _selectedEstablishment = e;
            view.SelectArea(id);
            view.SetHint($"Você selecionou: {e.Name}");
            view.ShowLocationDetails(
                e.Name,
                e.Segment,
                e.Description,
                e.RentCost,
                e.InitialPhysicalCapacity,
                e.BaseDailyDemand,
                e.ReferencePriceFactor,
                e.CompetitionLevel,
                e.Channels);
        }

        private void OnConfirm()
        {
            if (_selectedEstablishment == null) return;

            SaveDraft();

            // Nada de banco aqui. O Save acontece so no confirm da D3.
            InitialDecisionFlow.GoTo(NextState);
        }

        private void OnBack()
        {
            // Guarda o que esta na tela antes de sair: quando o jogador voltar
            // da identificacao, a area escolhida continua marcada.
            if (_selectedEstablishment != null)
                SaveDraft();

            // Sem este pedido a tela de identificacao veria o cadastro salvo e
            // pularia direto de volta para o jogo.
            PlayerSession.RequestIdentificationEdit();
            SceneManager.LoadScene(PreviousScene);
        }

        private void SaveDraft()
        {
            PlayerSession.SaveLocation(
                _selectedEstablishment.Id,
                _selectedEstablishment.Name,
                LocationZoneMap.ToZone(_selectedEstablishment.Id));
        }
    }
}
