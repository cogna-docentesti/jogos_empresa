using UnityEngine;

[CreateAssetMenu(menuName = "Game Data/Credit Line")]
public class CreditLineData : ScriptableObject
{
    [Header("Identificação")]
    public string id;
    public string displayName;

    [Header("Financeiro")]
    public float maxAmount;

    [Tooltip("Taxa mensal (ex: 0.05 = 5%)")]
    [Range(0f, 1f)]
    public float monthlyInterestRate;

    [Tooltip("Quantidade de rodadas (meses) para pagar")]
    public int termRounds;

    [Header("Parcelas")]
    public string installmentDescription = "Pagamento de parcelas de empréstimos do Banco CogCred, se houve.";

    [Header("Risco")]
    [Range(0f, 1f)]
    public float riskLevel;
}

