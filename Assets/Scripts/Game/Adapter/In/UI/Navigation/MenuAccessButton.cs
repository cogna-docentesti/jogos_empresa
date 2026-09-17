using UnityEngine;

namespace Game.Adapter.In.UI.Navigation
{
    /// <summary>
    /// Botao flutuante "Menu do Jogo", que vive no Canvas como irmao de todos
    /// os paineis - e nao dentro de nenhum deles.
    ///
    /// Por que assim: as telas antigas (Panel_Location, Panel_Restaurant,
    /// Panel_MenuPricing, Panel_Financial) tem barras superiores proprias e
    /// diferentes entre si. Enfiar um botao em cada uma significaria editar
    /// quatro layouts que ja funcionam. Um botao unico por cima de tudo
    /// resolve para a tela de hoje e para qualquer tela futura, de graca.
    ///
    /// Este componente fica SEMPRE ativo; quem liga e desliga e o filho
    /// buttonRoot. Se ele se desativasse, pararia de receber Update e nunca
    /// mais voltaria.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MenuAccessButton : MonoBehaviour
    {
        [Tooltip("O objeto do botao em si. E ele que liga e desliga.")]
        [SerializeField] private GameObject buttonRoot;

        [Tooltip("Referencia ao Panel_Menu. Estando ele visivel, o botao se esconde.")]
        [SerializeField] private GameObject menuPanel;

        [Tooltip("Mostrar tambem durante as decisoes iniciais (Localizacao, Restaurante, Cardapio, Revisao). "
               + "Desligado: antes de confirmar a configuracao quase nada do hub tem conteudo, "
               + "e o jogador foca em decidir.")]
        [SerializeField] private bool visibleDuringInitialSetup = false;

        private bool _lastVisible = true;

        private void OnEnable()
        {
            Apply(Evaluate(), force: true);
        }

        private void Update()
        {
            Apply(Evaluate(), force: false);
        }

        private bool Evaluate()
        {
            // 1. Em cima do proprio mapa o botao nao faz sentido.
            if (menuPanel != null && menuPanel.activeInHierarchy)
                return false;

            var navigator = MenuNavigator.Instance;

            if (navigator != null && navigator.Current == PanelId.Menu)
                return false;

            // 2. Antes de existir uma empresa, nao ha hub para visitar.
            var stateMachine = GameManager.Instance != null ? GameManager.Instance.StateMachine : null;

            if (stateMachine == null)
                return true;

            switch (stateMachine.CurrentState)
            {
                case GameState.Bootstrap:
                case GameState.MainMenu:
                case GameState.FinalReport:
                case GameState.GameOver_Bankruptcy:
                    return false;

                case GameState.Config_Location:
                case GameState.Config_Restaurant:
                case GameState.Config_TargetSegment:
                case GameState.Config_Review:
                    return visibleDuringInitialSetup;

                default:
                    return true;
            }
        }

        private void Apply(bool visible, bool force)
        {
            if (!force && visible == _lastVisible)
                return;

            _lastVisible = visible;

            if (buttonRoot != null)
                buttonRoot.SetActive(visible);
        }
    }
}
