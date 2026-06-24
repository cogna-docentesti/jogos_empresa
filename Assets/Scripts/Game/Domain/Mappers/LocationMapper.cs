// LocationMapper.cs
using System.Collections.Generic;
using System.Linq;
using Game.Domain.Entities;

public static class LocationMapper
{
    public static Establishment ToEstablishment(LocationData data)
    {
        if (data == null)
            return null;

        string channels = data.channels != null && data.channels.Length > 0
            ? string.Join(" · ", data.channels)
            : "-";

        return new Establishment(
            id: data.id,
            name: data.displayName,
            description: data.description ?? string.Empty,
            segment: FormatSegment(data.primarySegment),
            rentCost: data.rent,
            initialPhysicalCapacity: data.initialPhysicalCapacity,
            baseDailyDemand: data.baseDailyDemand,
            referencePriceFactor: data.referencePriceFactor,
            competitionLevel: FormatCompetition(data.competitionLevel),
            channels: channels,
            isFixed: false,
            isUnlocked: true
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

    private static string FormatSegment(Segment segment) => segment switch
    {
        Segment.LOW => "Classe baixa",
        Segment.MEDIUM => "Classe media",
        Segment.HIGH => "Classe alta",
        _ => segment.ToString()
    };

    private static string FormatCompetition(CompetitionLevel level) => level switch
    {
        CompetitionLevel.LOW => "Baixa",
        CompetitionLevel.MEDIUM => "Media",
        CompetitionLevel.HIGH => "Alta",
        _ => level.ToString()
    };
}
