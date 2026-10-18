using UnityEngine;

namespace Game.Adapter.In.UI
{
    /// <summary>Uniformly fits the approved 1920x1080 composition without cropping the form.</summary>
    [ExecuteAlways]
    public sealed class IdentificationDesignScale : MonoBehaviour
    {
        private void OnEnable() => Fit();
        private void LateUpdate() => Fit();
        private void OnRectTransformDimensionsChange() => Fit();
        private void Fit()
        {
            if (transform.parent is not RectTransform parent) return;
            float scale = Mathf.Min(parent.rect.width / 1920f, parent.rect.height / 1080f);
            if (scale > 0) transform.localScale = Vector3.one * scale;
        }
    }
}
