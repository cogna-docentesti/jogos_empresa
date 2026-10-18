using UnityEngine;

namespace Game.Adapter.In.UI
{
    public sealed class ConceptDetailsToggle : MonoBehaviour
    {
        [SerializeField] GameObject details;
        UnityEngine.UI.Button button;
        void Awake() { button=GetComponent<UnityEngine.UI.Button>(); button.onClick.AddListener(Toggle); }
        void OnDisable() { if(details!=null)details.SetActive(false); }
        void Toggle() { if(details!=null)details.SetActive(!details.activeSelf); }
    }
}
