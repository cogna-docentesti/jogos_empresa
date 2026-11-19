using Game.Infrastructure.Session;
using TMPro;
using UnityEngine;

namespace Game.Adapter.In.UI
{
    public sealed class HubCompanyLabel : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;
        public void Configure(TMP_Text value) => label=value;
        private void OnEnable() => Refresh();
        private void Refresh() { if(label!=null) label.text=string.IsNullOrWhiteSpace(PlayerSession.RestaurantName)?"Meu restaurante":PlayerSession.RestaurantName; }
    }
}
