using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Adapter.In.UI
{
    public sealed class RestaurantScreenView : MonoBehaviour
    {
        // ── Header ──────────────────────────────────────────────────────
        [Header("Header")]
        [SerializeField] private TextMeshProUGUI hintText;

        // ── Cards de restaurante ────────────────────────────────────────
        [Header("Restaurant Cards")]
        [SerializeField] private Button cardPodrao;
        [SerializeField] private Button cardJapones;
        [SerializeField] private Button cardFrances;

        // ── Botões de segmento ──────────────────────────────────────────
        [Header("Segment Buttons")]
        [SerializeField] private Button btnLow;
        [SerializeField] private Button btnMedium;
        [SerializeField] private Button btnHigh;

        // ── Painel de coerência ─────────────────────────────────────────
        [Header("Coherence Panel")]
        [SerializeField] private Image            coh1BarFill;
        [SerializeField] private TextMeshProUGUI coh1Tip;
        [SerializeField] private Image            coh2BarFill;
        [SerializeField] private TextMeshProUGUI coh2Tip;

        // ── Botões de ação ──────────────────────────────────────────────
        [Header("Action Buttons")]
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button backButton;

        // ── Cores de estado dos cards ───────────────────────────────────
        private static readonly Color CardNormal   = new Color(0.1f, 0.165f, 0.243f);
        private static readonly Color CardSelected = new Color(0.09f, 0.18f, 0.36f);
        private static readonly Color BorderNormal  = new Color(0.118f, 0.227f, 0.373f);
        private static readonly Color BorderSelected = new Color(0.145f, 0.565f, 0.922f);
        private static readonly Color SegBtnNormal   = new Color(0.051f, 0.106f, 0.176f);
        private static readonly Color SegBtnSelected = new Color(0.09f, 0.145f, 0.329f);

        // ── Hint ─────────────────────────────────────────────────────────
        public void SetHint(string v) => hintText.text = v;

        // ── Bind de ações ────────────────────────────────────────────────
        public void BindCardPodrao(UnityEngine.Events.UnityAction a)   => cardPodrao.onClick.AddListener(a);
        public void BindCardJapones(UnityEngine.Events.UnityAction a)  => cardJapones.onClick.AddListener(a);
        public void BindCardFrances(UnityEngine.Events.UnityAction a)  => cardFrances.onClick.AddListener(a);
        public void BindBtnLow(UnityEngine.Events.UnityAction a)       => btnLow.onClick.AddListener(a);
        public void BindBtnMedium(UnityEngine.Events.UnityAction a)    => btnMedium.onClick.AddListener(a);
        public void BindBtnHigh(UnityEngine.Events.UnityAction a)      => btnHigh.onClick.AddListener(a);
        public void BindConfirm(UnityEngine.Events.UnityAction a)      => confirmButton.onClick.AddListener(a);
        public void BindBack(UnityEngine.Events.UnityAction a)         => backButton.onClick.AddListener(a);

        // ── Seleção visual de card ───────────────────────────────────────
        public void SelectRestaurantCard(RestaurantType? selected)
        {
            ApplyCardState(cardPodrao, selected == RestaurantType.PODRAO);
            ApplyCardState(cardJapones, selected == RestaurantType.JAPONES);
            ApplyCardState(cardFrances, selected == RestaurantType.FRANCES);
        }

        private void ApplyCardState(Button card, bool isSelected)
        {
            var img = card.GetComponent<Image>();
            img.color = isSelected ? CardSelected : CardNormal;
            // Se você usa um filho "Border" como Image, mude a cor dele aqui também:
            var outline = card.GetComponent<Outline>();
            if (outline != null)
                outline.effectColor = isSelected ? BorderSelected : BorderNormal;
        }

        // ── Seleção visual de segmento ───────────────────────────────────
        public void SelectSegmentButton(Segment? selected)
        {
            ApplySegmentState(btnLow,    selected == Segment.LOW);
            ApplySegmentState(btnMedium, selected == Segment.MEDIUM);
            ApplySegmentState(btnHigh,   selected == Segment.HIGH);
        }

        private void ApplySegmentState(Button btn, bool isSelected)
        {
            var img  = btn.GetComponent<Image>();
            img.color = isSelected ? SegBtnSelected : SegBtnNormal;
            var txt  = btn.GetComponentInChildren<TextMeshProUGUI>();
            if (txt != null)
                txt.color = isSelected
                    ? new Color(0.576f, 0.773f, 0.992f)   // #93C5FD
                    : new Color(0.478f, 0.612f, 0.753f);   // #7A9CC0
        }

        // ── Habilitar/desabilitar confirmar ──────────────────────────────
        public void SetConfirmEnabled(bool v) => confirmButton.interactable = v;

        // ── Atualizar painel de coerência ────────────────────────────────
        public void UpdateCoherence(float coh1, string tip1, float coh2, string tip2)
        {
            if (coh1BarFill != null) coh1BarFill.fillAmount = coh1;
            if (coh1Tip != null)     coh1Tip.text            = tip1;
            if (coh2BarFill != null) coh2BarFill.fillAmount = coh2;
            if (coh2Tip != null)     coh2Tip.text            = tip2;

            // Cor da barra varia com o valor
            if (coh1BarFill != null) coh1BarFill.color = CoherenceColor(coh1);
            if (coh2BarFill != null) coh2BarFill.color = CoherenceColor(coh2);
        }

        private static Color CoherenceColor(float value)
        {
            if (value >= 0.7f) return new Color(0.086f, 0.639f, 0.29f);  // verde #16A34A
            if (value >= 0.4f) return new Color(0.851f, 0.604f, 0.043f); // âmbar
            return new Color(0.882f, 0.114f, 0.282f);                     // vermelho
        }
    }
}