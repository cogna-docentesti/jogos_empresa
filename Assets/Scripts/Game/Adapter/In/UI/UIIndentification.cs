using Game.Infrastructure.Session;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
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
        [SerializeField, Min(1)] private int studentNameMinLength = PlayerSession.NameMinLength;
        [SerializeField, Min(1)] private int raMinLength = PlayerSession.RAMinLength;

        [Header("Maximum Character Counts")]
        [SerializeField, Min(1)] private int studentNameMaxLength = PlayerSession.NameMaxLength;
        [SerializeField, Min(1)] private int raMaxLength = PlayerSession.RAMaxLength;

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

            isRegistering = false;
            PlayerSession.ConsumeIdentificationEditRequest();
            PlayerSession.TryLoadIdentification();
            studentNameInput.SetTextWithoutNotify(PlayerSession.StudentName ?? string.Empty);
            raInput.SetTextWithoutNotify(PlayerSession.StudentRA ?? string.Empty);
            studentNameInput.onValueChanged.AddListener(OnInputChanged);
            raInput.onValueChanged.AddListener(OnInputChanged);
            buttonRegister.onClick.AddListener(Register);
            studentNameInput.onSubmit.AddListener(SelectRA);
            raInput.onSubmit.AddListener(SubmitRA);
            OnInputChanged(string.Empty);
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            bool tab = keyboard != null && keyboard.tabKey.wasPressedThisFrame;
            bool backwards = keyboard != null && (keyboard.shiftKey.isPressed || keyboard.shiftKey.wasPressedThisFrame);
#if ENABLE_LEGACY_INPUT_MANAGER
            // TMP still consumes native keyboard events through the legacy backend.
            // Keep Tab working for native/injected input when both backends are enabled.
            tab |= UnityEngine.Input.GetKeyDown(KeyCode.Tab);
            backwards |= UnityEngine.Input.GetKey(KeyCode.LeftShift) || UnityEngine.Input.GetKey(KeyCode.RightShift);
            backwards |= UnityEngine.Input.GetKeyDown(KeyCode.LeftShift) || UnityEngine.Input.GetKeyDown(KeyCode.RightShift);
#endif
            if (!tab) return;
            if (FindFirstObjectByType<SharedComponentsShowcase>() != null || FindFirstObjectByType<SharedModalFocus>() != null) return;
            var selected = UnityEngine.EventSystems.EventSystem.current?.currentSelectedGameObject;
            UnityEngine.UI.Selectable target;
            if (selected == studentNameInput.gameObject)
                target = backwards ? (UnityEngine.UI.Selectable)buttonRegister : raInput;
            else if (selected == raInput.gameObject)
                target = backwards ? (UnityEngine.UI.Selectable)studentNameInput : buttonRegister;
            else
                target = backwards ? raInput : studentNameInput;
            target.Select();
            if (target is TMP_InputField input) input.ActivateInputField();
        }

        private void SelectRA(string _) { raInput.Select(); raInput.ActivateInputField(); }
        private void SubmitRA(string _) => Register();

        private void OnDisable()
        {
            studentNameInput?.onValueChanged.RemoveListener(OnInputChanged);
            raInput?.onValueChanged.RemoveListener(OnInputChanged);
            buttonRegister?.onClick.RemoveListener(Register);
            studentNameInput?.onSubmit.RemoveListener(SelectRA);
            raInput?.onSubmit.RemoveListener(SubmitRA);
        }

        private void OnInputChanged(string value)
        {
            validationText.text = string.Empty;
            SetInvalid(studentNameInput, false);
            SetInvalid(raInput, false);
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

            // Services and scene already exist. Only the visible state changes.
            if (GameManager.Instance != null && GameManager.Instance.ConfirmIdentification())
                return;

            isRegistering = false;
            validationText.text = "Não foi possível continuar. Abra o jogo pela GameScene.";
            buttonRegister.interactable = true;
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
            SetInvalid(input, true);
            input.Select();
            input.ActivateInputField();
            return false;
        }

        private static void SetInvalid(TMP_InputField input, bool invalid)
        {
            if (input != null && input.TryGetComponent<IdentificationInputAppearance>(out var appearance))
                appearance.IsInvalid = invalid;
        }
    }
}
