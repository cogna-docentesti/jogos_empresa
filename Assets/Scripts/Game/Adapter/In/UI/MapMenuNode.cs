using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Game.Adapter.In.UI
{
    /// <summary>
    /// Glow do node do mapa do menu.
    ///
    /// POR QUE ESTA CLASSE MUDOU:
    /// a versao anterior acendia o glow SOMENTE em OnPointerEnter e apagava em
    /// OnPointerExit - ou seja, dependia inteiramente de HOVER. No Editor isso
    /// funciona porque existe mouse. No celular nao existe hover: o dedo entra
    /// e sai no mesmo toque, entao o glow nunca aparecia. Era esse o motivo de
    /// "os overlays com glow nao funcionam no Android".
    ///
    /// Agora o brilho tem tres estados, e o de repouso e o mais importante:
    ///  - REPOUSO: pulsa devagar o tempo todo, para o jogador enxergar que
    ///    aquilo ali e clicavel mesmo sem encostar.
    ///  - DESTAQUE: hover no desktop, ou toque em qualquer plataforma.
    ///  - TRAVADO: brilho fixo, para marcar o node como selecionado.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public sealed class MapMenuNode : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler
    {
        [Header("Visual")]
        [SerializeField] private Image glowOverlay;
        [SerializeField] private string colorHex = Theme.GamePalette.HexPrimary;

        [Header("Brilho de repouso (funciona sem hover, essencial no celular)")]
        [Tooltip("Menor brilho do ciclo de respiracao. Zero deixa o node apagado no celular.")]
        [Range(0f, 1f)] [SerializeField] private float idleAlphaMin = 0.14f;

        [Tooltip("Maior brilho do ciclo de respiracao.")]
        [Range(0f, 1f)] [SerializeField] private float idleAlphaMax = 0.30f;

        [Tooltip("Segundos de um ciclo completo da respiracao.")]
        [SerializeField] private float idleCycleSeconds = 2.6f;

        [Header("Destaque")]
        [Tooltip("Brilho ao passar o mouse ou encostar o dedo.")]
        [Range(0f, 1f)] [SerializeField] private float highlightAlpha = 0.55f;

        [Tooltip("Brilho enquanto o node esta marcado como selecionado.")]
        [Range(0f, 1f)] [SerializeField] private float selectedAlpha = 0.45f;

        [SerializeField] private float transitionSeconds = 0.18f;

        private Color _color;
        private Coroutine _transition;

        private bool _highlighted;
        private bool _selected;

        /// <summary>Brilho travado, independente de toque. Use ao marcar a area escolhida.</summary>
        public bool IsSelected
        {
            get => _selected;
            set
            {
                if (_selected == value) return;
                _selected = value;
                RefreshTarget();
            }
        }

        private void Awake()
        {
            if (!ColorUtility.TryParseHtmlString(colorHex, out _color))
                _color = Color.white;

            if (idleAlphaMax < idleAlphaMin)
                idleAlphaMax = idleAlphaMin;

            SetAlpha(idleAlphaMin);
        }

        private void OnEnable()
        {
            // O painel do menu e ligado e desligado o tempo todo pela navegacao.
            // Reiniciar o estado aqui evita o node voltar preso em "aceso".
            _highlighted = false;
            RefreshTarget();
        }

        private void OnDisable()
        {
            StopTransition();
        }

        private void Update()
        {
            // A respiracao so roda quando o node esta em repouso; nos outros
            // estados quem manda e a transicao.
            if (_highlighted || _selected || _transition != null)
                return;

            SetAlpha(IdleAlphaNow());
        }

        private float IdleAlphaNow()
        {
            if (idleCycleSeconds <= 0f)
                return idleAlphaMin;

            // PingPong de 0..1 convertido para a faixa de repouso.
            float t = Mathf.PingPong(Time.unscaledTime / idleCycleSeconds * 2f, 1f);
            return Mathf.Lerp(idleAlphaMin, idleAlphaMax, Mathf.SmoothStep(0f, 1f, t));
        }

        // =====================================================
        //  EVENTOS DE PONTEIRO
        //  Enter/Exit servem o desktop. Down/Up servem o toque,
        //  onde Enter e Exit chegam praticamente juntos.
        // =====================================================

        public void OnPointerEnter(PointerEventData e) { _highlighted = true;  RefreshTarget(); }
        public void OnPointerExit(PointerEventData e)  { _highlighted = false; RefreshTarget(); }
        public void OnPointerDown(PointerEventData e)  { _highlighted = true;  RefreshTarget(); }

        public void OnPointerUp(PointerEventData e)
        {
            _highlighted = false;
            RefreshTarget();
        }

        private void RefreshTarget()
        {
            if (!isActiveAndEnabled)
                return;

            if (_highlighted)      AnimateTo(highlightAlpha);
            else if (_selected)    AnimateTo(selectedAlpha);
            else                   AnimateTo(IdleAlphaNow());
        }

        private void AnimateTo(float target)
        {
            StopTransition();
            _transition = StartCoroutine(Fade(target));
        }

        private void StopTransition()
        {
            if (_transition == null)
                return;

            StopCoroutine(_transition);
            _transition = null;
        }

        private IEnumerator Fade(float target)
        {
            float start = glowOverlay != null ? glowOverlay.color.a : 0f;
            float t = 0f;

            while (t < transitionSeconds)
            {
                t += Time.unscaledDeltaTime;
                SetAlpha(Mathf.Lerp(start, target, t / transitionSeconds));
                yield return null;
            }

            SetAlpha(target);
            _transition = null;
        }

        private void SetAlpha(float a)
        {
            if (glowOverlay != null)
                glowOverlay.color = new Color(_color.r, _color.g, _color.b, a);
        }
    }
}
