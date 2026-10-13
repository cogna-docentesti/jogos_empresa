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
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (IsOpen) Close();
                else Open();
            }
        }

        // ─────────────────────────────────────────────────────────────

        public void Open()
        {
            if (dialogRoot == null || _quitting)
                return;

            CurrentMode = EvaluateMode();
            ApplyMode(CurrentMode);
            dialogRoot.SetActive(true);
            dialogRoot.transform.SetAsLastSibling();
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

            switch (mode)
            {
                case ExitMode.BeforeFirstSave:
                    SetBody("Suas escolhas de localização, restaurante e cardápio só são salvas quando você confirma o cardápio.\n"
                          + "Se sair agora, elas serão perdidas.");
                    SetDiscardLabel("SAIR");
                    SetVisible(saveExitButton, false);
                    break;

                case ExitMode.UnsavedChanges:
                    SetBody("Você tem alterações que ainda não foram salvas.\nQuer salvar seu progresso antes de sair?");
                    SetDiscardLabel("SAIR SEM SALVAR");
                    SetVisible(saveExitButton, true);
                    break;

                default:
                    SetBody("Seu progresso está salvo.\nQuando voltar, o jogo continua de onde você parou.");
                    SetDiscardLabel("SAIR");
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
            Close();

            if (GameManager.Instance != null)
                GameManager.Instance.Shutdown(save);

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
