using UnityEngine;

public enum RestaurantType
{
    PODRAO,
    JAPONES,
    FRANCES
}

public enum Segment
{
    LOW,
    MEDIUM,
    HIGH
}

public enum PriceStrategy
{
    COMPETITIVE,
    VALUE_ADDED
}

public enum LocationZone
{
    INDUSTRIAL,
    COMMERCIAL,
    NOBLE
}

public enum EquipmentCategory
{
    BASIC,
    SPECIFIC
}

public enum GameSessionStatus
{
    IN_PROGRESS,
    COMPLETED,
    BANKRUPT
}

public enum RoleType
{
    // Operacional
    ATENDENTE,

    // Produção
    ESPECIALISTA,

    // Gestão
    GERENTE
}

public enum AlignmentClassification
{
    HIGH,
    ADEQUATE,
    FRAGILE,
    CRITICAL
}