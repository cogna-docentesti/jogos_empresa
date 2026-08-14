using System;
using System.Collections;
using Game.Adapter.In.UI.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Adapter.In.UI
{
    /// <summary>
    /// Marcador de uma area no mapa da tela de localizacao.
    ///
    /// Estrutura: um halo colorido atras, um card escuro com nome e subtitulo,
    /// e um badge circular com o icone da zona por cima.
    ///
    /// O brilho tem estado proprio e NAO depende de hover - celular nao tem
    /// hover, e foi assim que o glow do menu deixou de funcionar no Android.
    /// Em repouso ele pulsa de leve; selecionado, fica travado no maximo.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public sealed class LocationMarkerV2 : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler
    {
        [Header("Identificacao")]
        [SerializeField] private string locationId;

        [Header("Partes")]
        [SerializeField] private Image glow;
        [SerializeField] private Image card;
        [SerializeField] private Image cardBorder;
        [SerializeField] private Image badge;
        [SerializeField] private Image badgeIcon;
        [SerializeField] private Image badgeRing;
        [SerializeField] private TextMeshProUGUI nameLabel;
        [SerializeField] private TextMeshProUGUI subtitleLabel;

        [Header("Brilho")]
        [Range(0f, 1f)] [SerializeField] private float idleMin = 0.10f;
        [Range(0f, 1f)] [SerializeField] private float idleMax = 0.24f;
        [Range(0f, 1f)] [SerializeField] private float highlightAlpha = 0.55f;
        [Range(0f, 1f)] [SerializeField] private float selectedAlpha = 0.85f;
        [SerializeField] private float idleCycleSeconds = 3f;
        [SerializeField] private float transitionSeconds = 0.18f;

        private Color _accent = Color.white;
        private Coroutine _fade;
        private bool _pressed;
        private bool _selected;

        // ---- skin (opcional) ----
        // Preenchidos por ApplySkin. Vazios: o marcador segue como era, colorindo
        // as Images pela cor da zona em vez de trocar sprite.
        private Sprite _pinIdle;
        private Sprite _pinActive;
        private Sprite _glowIdle;
        private Sprite _glowActive;
        private Color  _accentSoft = Color.white;
        private bool   _skinned;

        public string LocationId => locationId;

        /// <summary>Disparado no clique. O controller assina.</summary>
        public event Action<string> OnClicked;

        private void Awake()
        {
            GetComponent<Button>().onClick.AddListener(HandleClick);
        }

        private void OnDestroy()
        {
            var button = GetComponent<Button>();

            if (button != null)
                button.onClick.RemoveListener(HandleClick);
        }

        private void OnEnable()
        {
            _pressed = false;
            ApplyTarget(instant: true);
        }

        private void Update()
        {
            if (_pressed || _selected || _fade != null)
                return;

            SetGlowAlpha(IdleAlphaNow());
        }

        // =====================================================
        //  CONFIGURACAO
        // =====================================================

        /// <summary>Preenche o marcador com os dados da area. Chamado pelo controller.</summary>
        public void Bind(string id, string displayName, string subtitle, string accentHex, Sprite icon)
        {
            locationId = id;

            if (!ColorUtility.TryParseHtmlString(accentHex, out _accent))
                _accent = GamePalette.AccentBlue;

            if (nameLabel != null)     nameLabel.text = displayName;
            if (subtitleLabel != null) subtitleLabel.text = subtitle;

            if (badgeIcon != null && icon != null)
                badgeIcon.sprite = icon;

            if (badge != null)     badge.color = _accent;
            if (badgeRing != null) badgeRing.color = GamePalette.WithAlpha(_accent, 0.35f);

            SetSelected(false);
        }

        /// <summary>
        /// Liga a arte do redesign neste marcador. Chamado pelo controller/binder
        /// logo depois do Bind. Sem isto o marcador continua procedural.
        ///
        /// O pino escolhido e um PNG diferente do normal (preenchido x vazado),
        /// entao a troca acontece em SetSelected, junto com o resto do estado -
        /// nao existe um caminho separado para "skin".
        /// </summary>
        public void ApplySkin(
            Sprite pinIdle, Sprite pinActive,
            Sprite glowIdle, Sprite glowActive,
            Color accent, Color accentSoft,
            Sprite cardSprite)
        {
            _pinIdle    = pinIdle;
            _pinActive  = pinActive;
            _glowIdle   = glowIdle;
            _glowActive = glowActive;
            _accent     = accent;
            _accentSoft = accentSoft;
            _skinned    = pinIdle != null || pinActive != null;

            if (cardSprite != null && card != null)
            {
                card.sprite = cardSprite;
                card.color  = Color.white;
                card.type   = Image.Type.Sliced;
            }

            if (_skinned)
            {
                // O PNG do pino ja traz anel, miolo e icone desenhados; as tres
                // Images procedurais atras dele viram ruido.
                if (badgeRing != null) badgeRing.enabled = false;
                if (badgeIcon != null) badgeIcon.enabled = false;

                if (badge != null)
                {
                    badge.color         = Color.white;
                    badge.preserveAspect = true;
                }
            }

            SetSelected(_selected);
        }

        public void SetSelected(bool selected)
        {
            _selected = selected;

            if (_skinned)
            {
                if (badge != null)
                {
                    var wanted = selected ? _pinActive : _pinIdle;

                    if (wanted != null)
                        badge.sprite = wanted;
                }

                if (glow != null)
                {
                    var wantedGlow = selected ? _glowActive : _glowIdle;

                    if (wantedGlow != null)
                        glow.sprite = wantedGlow;
                }

                // Com sprite pronto, a borda do card acompanha em cor clara.
                if (cardBorder != null)
                    cardBorder.color = selected ? _accentSoft : GamePalette.Hairline;
            }
            else if (cardBorder != null)
            {
                // A borda do card acompanha: apagada em repouso, acesa quando escolhida.
                cardBorder.color = selected ? _accent : GamePalette.Hairline;
            }

            if (nameLabel != null)
                nameLabel.color = GamePalette.Ink;

            ApplyTarget(instant: false);
        }

        public void SetInteractable(bool value)
        {
            var button = GetComponent<Button>();

            if (button != null)
                button.interactable = value;

            if (card != null)
                card.color = value ? GamePalette.MapLabel
                                   : GamePalette.WithAlpha(GamePalette.MapLabel, 0.45f);
        }

        // =====================================================
        //  PONTEIRO
        // =====================================================

        public void OnPointerEnter(PointerEventData e) { _pressed = true;  ApplyTarget(false); }
        public void OnPointerExit(PointerEventData e)  { _pressed = false; ApplyTarget(false); }
        public void OnPointerDown(PointerEventData e)  { _pressed = true;  ApplyTarget(false); }
        public void OnPointerUp(PointerEventData e)    { _pressed = false; ApplyTarget(false); }

        private void HandleClick()
        {
            OnClicked?.Invoke(locationId);
        }

        // =====================================================
        //  BRILHO
        // =====================================================

        private float IdleAlphaNow()
        {
            if (idleCycleSeconds <= 0f)
                return idleMin;

            float t = Mathf.PingPong(Time.unscaledTime / idleCycleSeconds * 2f, 1f);
            return Mathf.Lerp(idleMin, idleMax, Mathf.SmoothStep(0f, 1f, t));
        }

        private float TargetAlpha()
        {
            if (_selected) return selectedAlpha;
            if (_pressed)  return highlightAlpha;
            return IdleAlphaNow();
        }

        private void ApplyTarget(bool instant)
        {
            if (!isActiveAndEnabled)
                return;

            if (_fade != null)
            {
                StopCoroutine(_fade);
                _fade = null;
            }

            if (instant)
            {
                SetGlowAlpha(TargetAlpha());
                return;
            }

            _fade = StartCoroutine(Fade(TargetAlpha()));
        }

        private IEnumerator Fade(float target)
        {
            float start = glow != null ? glow.color.a : 0f;
            float t = 0f;

            while (t < transitionSeconds)
            {
                t += Time.unscaledDeltaTime;
                SetGlowAlpha(Mathf.Lerp(start, target, t / transitionSeconds));
                yield return null;
            }

            SetGlowAlpha(target);
            _fade = null;
        }

        private void SetGlowAlpha(float a)
        {
            if (glow == null) return;

            // O PNG do brilho ja vem na cor da zona; tingir de novo pelo accent
            // dobraria a saturacao. Skinned: mexe so no alfa.
            glow.color = _skinned
                ? new Color(1f, 1f, 1f, a)
                : GamePalette.WithAlpha(_accent, a);
        }
    }
}
