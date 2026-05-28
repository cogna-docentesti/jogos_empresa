using System.Collections.Generic;
using System.Linq;
using UnityEditor.U2D.Aseprite;

public static class AlignmentEngine
{
    public static AlignmentResult Calculate(
        RestaurantType restaurantType,
        Segment targetSegment,
        LocationZone locationZone,
        PriceStrategy priceStrategy,
        TeamSelectionData teamData,
        List<RoleData> availableRoles
    )
    {
        int restaurantTargetScore = GetRestaurantTargetScore(restaurantType, targetSegment);
        int restaurantLocationScore = GetRestaurantLocationScore(restaurantType, locationZone);
        int restaurantPriceScore = GetRestaurantPriceScore(restaurantType, priceStrategy);
        int restaurantTeamScore = GetTeamScore(teamData, availableRoles);

        int totalScore =
            restaurantTargetScore +
            restaurantLocationScore +
            restaurantPriceScore +
            restaurantTeamScore;

        var classification = GetClassification(totalScore);
        float factor = GetFactor(classification);

        return new AlignmentResult
        {
            restaurantTargetScore = restaurantTargetScore,
            restaurantLocationScore = restaurantLocationScore,
            restaurantPriceScore = restaurantPriceScore,
            restaurantTeamScore = restaurantTeamScore,
            totalScore = totalScore,
            classification = classification,
            alignmentFactor = factor,
            message = GetMessage(classification)
        };
    }

    private static int GetRestaurantTargetScore(RestaurantType restaurant, Segment target)
    {
        return restaurant switch
        {
            RestaurantType.PODRAO => target switch
            {
                Segment.LOW => 20,
                Segment.MEDIUM => 10,
                Segment.HIGH => 0,
                _ => 0
            },

            RestaurantType.JAPONES => target switch
            {
                Segment.LOW => 0,
                Segment.MEDIUM => 20,
                Segment.HIGH => 15,
                _ => 0
            },

            RestaurantType.FRANCES => target switch
            {
                Segment.LOW => 0,
                Segment.MEDIUM => 5,
                Segment.HIGH => 20,
                _ => 0
            },

            _ => 0
        };
    }

    private static int GetRestaurantLocationScore(RestaurantType restaurant, LocationZone location)
    {
        return restaurant switch
        {
            RestaurantType.PODRAO => location switch
            {
                LocationZone.INDUSTRIAL => 20,
                LocationZone.COMMERCIAL => 12,
                LocationZone.NOBLE => 0,
                _ => 0
            },

            RestaurantType.JAPONES => location switch
            {
                LocationZone.INDUSTRIAL => 5,
                LocationZone.COMMERCIAL => 20,
                LocationZone.NOBLE => 15,
                _ => 0
            },

            RestaurantType.FRANCES => location switch
            {
                LocationZone.INDUSTRIAL => 0,
                LocationZone.COMMERCIAL => 8,
                LocationZone.NOBLE => 20,
                _ => 0
            },

            _ => 0
        };
    }

    private static int GetRestaurantPriceScore(RestaurantType restaurant, PriceStrategy price)
    {
        return restaurant switch
        {
            RestaurantType.PODRAO => price switch
            {
                PriceStrategy.COMPETITIVE => 20,
                PriceStrategy.VALUE_ADDED => 12,
                _ => 0
            },

            RestaurantType.JAPONES => price switch
            {
                PriceStrategy.COMPETITIVE => 18,
                PriceStrategy.VALUE_ADDED => 20,
                _ => 0
            },

            RestaurantType.FRANCES => price switch
            {
                PriceStrategy.COMPETITIVE => 16,
                PriceStrategy.VALUE_ADDED => 20,
                _ => 0
            },

            _ => 0
        };
    }

    private static bool HasRoleType(
     TeamSelectionData teamData,
     List<RoleData> allRoles,
     RoleType roleType)
    {
        foreach (var member in teamData.members)
        {
            var role = allRoles.FirstOrDefault(r => r.id == member.roleId);

            if (role != null &&
                role.roleType == roleType &&
                member.quantity > 0)
            {
                return true;
            }
        }

        return false;
    }

    private static int GetTeamScore(TeamSelectionData teamData,List<RoleData> allRoles)
    {
        bool hasSpecialist = HasRoleType(
            teamData,
            allRoles,
            RoleType.ESPECIALISTA
        );

        return hasSpecialist ? 20 : 5;
    }

    private static AlignmentClassification GetClassification(int totalScore)
    {
        if (totalScore >= 85)
            return AlignmentClassification.HIGH;

        if (totalScore >= 70)
            return AlignmentClassification.ADEQUATE;

        if (totalScore >= 50)
            return AlignmentClassification.FRAGILE;

        return AlignmentClassification.CRITICAL;
    }

    private static float GetFactor(AlignmentClassification classification)
    {
        return classification switch
        {
            AlignmentClassification.HIGH => 1.2f,
            AlignmentClassification.ADEQUATE => 1.0f,
            AlignmentClassification.FRAGILE => 0.75f,
            AlignmentClassification.CRITICAL => 0.45f,
            _ => 1.0f
        };
    }

    private static string GetMessage(AlignmentClassification classification)
    {
        return classification switch
        {
            AlignmentClassification.HIGH =>
                "Modelo coerente. A combinação tende a aumentar demanda, satisfação e reputação.",

            AlignmentClassification.ADEQUATE =>
                "Modelo viável. A combinação apresenta bom funcionamento, mas pode ter limitações.",

            AlignmentClassification.FRAGILE =>
                "Modelo frágil. A combinação apresenta riscos e pode reduzir a demanda.",

            AlignmentClassification.CRITICAL =>
                "Modelo crítico. A combinação tende a gerar baixa demanda e dificuldade financeira.",

            _ => ""
        };
    }
}
