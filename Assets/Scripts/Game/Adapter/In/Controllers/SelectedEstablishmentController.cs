using Game.Adapter.In.UI;
using Game.Infrastructure.Session;
using UnityEngine;

namespace Game.Adapter.In.Controllers
{
    public sealed class SelectedEstablishmentController : MonoBehaviour
    {
        [SerializeField] private SelectedEstablishmentView view;

        private void Start()
        {
            if (string.IsNullOrWhiteSpace(PlayerSession.SelectedEstablishmentName))
            {
                view.SetSelectedText("Nenhum estabelecimento foi selecionado.");
                return;
            }

            view.SetSelectedText($"{PlayerSession.SelectedEstablishmentName} selecionado.");
        }
    }
}