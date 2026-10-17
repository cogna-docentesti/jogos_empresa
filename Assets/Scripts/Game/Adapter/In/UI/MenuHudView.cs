using Game.Adapter.In.UI.Theme;
using Game.Domain.Service;
using Game.Infrastructure.Session;
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
        [SerializeField] private TextMeshProUGUI companyValue;
        [SerializeField] private TextMeshProUGUI progressValue;
        [SerializeField] private RectTransform progressFill;
        private float nextRefresh;

        [Header("Alerta")]
        [Tooltip("Cor do Caixa quando fica negativo. A cor normal e a que o TMP ja tem na cena.")]
        [SerializeField] private Color negativeCashColor = GamePalette.Parse(GamePalette.HexNegativeOnDark);

        private Color _cashNormalColor = Color.white;

        private void Awake()
        {
            if (cashValue != null)
                _cashNormalColor = cashValue.color;
        }

        private void OnEnable()
        {
            ApplyTypography();
            Refresh();
        }

        public void ApplyTypography()
        {
            foreach (var text in new[] { scoreValue, cashValue, roundValue })
                GameTypography.KeepSize(text, TypographyRole.Kpi);
            GameTypography.KeepSize(companyValue, TypographyRole.Body);
            GameTypography.KeepSize(progressValue, TypographyRole.Label);
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
                roundValue.text = $"Mês {summary.Round} de 3";
            if(companyValue!=null) companyValue.text=string.IsNullOrWhiteSpace(PlayerSession.RestaurantName)?"Meu restaurante":PlayerSession.RestaurantName;
            int completed=summary.HasSession?Mathf.Clamp(summary.Round-1,0,3):0;
            if(progressValue!=null) progressValue.text=$"{completed} de 3 meses concluídos";
            if(progressFill!=null) progressFill.anchorMax=new Vector2(completed/3f,1);
        }

        private void Update()
        {
            if(Time.unscaledTime<nextRefresh) return;
            nextRefresh=Time.unscaledTime+.5f;
            Refresh();
        }
    }
}
