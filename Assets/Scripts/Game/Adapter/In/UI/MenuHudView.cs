using Game.Adapter.In.UI.Theme;
using Game.Domain.Service;
using TMPro;
using UnityEngine;

namespace Game.Adapter.In.UI
{
    /// <summary>
    /// Pills de Score e Caixa da barra superior do Menu do Jogo.
    /// Atualiza sozinho toda vez que o mapa volta a ficar visivel, entao
    /// reflete a ultima escolha feita no Banco, no RH ou na Loja.
    /// </summary>
    public sealed class MenuHudView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI scoreValue;
        [SerializeField] private TextMeshProUGUI cashValue;
        [SerializeField] private TextMeshProUGUI roundValue;

        [Header("Alerta")]
        [Tooltip("Cor do Caixa quando fica negativo. A cor normal e a que o TMP ja tem na cena.")]
        [SerializeField] private Color negativeCashColor = new Color(0.973f, 0.443f, 0.443f);

        private Color _cashNormalColor = Color.white;

        private void Awake()
        {
            if (cashValue != null)
                _cashNormalColor = cashValue.color;
        }

        private void OnEnable()
        {
            Refresh();
        }

        public void Refresh()
        {
            var summary = EstablishmentSummaryService.Build();

            // A cor do score saiu: era decoracao fixa, agora vale a do TMP na cena.
            if (scoreValue != null)
                scoreValue.text = summary.HasSession ? summary.Score.ToString() : "0";

            if (cashValue != null)
            {
                cashValue.text = EstablishmentSummaryService.Brl(summary.Cash);

                // Caixa negativo e aviso, nao estilo - por isso continua no codigo.
                // Mas as duas cores vem do Inspector: a normal e a que o TMP ja
                // tinha, a de alerta e campo serializado.
                cashValue.color = summary.Cash >= 0f ? _cashNormalColor : negativeCashColor;
            }

            if (roundValue != null)
                roundValue.text = $"Rodada {summary.Round}";
        }
    }
}
