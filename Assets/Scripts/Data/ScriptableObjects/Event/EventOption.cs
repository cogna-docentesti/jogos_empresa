using System;
using UnityEngine;

[Serializable]
public class EventOption
{
    public string id;
    public string description;

    [Header("Impacto Financeiro")]
    public float cashChange;

    [Header("Impacto em Reputação")]
    public int reputationChange;

    [Header("Impacto em Clientes")]
    public int clientsChange;

    [Header("Risco")]
    [Range(0f, 1f)]
    public float riskFactor;
}