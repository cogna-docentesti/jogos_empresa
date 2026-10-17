using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Adapter.In.UI
{
    /// <summary>
    /// E-05: botao Sair + dialogo "salvar antes de sair?".
    ///
    /// Por que a pergunta acontece aqui e nao no OnApplicationQuit: quando a
    /// Unity chama o OnApplicationQuit ela ja esta desmontando o app. Nao existe
    /// proximo frame para desenhar um dialogo nem para esperar um clique.
    /// Entao a pergunta precisa vir ANTES, num botao Sair dentro do jogo.
    ///
    /// O texto e os botoes do dialogo dependem do momento do jogo:
    ///  - Antes do confirm da D3: nada foi gravado ainda (regra da E-03). O
    ///    dialogo avisa que as decisoes iniciais serao perdidas. Nao ha
    ///    "Salvar e sair", porque salvar antes da D3 nao e permitido.
    ///  - Depois da D3, com alteracoes pendentes: Cancelar / Sair sem salvar /
    ///    Salvar e sair.
    ///  - Depois da D3, tudo gravado: Cancelar / Sair.
    ///
    /// Esc no teclado (e o botao Voltar do Android, que a Unity entrega como Esc)
    /// abre e fecha o dialogo.
    ///
    /// Este componente fica num objeto sempre ativo (ExitUI). Quem liga e
    /// desliga e o filho dialogRoot.
    ///
    /// O botao Sair (ExitUI/ExitButton) fica embaixo, no centro da tela: nas
    /// telas D1 a D3 o VOLTAR ocupa o canto esquerdo e o CONTINUAR o direito,
    /// e o MENU fica no canto inferior direito. Este script nao mexe na posicao
    /// do botao: o que estiver no RectTransform da cena e o que vale.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ExitDialogController : MonoBehaviour
    {
        public enum ExitMode
        {
            /// <summary>Antes do confirm da D3: nada a salvar.</summary>
            BeforeFirstSave,
            /// <summary>Partida gravada e com alteracoes que ainda nao foram para o banco.</summary>
            UnsavedChanges,
            /// <summary>Partida gravada e sem nada pendente.</summary>
            AllSaved
        }

        [Header("Botao que abre o dialogo")]
        [SerializeField] private Button openButton;

        [Header("Dialogo")]
        [SerializeField] private GameObject dialogRoot;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI bodyText;
        [SerializeField] private Button cancelButton;
        [SerializeField] private Button discardButton;
        [SerializeField] private TextMeshProUGUI discardLabel;
        [SerializeField] private Button saveExitButton;

        public ExitMode CurrentMode { get; private set; }

        public bool IsOpen => dialogRoot != null && dialogRoot.activeSelf;

        private bool _quitting;

        private void Awake()
        {
            if (openButton != null)
                openButton.onClick.AddListener(Open);

            cancelButton?.onClick.AddListener(Close);
            discardButton?.onClick.AddListener(() => Quit(save: false));
            saveExitButton?.onClick.AddListener(() => Quit(save: true));

            if (dialogRoot != null)
                dialogRoot.SetActive(false);
        }

        private void Update()
        {
            bool escape = UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame;
#if ENABLE_LEGACY_INPUT_MANAGER
            escape |= Input.GetKeyDown(KeyCode.Escape);
#endif
            if (escape)
            {
                // Both update orders preserve the active modal's ownership of Escape.
                if (SharedModalFocus.LastCancelFrame == Time.frameCount || SharedModalFocus.Active != null) return;
                if (IsOpen) Close();
                else Open();
            }
        }

        // ─────────────────────────────────────────────────────────────

        public void Open()
        {
            if (dialogRoot == null || _quitting || IsOpen || SharedModalFocus.Active != null)
                return;

            CurrentMode = EvaluateMode();
            ApplyMode(CurrentMode);
            dialogRoot.GetComponent<SharedModalFocus>()?.Configure(cancelButton, cancelButton);
            dialogRoot.SetActive(true);
            dialogRoot.transform.SetAsLastSibling();
            cancelButton?.Select();
        }

        public void Close()
        {
            if (dialogRoot != null)
                dialogRoot.SetActive(false);
        }

        public static ExitMode EvaluateMode()
        {
            if (!GameSessionState.HasSession || !GameSessionState.IsPersisted)
                return ExitMode.BeforeFirstSave;

            return GameSessionState.HasUnsavedChanges ? ExitMode.UnsavedChanges : ExitMode.AllSaved;
        }

        private void ApplyMode(ExitMode mode)
        {
            if (titleText != null)
                titleText.text = "Sair do jogo";
            if (bodyText != null) bodyText.color = Theme.GamePalette.Ink;
            if (saveExitButton != null) saveExitButton.GetComponentInChildren<TMP_Text>(true).text = "Salvar e sair";

            switch (mode)
            {
                case ExitMode.BeforeFirstSave:
                    SetBody(GameSessionState.HasIncompatibleSave
                        ? "A sessão anterior será mantida. Você poderá decidir sobre o reinício quando abrir o jogo novamente."
                        : "As escolhas de localização, restaurante e cardápio ainda estão em rascunho. Elas só são salvas ao confirmar o cardápio.\n\nSe sair agora, essas escolhas serão perdidas. O cadastro já confirmado será mantido.");
                    SetDiscardLabel("Sair sem salvar");
                    SetVisible(saveExitButton, false);
                    break;

                case ExitMode.UnsavedChanges:
                    SetBody("Você tem alterações que ainda não foram salvas.\nQuer salvar seu progresso antes de sair?");
                    SetDiscardLabel("Sair sem salvar");
                    SetVisible(saveExitButton, true);
                    break;

                default:
                    SetBody(GameSessionState.HasActiveSession
                        ? "Seu progresso está salvo.\nQuando voltar, o jogo continua de onde você parou."
                        : "Sua campanha encerrada e os resultados estão salvos. Você poderá consultá-los quando voltar ao jogo.");
                    SetDiscardLabel("Sair");
                    SetVisible(saveExitButton, false);
                    break;
            }
        }

        /// <summary>Fecha o jogo. save = true grava antes; false descarta o que estiver pendente.</summary>
        public void Quit(bool save)
        {
            if (_quitting)
                return;

            _quitting = true;
            if (GameManager.Instance == null || !GameManager.Instance.TryShutdown(save, out _))
            {
                _quitting = false;
                if (titleText != null) titleText.text = save ? "Não foi possível salvar" : "Não foi possível sair";
                SetBody("O jogo continua aberto e seu progresso permanece na memória.\n\nTente novamente ou cancele para continuar jogando. Se escolher Sair sem salvar, as alterações pendentes serão perdidas.");
                if (bodyText != null) bodyText.color = Theme.GamePalette.ErrorText;
                if (saveExitButton != null && saveExitButton.gameObject.activeSelf)
                    saveExitButton.GetComponentInChildren<TMP_Text>(true).text = "Tentar salvar e sair";
                cancelButton?.Select();
                return;
            }
            Close();

#if UNITY_EDITOR
            // Application.Quit nao faz nada dentro do editor: aqui o equivalente
            // e parar o Play Mode.
            UnityEditor.EditorApplication.isPlaying = false;
#else
            UnityEngine.Application.Quit();
#endif
        }

        // ─────────────────────────────────────────────────────────────

        private void SetBody(string text)
        {
            if (bodyText != null)
                bodyText.text = text;
        }

        private void SetDiscardLabel(string text)
        {
            if (discardLabel != null)
                discardLabel.text = text;
        }

        private static void SetVisible(Component component, bool visible)
        {
            if (component != null)
                component.gameObject.SetActive(visible);
        }

        /// <summary>Usado pelo builder de Editor para ligar as referencias.</summary>
        public void EditorConfigure(Button open,
            GameObject root, TextMeshProUGUI title, TextMeshProUGUI body,
            Button cancel, Button discard, TextMeshProUGUI discardText, Button saveExit)
        {
            openButton = open;
            dialogRoot = root;
            titleText = title;
            bodyText = body;
            cancelButton = cancel;
            discardButton = discard;
            discardLabel = discardText;
            saveExitButton = saveExit;
        }
    }
}
