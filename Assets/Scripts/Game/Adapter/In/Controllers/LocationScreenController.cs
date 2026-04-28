using System.Linq;
using Game.Adapter.Out.Persistence;
using Game.Domain.Entities;
using Game.Domain.Ports.Out;
using Game.Infrastructure.Session;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Adapter.In.Controllers
{
    public sealed class LocationScreenController : MonoBehaviour
    {
        [SerializeField] private Game.Adapter.In.UI.LocationScreenView view;
        [SerializeField] private string nextSceneName = "SelectedEstablishmentScene";
        [SerializeField] private string previousSceneName = "MainMenuScene";

        private ILocationRepository _repository;
        private LocationScreenData  _data;
        private Establishment       _selectedEstablishment;

        private void Awake()
        {
            // Composição manual (sem DI container)
            // Troque StaticLocationRepository por SqliteLocationRepository na fase 2
            _repository = new StaticLocationRepository();
        }

        private void Start()
        {
            _data = _repository.GetLocationScreenData("university_region");
            Render();
            BindActions();
        }

        private void Render()
        {
            view.SetRegionName(_data.RegionName);
            view.SetHint("Selecione uma área no mapa para continuar.");
            view.ShowEmptyState();

            view.SetBankVisible(IsAvailable("bank"));
            view.SetStoreVisible(IsAvailable("store"));
            view.SetMarketingVisible(IsAvailable("marketing"));
            view.SetUniversityVisible(IsAvailable("university"));
            view.SetCondominiumVisible(IsAvailable("condominium"));
        }

        private bool IsAvailable(string id) =>
            _data.Establishments.Any(e => e.Id == id && e.IsUnlocked);

        private void BindActions()
        {
            view.BindBankAction(()        => OnLocationSelected("bank"));
            view.BindStoreAction(()       => OnLocationSelected("store"));
            view.BindMarketingAction(()   => OnLocationSelected("marketing"));
            view.BindUniversityAction(()  => OnLocationSelected("university"));
            view.BindCondominiumAction(() => OnLocationSelected("condominium"));
            view.BindConfirmAction(OnConfirm);
            view.BindBackAction(OnBack);
        }

        private void OnLocationSelected(string id)
        {
            var e = _data.Establishments.FirstOrDefault(x => x.Id == id);
            if (e == null) return;

            _selectedEstablishment = e;
            view.SetHint($"Você selecionou: {e.Name}");
            view.ShowLocationDetails(
                e.Segment, e.RentCost,
                e.TicketCompat, e.CompetitionLevel, e.Channels);
        }

        private void OnConfirm()
        {
            if (_selectedEstablishment == null) return;
            PlayerSession.SaveSelectedEstablishment(
                _selectedEstablishment.Id,
                _selectedEstablishment.Name);
            SceneManager.LoadScene(nextSceneName);
        }

        private void OnBack() =>
            SceneManager.LoadScene(previousSceneName);
    }
}
