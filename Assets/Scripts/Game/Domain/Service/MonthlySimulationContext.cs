using System;
using System.Collections.Generic;

/// <summary>Inputs for a deterministic calculation. The engine never changes them.</summary>
public sealed class MonthlySimulationContext
{
    public GameSessionEntity Session;
    public RestaurantData Restaurant;
    public LocationData Location;
    public IReadOnlyList<RoleData> Roles = Array.Empty<RoleData>();
    public IReadOnlyList<EquipmentData> Equipment = Array.Empty<EquipmentData>();
    public CreditLineData CreditLine;
    public MonthlySimulationSettings Settings;
    // Recorded choices are pending settlement, not previously applied cash changes.
    public IReadOnlyList<SessionEventHistoryEntity> Events = Array.Empty<SessionEventHistoryEntity>();
}
