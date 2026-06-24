using System.Linq;
using Game.Adapter.Out.Persistence;
using Game.Domain.Entities;
using Game.Application.Ports.Out;
using Game.Infrastructure.Session;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Adapter.In.Controllers
{
    public sealed class LocationScreenController : MonoBehaviour
    {
        [SerializeField] private Game.Adapter.In.UI.LocationScreenView view;
        [SerializeField] private string nextSceneName     = "SelectedEstablishmentScene";
        [SerializeField] private string previousSceneName = "MainMenuScene";

        private ILocationRepository _repository;
        private LocationScreenData  _data;
        private Establishment       _selectedEstablishment;

        // ── Ciclo de vida ─────────────────────────────────────────────────

        private void Awake()
        {
            // Composição manual — trocar por SqliteLocationRepository na Fase 2
            _repository = new StaticLocationRepository();
        }

        private void Start()
        {
            _data = _repository.GetLocationScreenData("university_region");

            InitializeAreaVisuals(); 
            Render();
            BindActions();
        }


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

        private bool IsAvailable(string id) =>
            _data.Establishments.Any(e => e.Id == id && e.IsUnlocked);


        private void BindActions()
        {
            view.BindBankAction(() =>
            {
                Debug.Log("[Controller] BankButton clicado!");
                OnLocationSelected("bank");
            });

             view.BindUniversityAction(()  => OnLocationSelected("university"));

             view.BindMarketingAction(()   => OnLocationSelected("marketing"));

             view.BindStoreAction(()       => OnLocationSelected("store"));

            view.BindCondominiumAction(() => OnLocationSelected("condominium"));

            view.BindConfirmAction(OnConfirm);
            view.BindBackAction(OnBack);
        }


        private void OnLocationSelected(string id)
        {
            Debug.Log("Entrei no bind actions"+id);
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
                    e.TicketCompat,
                    e.CompetitionLevel,
                    e.Channels);
        }


       private void OnConfirm()
        {
            if (_selectedEstablishment == null) return;

            // Salva a escolha na sessão em MEMÓRIA (não no SQLite ainda)
            LocationZone zone = _selectedEstablishment.Id switch
            {
                "bank"        => LocationZone.Financas,
                "university"  => LocationZone.Educacao,
                "store"       => LocationZone.Comercio,
                "condominium" => LocationZone.Residencial,
                "marketing"   => LocationZone.Servicos,
                _             => LocationZone.Financas
            };
            
            // SetLocation com save: false → só memória, NÃO banco
            GameSessionState.SetLocation(zone);
            
            // Pede ao GameManager para avançar o estado
            GameManager.Instance.StateMachine
                .TryChangeState(GameState.Config_Restaurant);
        }

        private void OnBack()
        {
            // Nenhum estado anterior — volta para o MainMenu
            GameManager.Instance.StateMachine.TryChangeState(GameState.MainMenu);
        }

    }
}