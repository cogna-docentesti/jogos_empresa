using UnityEngine;

namespace Game.Adapter.In.UI
{
    /// <summary>Animates one complete Single sprite, without changing its UVs or import settings.</summary>
    public sealed class UiMotion : MonoBehaviour
    {
        public enum Motion { Spinner, Tap, Drag, Focus }
        [SerializeField] private Motion motion;
        private Vector3 origin, scale;
        private Quaternion rotation;
        private float elapsed;
        public void Configure(Motion value) => motion = value;
        private void OnEnable() { origin = transform.localPosition; scale = transform.localScale; rotation = transform.localRotation; elapsed = 0; }
        private void Update()
        {
            elapsed += Time.unscaledDeltaTime;
            float wave = Mathf.Sin(elapsed * 4);
            if (motion == Motion.Spinner) transform.localRotation = rotation * Quaternion.Euler(0, 0, -elapsed * 220);
            else if (motion == Motion.Drag) transform.localPosition = origin + Vector3.up * (wave * 8);
            else transform.localScale = scale * (1 + (motion == Motion.Focus ? .08f : .06f) * wave);
        }
        private void OnDisable() { transform.localPosition = origin; transform.localScale = scale; transform.localRotation = rotation; }
    }
}
