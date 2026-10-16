using System;
using UnityEngine;

namespace Game.Adapter.In.UI.Theme
{
    /// <summary>Approved, individually addressable artwork. Populated from the art manifest in the Editor.</summary>
    [CreateAssetMenu(menuName = "Cogna/Shared UI skin")]
    public sealed class SharedUiSkin : ScriptableObject
    {
        [Serializable] public struct Entry { public string id; public Sprite sprite; }
        public Entry[] entries = Array.Empty<Entry>();
        public static SharedUiSkin Current => Resources.Load<SharedUiSkin>("SharedUiSkin");
        public Sprite Get(string id)
        {
            foreach (var entry in entries) if (entry.id == id) return entry.sprite;
            return null;
        }
    }
}
