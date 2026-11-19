using TMPro;
using UnityEngine;

namespace Game.Adapter.In.UI
{
    /// <summary>Opt-in component composition inside GameScene. It never reads/writes game data.</summary>
    public sealed class SharedComponentsShowcase : MonoBehaviour
    {
        public UnityEngine.UI.Button primary, openModal, closeModal, confirmModal, selectCard;
        public UnityEngine.UI.Toggle checkbox, radio, toggle;
        public UnityEngine.UI.Slider slider;
        public GameObject modal;
        public TMP_Text status;
        public TMP_InputField input;
        private int clicks;
        private void Awake()
        {
            primary.onClick.AddListener(()=>status.text=$"Ação recebida: {++clicks}");
            openModal.onClick.AddListener(()=>modal.SetActive(true));
            closeModal.onClick.AddListener(()=>modal.SetActive(false));
            confirmModal.onClick.AddListener(()=>{status.text="Modal confirmado"; modal.SetActive(false);});
            selectCard.onClick.AddListener(()=>{
                var appearance=selectCard.GetComponent<SharedControlAppearance>();
                appearance.SetSelected(!appearance.IsSelected);
                status.text=appearance.IsSelected?"Card selecionado":"Seleção removida";
            });
            slider.onValueChanged.AddListener(v=>status.text=$"Controle: {v:0}%");
            input.onValueChanged.AddListener(v=>input.GetComponent<SharedControlAppearance>().IsInvalid=v.Length>0 && v.Length<5);
        }
        private void OnEnable() { if(primary!=null) primary.Select(); }
        private void Start()
        {
            Canvas.ForceUpdateCanvases();
            var scroll=GetComponentInChildren<UnityEngine.UI.ScrollRect>();
            if(scroll!=null) scroll.verticalNormalizedPosition=1;
        }
    }
}
