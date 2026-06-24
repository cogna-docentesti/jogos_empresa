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

public enum LocationZone
{
    Financas,
    Educacao,
    Comercio,
    Residencial,
    Servicos
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
    GARCOM,

    // Produ��o
    CHAPEIRO,
    CHEF,
    SUSHIMAN,

    // Gest�o
    GERENTE
}
