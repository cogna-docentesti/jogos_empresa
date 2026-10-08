using Game.Infrastructure.Session;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.Adapter.In.UI
{
    public sealed class UIIndentification : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TMP_InputField studentNameInput;
        [SerializeField] private TMP_InputField raInput;
        [SerializeField] private Button buttonRegister;
        [SerializeField] private TMP_Text validationText;

        [Header("Minimum Character Counts")]
        [SerializeField, Min(1)] private int studentNameMinLength = 3;
        [SerializeField, Min(1)] private int raMinLength = 5;

        [Header("Maximum Character Counts")]
        [SerializeField, Min(1)] private int studentNameMaxLength = 100;
        [SerializeField, Min(1)] private int raMaxLength = 20;

        [Header("Validation Messages")]
        [Tooltip("Use {min} and {max} to display the character limits configured for each field.")]
        [SerializeField, TextArea] private string studentNameError = "O nome do aluno deve ter pelo menos {min} caracteres.";
        [SerializeField, TextArea] private string raError = "O RA deve ter pelo menos {min} caracteres.";
        [SerializeField, TextArea] private string studentNameMaxError = "O nome do aluno deve ter no máximo {max} caracteres.";
        [SerializeField, TextArea] private string raMaxError = "O RA deve ter no máximo {max} caracteres.";
        [SerializeField, TextArea] private string saveError = "Não foi possível salvar o cadastro. Tente novamente.";

        private bool isRegistering;

        private void OnValidate()
        {
            studentNameMinLength = Mathf.Max(1, studentNameMinLength);
            raMinLength = Mathf.Max(1, raMinLength);
            studentNameMaxLength = Mathf.Max(studentNameMinLength, studentNameMaxLength);
            raMaxLength = Mathf.Max(raMinLength, raMaxLength);
        }

        private void Start()
        {
            // Quando o jogador aperta Voltar na D1, ele quer corrigir o cadastro.
            // Nesse caso a tela mostra os dados preenchidos e espera o clique,
            // em vez de pular sozinha para o jogo.
            bool editRequested = PlayerSession.ConsumeIdentificationEditRequest();

            if (!PlayerSession.TryLoadIdentification())
                return;

            studentNameInput.SetTextWithoutNotify(PlayerSession.StudentName);
            raInput.SetTextWithoutNotify(PlayerSession.StudentRA);
            OnInputChanged(string.Empty);

            if (!editRequested && ValidateForm())
                OpenGameScene();
        }

        private void OnEnable()
        {
            if (studentNameInput == null || raInput == null
                || buttonRegister == null || validationText == null)
            {
                Debug.LogError("[UIIndentification] Assign all input fields, the register button, and the Validation text in the Inspector.", this);
                if (buttonRegister != null)
                    buttonRegister.interactable = false;
                enabled = false;
                return;
            }

            studentNameInput.onValueChanged.AddListener(OnInputChanged);
            raInput.onValueChanged.AddListener(OnInputChanged);
            buttonRegister.onClick.AddListener(Register);
            OnInputChanged(string.Empty);
        }

        private void OnDisable()
        {
            studentNameInput?.onValueChanged.RemoveListener(OnInputChanged);
            raInput?.onValueChanged.RemoveListener(OnInputChanged);
            buttonRegister?.onClick.RemoveListener(Register);
        }

        private void OnInputChanged(string value)
        {
            validationText.text = string.Empty;
            buttonRegister.interactable = !isRegistering
                && !string.IsNullOrWhiteSpace(studentNameInput.text)
                && !string.IsNullOrWhiteSpace(raInput.text);
        }

        public void Register()
        {
            if (!isActiveAndEnabled || isRegistering)
                return;

            if (!ValidateForm())
                return;

            try
            {
                string studentName = studentNameInput.text.Trim();
                string studentRA = raInput.text.Trim();
                string restaurantName = $"{studentRA} {studentName}";
                PlayerSession.SaveIdentification(studentName, studentRA, restaurantName);
            }
            catch (PlayerPrefsException exception)
            {
                Debug.LogError("[UIIndentification] Failed to save registration: " + exception.Message, this);
                validationText.text = saveError;
                return;
            }

            OpenGameScene();
        }

        private void OpenGameScene()
        {
            isRegistering = true;
            buttonRegister.interactable = false;
            validationText.text = string.Empty;

            SceneManager.LoadScene(Game.Infrastructure.SceneNames.Game);
        }

        private bool ValidateForm()
        {
            return ValidateInput(studentNameInput, studentNameMinLength, studentNameMaxLength, studentNameError, studentNameMaxError)
                && ValidateInput(raInput, raMinLength, raMaxLength, raError, raMaxError);
        }

        private bool ValidateInput(TMP_InputField input, int minLength, int maxLength, string minError, string maxError)
        {
            int minimum = Mathf.Max(1, minLength);
            int maximum = Mathf.Max(minimum, maxLength);
            int length = (input.text ?? string.Empty).Trim().Length;
            if (length >= minimum && length <= maximum)
                return true;

            string errorMessage = length < minimum ? minError : maxError;
            validationText.text = (errorMessage ?? string.Empty)
                .Replace("{min}", minimum.ToString())
                .Replace("{max}", maximum.ToString());
            input.Select();
            input.ActivateInputField();
            return false;
        }
    }
}
