using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Adapter.In.UI.Navigation
{
    /// <summary>
    /// Navegacao entre os paineis revisitaveis do hub, com historico.
    /// Toda tela aberta empilha a anterior, entao o botao Voltar sempre
    /// devolve o jogador exatamente para onde ele estava - inclusive se ele
    /// quiser mudar a escolha que acabou de fazer.
    ///
    /// Esta classe NAO fala com GameState nem com o banco. Ela so liga e
    /// desliga GameObjects registrados no PanelRegistry. Isso mantem a
    /// navegacao isolada do fluxo de decisoes (UIStateListener) e da
    /// persistencia (GameSessionState), que continuam intactos.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MenuNavigator : MonoBehaviour
    {
        public static MenuNavigator Instance { get; private set; }

        [SerializeField] private PanelRegistry registry;

        [Tooltip("Painel exibido quando a navegacao inicia e para onde o Voltar cai quando o historico acaba.")]
        [SerializeField] private PanelId rootPanel = PanelId.Menu;

        [Tooltip("Abrir o rootPanel automaticamente no Start. Deixe DESLIGADO enquanto o UIStateListener "
               + "for quem mostra o Panel_Menu no estado Management_Hub - senao os dois disputam a tela. "
               + "Ligue apenas para testar o mapa isolado.")]
        [SerializeField] private bool openRootOnStart = false;

        [Tooltip("Profundidade maxima do historico. Evita crescer sem limite em sessoes longas.")]
        [SerializeField] private int maxHistory = 32;

        private readonly List<PanelId> _history = new List<PanelId>();

        public PanelId Current { get; private set; } = PanelId.None;

        public bool CanGoBack => _history.Count > 0;

        /// <summary>Disparado sempre que a tela visivel muda. (anterior, atual)</summary>
        public event Action<PanelId, PanelId> OnPanelChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[MenuNavigator] Ja existe uma instancia na cena. Este componente sera ignorado.");
                return;
            }

            Instance = this;

            if (registry == null)
                registry = GetComponent<PanelRegistry>();

            if (registry == null)
                Debug.LogError("[MenuNavigator] PanelRegistry nao foi configurado.");
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void Start()
        {
            if (openRootOnStart)
                OpenRoot();
        }

        // =====================================================
        //  API PUBLICA
        // =====================================================

        /// <summary>Abre um painel empilhando o atual no historico.</summary>
        public void Open(PanelId id)
        {
            if (id == PanelId.None || registry == null)
                return;

            if (id == Current)
                return;

            if (!registry.IsManaged(id))
            {
                Debug.LogWarning($"[MenuNavigator] PanelId {id} nao esta no PanelRegistry. Nada foi aberto.");
                return;
            }

            if (Current != PanelId.None)
                Push(Current);

            Show(id);
        }

        /// <summary>Abre um painel LIMPANDO o historico. Usado pelo hub (mapa).</summary>
        public void OpenAsRoot(PanelId id)
        {
            if (id == PanelId.None || registry == null || !registry.IsManaged(id))
                return;

            _history.Clear();
            Show(id);
        }

        /// <summary>Volta para a tela anterior. Se nao houver, cai no rootPanel.</summary>
        public void Back()
        {
            if (_history.Count == 0)
            {
                OpenRoot();
                return;
            }

            var previous = _history[_history.Count - 1];
            _history.RemoveAt(_history.Count - 1);

            Show(previous);
        }

        /// <summary>Volta direto para o mapa do Menu, descartando o historico.</summary>
        public void OpenRoot()
        {
            OpenAsRoot(rootPanel);
        }

        /// <summary>Esquece o historico sem trocar de tela.</summary>
        public void ClearHistory()
        {
            _history.Clear();
        }

        /// <summary>
        /// Encerra a navegacao do hub e esconde seus paineis. Usado quando o
        /// fluxo volta a ser controlado pela state machine.
        /// </summary>
        public void CloseAll()
        {
            _history.Clear();

            var previous = Current;
            Current = PanelId.None;

            if (registry != null)
                registry.HideAll();

            if (previous != PanelId.None)
                OnPanelChanged?.Invoke(previous, Current);
        }

        // =====================================================
        //  INTERNO
        // =====================================================

        private void Push(PanelId id)
        {
            _history.Add(id);

            while (_history.Count > maxHistory)
                _history.RemoveAt(0);
        }

        private void Show(PanelId id)
        {
            var target = registry.Resolve(id);

            if (target == null)
            {
                Debug.LogError($"[MenuNavigator] O painel {id} esta registrado mas o GameObject e nulo.");
                return;
            }

            var previous = Current;
            Current = id;

            registry.HideAll();
            target.SetActive(true);

            OnPanelChanged?.Invoke(previous, Current);
        }
    }
}
