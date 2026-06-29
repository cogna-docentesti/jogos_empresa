// Game/Adapter/Out/Persistence/StaticLocationRepository.cs
using System.Collections.Generic;
using System.Linq;
using Game.Domain.Entities;
using Game.Application.Ports.Out;
using UnityEngine;

namespace Game.Adapter.Out.Persistence
{
    public sealed class StaticLocationRepository : ILocationRepository
    {
        private static LocationScreenData _cache;

        public LocationScreenData GetLocationScreenData(string regionId)
        {
            if (_cache != null)
                return _cache;

            LocationData[] all = Resources.LoadAll<LocationData>("Locations");

            if (all == null || all.Length == 0)
            {
                Debug.LogError("[StaticLocationRepository] Nenhum LocationData encontrado em Assets/Resources/Locations/.");
                _cache = new LocationScreenData("Erro", new List<Establishment>());
                return _cache;
            }

            var establishments = all
                .OrderBy(d => d.displayName)
                .Select(MapToEstablishment)
                .ToList();

            _cache = new LocationScreenData("Localização", establishments);

            Debug.Log($"[StaticLocationRepository] {establishments.Count} localizacoes carregadas.");
            return _cache;
        }

        private static Establishment MapToEstablishment(LocationData d)
        {
            string channels = d.channels != null && d.channels.Length > 0
                ? string.Join(" · ", d.channels)
                : "-";

            return new Establishment(
                id: d.id,
                name: d.displayName,
                description: d.description ?? string.Empty,
                segment: FormatSegment(d.primarySegment),
                rentCost: d.rent,
                initialPhysicalCapacity: d.initialPhysicalCapacity,
                baseDailyDemand: d.baseDailyDemand,
                referencePriceFactor: d.referencePriceFactor,
                competitionLevel: FormatCompetition(d.competitionLevel),
                channels: channels,
                isFixed: false,
                isUnlocked: true
            );
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

        public static void ClearCache() => _cache = null;
    }
}
