using Game.Domain.Service;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Game.Adapter.In.UI
{
    /// <summary>Presentation and explicit confirmation for destructive reset and incompatible saves.</summary>
    public sealed class EstablishmentSummaryView : MonoBehaviour
    {
        [SerializeField] private EstablishmentReviewContent presentation;
        [SerializeField] private Button resetButton;
        [SerializeField] private GameObject resetWarning;
        [SerializeField] private Button cancelResetButton;
        [SerializeField] private Button confirmResetButton;
        [SerializeField] private TMP_Text resetTitle, resetBody, resetError;
        private bool incompatible;
        private UnityAction resetAction;
        public bool IsResetOpen => resetWarning != null && resetWarning.activeInHierarchy;
        private const string ResetMessage = "As decisões, compras e resultados desta campanha serão apagados permanentemente. O cadastro também será limpo.\n\nVocê voltará à identificação para refazer as escolhas iniciais. Esta ação não pode ser desfeita.";
        private const string IncompatibleMessage = "Esta sessão pertence ao ciclo anterior e não pode ser continuada no jogo de três meses.\n\nReiniciar apaga a sessão anterior, seus resultados e o cadastro. Para manter os dados, escolha Sair sem reiniciar. Nada é apagado ao abrir este aviso.";

        private void Awake() { if (resetWarning != null) resetWarning.SetActive(false); }
        public void BindReset(UnityAction onConfirmed)
        {
            resetAction = onConfirmed;
            Bind(resetButton, ShowResetWarning);
            Bind(confirmResetButton, () => resetAction?.Invoke());
            Bind(cancelResetButton, Cancel);
        }
        private static void Bind(Button button, UnityAction action)
        { if (button == null) return; button.onClick.RemoveAllListeners(); button.onClick.AddListener(action); }
        private void Cancel()
        {
            if (incompatible)
            {
                // Leaving preserves the incompatible row and registration. Only the danger action deletes them.
                FindFirstObjectByType<ExitDialogController>()?.Quit(false);
            }
            else HideResetWarning();
        }
        public void HideResetWarning()
        {
            if (resetWarning == null) return;
            resetWarning.SetActive(false);
            incompatible = false;
            SetCopy();
        }
        public void ShowIncompatibleSave(UnityAction onConfirmed)
        {
            if (IsResetOpen) return;
            incompatible = true; BindReset(onConfirmed); ShowWarning();
        }
        public void HideIncompatibleSave() { if (incompatible) HideResetWarning(); }
        public void ShowResetFailure()
        {
            if (resetError != null)
            {
                resetError.text = "Não foi possível reiniciar. Os dados foram mantidos. Tente novamente ou saia deste aviso.";
                resetError.gameObject.SetActive(true);
            }
            cancelResetButton?.Select();
        }
        public void Bind(EstablishmentSummary summary) => presentation?.Bind(summary ?? EstablishmentSummaryService.BuildDraft());
        private void SetCopy()
        {
            if (resetTitle != null) resetTitle.text = incompatible ? "Sessão incompatível" : "Reiniciar esta campanha?";
            if (resetBody != null) resetBody.text = incompatible ? IncompatibleMessage : ResetMessage;
            if (resetError != null) { resetError.text = ""; resetError.gameObject.SetActive(false); }
            if (cancelResetButton != null) cancelResetButton.GetComponentInChildren<TMP_Text>(true).text = incompatible ? "Sair sem reiniciar" : "Cancelar";
            if (confirmResetButton != null) confirmResetButton.GetComponentInChildren<TMP_Text>(true).text = incompatible ? "Apagar e reiniciar" : "Reiniciar jogo";
        }
        private void ShowResetWarning()
        {
            if (SharedModalFocus.Active != null) return;
            incompatible = false; ShowWarning();
        }
        private void ShowWarning()
        {
            if (resetWarning == null || IsResetOpen) return;
            var canvas = GetComponentInParent<Canvas>(true)?.rootCanvas;
            if (canvas == null) return;
            if (resetWarning.transform.parent != canvas.transform) resetWarning.transform.SetParent(canvas.transform, false);
            resetWarning.transform.SetAsLastSibling();
            SetCopy();
            resetWarning.GetComponent<SharedModalFocus>()?.Configure(cancelResetButton, cancelResetButton, !incompatible);
            resetWarning.SetActive(true);
            cancelResetButton?.Select();
        }
        private void OnDisable()
        {
            // The incompatible warning deliberately lives outside its inactive source panel.
            // Do not reparent while an ancestor is being deactivated. The hosted modal stays reusable.
            if (!incompatible && resetWarning != null) resetWarning.SetActive(false);
        }
    }
}
