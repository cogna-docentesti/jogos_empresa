using UnityEngine;

namespace Game.Adapter.In.UI.Layout
{
    /// <summary>
    /// Estica na largura do pai, mas nunca alem de maxWidth - e mantem
    /// centralizado.
    ///
    /// Por que isso importa aqui: o CanvasScaler esta com Match = Height.
    /// Num celular landscape 2640x1200, a escala e 1200/1080 = 1.11 e o canvas
    /// passa a ter ~2376 unidades de LARGURA, nao 1920. Conteudo esticado nessa
    /// largura toda separa rotulo e valor por quase 800px e a leitura morre.
    ///
    /// Travar a largura resolve isso de uma vez, em qualquer aparelho.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    [DisallowMultipleComponent]
    public sealed class MaxWidthFitter : MonoBehaviour
    {
        [Tooltip("Largura maxima em unidades de canvas.")]
        [SerializeField] private float maxWidth = 1800f;

        [Tooltip("Margem minima de cada lado quando a tela e mais estreita que maxWidth.")]
        [SerializeField] private float sideMargin = 48f;

        private RectTransform _rect;
        private RectTransform _parent;
        private float _lastParentWidth = -1f;

        private void Awake()
        {
            Cache();
        }

        private void OnEnable()
        {
            Cache();
            Apply(force: true);
        }

        private void Update()
        {
            Apply(force: false);
        }

        private void Cache()
        {
            _rect   = GetComponent<RectTransform>();
            _parent = _rect != null ? _rect.parent as RectTransform : null;
        }

        private void Apply(bool force)
        {
            if (_rect == null || _parent == null)
            {
                Cache();

                if (_rect == null || _parent == null)
                    return;
            }

            float parentWidth = _parent.rect.width;

            if (parentWidth <= 0f)
                return;

            if (!force && Mathf.Approximately(parentWidth, _lastParentWidth))
                return;

            _lastParentWidth = parentWidth;

            float inset = Mathf.Max(sideMargin, (parentWidth - maxWidth) * 0.5f);

            _rect.anchorMin = new Vector2(0f, _rect.anchorMin.y);
            _rect.anchorMax = new Vector2(1f, _rect.anchorMax.y);
            _rect.offsetMin = new Vector2(inset, _rect.offsetMin.y);
            _rect.offsetMax = new Vector2(-inset, _rect.offsetMax.y);
        }

        public void EditorConfigure(float newMaxWidth, float newSideMargin)
        {
            maxWidth   = newMaxWidth;
            sideMargin = newSideMargin;
        }
    }
}
