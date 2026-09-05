using System;

namespace Match3Lab.Core
{
    /// <summary>
    /// PCG-XSH-RR 32-bit generator (O'Neill, 2014). Used instead of <see cref="System.Random"/>
    /// because the same seed must produce the same game in Unity's Mono/IL2CPP runtime and in
    /// the .NET test runner — and <c>System.Random</c>'s seeded algorithm is not guaranteed to
    /// match across runtimes. Small, fast, and its output is verifiable against published
    /// reference values (see the tests).
    /// </summary>
    public sealed class Pcg32
    {
        private const ulong Multiplier = 6364136223846793005UL;

        private ulong _state;
        private readonly ulong _increment;

        public Pcg32(ulong seed, ulong sequence = 54UL)
        {
            _increment = (sequence << 1) | 1UL;
            _state = 0UL;
            NextUInt();
            _state += seed;
            NextUInt();
        }

        // Raw-state constructor for Clone(); the marker parameter keeps it distinct from the seeding one.
        private Pcg32(ulong state, ulong increment, bool rawState)
        {
            _state = state;
            _increment = increment;
        }

        /// <summary>An independent copy that will produce the same future sequence.</summary>
        public Pcg32 Clone() => new Pcg32(_state, _increment, true);

        public uint NextUInt()
        {
            ulong old = _state;
            _state = unchecked(old * Multiplier + _increment);
            uint xorShifted = (uint)(((old >> 18) ^ old) >> 27);
            int rot = (int)(old >> 59);
            return (xorShifted >> rot) | (xorShifted << ((-rot) & 31));
        }

        /// <summary>Uniform integer in [0, maxExclusive). Rejection sampling, so no modulo bias.</summary>
        public int NextInt(int maxExclusive)
        {
            if (maxExclusive <= 0) throw new ArgumentOutOfRangeException(nameof(maxExclusive));
            uint bound = (uint)maxExclusive;
            uint threshold = unchecked((uint)(-maxExclusive)) % bound;
            while (true)
            {
                uint r = NextUInt();
                if (r >= threshold) return (int)(r % bound);
            }
        }

        /// <summary>Uniform integer in [minInclusive, maxExclusive).</summary>
        public int NextInt(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive) throw new ArgumentOutOfRangeException(nameof(maxExclusive));
            return minInclusive + NextInt(maxExclusive - minInclusive);
        }

        /// <summary>Uniform double in [0, 1).</summary>
        public double NextDouble() => NextUInt() * (1.0 / 4294967296.0);
    }
}
