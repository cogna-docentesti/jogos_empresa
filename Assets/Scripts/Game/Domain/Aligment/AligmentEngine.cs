using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class AlignmentEngine
{
    public static AlignmentResult Calculate(
        RestaurantType restaurantType,
        Segment targetSegment,
        LocationZone locationZone,
        RestaurantData restaurantData,
        MenuPricingData menuPricing,
        TeamSelectionData teamData,
        List<RoleData> availableRoles
    )
    {
        int restaurantTargetScore = GetRestaurantTargetScore(restaurantType, targetSegment);
        int restaurantLocationScore = GetRestaurantLocationScore(restaurantType, locationZone);
        int restaurantPriceScore = GetRestaurantPriceScore(restaurantType, restaurantData, menuPricing);
        int restaurantTeamScore = GetTeamScore(restaurantType, restaurantData, teamData, availableRoles);

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
                LocationZone.Comercio => 20,
                LocationZone.Educacao => 18,
                LocationZone.Servicos => 14,
                LocationZone.Financas => 10,
                LocationZone.Residencial => 8,
                _ => 0
            },

            RestaurantType.JAPONES => location switch
            {
                LocationZone.Financas => 20,
                LocationZone.Educacao => 15,
                LocationZone.Comercio => 14,
                LocationZone.Servicos => 12,
                LocationZone.Residencial => 10,
                _ => 0
            },

            RestaurantType.FRANCES => location switch
            {
                LocationZone.Financas => 20,
                LocationZone.Residencial => 16,
                LocationZone.Servicos => 12,
                LocationZone.Comercio => 10,
                LocationZone.Educacao => 6,
                _ => 0
            },

            _ => 0
        };
    }

    private static int GetRestaurantPriceScore(
        RestaurantType restaurant,
        RestaurantData restaurantData,
        MenuPricingData pricing
    )
    {
        if (restaurantData?.products == null || restaurantData.products.Length == 0)
            return 10;

        float total = 0f;
        int count = 0;

        foreach (var product in restaurantData.products)
        {
            if (product == null)
                continue;

            float selectedPrice = product.price;
            if (MenuPricingHelper.TryGetPrice(pricing, product.id, out float savedPrice))
                selectedPrice = savedPrice;

            total += GetProductPriceScore(restaurant, product, selectedPrice);
            count++;
        }

        return count > 0 ? Mathf.RoundToInt(total / count) : 10;
    }

    private static int GetProductPriceScore(RestaurantType restaurant, ProductData product, float selectedPrice)
    {
        if (product.maxPrice <= product.minPrice)
            return 10;

        float normalizedPrice = Mathf.InverseLerp(
            product.minPrice,
            product.maxPrice,
            product.ClampPrice(selectedPrice)
        );

        return restaurant switch
        {
            RestaurantType.PODRAO => normalizedPrice switch
            {
                <= 0.35f => 20,
                <= 0.60f => 14,
                _ => 8
            },

            RestaurantType.JAPONES => normalizedPrice switch
            {
                >= 0.20f and <= 0.75f => 20,
                > 0.75f => 16,
                _ => 14
            },

            RestaurantType.FRANCES => normalizedPrice switch
            {
                >= 0.55f => 20,
                >= 0.35f => 14,
                _ => 8
            },

            _ => 0
        };
    }

    private static bool HasRoleType(
        TeamSelectionData teamData,
        List<RoleData> allRoles,
        RoleType roleType)
    {
        if (teamData?.members == null || allRoles == null)
            return false;

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

    private static bool HasRoleId(TeamSelectionData teamData, string roleId)
    {
        if (teamData?.members == null || string.IsNullOrWhiteSpace(roleId))
            return false;

        return teamData.members.Any(member =>
            member.roleId == roleId &&
            member.quantity > 0
        );
    }

    private static int GetTeamScore(
        RestaurantType restaurant,
        RestaurantData restaurantData,
        TeamSelectionData teamData,
        List<RoleData> allRoles
    )
    {
        if (restaurantData?.requiredRoles != null && restaurantData.requiredRoles.Length > 0)
        {
            bool hasRequiredRoles = restaurantData.requiredRoles.All(requirement =>
                requirement == null ||
                requirement.quantity <= 0 ||
                HasRoleId(teamData, requirement.roleId)
            );

            return hasRequiredRoles ? 20 : 5;
        }

        RoleType specialistRole = restaurant switch
        {
            RestaurantType.PODRAO => RoleType.CHAPEIRO,
            RestaurantType.JAPONES => RoleType.SUSHIMAN,
            RestaurantType.FRANCES => RoleType.CHEF,
            _ => RoleType.ATENDENTE
        };

        return HasRoleType(teamData, allRoles, specialistRole) ? 20 : 5;
    }

    private static AlignmentClassification GetClassification(int totalScore)
    {
        if (totalScore >= 70)
            return AlignmentClassification.HIGH;

        if (totalScore >= 55)
            return AlignmentClassification.ADEQUATE;

        if (totalScore >= 35)
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
                "Modelo coerente. A combinacao tende a aumentar demanda, satisfacao e reputacao.",

            AlignmentClassification.ADEQUATE =>
                "Modelo viavel. A combinacao apresenta bom funcionamento, mas pode ter limitacoes.",

            AlignmentClassification.FRAGILE =>
                "Modelo fragil. A combinacao apresenta riscos e pode reduzir a demanda.",

            AlignmentClassification.CRITICAL =>
                "Modelo critico. A combinacao tende a gerar baixa demanda e dificuldade financeira.",

            _ => ""
        };
    }
}
