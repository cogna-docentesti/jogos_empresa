using UnityEngine;

[CreateAssetMenu(fileName = "LocationData", menuName = "Game Data/LocationData")]
public class LocationData : ScriptableObject
{
    [Header("Identificacao")]
    public string id;
    public string displayName;

    [Header("Classificacao")]
    public LocationZone zone;

    [Header("Custos")]
    public int rent;

    [Header("Perfil de Publico")]
    public Segment primarySegment;

    [Header("Concorrencia")]
    public CompetitionLevel competitionLevel;

    [Header("Canais de Venda")]
    public string[] channels;

    [Header("Coerencia")]
    [Range(1, 3)]
    public int coherenceLevel;

    [Header("Preco")]
    [Range(0f, 2f)]
    public float referencePriceFactor = 1f;

    [Header("Operacao")]
    public int initialPhysicalCapacity;
    public int baseDailyDemand;

    [Header("Visual (UI)")]
    public string colorHex = "#FFFFFF";
    public Sprite pinIcon;

    [TextArea(2, 4)]
    public string description;

    [Header("Apresentacao (tela de localizacao)")]
    [Tooltip("Linha curta sob o nome da area. Ex: \"Instituicao financeira\". " +
             "Vazio: a tela cai no rotulo do segmento.")]
    public string subtitle;

    [Tooltip("Destaque exibido na caixa com estrela do painel lateral. " +
             "Ex: \"Maior circulacao em dias uteis\". Vazio: a caixa nao aparece.")]
    public string highlight;

    /// <summary>Subtitulo com fallback, para a UI nunca ficar com campo vazio.</summary>
    public string SubtitleOrDefault
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(subtitle))
                return subtitle;

            return primarySegment switch
            {
                Segment.LOW    => "Publico popular",
                Segment.MEDIUM => "Publico medio",
                Segment.HIGH   => "Publico alto",
                _              => string.Empty
            };
        }
    }

    /// <summary>Investimento derivado do aluguel, para o indicador da tela.</summary>
    public string InvestmentLabel
    {
        get
        {
            if (rent >= 10000) return "Alto";
            if (rent >= 6000)  return "Medio";
            return "Baixo";
        }
    }

    /// <summary>Movimento derivado da demanda diaria base.</summary>
    public string TrafficLabel
    {
        get
        {
            if (baseDailyDemand >= 90) return "Alto";
            if (baseDailyDemand >= 55) return "Medio";
            return "Baixo";
        }
    }

    /// <summary>Concorrencia em portugues, direto do enum.</summary>
    public string CompetitionLabel => competitionLevel switch
    {
        CompetitionLevel.LOW    => "Baixa",
        CompetitionLevel.MEDIUM => "Media",
        CompetitionLevel.HIGH   => "Alta",
        _                       => "-"
    };

    private void OnValidate()
    {
        coherenceLevel = primarySegment switch
        {
            Segment.LOW => 1,
            Segment.MEDIUM => 2,
            Segment.HIGH => 3,
            _ => coherenceLevel
        };
    }
}
