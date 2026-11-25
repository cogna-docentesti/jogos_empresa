// Game/Domain/Simulation/DeterministicRandom.cs
using System;

namespace Game.Domain.Simulation
{
    /// <summary>
    /// Sorteios reproduzíveis. Mesma semente + mesma chave (mês, dia, assunto, índice) = mesmo número,
    /// não importa em que ordem as chamadas acontecem. Por isso sair e voltar no meio do mês
    /// repete os mesmos acontecimentos (pilar 4 do GDD).
    /// </summary>
    public sealed class DeterministicRandom
    {
        private readonly ulong _seed;

        public DeterministicRandom(int seed)
        {
            _seed = Mix((ulong)(uint)seed + 0x9E3779B97F4A7C15UL);
        }

        /// <summary>Número entre 0 (inclusive) e 1 (exclusive).</summary>
        public double Uniform(int month, int day, string topic, int index = 0)
        {
            ulong h = _seed;
            h = Mix(h ^ (ulong)(uint)month);
            h = Mix(h ^ ((ulong)(uint)day << 8));
            h = Mix(h ^ StableHash(topic));
            h = Mix(h ^ ((ulong)(uint)index << 16));
            return (h >> 11) * (1.0 / 9007199254740992.0); // 53 bits
        }

        /// <summary>true com a probabilidade informada.</summary>
        public bool Happens(double probability, int month, int day, string topic, int index = 0)
            => Uniform(month, day, topic, index) < probability;

        /// <summary>Normal padrão (média 0, desvio 1) pelo método de Box-Muller.</summary>
        public double StandardNormal(int month, int day, string topic, int index = 0)
        {
            double u1 = Math.Max(Uniform(month, day, topic, 2 * index), 1e-12);
            double u2 = Uniform(month, day, topic, 2 * index + 1);
            return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
        }

        /// <summary>Ruído multiplicativo em torno de 1 (lognormal). sigma = 0,12 no GDD.</summary>
        public double LogNormal(int month, int day, string topic, double sigma)
            => Math.Exp(sigma * StandardNormal(month, day, topic));

        // SplitMix64: espalha os bits para chaves parecidas darem números bem diferentes
        private static ulong Mix(ulong z)
        {
            z += 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        // FNV-1a: hash de texto que não muda entre execuções (string.GetHashCode muda)
        private static ulong StableHash(string text)
        {
            ulong h = 14695981039346656037UL;
            if (text == null) return h;
            foreach (char c in text) { h ^= c; h *= 1099511628211UL; }
            return h;
        }

        /// <summary>Semente estável a partir do sessionId.</summary>
        public static int SeedFromSessionId(string sessionId) => (int)(StableHash(sessionId) & 0x7FFFFFFF);
    }
}
