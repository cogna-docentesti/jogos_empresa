using UnityEngine;
using UnityEngine.UI;

namespace Game.Adapter.In.UI.Navigation
{
    /// <summary>
    /// Cola um Button na navegacao de forma declarativa, sem UnityEvent no Inspector.
    /// Basta escolher a Acao e (quando for Open) o Painel de destino.
    ///
    /// Vantagem sobre arrastar o OnClick na mao: o vinculo e serializado como
    /// enum, entao nunca quebra ao renomear GameObject, ao recriar a cena pelo
    /// builder ou ao mover o script de pasta.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public sealed class PanelNavButton : MonoBehaviour
    {
        public enum NavAction
        {
            /// <summary>Abre o painel alvo empilhando o atual (permite Voltar).</summary>
            Open = 0,

            /// <summary>Volta para a tela anterior do historico.</summary>
            Back = 1,

            /// <summary>Volta para o mapa do Menu, limpando o historico.</summary>
            OpenMenu = 2
        }

        [SerializeField] private NavAction action = NavAction.Open;
        [SerializeField] private PanelId target = PanelId.None;

        private Button _button;

        private void Awake()
        {
            _button = GetComponent<Button>();
            _button.onClick.AddListener(Execute);
        }

        private void OnDestroy()
        {
            if (_button != null)
                _button.onClick.RemoveListener(Execute);
        }

        private void Execute()
        {
            var nav = MenuNavigator.Instance;

            if (nav == null)
            {
                Debug.LogWarning("[PanelNavButton] Nenhum MenuNavigator ativo na cena.");
                return;
            }

            switch (action)
            {
                case NavAction.Open:
                    nav.Open(target);
                    break;

                case NavAction.Back:
                    nav.Back();
                    break;

                case NavAction.OpenMenu:
                    nav.OpenRoot();
                    break;
            }
        }

        /// <summary>Usado pelo builder de Editor.</summary>
        public void EditorConfigure(NavAction navAction, PanelId navTarget)
        {
            action = navAction;
            target = navTarget;
        }
    }
}
