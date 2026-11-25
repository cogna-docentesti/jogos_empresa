using System;
using System.Collections.Generic;
using System.Text;
using Game.Adapter.In.UI.Theme;
using Game.Domain.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Adapter.In.UI.Simulation
{
    /// <summary>
    /// Business Model Canvas montado a partir das decisoes (nove blocos no layout classico).
    /// Cada bloco da cena e ligado a um id do BusinessModelCanvasBuilder no Inspector.
    /// </summary>
    public sealed class BusinessModelCanvasView : MonoBehaviour
    {
        [Serializable]
        public sealed class BlockSlot
        {
            [Tooltip("customer_segments, value_propositions, channels, customer_relationships, revenue_streams, key_resources, key_activities, key_partnerships, cost_structure")]
            public string blockId;
            public TMP_Text title;
            public TMP_Text body;
        }

        [SerializeField] private GameObject root;
        [SerializeField] private List<BlockSlot> slots = new List<BlockSlot>();
        [SerializeField] private Button closeButton;

        private bool _wired;

        private GameObject Root => root != null ? root : gameObject;

        public void Show(BusinessModelCanvas canvas)
        {
            if (!_wired)
            {
                _wired = true;
                if (closeButton != null) closeButton.onClick.AddListener(Hide);
            }
            Root.SetActive(true);
            foreach (var slot in slots)
            {
                if (slot == null) continue;
                var block = canvas?.Block(slot.blockId);
                if (slot.title != null) slot.title.text = block != null ? block.order + ". " + block.title : slot.blockId;
                if (slot.body == null) continue;
                if (block == null) { slot.body.text = string.Empty; continue; }
                var text = new StringBuilder();
                foreach (var item in block.items) text.Append("• ").Append(item).Append('\n');
                foreach (var note in block.notes) text.Append("<color=").Append(GamePalette.HexWarn).Append(">! ").Append(note).Append("</color>\n");
                slot.body.text = text.ToString().TrimEnd('\n');
            }
        }

        public void Hide() => Root.SetActive(false);
    }
}
