using UnityEngine;

namespace Game.Adapter.In.UI.Layout
{
    /// <summary>
    /// Afasta o conteudo do furo de camera, do notch e da barra de gestos.
    ///
    /// A implementacao classica sobrescreve anchorMin/anchorMax com as fracoes
    /// do Screen.safeArea. Isso SO funciona se o objeto for filho direto de algo
    /// que cobre a tela inteira - num objeto aninhado (dentro de uma barra de
    /// 112px, por exemplo) as fracoes passam a ser calculadas sobre a barra e o
    /// resultado fica errado.
    ///
    /// Aqui a margem e convertida para PIXELS de canvas e somada aos offsets
    /// originais. As ancoras nao sao tocadas, entao funciona em qualquer nivel
    /// da hierarquia e nao quebra layouts esticados nem barras ancoradas no topo.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    [DisallowMultipleComponent]
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        [SerializeField] private bool applyLeft   = true;
        [SerializeField] private bool applyRight  = true;
        [SerializeField] private bool applyTop    = true;
        [SerializeField] private bool applyBottom = true;

        private RectTransform _rect;
        private Canvas _rootCanvas;

        private Vector2 _baseOffsetMin;
        private Vector2 _baseOffsetMax;
        private bool _captured;

        private Rect _lastSafeArea;
        private Vector2Int _lastResolution;

        private void OnEnable()
        {
            Capture();
            Apply(force: true);
        }

        private void Update()
        {
            Apply(force: false);
        }

        /// <summary>
        /// Guarda os offsets que o layout definiu, ANTES de qualquer margem de
        /// area segura. Todo recalculo parte daqui, entao trocar de orientacao
        /// varias vezes nao vai acumulando margem.
        /// </summary>
        private void Capture()
        {
            if (_rect == null)
                _rect = GetComponent<RectTransform>();

            if (_rootCanvas == null)
            {
                var canvas = GetComponentInParent<Canvas>();
                _rootCanvas = canvas != null ? canvas.rootCanvas : null;
            }

            if (_captured)
                return;

            _baseOffsetMin = _rect.offsetMin;
            _baseOffsetMax = _rect.offsetMax;
            _captured = true;
        }

        private void Apply(bool force)
        {
            if (_rect == null || !_captured)
                Capture();

            if (_rect == null)
                return;

            var safeArea   = Screen.safeArea;
            var resolution = new Vector2Int(Screen.width, Screen.height);

            if (!force && safeArea == _lastSafeArea && resolution == _lastResolution)
                return;

            _lastSafeArea   = safeArea;
            _lastResolution = resolution;

            if (resolution.x <= 0 || resolution.y <= 0)
                return;

            // Quanto sobra fora da area segura, em pixels de tela...
            float leftPx   = safeArea.xMin;
            float bottomPx = safeArea.yMin;
            float rightPx  = resolution.x - safeArea.xMax;
            float topPx    = resolution.y - safeArea.yMax;

            // ...convertido para unidades de canvas.
            float scaleX = 1f;
            float scaleY = 1f;

            if (_rootCanvas != null)
            {
                var canvasRect = _rootCanvas.transform as RectTransform;

                if (canvasRect != null && canvasRect.rect.width > 0f && canvasRect.rect.height > 0f)
                {
                    scaleX = canvasRect.rect.width  / resolution.x;
                    scaleY = canvasRect.rect.height / resolution.y;
                }
            }

            float left   = applyLeft   ? leftPx   * scaleX : 0f;
            float right  = applyRight  ? rightPx  * scaleX : 0f;
            float bottom = applyBottom ? bottomPx * scaleY : 0f;
            float top    = applyTop    ? topPx    * scaleY : 0f;

            _rect.offsetMin = new Vector2(_baseOffsetMin.x + left,  _baseOffsetMin.y + bottom);
            _rect.offsetMax = new Vector2(_baseOffsetMax.x - right, _baseOffsetMax.y - top);
        }

        /// <summary>Rele os offsets atuais como nova base. Use se o layout mudar em runtime.</summary>
        public void Recapture()
        {
            _captured = false;
            Capture();
            Apply(force: true);
        }
    }
}
