using UnityEngine;
using UnityEngine.UI;

namespace Game.Adapter.In.UI.Effects
{
    /// <summary>
    /// Gradiente vertical aplicado na MALHA do Graphic, nao em camadas empilhadas.
    ///
    /// Por que isso e melhor que empilhar Images:
    ///
    ///  1. Sem degrau. Empilhar uma faixa clara por cima de uma base escura cria
    ///     uma aresta dura na altura da emenda. Era o defeito visivel do "Sheen".
    ///
    ///  2. E exato, nao aproximado. A malha de um Image Sliced tem vertices em
    ///     apenas 4 linhas de Y (0, borda, altura-borda, altura). Avaliamos uma
    ///     funcao linear nessas 4 linhas e a GPU interpola linearmente entre
    ///     elas. Interpolacao linear de funcao linear e a propria funcao: nao ha
    ///     banding e nao ha erro.
    ///
    ///  3. Sobrevive ao Button. O ColorTint escreve em CanvasRenderer.SetColor,
    ///     que multiplica a malha inteira de forma uniforme - o gradiente
    ///     continua intacto no hover e no pressed. Com camadas empilhadas o tint
    ///     atinge so o targetGraphic e o botao fica de duas cores.
    ///
    /// ATENCAO: a cor do Image precisa ficar em BRANCO. O gradiente MULTIPLICA a
    /// cor do vertice; se o Image ja estiver dourado, o dourado entra duas vezes
    /// e o botao fica marrom.
    /// </summary>
    [AddComponentMenu("UI/Effects/Vertical Gradient")]
    [RequireComponent(typeof(Graphic))]
    [DisallowMultipleComponent]
    public sealed class VerticalGradient : BaseMeshEffect
    {
        [SerializeField] private Color top    = new Color(0.94f, 0.75f, 0.35f, 1f);
        [SerializeField] private Color bottom = new Color(0.77f, 0.53f, 0.16f, 1f);

        public Color Top    => top;
        public Color Bottom => bottom;

        /// <summary>Usado pelo builder de Editor e por qualquer troca em runtime.</summary>
        public void SetColors(Color topColor, Color bottomColor)
        {
            top    = topColor;
            bottom = bottomColor;

            if (graphic != null)
                graphic.SetVerticesDirty();
        }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || graphic == null || vh.currentVertCount == 0)
                return;

            var rect = graphic.rectTransform.rect;

            float yMin = rect.yMin;
            float yMax = rect.yMax;

            // Altura zero acontece durante o primeiro layout. Sair aqui evita
            // uma divisao por zero dentro do InverseLerp.
            if (Mathf.Approximately(yMax, yMin))
                return;

            var vertex = new UIVertex();

            for (int i = 0; i < vh.currentVertCount; i++)
            {
                vh.PopulateUIVertex(ref vertex, i);

                float t    = Mathf.InverseLerp(yMin, yMax, vertex.position.y);
                Color tint = Color.Lerp(bottom, top, t);

                vertex.color = (Color)vertex.color * tint;

                vh.SetUIVertex(vertex, i);
            }
        }

        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();

            if (graphic != null)
                graphic.SetVerticesDirty();
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();

            if (graphic != null)
                graphic.SetVerticesDirty();
        }
#endif
    }
}
