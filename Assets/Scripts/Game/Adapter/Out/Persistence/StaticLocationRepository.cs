using UnityEngine;
using System.Collections.Generic;
using Game.Domain.Entities;
using Game.Domain.Ports.Out;

namespace Game.Adapter.Out.Persistence
{
    public sealed class StaticLocationRepository : ILocationRepository
    {
        public LocationScreenData GetLocationScreenData(string regionId)
        {
            var establishments = new List<Establishment>
            {
                new Establishment(
                    id: "bank", name: "Banco",
                    description: "Solicitar empréstimos.",
                    segment: "Finanças", rentCost: 3500,
                    ticketCompat: 80, competitionLevel: 40,
                    channels: "Presencial",
                    isFixed: true, isUnlocked: true),

                new Establishment(
                    id: "store", name: "Loja do Empreendedor",
                    description: "Comprar equipamentos.",
                    segment: "Comércio", rentCost: 2200,
                    ticketCompat: 65, competitionLevel: 55,
                    channels: "Delivery, Presencial",
                    isFixed: true, isUnlocked: true),

                new Establishment(
                    id: "marketing", name: "Outdoor",
                    description: "Decisões de marketing.",
                    segment: "Serviços", rentCost: 1800,
                    ticketCompat: 50, competitionLevel: 30,
                    channels: "Mídia Física",
                    isFixed: true, isUnlocked: true),

                new Establishment(
                    id: "university", name: "Universidade",
                    description: "Público universitário.",
                    segment: "Educação", rentCost: 2800,
                    ticketCompat: 45, competitionLevel: 70,
                    channels: "Presencial, App",
                    isFixed: false, isUnlocked: false),

                new Establishment(
                    id: "condominium", name: "Condomínio",
                    description: "Público classe A.",
                    segment: "Residencial", rentCost: 4500,
                    ticketCompat: 90, competitionLevel: 25,
                    channels: "Delivery",
                    isFixed: false, isUnlocked: false),
            };

            return new LocationScreenData("Região Universitária", establishments);
        }
    }
}
