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
        [SerializeField] private TMP_InputField restaurantNameInput;
        [SerializeField] private Button buttonRegister;
        [SerializeField] private TMP_Text validationText;

        [Header("Minimum Character Counts")]
        [SerializeField, Min(1)] private int studentNameMinLength = 3;
        [SerializeField, Min(1)] private int raMinLength = 5;
        [SerializeField, Min(1)] private int restaurantNameMinLength = 3;

        [Header("Maximum Character Counts")]
        [SerializeField, Min(1)] private int studentNameMaxLength = 100;
        [SerializeField, Min(1)] private int raMaxLength = 20;
        [SerializeField, Min(1)] private int restaurantNameMaxLength = 60;

        [Header("Validation Messages")]
        [Tooltip("Use {min} and {max} to display the character limits configured for each field.")]
        [SerializeField, TextArea] private string studentNameError = "O nome do aluno deve ter pelo menos {min} caracteres.";
        [SerializeField, TextArea] private string raError = "O RA deve ter pelo menos {min} caracteres.";
        [SerializeField, TextArea] private string restaurantNameError = "O nome do restaurante deve ter pelo menos {min} caracteres.";
        [SerializeField, TextArea] private string studentNameMaxError = "O nome do aluno deve ter no máximo {max} caracteres.";
        [SerializeField, TextArea] private string raMaxError = "O RA deve ter no máximo {max} caracteres.";
        [SerializeField, TextArea] private string restaurantNameMaxError = "O nome do restaurante deve ter no máximo {max} caracteres.";
        [SerializeField, TextArea] private string saveError = "Não foi possível salvar o cadastro. Tente novamente.";

        private bool isRegistering;

        private void OnValidate()
        {
            studentNameMinLength = Mathf.Max(1, studentNameMinLength);
            raMinLength = Mathf.Max(1, raMinLength);
            restaurantNameMinLength = Mathf.Max(1, restaurantNameMinLength);
            studentNameMaxLength = Mathf.Max(studentNameMinLength, studentNameMaxLength);
            raMaxLength = Mathf.Max(raMinLength, raMaxLength);
            restaurantNameMaxLength = Mathf.Max(restaurantNameMinLength, restaurantNameMaxLength);
        }

        private void Start()
        {
            if (!PlayerSession.TryLoadIdentification())
                return;

            studentNameInput.SetTextWithoutNotify(PlayerSession.StudentName);
            raInput.SetTextWithoutNotify(PlayerSession.StudentRA);
            restaurantNameInput.SetTextWithoutNotify(PlayerSession.RestaurantName);
            OnInputChanged(string.Empty);

            if (ValidateForm())
                OpenGameScene();
        }

        private void OnEnable()
        {
            if (studentNameInput == null || raInput == null || restaurantNameInput == null
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
            restaurantNameInput.onValueChanged.AddListener(OnInputChanged);
            buttonRegister.onClick.AddListener(Register);
            OnInputChanged(string.Empty);
        }

        private void OnDisable()
        {
            studentNameInput?.onValueChanged.RemoveListener(OnInputChanged);
            raInput?.onValueChanged.RemoveListener(OnInputChanged);
            restaurantNameInput?.onValueChanged.RemoveListener(OnInputChanged);
            buttonRegister?.onClick.RemoveListener(Register);
        }

        private void OnInputChanged(string value)
        {
            validationText.text = string.Empty;
            buttonRegister.interactable = !isRegistering
                && !string.IsNullOrWhiteSpace(studentNameInput.text)
                && !string.IsNullOrWhiteSpace(raInput.text)
                && !string.IsNullOrWhiteSpace(restaurantNameInput.text);
        }

        public void Register()
        {
            if (!isActiveAndEnabled || isRegistering)
                return;

            if (!ValidateForm())
                return;

            try
            {
                PlayerSession.SaveIdentification(studentNameInput.text, raInput.text, restaurantNameInput.text);
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

            SceneManager.LoadScene("GameScene");
        }

        private bool ValidateForm()
        {
            return ValidateInput(studentNameInput, studentNameMinLength, studentNameMaxLength, studentNameError, studentNameMaxError)
                && ValidateInput(raInput, raMinLength, raMaxLength, raError, raMaxError)
                && ValidateInput(restaurantNameInput, restaurantNameMinLength, restaurantNameMaxLength, restaurantNameError, restaurantNameMaxError);
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
