using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Adapter.In.UI.Navigation
{
    /// <summary>
    /// Tabela PanelId -> GameObject.
    /// Fica em um unico objeto da cena (Canvas > UiNavigation) e e a UNICA
    /// coisa que precisa ser religada se voce renomear ou mover um painel.
    ///
    /// Importante: o registry so conhece os paineis listados aqui. Nada fora
    /// desta lista e ligado ou desligado pela navegacao, entao o UIStateListener
    /// continua dono do fluxo por GameState sem conflito.
    /// </summary>
    public sealed class PanelRegistry : MonoBehaviour
    {
        [Serializable]
        public sealed class Binding
        {
            public PanelId id = PanelId.None;
            public GameObject root;
        }

        [SerializeField] private List<Binding> bindings = new List<Binding>();

        private readonly Dictionary<PanelId, GameObject> _lookup = new Dictionary<PanelId, GameObject>();
        private bool _built;

        public IReadOnlyList<Binding> Bindings => bindings;

        private void Awake()
        {
            Build();
        }

        private void Build()
        {
            if (_built) return;

            _lookup.Clear();

            foreach (var b in bindings)
            {
                if (b == null || b.id == PanelId.None || b.root == null)
                    continue;

                if (_lookup.ContainsKey(b.id))
                {
                    Debug.LogWarning($"[PanelRegistry] PanelId duplicado: {b.id}. O primeiro sera usado.");
                    continue;
                }

                _lookup.Add(b.id, b.root);
            }

            _built = true;
        }

        public GameObject Resolve(PanelId id)
        {
            Build();
            return _lookup.TryGetValue(id, out var go) ? go : null;
        }

        public bool IsManaged(PanelId id)
        {
            Build();
            return _lookup.ContainsKey(id);
        }

        /// <summary>Desliga todos os paineis conhecidos pelo registry.</summary>
        public void HideAll()
        {
            Build();

            foreach (var pair in _lookup)
            {
                if (pair.Value != null)
                    pair.Value.SetActive(false);
            }
        }

        /// <summary>Usado pelo builder de Editor para popular a lista sem duplicar.</summary>
        public void EditorSetBinding(PanelId id, GameObject root)
        {
            if (id == PanelId.None) return;

            foreach (var b in bindings)
            {
                if (b != null && b.id == id)
                {
                    b.root = root;
                    return;
                }
            }

            bindings.Add(new Binding { id = id, root = root });
        }
    }
}
