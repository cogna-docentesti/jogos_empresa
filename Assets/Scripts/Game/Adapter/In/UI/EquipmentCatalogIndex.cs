using System;
using UnityEngine;

namespace Game.Adapter.In.UI
{
    /// <summary>Resource addresses only. EquipmentData remains the source of IDs, prices and eligibility.</summary>
    public sealed class EquipmentCatalogIndex : ScriptableObject
    {
        public string[] resourcePaths = Array.Empty<string>();
    }
}
