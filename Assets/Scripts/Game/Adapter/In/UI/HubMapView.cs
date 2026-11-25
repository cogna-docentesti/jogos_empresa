using System;
using Game.Adapter.In.UI.Navigation;
using TMPro;
using UnityEngine;

namespace Game.Adapter.In.UI
{
    public sealed class HubMapView : MonoBehaviour
    {
        [Serializable] public struct Zone { public string id; public UnityEngine.UI.Image overlay; }
        [Serializable] public struct Restaurant { public RestaurantType type; public UnityEngine.UI.Image image; }
        [SerializeField] private MapMenuNode[] nodes=Array.Empty<MapMenuNode>();
        [SerializeField] private Zone[] zones=Array.Empty<Zone>();
        [SerializeField] private Restaurant[] restaurants=Array.Empty<Restaurant>();
        [SerializeField] private UnityEngine.UI.Image centerOverlay;
        [SerializeField] private TMP_Text locationLabel, restaurantLabel;
        private (LocationZone? zone, RestaurantType? restaurant, bool available) lastSelection;
        public MapMenuNode[] Nodes => nodes;

        public void Configure(MapMenuNode[] markers, Zone[] areas, Restaurant[] variants,
            UnityEngine.UI.Image center, TMP_Text location, TMP_Text restaurant)
        { nodes=markers; zones=areas; restaurants=variants; centerOverlay=center; locationLabel=location; restaurantLabel=restaurant; }
        private void OnEnable() => Refresh();
        private void Update()
        {
            var selection = ReadSelection();
            if (selection != lastSelection) ApplySelection(selection);
        }

        private static (LocationZone? zone, RestaurantType? restaurant, bool available) ReadSelection()
        {
            var session = GameSessionState.IsPersisted ? GameSessionState.Current : null;
            return (session?.locationZone, session?.restaurantType, MenuNavigator.HubAvailable);
        }

        public void Refresh() => ApplySelection(ReadSelection());

        private void ApplySelection((LocationZone? zone, RestaurantType? restaurant, bool available) selection)
        {
            lastSelection = selection;
            string id = selection.zone.HasValue ? LocationZoneMap.ToId(selection.zone.Value) : null;
            foreach (var zone in zones)
                if (zone.overlay != null) zone.overlay.enabled = id == zone.id;

            bool hasFacade = false;
            foreach (var variant in restaurants)
            {
                if (variant.image == null) continue;
                bool selected = variant.type == selection.restaurant;
                variant.image.enabled = selected;
                hasFacade |= selected;
            }
            if (centerOverlay != null) centerOverlay.enabled = hasFacade;

            foreach (var node in nodes)
            {
                if (node == null) continue;
                node.GetComponent<UnityEngine.UI.Button>().interactable = selection.available;
                node.SetSelected(id != null && node.LocationId == id);
            }

            if (locationLabel != null)
            {
                var location = id != null ? Resources.Load<LocationData>("Locations/LOC_" + id) : null;
                locationLabel.text = location != null ? location.displayName : "Localização pendente";
            }
            if (restaurantLabel != null)
                restaurantLabel.text = selection.restaurant switch
                {
                    RestaurantType.FRANCES => "Restaurante francês",
                    RestaurantType.JAPONES => "Restaurante japonês",
                    RestaurantType.PODRAO => "Restaurante de lanches",
                    _ => "Restaurante pendente"
                };
        }
    }
}
