using UnityEngine;

[CreateAssetMenu(menuName = "Game Data/Credit Line")]
public class CreditLineData : ScriptableObject
{
    [Header("Identificação")]
    public string id;
    public string displayName;

    [TextArea(2, 4)]
    [Tooltip("Texto explicativo exibido no card da linha de credito.")]
    public string cardDescription;

    [Header("Visual")]
    [Tooltip("Icone exibido no card desta linha de credito.")]
    public Sprite icon;

    [Header("Financeiro")]
    public float maxAmount;

    [Tooltip("Taxa mensal (ex: 0.05 = 5%)")]
    [Range(0f, 1f)]
    public float monthlyInterestRate;

    [Tooltip("Legado: preservado para compatibilidade dos assets. Nao e usado; o prazo depende dos meses restantes no trimestre.")]
    public int termRounds;

    [Header("Parcelas")]
    public string installmentDescription = "Pagamento de parcelas de empréstimos do Banco CogCred, se houve.";

    [Header("Risco")]
    [Range(0f, 1f)]
    public float riskLevel;
}

