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
        private string lastSnapshot;
        public MapMenuNode[] Nodes => nodes;

        public void Configure(MapMenuNode[] markers, Zone[] areas, Restaurant[] variants,
            UnityEngine.UI.Image center, TMP_Text location, TMP_Text restaurant)
        { nodes=markers; zones=areas; restaurants=variants; centerOverlay=center; locationLabel=location; restaurantLabel=restaurant; }
        private void OnEnable() => Refresh();
        private void Update()
        {
            var session=GameSessionState.Current;
            string snapshot=$"{session?.locationZone}|{session?.restaurantType}|{MenuNavigator.HubAvailable}";
            if(snapshot!=lastSnapshot) { lastSnapshot=snapshot; Refresh(); }
        }
        public void Refresh()
        {
            var session=GameSessionState.Current;
            string id=session!=null?LocationZoneMap.ToId(session.locationZone):null;
            foreach(var zone in zones) if(zone.overlay!=null) zone.overlay.enabled=id==zone.id;
            foreach(var variant in restaurants) if(variant.image!=null) variant.image.enabled=session!=null && variant.type==session.restaurantType;
            if(centerOverlay!=null) centerOverlay.enabled=session!=null;
            foreach(var node in nodes)
            {
                if(node==null) continue;
                node.GetComponent<UnityEngine.UI.Button>().interactable=MenuNavigator.HubAvailable;
                node.SetSelected(node.LocationId==id);
            }
            var location=Resources.Load<LocationData>("Locations/LOC_"+id);
            if(locationLabel!=null) locationLabel.text=location!=null?location.displayName:"Localização pendente";
            if(restaurantLabel!=null) restaurantLabel.text=session==null?"Restaurante pendente":session.restaurantType switch
            { RestaurantType.FRANCES=>"Restaurante francês",RestaurantType.JAPONES=>"Restaurante japonês",_=>"Restaurante de lanches" };
        }
    }
}
