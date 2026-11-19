using System.Collections.Generic;

namespace BattleHorizons.Core
{
    /// <summary>
    /// Deterministic, allocation-free pseudo-random stream (xoshiro128** with a
    /// splitmix32 seeding pass).
    ///
    /// validate_csharp.py rule 8 forbids unseeded `System.Random` and unseeded
    /// `UnityEngine.Random` in gameplay code: every match system (loot rolls,
    /// spawn points, safe-zone circles) must be reproducible from a seed so a
    /// bug report like "match 4711, seed 20261119" can be replayed exactly.
    /// Always use this class instead of UnityEngine.Random in gameplay code.
    /// </summary>
    public sealed class RandomStream
    {
        private uint _s0;
        private uint _s1;
        private uint _s2;
        private uint _s3;

        /// <summary>The seed this stream was initialised with (for save games / logs).</summary>
        public int Seed { get; private set; }

        public RandomStream(int seed)
        {
            Init(seed);
        }

        /// <summary>Restarts the stream from a seed; identical seeds give identical sequences.</summary>
        public void Init(int seed)
        {
            Seed = seed;
            var z = unchecked((uint)seed);
            _s0 = SplitMix(ref z);
            _s1 = SplitMix(ref z);
            _s2 = SplitMix(ref z);
            _s3 = SplitMix(ref z);
            if ((_s0 | _s1 | _s2 | _s3) == 0u)
            {
                // xoshiro must never reach the all-zero state.
                _s0 = 0x9E3779B9u;
            }
        }

        public uint NextUInt()
        {
            var result = Rotl(unchecked(_s1 * 5u), 7) * 9u;
            var t = _s1 << 9;
            _s2 ^= _s0;
            _s3 ^= _s1;
            _s1 ^= _s2;
            _s0 ^= _s3;
            _s2 ^= t;
            _s3 = Rotl(_s3, 11);
            return result;
        }

        /// <summary>Uniform float in [0, 1).</summary>
        public float NextFloat() => (NextUInt() >> 8) * (1.0f / 16777216.0f);

        /// <summary>Uniform float in [min, max).</summary>
        public float Range(float min, float max) => min + (max - min) * NextFloat();

        /// <summary>Uniform int in [minInclusive, maxInclusive]; returns min when max &lt; min.</summary>
        public int Range(int minInclusive, int maxInclusive)
        {
            if (maxInclusive <= minInclusive)
            {
                return minInclusive;
            }

            var span = (uint)(maxInclusive - minInclusive + 1);
            return minInclusive + (int)(NextUInt() % span);
        }

        /// <summary>True with the given probability (clamped to [0, 1]).</summary>
        public bool Chance(float probability)
        {
            if (probability <= 0f)
            {
                return false;
            }

            return probability >= 1f || NextFloat() < probability;
        }

        /// <summary>
        /// Index of one entry, each proportional to its weight. Non-positive
        /// total weight returns 0 so callers never index out of range.
        /// </summary>
        public int WeightedIndex(IList<float> weights)
        {
            if (weights == null || weights.Count == 0)
            {
                return -1;
            }

            var total = 0f;
            for (var i = 0; i < weights.Count; i++)
            {
                if (weights[i] > 0f)
                {
                    total += weights[i];
                }
            }

            if (total <= 0f)
            {
                return 0;
            }

            var roll = NextFloat() * total;
            for (var i = 0; i < weights.Count; i++)
            {
                if (weights[i] <= 0f)
                {
                    continue;
                }

                roll -= weights[i];
                if (roll <= 0f)
                {
                    return i;
                }
            }

            return weights.Count - 1;
        }

        /// <summary>Uniform pick from a list; default(T) when null/empty.</summary>
        public T Pick<T>(IList<T> items) =>
            items == null || items.Count == 0 ? default : items[Range(0, items.Count - 1)];

        /// <summary>Uniform point inside a unit XZ disc (used by the safe-zone shifter).</summary>
        public UnityEngine.Vector2 NextPointInUnitDisc()
        {
            var angle = Range(0f, UnityEngine.Mathf.PI * 2f);
            var distance = UnityEngine.Mathf.Sqrt(NextFloat());
            return new UnityEngine.Vector2(
                UnityEngine.Mathf.Cos(angle) * distance,
                UnityEngine.Mathf.Sin(angle) * distance);
        }

        private static uint Rotl(uint x, int k) => (x << k) | (x >> (32 - k));

        private static uint SplitMix(ref uint z)
        {
            z = unchecked(z + 0x9E3779B9u);
            var t = z ^ (z >> 16);
            t = unchecked(t * 0x85EBCA6Bu);
            var u = t ^ (t >> 13);
            u = unchecked(u * 0xC2B2AE35u);
            return u ^ (u >> 16);
        }
    }
}
