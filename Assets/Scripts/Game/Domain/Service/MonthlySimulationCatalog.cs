using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class MonthlySimulationCatalog
{
    public static MonthlySimulationContext Load(GameSessionEntity session,
        IReadOnlyList<SessionEventHistoryEntity> events = null)
    {
        if (session == null) throw new ArgumentNullException(nameof(session));
        return new MonthlySimulationContext
        {
            Session = session,
            Restaurant = Resources.LoadAll<RestaurantData>("Restaurants")
                .SingleOrDefault(item => item.type == session.restaurantType),
            Location = Resources.LoadAll<LocationData>("Locations")
                .SingleOrDefault(item => item.zone == session.locationZone),
            Roles = Resources.LoadAll<RoleData>("Roles"),
            Equipment = Resources.LoadAll<EquipmentData>("Equipments"),
            CreditLine = LoanService.FindLine(session.creditLineId),
            Settings = Resources.Load<MonthlySimulationSettings>("MonthlySimulation/DefaultSettings"),
            Events = events ?? Array.Empty<SessionEventHistoryEntity>()
        };
    }
}
