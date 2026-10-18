using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Game.Adapter.In.UI
{

    public sealed class LocationAreaComponent : MonoBehaviour
    {
        [Header("Glow / Outline neon")]
        [SerializeField] private Image glowOverlay;

        [Header("Pin")]
        [SerializeField] private GameObject pinRoot;
        [SerializeField] private Image      pinCircle;
        [SerializeField] private Image      pinIcon;

        [Header("Labels do mapa (sempre visíveis)")]
        [SerializeField] private TextMeshProUGUI labelName;
        [SerializeField] private TextMeshProUGUI labelSubtitle;

        [Header("Parâmetros")]
        [Range(0f, 1f)] [SerializeField] private float glowAlphaSelected = 0.3f;
        [Range(0f, 1f)] [SerializeField] private float glowAlphaHover    = 0.25f;
        [SerializeField] private float animDuration = 0.22f;

        // ── Estado interno ────────────────────────────────────────────────
        private Color     _color;
        private bool      _isSelected;
        private Coroutine _glowCoroutine;

 
        public void Initialize(string name, string subtitle, string colorHex, Sprite icon)
        {
            if (!ColorUtility.TryParseHtmlString(colorHex, out _color))
                _color = Color.white;

            if (labelName != null)     labelName.text     = name;
            if (labelSubtitle != null) labelSubtitle.text = subtitle;
            if (pinCircle != null)     pinCircle.color    = _color;
            if (pinIcon != null && icon != null) pinIcon.sprite = icon;

            SetGlowAlpha(0f);
            if (pinRoot != null) pinRoot.SetActive(false);
        }

        public void Select()
        {
            _isSelected = true;
            AnimateGlow(glowAlphaSelected);
            ActivatePin();
        }

        public void Deselect()
        {
            _isSelected = false;
            AnimateGlow(0f);
            if (pinRoot != null) pinRoot.SetActive(false);
        }

        public void OnHoverEnter()
        {
            if (!_isSelected) AnimateGlow(glowAlphaHover);
        }

        public void OnHoverExit()
        {
            if (!_isSelected) AnimateGlow(0f);
        }

        // ── Pin ──────────────────────────────────────────────────────────

        private void ActivatePin()
        {
            if (pinRoot == null) return;
            pinRoot.SetActive(true);
            StartCoroutine(AnimatePinDrop());
        }

        private IEnumerator AnimatePinDrop()
        {
            var rt = pinRoot.GetComponent<RectTransform>();
            Vector2 target = rt.anchoredPosition;
            Vector2 start  = target + new Vector2(0f, 10f);

            float elapsed = 0f, duration = 0.28f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                // Ease-out cúbico: desacelera ao pousar
                float t = 1f - Mathf.Pow(1f - elapsed / duration, 3f);
                rt.anchoredPosition = Vector2.Lerp(start, target, t);
                yield return null;
            }
            rt.anchoredPosition = target;
            yield return StartCoroutine(PinPulse(rt));
        }

        private IEnumerator PinPulse(RectTransform rt)
        {
            Vector3 big = Vector3.one * 1.18f;
            float   dur = 0.10f;
            float   e   = 0f;

            while (e < dur)
            {
                e += Time.deltaTime;
                rt.localScale = Vector3.Lerp(Vector3.one, big, e / dur);
                yield return null;
            }
            e = 0f;
            while (e < dur)
            {
                e += Time.deltaTime;
                rt.localScale = Vector3.Lerp(big, Vector3.one, e / dur);
                yield return null;
            }
            rt.localScale = Vector3.one;
        }

        // ── Glow ─────────────────────────────────────────────────────────

        private void AnimateGlow(float targetAlpha)
        {
            if (glowOverlay == null) return;
            if (_glowCoroutine != null) StopCoroutine(_glowCoroutine);
            _glowCoroutine = StartCoroutine(LerpGlow(targetAlpha));
        }

        private IEnumerator LerpGlow(float targetAlpha)
        {
            float startAlpha = glowOverlay.color.a;
            float elapsed    = 0f;

            while (elapsed < animDuration)
            {
                elapsed += Time.deltaTime;
                SetGlowAlpha(Mathf.Lerp(startAlpha, targetAlpha, elapsed / animDuration));
                yield return null;
            }
            SetGlowAlpha(targetAlpha);
        }

        private void SetGlowAlpha(float alpha)
        {
            if (glowOverlay != null)
                glowOverlay.color = new Color(_color.r, _color.g, _color.b, alpha);
        }
    }
}
