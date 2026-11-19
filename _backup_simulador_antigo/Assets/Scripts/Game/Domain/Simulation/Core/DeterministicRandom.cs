using System;
using System.Text;

namespace Game.Domain.Simulation
{
    /// <summary>
    /// Gerador aleatorio "sem estado" (counter-based).
    ///
    /// Cada numero sorteado depende so da semente e de uma chave, por exemplo
    /// (mes 2, dia 14, "evento", "freezer_breakdown", "unidade 1"). Consequencias:
    ///  - A mesma semente e as mesmas decisoes reproduzem exatamente o mesmo cenario.
    ///  - Acrescentar um evento novo ao catalogo nao muda o ruido de demanda nem os
    ///    sorteios dos outros eventos, porque cada sorteio tem a sua chave.
    ///  - Reiniciar um mes no meio nao permite "sortear de novo": os mesmos numeros voltam.
    ///
    /// Nao usa string.GetHashCode (que varia entre plataformas no .NET moderno).
    /// Usa FNV-1a 64 bits nas chaves e o finalizador SplitMix64.
    /// </summary>
    public readonly struct DeterministicRandom
    {
        public readonly ulong Seed;

        public DeterministicRandom(ulong seed)
        {
            Seed = seed;
        }

        public static DeterministicRandom FromSession(string sessionId, long seedOverride = 0)
        {
            if (seedOverride != 0) return new DeterministicRandom(unchecked((ulong)seedOverride));
            return new DeterministicRandom(Fnv(sessionId ?? "sessao", 1469598103934665603UL));
        }

        /// <summary>Numero uniforme em [0, 1) para a chave dada.</summary>
        public double Uniform(int month, int day, string stream, string key = null, int index = 0)
        {
            ulong h = Seed;
            h = Mix(h ^ (ulong)(uint)month * 0x9E3779B97F4A7C15UL);
            h = Mix(h ^ (ulong)(uint)day * 0xC2B2AE3D27D4EB4FUL);
            h = Fnv(stream ?? string.Empty, h);
            if (!string.IsNullOrEmpty(key)) h = Fnv(key, h);
            h = Mix(h ^ (ulong)(uint)index * 0x165667B19E3779F9UL);
            // 53 bits de mantissa: resultado exatamente representavel em double, sempre < 1.
            return (h >> 11) * (1.0 / 9007199254740992.0);
        }

        /// <summary>Normal padrao (media 0, desvio 1) por Box-Muller, com chaves derivadas.</summary>
        public double StandardNormal(int month, int day, string stream, string key = null, int index = 0)
        {
            double u1 = Uniform(month, day, stream, key, index * 2);
            double u2 = Uniform(month, day, stream, key, index * 2 + 1);
            if (u1 < 1e-12) u1 = 1e-12;
            return Math.Sqrt(-2d * Math.Log(u1)) * Math.Cos(2d * Math.PI * u2);
        }

        /// <summary>
        /// Ruido multiplicativo lognormal com media 1. sigma = 0,12 da cerca de 12% de
        /// variacao tipica entre dias parecidos.
        /// </summary>
        public double LogNormalNoise(double sigma, int month, int day, string stream, string key = null)
        {
            if (sigma <= 0d) return 1d;
            double z = StandardNormal(month, day, stream, key);
            return Math.Exp(sigma * z - 0.5d * sigma * sigma);
        }

        private static ulong Fnv(string text, ulong hash)
        {
            const ulong prime = 1099511628211UL;
            var bytes = Encoding.UTF8.GetBytes(text);
            for (int i = 0; i < bytes.Length; i++)
            {
                hash ^= bytes[i];
                hash = unchecked(hash * prime);
            }
            return Mix(hash);
        }

        private static ulong Mix(ulong z)
        {
            unchecked
            {
                z += 0x9E3779B97F4A7C15UL;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }
        }
    }
}
