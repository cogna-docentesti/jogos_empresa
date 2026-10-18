using UnityEngine;

namespace Game.Adapter.In.UI
{
    /// <summary>Local sample state; deliberately separate from catalog IDs and game services.</summary>
    public sealed class SharedShowcaseSelection : MonoBehaviour
    {
        private void Awake() => GetComponent<UnityEngine.UI.Button>().onClick.AddListener(()=>{
            var appearance=GetComponent<SharedControlAppearance>(); appearance.SetSelected(!appearance.IsSelected);
        });
    }
}
