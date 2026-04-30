// Game/Adapter/Out/Persistence/StaticLocationRepository.cs
using System.Collections.Generic;
using System.Linq;
using Game.Domain.Entities;
using Game.Application.Ports.Out;
using UnityEngine;

namespace Game.Adapter.Out.Persistence
{
    /// <summary>
    /// Lê LocationData ScriptableObjects de Resources/Locations/ e os
    /// converte para domain entities. O Controller e a View nunca
    /// tocam no ScriptableObject — só veem Establishment.
    ///
    /// Para migrar para SQLite na Fase 2: crie SqliteLocationRepository
    /// implementando ILocationRepository e troque no Controller.
    /// Nenhuma outra classe precisa mudar.
    /// </summary>
    public sealed class StaticLocationRepository : ILocationRepository
    {
        private static LocationScreenData _cache;

        public LocationScreenData GetLocationScreenData(string regionId)
        {
            if (_cache != null) return _cache;

            LocationData[] all = Resources.LoadAll<LocationData>("Locations");

            if (all == null || all.Length == 0)
            {
                Debug.LogError(
                    "[StaticLocationRepository] Nenhum LocationData encontrado " +
                    "em Assets/Resources/Locations/. Verifique os assets.");

                _cache = new LocationScreenData("Erro", new List<Establishment>());
                return _cache;
            }

            var establishments = all
                .OrderBy(d => d.displayName)
                .Select(MapToEstablishment)
                .ToList();

            _cache = new LocationScreenData("Selecione uma Localização", establishments);

            Debug.Log($"[StaticLocationRepository] {establishments.Count} localizações carregadas.");
            return _cache;
        }

        // ── Mapping ───────────────────────────────────────────────────────
        // Toda a lógica de transformação ScriptableObject → Establishment
        // fica aqui. A View nunca sabe que ScriptableObject existe.

        private static Establishment MapToEstablishment(LocationData d)
        {
            string channels = d.channels != null && d.channels.Length > 0
                ? string.Join(" · ", d.channels)
                : "—";

            // Ticket médio do range configurado no ScriptableObject
            int ticketMid = (d.ticketCompatibleMin + d.ticketCompatibleMax) / 2;

            // coherenceLevel (1-3) → escala 0-100 para a UI
            int competition = d.coherenceLevel switch
            {
                1 => 25,
                2 => 55,
                3 => 85,
                _ => 0
            };

            return new Establishment(
                id:               d.id,
                name:             d.displayName,
                description:      d.description ?? string.Empty,
                segment:          d.primarySegment.ToString(),
                rentCost:         d.rent,
                ticketCompat:     ticketMid,
                competitionLevel: competition,
                channels:         channels,
                isFixed:          false,
                isUnlocked:       true
            );
        }

        public static void ClearCache() => _cache = null;
    }
}