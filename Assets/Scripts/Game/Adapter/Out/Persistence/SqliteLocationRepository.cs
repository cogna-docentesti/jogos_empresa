using System;
using System.Collections.Generic;
using System.Linq;
using Game.Application.Ports.Out;
using Game.Domain.Entities;
using SQLite4Unity3d;
using UnityEngine;

namespace Game.Adapter.Out.Persistence
{
    /// <summary>
    /// E-06: localizacoes da D1 lidas do SQLite (tabela LocationEntity).
    ///
    /// Implementa a mesma porta ILocationRepository que o StaticLocationRepository.
    /// O LocationScreenController conhece so a interface, entao trocar a origem
    /// dos dados foi mudar uma linha no Awake dele.
    ///
    /// Carga inicial: se a tabela estiver vazia (primeira vez que o jogo abre
    /// com este codigo), copia os ScriptableObjects de Resources/Locations para
    /// o banco. Depois disso o banco e a fonte. Para levar uma mudanca feita nos
    /// assets para um banco que ja existe, use no editor
    /// Tools > Jogos de Empresa > Recarregar localizacoes no banco.
    /// </summary>
    public sealed class SqliteLocationRepository : ILocationRepository
    {
        private const string ResourcesFolder = "Locations";

        private readonly SQLiteConnection _db;

        public SqliteLocationRepository(SQLiteConnection db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _db.CreateTable<LocationEntity>();
            SeedIfEmpty(_db);
        }

        public LocationScreenData GetLocationScreenData(string regionId)
        {
            var rows = _db.Table<LocationEntity>()
                          .OrderBy(x => x.displayName)
                          .ToList();

            if (rows.Count == 0)
            {
                Debug.LogError("[SqliteLocationRepository] Tabela LocationEntity vazia e nenhum asset para copiar.");
                return new LocationScreenData("Erro", new List<Establishment>());
            }

            return new LocationScreenData("Localização", rows.Select(Map).ToList());
        }

        // ─────────────────────────────────────────────────────────────
        //  Carga a partir dos assets
        // ─────────────────────────────────────────────────────────────

        /// <summary>Copia os assets so se a tabela estiver vazia. Devolve quantas linhas gravou.</summary>
        public static int SeedIfEmpty(SQLiteConnection db)
        {
            if (db.Table<LocationEntity>().Count() > 0)
                return 0;

            int count = CopyAssetsToDatabase(db);
            if (count > 0)
                Debug.Log($"[SqliteLocationRepository] Primeira carga: {count} localizacoes copiadas dos assets para o banco.");
            return count;
        }

        /// <summary>
        /// Grava (InsertOrReplace) todas as localizacoes dos assets no banco.
        /// Linhas com o mesmo id sao sobrescritas; nada e apagado.
        /// </summary>
        public static int CopyAssetsToDatabase(SQLiteConnection db)
        {
            var assets = Resources.LoadAll<LocationData>(ResourcesFolder);
            if (assets == null || assets.Length == 0)
            {
                Debug.LogError("[SqliteLocationRepository] Nenhum LocationData em Resources/Locations/.");
                return 0;
            }

            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            int count = 0;

            db.RunInTransaction(() =>
            {
                foreach (var asset in assets)
                {
                    if (asset == null || string.IsNullOrWhiteSpace(asset.id))
                        continue;

                    db.InsertOrReplace(FromAsset(asset, now));
                    count++;
                }
            });

            return count;
        }

        public static LocationEntity FromAsset(LocationData a, long seededAt) => new LocationEntity
        {
            id                      = a.id,
            displayName             = a.displayName,
            zone                    = (int)a.zone,
            rent                    = a.rent,
            primarySegment          = (int)a.primarySegment,
            competitionLevel        = (int)a.competitionLevel,
            channels                = a.channels != null && a.channels.Length > 0
                                        ? string.Join(" · ", a.channels)
                                        : "-",
            coherenceLevel          = a.coherenceLevel,
            referencePriceFactor    = a.referencePriceFactor,
            initialPhysicalCapacity = a.initialPhysicalCapacity,
            baseDailyDemand         = a.baseDailyDemand,
            colorHex                = a.colorHex,
            description             = a.description ?? string.Empty,
            seededAt                = seededAt
        };

        // ─────────────────────────────────────────────────────────────
        //  Linha do banco -> entidade de dominio
        // ─────────────────────────────────────────────────────────────

        // Mesmo mapeamento do StaticLocationRepository, para a tela mostrar
        // exatamente o mesmo texto vindo de qualquer uma das duas fontes.
        private static Establishment Map(LocationEntity d) => new Establishment(
            id:                      d.id,
            name:                    d.displayName,
            description:             d.description ?? string.Empty,
            segment:                 FormatSegment((Segment)d.primarySegment),
            rentCost:                d.rent,
            initialPhysicalCapacity: d.initialPhysicalCapacity,
            baseDailyDemand:         d.baseDailyDemand,
            referencePriceFactor:    d.referencePriceFactor,
            competitionLevel:        FormatCompetition((CompetitionLevel)d.competitionLevel),
            channels:                string.IsNullOrEmpty(d.channels) ? "-" : d.channels,
            isFixed:                 false,
            isUnlocked:              true);

        private static string FormatSegment(Segment segment) => segment switch
        {
            Segment.LOW => "Classe C",
            Segment.MEDIUM => "Classe B",
            Segment.HIGH => "Classe A",
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
}
