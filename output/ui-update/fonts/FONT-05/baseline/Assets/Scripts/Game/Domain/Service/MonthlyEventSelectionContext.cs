using System;
using System.Collections.Generic;

/// <summary>A snapshot supplied by the caller. Selection never changes these collections.</summary>
public sealed class MonthlyEventSelectionContext
{
    public int Month { get; set; }
    public GameSessionEntity Session { get; set; }
    public IReadOnlyList<EventData> EventCatalog { get; set; }
    public RestaurantData Restaurant { get; set; }
    public IReadOnlyList<EquipmentData> EquipmentCatalog { get; set; }
    public IEnumerable<string> OccurredEventIds { get; set; } = Array.Empty<string>();
    public IEnumerable<string> ReservedEventIds { get; set; } = Array.Empty<string>();
    public IEnumerable<EventExclusionGroup> UsedGroupsInMonth { get; set; } = Array.Empty<EventExclusionGroup>();
    // When topping up a month, include every already selected/occurred event of that month.
    public int AlreadySelectedCountInMonth { get; set; }
}

public sealed class MonthlyEventSelectionResult
{
    public bool IsValid { get; }
    public IReadOnlyList<EventData> Events { get; }
    public IReadOnlyList<string> Diagnostics { get; }
    public bool MinimumCountReached { get; }

    internal MonthlyEventSelectionResult(bool valid, List<EventData> events, List<string> diagnostics, bool minimumReached)
    {
        IsValid = valid;
        Events = events.AsReadOnly();
        Diagnostics = diagnostics.AsReadOnly();
        MinimumCountReached = minimumReached;
    }
}
