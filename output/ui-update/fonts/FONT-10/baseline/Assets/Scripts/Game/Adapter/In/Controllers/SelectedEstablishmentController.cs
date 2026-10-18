using Game.Adapter.In.UI;
using Game.Infrastructure.Session;
using UnityEngine;

namespace Game.Adapter.In.Controllers
{
    public sealed class SelectedEstablishmentController : MonoBehaviour
    {
        [SerializeField] private SelectedEstablishmentView view;

        private void Awake() { if (view == null) view = GetComponent<SelectedEstablishmentView>(); }
        private void OnEnable() => Refresh();
        public void Refresh()
        {
            view?.Bind(Game.Domain.Service.EstablishmentSummaryService.Build());
        }
    }
}
