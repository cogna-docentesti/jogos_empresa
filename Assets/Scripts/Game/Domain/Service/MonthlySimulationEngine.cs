using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>Calculates one month without database access, randomness or state changes.</summary>
public static class MonthlySimulationEngine
{
    public const int CycleMonths = 3;

    public static RoundResultEntity Calculate(MonthlySimulationContext context)
    {
        if (context == null) throw new ArgumentNullException(nameof(context));
        var session = context.Session;
        var restaurant = context.Restaurant;
        var location = context.Location;
        var settings = context.Settings;
        Require(session != null && !string.IsNullOrWhiteSpace(session.sessionId), "A session is required.");
        Require(session.currentRound >= 1 && session.currentRound <= CycleMonths, "Month must be between 1 and 3.");
        Require(restaurant != null && restaurant.type == session.restaurantType, "The restaurant catalog is missing or mismatched.");
        Require(location != null && location.zone == session.locationZone, "The location catalog is missing or mismatched.");
        Require(settings != null && settings.operatingDays >= 1 && settings.operatingDays <= 31, "Operating days must be between 1 and 31.");
        NonNegative(settings.priceElasticity, "Price elasticity");
        NonNegative(settings.minimumPriceDemandFactor, "Minimum price demand factor");
        NonNegative(settings.maximumPriceDemandFactor, "Maximum price demand factor");
        NonNegative(settings.minimumReputationDemandFactor, "Minimum reputation demand factor");
        NonNegative(settings.maximumReputationDemandFactor, "Maximum reputation demand factor");
        Require(settings.maximumPriceDemandFactor >= settings.minimumPriceDemandFactor
            && settings.maximumReputationDemandFactor >= settings.minimumReputationDemandFactor,
            "Demand factor limits are reversed.");
        Require(settings.satisfactoryServiceRatio >= 0f && settings.satisfactoryServiceRatio <= 1f,
            "The satisfactory service ratio must be between zero and one.");
        NonNegative(settings.qualityReputationScale, "Quality reputation scale");
        Finite(session.currentCash, "Opening cash");
        NonNegative(session.loanBalance, "Loan balance");
        Require(session.reputationScore >= 0 && session.reputationScore <= 100, "Reputation must be between zero and 100.");
        NonNegative(location.rent, "Rent");
        NonNegative(location.baseDailyDemand, "Daily demand");
        NonNegative(location.initialPhysicalCapacity, "Physical capacity");
        NonNegative(location.referencePriceFactor, "Location price factor");
        Require(location.referencePriceFactor > 0f, "The location price factor must be positive.");
        NonNegative(restaurant.baseMonthlyCost, "Utilities");
        NonNegative(restaurant.estimatedMonthlyCustomers, "Estimated monthly customers");

        var pricing = MenuPricingHelper.FromJson(session.menuPricingJson);
        var team = TeamSelectionHelper.FromJson(session.teamJson);
        var equipment = EquipmentSelectionHelper.FromJson(session.equipmentJson);
        Require(pricing.items != null, "The menu pricing list is missing.");
        var prices = new Dictionary<string, float>(StringComparer.Ordinal);
        foreach (var item in pricing.items)
        {
            Require(item != null && !string.IsNullOrWhiteSpace(item.productId), "A menu entry is invalid.");
            NonNegative(item.selectedPrice, "Selected price");
            Require(!prices.ContainsKey(item.productId), "A product is priced more than once.");
            prices.Add(item.productId, item.selectedPrice);
        }

        Require(restaurant.products != null && restaurant.products.Length > 0, "The restaurant has no products.");
        double priceSum = 0d, referenceSum = 0d, supplySum = 0d;
        var productIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var product in restaurant.products)
        {
            Require(product != null && !string.IsNullOrWhiteSpace(product.id) && productIds.Add(product.id), "A product is missing or duplicated.");
            Require(prices.TryGetValue(product.id, out float price), "A product has no saved price: " + product.id);
            Require(product.IsPriceInRange(price), "A product price is outside its configured range: " + product.id);
            NonNegative(product.price, "Reference price");
            Require(product.price > 0, "Reference prices must be positive.");
            Require(product.inputCostRatio >= 0f && product.inputCostRatio <= 1f, "Input cost ratios must be between zero and one.");
            priceSum += price;
            referenceSum += product.price;
            supplySum += price * (double)product.inputCostRatio;
        }
        Require(prices.Count == productIds.Count, "The saved menu contains products from another restaurant.");
        double ticket = priceSum / productIds.Count;
        Require(ticket > 0d, "The average ticket must be positive.");
        double referenceTicket = referenceSum / productIds.Count * location.referencePriceFactor;
        double priceFactor = Clamp(Math.Pow(referenceTicket / ticket, settings.priceElasticity),
            settings.minimumPriceDemandFactor, settings.maximumPriceDemandFactor);
        double reputationFactor = settings.minimumReputationDemandFactor
            + (settings.maximumReputationDemandFactor - settings.minimumReputationDemandFactor) * session.reputationScore / 100d;

        var roles = Catalog(context.Roles, item => item.id, "role");
        var equipmentCatalog = Catalog(context.Equipment, item => item.id, "equipment");
        var hired = new Dictionary<string, int>(StringComparer.Ordinal);
        double salaries = 0d, teamCapacity = 0d, quality = 0d;
        foreach (var member in team.members)
        {
            Require(member != null && member.quantity >= 0 && !string.IsNullOrWhiteSpace(member.roleId), "A team entry is invalid.");
            Require(!hired.ContainsKey(member.roleId), "A role is selected more than once.");
            hired.Add(member.roleId, member.quantity);
            if (member.quantity == 0) continue;
            Require(roles.TryGetValue(member.roleId, out var role), "A selected role is missing: " + member.roleId);
            NonNegative(role.salary, "Salary");
            NonNegative(role.maxClientsSupported, "Role capacity");
            NonNegative(role.qualityContribution, "Role quality");
            Require(role.qualityContribution <= 1f, "Role quality cannot exceed one.");
            salaries += role.salary * (double)member.quantity;
            teamCapacity += role.maxClientsSupported * (double)member.quantity;
            quality += role.qualityContribution * (double)member.quantity;
        }

        double equipmentCapacity = 0d;
        var ownedIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (string id in equipment.equipmentIds)
        {
            Require(!string.IsNullOrWhiteSpace(id), "An equipment selection has no ID.");
            if (!ownedIds.Add(id)) continue;
            Require(equipmentCatalog.TryGetValue(id, out var item), "Selected equipment is missing: " + id);
            if (item.applicableTypes != null && item.applicableTypes.Length > 0
                && !item.applicableTypes.Contains(session.restaurantType)) continue;
            NonNegative(item.capacityBonus, "Equipment capacity");
            NonNegative(item.qualityBonus, "Equipment quality");
            Require(item.qualityBonus <= 1f, "Equipment quality cannot exceed one.");
            equipmentCapacity += item.capacityBonus;
            quality += item.qualityBonus;
        }

        // Recompute alignment so monthly price and team changes affect the calculation.
        var alignment = AlignmentEngine.Calculate(session.restaurantType, session.targetSegment,
            session.locationZone, restaurant, pricing, team, roles.Values.ToList());
        double coverage = RequiredTeamCoverage(restaurant.requiredRoles, hired);
        if (restaurant.requiredEquipmentIds != null
            && restaurant.requiredEquipmentIds.Any(id => !ownedIds.Contains(id))) coverage = 0d;
        double physicalCapacity = location.initialPhysicalCapacity * (double)settings.operatingDays + equipmentCapacity;
        double capacity = teamCapacity > 0d ? Math.Min(physicalCapacity, teamCapacity + equipmentCapacity) * coverage : 0d;
        double baseDemand = location.baseDailyDemand * (double)settings.operatingDays;
        if (restaurant.estimatedMonthlyCustomers > 0)
            baseDemand = Math.Min(baseDemand, restaurant.estimatedMonthlyCustomers);
        double monthlyDemand = baseDemand * alignment.alignmentFactor * priceFactor * reputationFactor;

        var events = ReadEvents(context, settings.operatingDays);
        double potential = 0d, served = 0d, revenue = 0d, supplies = 0d;
        double cashImpact = 0d;
        long eventReputation = 0;
        foreach (var item in events)
        {
            cashImpact += item.History.cashChange;
            eventReputation += item.History.reputationChange;
        }
        for (int day = 1; day <= settings.operatingDays; day++)
        {
            double demandMultiplier = 1d, capacityMultiplier = 1d, stockMultiplier = 1d;
            double costMultiplier = 1d, ticketMultiplier = 1d, extraClients = 0d;
            foreach (var item in events)
            {
                if (item.History.day == day) extraClients += item.History.clientsChange;
                if (day < item.History.day || (item.Effect.duration != EventEffectDuration.CURRENT_MONTH && day != item.History.day)) continue;
                demandMultiplier *= item.Effect.demandMultiplier;
                capacityMultiplier *= item.Effect.capacityMultiplier;
                stockMultiplier *= item.Effect.stockMultiplier;
                costMultiplier *= item.Effect.ingredientCostMultiplier;
                ticketMultiplier *= item.Effect.averageTicketMultiplier;
            }
            double demand = Math.Max(0d, monthlyDemand / settings.operatingDays * demandMultiplier + extraClients);
            double dailyCapacity = capacity / settings.operatingDays * capacityMultiplier * stockMultiplier;
            double clients = Math.Min(demand, dailyCapacity);
            potential += demand;
            served += clients;
            revenue += clients * ticket * ticketMultiplier;
            supplies += clients * supplySum / productIds.Count * costMultiplier;
        }

        int customers = WholeCustomers(served);
        // Allocate only whole customers while preserving the relative daily sales mix.
        double wholeCustomerRatio = served > 0d ? customers / served : 0d;
        float grossRevenue = Money(revenue * wholeCustomerRatio);
        float supplyCost = Money(supplies * wholeCustomerRatio);
        float payroll = Money(salaries);
        float loanPrincipal = 0f, loanPayment = 0f;
        if (session.loanBalance > 0f)
        {
            Require(context.CreditLine != null && context.CreditLine.id == session.creditLineId, "The active credit line is missing.");
            NonNegative(context.CreditLine.monthlyInterestRate, "Monthly interest rate");
            loanPrincipal = session.currentRound == CycleMonths ? session.loanBalance
                : Money(LoanService.PrincipalPayment(session.loanBalance, session.currentRound));
            loanPayment = Money(loanPrincipal + session.loanBalance * (double)context.CreditLine.monthlyInterestRate);
        }
        float eventCash = Money(cashImpact);
        float net = Money((double)grossRevenue - supplyCost - location.rent - payroll - restaurant.baseMonthlyCost - loanPayment + eventCash);
        double serviceRatio = potential > 0d ? customers / potential : 0d;
        long operationalReputation = potential > 0d && serviceRatio >= settings.satisfactoryServiceRatio
            ? settings.satisfactoryServiceReputationGain + (long)Math.Round(Clamp(quality, 0d, 1d) * settings.qualityReputationScale, MidpointRounding.AwayFromZero)
            : settings.poorServiceReputationPenalty;
        int reputationAtEnd = (int)Clamp(session.reputationScore + eventReputation + operationalReputation, 0d, 100d);

        return new RoundResultEntity
        {
            sessionId = session.sessionId, round = session.currentRound,
            baseDemand = WholeCustomers(baseDemand), potentialDemand = WholeCustomers(potential),
            serviceCapacity = WholeCustomers(capacity), customers = customers,
            averageTicket = customers > 0 ? Money((double)grossRevenue / customers) : Money(ticket),
            grossRevenue = grossRevenue, supplyCost = supplyCost, rent = location.rent,
            salaries = payroll, utilities = restaurant.baseMonthlyCost,
            loanPayment = loanPayment, loanPrincipalPayment = loanPrincipal,
            thirteenthSalary = 0f, eventCashImpact = eventCash,
            netResult = net, openingCash = session.currentCash, closingCash = Money((double)session.currentCash + net),
            reputationAtStart = session.reputationScore, reputationAtEnd = reputationAtEnd,
            reputationDelta = reputationAtEnd - session.reputationScore,
            coherenceFactor = alignment.alignmentFactor,
            eventId = events.Count == 1 ? events[0].History.eventId : null,
            eventChoice = -1,
            alignmentScore = alignment.totalScore,
            alignmentClassification = alignment.classification.ToString()
        };
    }

    private sealed class RecordedEffect
    {
        public SessionEventHistoryEntity History;
        public EventEffectData Effect;
    }

    private static List<RecordedEffect> ReadEvents(MonthlySimulationContext context, int days)
    {
        var result = new List<RecordedEffect>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var history in context.Events ?? Array.Empty<SessionEventHistoryEntity>())
        {
            Require(history != null && history.sessionId == context.Session.sessionId && history.round == context.Session.currentRound,
                "Event history belongs to another session or month.");
            Require(!string.IsNullOrWhiteSpace(history.historyId) && ids.Add(history.historyId), "Event history is missing an ID or duplicated.");
            Require(history.day >= 1 && history.day <= days, "An event day is outside the operating month.");
            Finite(history.cashChange, "Event cash change");
            var effect = string.IsNullOrWhiteSpace(history.effectsJson) ? new EventEffectData()
                : JsonUtility.FromJson<EventEffectData>(history.effectsJson);
            Require(effect != null && Enum.IsDefined(typeof(EventEffectDuration), effect.duration), "An event effect is invalid.");
            NonNegative(effect.demandMultiplier, "Event demand multiplier");
            NonNegative(effect.capacityMultiplier, "Event capacity multiplier");
            NonNegative(effect.stockMultiplier, "Event stock multiplier");
            NonNegative(effect.ingredientCostMultiplier, "Event ingredient cost multiplier");
            NonNegative(effect.averageTicketMultiplier, "Event ticket multiplier");
            result.Add(new RecordedEffect { History = history, Effect = effect });
        }
        return result.OrderBy(item => item.History.day).ThenBy(item => item.History.historyId, StringComparer.Ordinal).ToList();
    }

    private static Dictionary<string, T> Catalog<T>(IReadOnlyList<T> items, Func<T, string> id, string label) where T : UnityEngine.Object
    {
        var result = new Dictionary<string, T>(StringComparer.Ordinal);
        foreach (var item in items ?? Array.Empty<T>())
        {
            Require(item != null && !string.IsNullOrWhiteSpace(id(item)), "An invalid " + label + " catalog entry was found.");
            Require(!result.ContainsKey(id(item)), "A duplicate " + label + " ID was found: " + id(item));
            result.Add(id(item), item);
        }
        return result;
    }

    private static double RequiredTeamCoverage(RoleRequirement[] requirements, Dictionary<string, int> hired)
    {
        double coverage = 1d;
        var required = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var requirement in requirements ?? Array.Empty<RoleRequirement>())
        {
            Require(requirement != null && requirement.quantity >= 0 && !string.IsNullOrWhiteSpace(requirement.roleId), "A role requirement is invalid.");
            required.TryGetValue(requirement.roleId, out long previous);
            required[requirement.roleId] = previous + requirement.quantity;
        }
        foreach (var requirement in required)
        {
            if (requirement.Value == 0) continue;
            hired.TryGetValue(requirement.Key, out int quantity);
            coverage = Math.Min(coverage, quantity / (double)requirement.Value);
        }
        return coverage;
    }

    private static int WholeCustomers(double value)
    {
        Require(!double.IsNaN(value) && !double.IsInfinity(value) && value >= 0d && value <= int.MaxValue, "Customer count exceeds its supported range.");
        return (int)Math.Floor(value + 1e-7d);
    }

    private static float Money(double value)
    {
        Require(!double.IsNaN(value) && !double.IsInfinity(value) && Math.Abs(value) <= float.MaxValue, "A monetary value exceeds its supported range.");
        return (float)Math.Round(value, 2, MidpointRounding.AwayFromZero);
    }

    private static double Clamp(double value, double minimum, double maximum) => Math.Max(minimum, Math.Min(maximum, value));
    private static void NonNegative(double value, string label) { Finite(value, label); Require(value >= 0d, label + " cannot be negative."); }
    private static void Finite(double value, string label) => Require(!double.IsNaN(value) && !double.IsInfinity(value), label + " must be finite.");
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
