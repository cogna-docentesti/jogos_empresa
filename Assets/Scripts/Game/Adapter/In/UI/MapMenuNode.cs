using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Game.Adapter.In.UI
{
    // Versão enxuta do LocationAreaComponent, só para o menu:
    // o glow neon acende no toque/hover.
    [RequireComponent(typeof(Button))]
    public sealed class MapMenuNode : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler
    {
        [Header("Visual")]
        [SerializeField] private Image glowOverlay;
        [SerializeField] private string colorHex = Theme.GamePalette.HexPrimary;

        [Header("Animação")]
        [Range(0f,1f)] [SerializeField] private float hoverAlpha = 0.45f;
        [SerializeField] private float duration = 0.18f;

        private Color _color;
        private Coroutine _co;

        private void Awake()
        {
            if (!ColorUtility.TryParseHtmlString(colorHex, out _color))
                _color = Color.white;
            SetAlpha(0f);
        }

        public void OnPointerEnter(PointerEventData e) => AnimateTo(hoverAlpha);
        public void OnPointerExit(PointerEventData e)  => AnimateTo(0f);

        private void AnimateTo(float a)
        {
            if (_co != null) StopCoroutine(_co);
            _co = StartCoroutine(Lerp(a));
        }

        private IEnumerator Lerp(float target)
        {
            float start = glowOverlay != null ? glowOverlay.color.a : 0f;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                SetAlpha(Mathf.Lerp(start, target, t / duration));
                yield return null;
            }
            SetAlpha(target);
        }

        private void SetAlpha(float a)
        {
            if (glowOverlay != null)
                glowOverlay.color = new Color(_color.r, _color.g, _color.b, a);
        }
    }
}