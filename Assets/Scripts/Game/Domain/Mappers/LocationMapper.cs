// LocationMapper.cs
using System.Collections.Generic;
using System.Linq;
using Game.Domain.Entities;

public static class LocationMapper
{

    public static Establishment ToEstablishment(LocationData data)
    {
        if (data == null) return null;

        string channels    = data.channels != null
                             ? string.Join(" · ", data.channels)
                             : "—";


        int ticketCompat   = (data.ticketCompatibleMin + data.ticketCompatibleMax) / 2;


        int competitionLevel = data.coherenceLevel;


        bool isFixed    = false;
        bool isUnlocked = true;

        return new Establishment(
            id:               data.id,
            name:             data.displayName,
            description:      data.description ?? string.Empty,
            segment:          data.primarySegment.ToString(),
            rentCost:         data.rent,
            ticketCompat:     ticketCompat,
            competitionLevel: competitionLevel,
            channels:         channels,
            isFixed:          isFixed,
            isUnlocked:       isUnlocked
        );
    }

    public static LocationScreenData ToScreenData(
        string regionName,
        IEnumerable<LocationData> locations)
    {
        var establishments = locations
            .Where(l => l != null)
            .Select(ToEstablishment)
            .ToList();

        return new LocationScreenData(regionName, establishments);
    }
}