using Game.Adapter.In.UI.Theme;
using Game.Domain.Service;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Game.Adapter.In.UI
{
    /// <summary>
    /// Camada burra da tela "Meu Estabelecimento": so recebe um
    /// EstablishmentSummary pronto e escreve nos TextMeshPro.
    /// Nao calcula nada e nao conhece GameSessionState.
    /// </summary>
    public sealed class EstablishmentSummaryView : MonoBehaviour
    {
        [SerializeField] private EstablishmentReviewContent presentation;
        [Header("Reset do jogo")]
        [SerializeField] private Button resetButton;
        [SerializeField] private GameObject resetWarning;
        [SerializeField] private Button cancelResetButton;
        [SerializeField] private Button confirmResetButton;

        private Transform warningOriginalParent;
        private Transform modalHostParent;
        private GameObject previousFocus;
        private readonly System.Collections.Generic.Dictionary<TextMeshProUGUI, string> warningOriginalTexts
            = new System.Collections.Generic.Dictionary<TextMeshProUGUI, string>();

        private void Awake()
        {
            HideResetWarning();
        }

        private void Update()
        {
            if (resetWarning == null || !resetWarning.activeInHierarchy || EventSystem.current == null) return;
            var focus = EventSystem.current.currentSelectedGameObject;
            if (focus == null || !focus.transform.IsChildOf(resetWarning.transform))
                (cancelResetButton != null && cancelResetButton.gameObject.activeInHierarchy ? cancelResetButton : confirmResetButton)?.Select();
        }

        public void BindReset(UnityAction onConfirmed)
        {
            if (resetButton != null)
            {
                resetButton.onClick.RemoveAllListeners();
                resetButton.onClick.AddListener(ShowResetWarning);
            }

            if (cancelResetButton != null)
            {
                cancelResetButton.onClick.RemoveAllListeners();
                cancelResetButton.onClick.AddListener(HideResetWarning);
            }

            if (confirmResetButton != null)
            {
                confirmResetButton.onClick.RemoveAllListeners();
                if (onConfirmed != null)
                    confirmResetButton.onClick.AddListener(onConfirmed);
            }
        }

        public void HideResetWarning()
        {
            if (resetWarning != null)
            {
                resetWarning.SetActive(false);
                if (modalHostParent != null)
                {
                    resetWarning.transform.SetParent(modalHostParent, false);
                    modalHostParent = null;
                }
                if (EventSystem.current != null && previousFocus != null && previousFocus.activeInHierarchy)
                    EventSystem.current.SetSelectedGameObject(previousFocus);
                previousFocus = null;
                if (warningOriginalParent != null)
                {
                    resetWarning.transform.SetParent(warningOriginalParent, false);
                    warningOriginalParent = null;
                    foreach (var item in warningOriginalTexts)
                        item.Key.text = item.Value;
                    warningOriginalTexts.Clear();
                    cancelResetButton?.gameObject.SetActive(true);
                }
            }
        }

        public void ShowIncompatibleSave(UnityAction onConfirmed)
        {
            if (warningOriginalParent != null)
                return;
            if (resetWarning == null || confirmResetButton == null)
            {
                Debug.LogError("Modal de reinicio nao configurado.");
                return;
            }
            // Reutiliza o modal existente sem abrir o hub ou o resumo da empresa.
            var canvas = GetComponentInParent<Canvas>(true);
            if (canvas == null)
                return;
            warningOriginalParent = resetWarning.transform.parent;
            foreach (var label in resetWarning.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                warningOriginalTexts[label] = label.text;
                if (label.GetComponentInParent<Button>() == null)
                    label.text = label.text.Length > 100
                        ? "Esta sessão foi criada em uma versão anterior do jogo e não é compatível com o novo ciclo trimestral de 3 meses. Para continuar, será necessário iniciar uma nova sessão.\n\nAo confirmar, a sessão anterior e seus dados serão apagados."
                        : "Sessão incompatível";
            }
            BindReset(onConfirmed);
            var confirmLabel = confirmResetButton.GetComponentInChildren<TextMeshProUGUI>(true);
            if (confirmLabel != null)
                confirmLabel.text = "Iniciar nova sessão";
            cancelResetButton?.gameObject.SetActive(false);
            resetWarning.transform.SetParent(canvas.transform, false);
            ShowResetWarning();
        }

        public void HideIncompatibleSave()
        {
            if (warningOriginalParent != null)
                HideResetWarning();
        }

        public void Bind(EstablishmentSummary summary)
        {
            presentation?.Bind(summary ?? EstablishmentSummaryService.BuildDraft());
        }
        private void ShowResetWarning()
        {
            if (resetWarning == null)
                return;

            resetWarning.SetActive(true);
            var canvas = GetComponentInParent<Canvas>(true);
            if (canvas != null && resetWarning.transform.parent != canvas.transform)
            {
                modalHostParent = resetWarning.transform.parent;
                resetWarning.transform.SetParent(canvas.transform, false);
            }
            resetWarning.transform.SetAsLastSibling();
            if (EventSystem.current != null) previousFocus = EventSystem.current.currentSelectedGameObject;
            (cancelResetButton != null && cancelResetButton.gameObject.activeInHierarchy ? cancelResetButton : confirmResetButton)?.Select();
        }
    }
}
