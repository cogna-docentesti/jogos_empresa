using UnityEngine;

[CreateAssetMenu(menuName = "Game Data/Role")]
public class RoleData : ScriptableObject
{
    [Header("Identificação")]
    public string id;
    public string displayName;

    [Header("Classificação")]
    public RoleType roleType;

    [Header("Financeiro")]
    public int salary;

    [Header("Capacidade")]
    public int maxClientsSupported;

    [Header("Qualidade")]
    [Range(0f, 1f)]
    public float qualityContribution;
}
