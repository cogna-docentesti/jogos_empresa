using System;
using System.Collections.Generic;

namespace Game.Domain.Simulation
{
    public static class Bottleneck
    {
        public const string Front = "FRONT";
        public const string Kitchen = "KITCHEN";
        public const string Physical = "PHYSICAL";
        public const string Stock = "STOCK";
        public const string None = "NONE";
    }

    public sealed class CapacityQuery
    {
        public RestaurantSpec Restaurant;
        public LocationSpec Location;
        /// <summary>Pessoas trabalhando hoje por cargo (ja sem vagas abertas e sem faltas nao cobertas).</summary>
        public IReadOnlyDictionary<string, double> WorkingPeople;
        public SimulationCatalog Catalog;
        public double Productivity = 1d;
        public double FrontFactor = 1d;
        public double KitchenFactor = 1d;
        public double PhysicalFactor = 1d;
        /// <summary>Fator de disponibilidade dos equipamentos sobre cada frente (1 = tudo funcionando).</summary>
        public double EquipmentFrontFactor = 1d;
        public double EquipmentKitchenFactor = 1d;
        public double EquipmentPhysicalFactor = 1d;
        public double EquipmentKitchenBonus;
        public double EquipmentPhysicalBonus;
    }

    public sealed class CapacityResult
    {
        public double Front;
        public double Kitchen;
        public double Physical;
        public double Capacity;
        public string Bottleneck;
    }

    /// <summary>
    /// Capacidade maxima diaria = menor valor entre atendimento, cozinha e salao (Bloco 4).
    /// Atendimento e cozinha dependem de quantas pessoas trabalham hoje, da capacidade de
    /// cada cargo e da produtividade; o salao vem da localizacao e do mobiliario.
    /// </summary>
    public static class OperationsModel
    {
        public static CapacityResult Capacity(CapacityQuery q)
        {
            double front = 0d, kitchen = 0d;
            if (q.WorkingPeople != null)
            {
                foreach (var pair in q.WorkingPeople)
                {
                    if (pair.Value <= 0d) continue;
                    if (!q.Catalog.Roles.TryGetValue(pair.Key, out var role) || role.Profile == null) continue;
                    double perPerson = role.Profile.CapacityFor(q.Restaurant.TypeId);
                    if (role.Profile.function == RoleFunction.Front) front += pair.Value * perPerson;
                    else if (role.Profile.function == RoleFunction.Kitchen) kitchen += pair.Value * perPerson;
                }
            }

            front *= q.Productivity * q.FrontFactor * q.EquipmentFrontFactor;
            kitchen = (kitchen * q.Productivity + Math.Max(0d, q.EquipmentKitchenBonus)) * q.KitchenFactor * q.EquipmentKitchenFactor;
            // Sem atendimento ou sem producao o restaurante nao vende, mesmo com equipamento.
            front = Math.Max(0d, front);
            kitchen = Math.Max(0d, kitchen);
            double physical = (q.Location.PhysicalCapacity + Math.Max(0d, q.EquipmentPhysicalBonus)) * q.PhysicalFactor * q.EquipmentPhysicalFactor;

            var result = new CapacityResult { Front = front, Kitchen = kitchen, Physical = physical };
            result.Capacity = Math.Max(0d, Math.Min(front, Math.Min(kitchen, physical)));
            if (result.Capacity == front) result.Bottleneck = Bottleneck.Front;
            else if (result.Capacity == kitchen) result.Bottleneck = Bottleneck.Kitchen;
            else result.Bottleneck = Bottleneck.Physical;
            return result;
        }

        /// <summary>
        /// Produtividade = interpolacao pela moral x (1 + experiencia) x (1 + bonus de gestao) x alavancas.
        /// Moral 65 e sem experiencia: cerca de 1,05.
        /// </summary>
        public static double Productivity(StaffParameters p, double morale, double experienceMonths, double managerBonus, double leverFactor)
        {
            double byMorale = SimMath.Lerp(p.productivityAtZeroMorale, p.productivityAtFullMorale, SimMath.Clamp(morale, 0d, 100d) / 100d);
            double experience = Math.Min(p.experienceGainCap, Math.Max(0d, experienceMonths) * p.experienceGainPerMonth);
            return byMorale * (1d + experience) * (1d + managerBonus) * leverFactor;
        }

        /// <summary>
        /// Tempo medio de atendimento: constante ate 75% de ocupacao e crescente depois.
        /// Em 100% de ocupacao e 1,8x o tempo base; em 125%, 2,6x.
        /// </summary>
        public static double WaitMinutes(double baseMinutes, double utilization)
        {
            double u = SimMath.Clamp(utilization, 0d, 2d);
            return baseMinutes * (1d + 3.2d * Math.Max(0d, u - 0.75d));
        }

        /// <summary>Taxa de erros nos pedidos: base x fator (treinamento) + inclinacao x sobrecarga.</summary>
        public static double ErrorRate(StaffParameters p, double leverFactor, double utilization)
        {
            return SimMath.Clamp(p.baseErrorRate * leverFactor + p.overloadErrorSlope * Math.Max(0d, utilization - 1d), 0d, 0.6d);
        }

        /// <summary>Fator de disponibilidade de uma categoria: 1 - (1 - fator_parado) x fracao_parada.</summary>
        public static double AvailabilityFactor(int owned, int working, double factorWhenDown, double factorWhenMissing)
        {
            if (owned <= 0) return factorWhenMissing;
            double brokenShare = (owned - Math.Max(0, Math.Min(owned, working))) / (double)owned;
            return 1d - (1d - factorWhenDown) * brokenShare;
        }

        /// <summary>Folha mensal = soma(salario x quantidade) x encargos x politica de remuneracao.</summary>
        public static double MonthlyPayroll(SimulationCatalog catalog, IReadOnlyDictionary<string, int> team, double chargesMultiplier, double policyFactor)
        {
            double total = 0d;
            if (team == null) return 0d;
            foreach (var pair in team)
            {
                if (pair.Value <= 0) continue;
                if (!catalog.Roles.TryGetValue(pair.Key, out var role)) continue;
                total += role.Salary * pair.Value;
            }
            return total * chargesMultiplier * policyFactor;
        }
    }
}
