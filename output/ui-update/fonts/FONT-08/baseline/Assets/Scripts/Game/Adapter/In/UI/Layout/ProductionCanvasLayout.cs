using System.Collections.Generic;
using UnityEngine;

namespace Game.Adapter.In.UI.Layout
{
    /// <summary>One uniformly scaled 1920x1080 coordinate frame for production panels and global controls.</summary>
    [DefaultExecutionOrder(-10000)]
    [RequireComponent(typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler))]
    public sealed class ProductionCanvasLayout : MonoBehaviour
    {
        private struct Item { public RectTransform rect; public Vector2 size, position; public Vector3 scale; }
        private readonly List<Item> items = new();
        private RectTransform root;
        private Rect lastSafe;
        private Vector2 lastSize;
        private static readonly Vector2 Design = new(1920, 1080);
        private void Awake()
        {
            root = (RectTransform)transform;
            var scaler = GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.Expand;
            foreach (Transform child in transform)
            {
                if (child is not RectTransform r) continue;
                // Interpret the existing serialized anchors in their authored design frame,
                // before the physical aspect ratio can stretch individual global controls.
                Vector2 min = -Design * .5f + Vector2.Scale(r.anchorMin, Design) + r.offsetMin;
                Vector2 max = -Design * .5f + Vector2.Scale(r.anchorMax, Design) + r.offsetMax;
                items.Add(new Item { rect = r, size = max-min, position = min + Vector2.Scale(max-min,r.pivot), scale = r.localScale });
                var safe = r.GetComponent<SafeAreaFitter>();
                if (safe != null) safe.enabled = false; // The shared frame applies the safe area once.
            }
            Apply(Screen.safeArea);
        }
        private void LateUpdate() => Apply(Screen.safeArea);
        public void Apply(Rect safeArea)
        {
            if (root == null || Screen.width <= 0 || Screen.height <= 0) return;
            if (lastSafe == safeArea && lastSize == root.rect.size) return;
            lastSafe = safeArea; lastSize = root.rect.size;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, safeArea.min, null, out var min);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, safeArea.max, null, out var max);
            var available = max-min;
            float scale = Mathf.Min(available.x/Design.x, available.y/Design.y);
            var center = (min+max)*.5f;
            foreach (var item in items)
            {
                if (item.rect == null) continue;
                item.rect.anchorMin = item.rect.anchorMax = new Vector2(.5f,.5f);
                item.rect.sizeDelta = item.size;
                item.rect.anchoredPosition = center + item.position*scale;
                item.rect.localScale = item.scale*scale;
            }
        }
    }
}
