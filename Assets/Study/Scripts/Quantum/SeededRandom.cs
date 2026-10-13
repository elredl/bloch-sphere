namespace Study.Quantum
{
    // SplitMix64. Used instead of System.Random because its output is fully
    // specified, so a seed gives the same shot sequence in the Editor, on the
    // desktop build and on Quest (IL2CPP). Every participant sees the same demo.
    public sealed class SeededRandom
    {
        ulong state;

        public SeededRandom(ulong seed)
        {
            state = seed;
        }

        public ulong NextUInt64()
        {
            ulong z = state += 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        // Uniform in [0, 1), using the top 53 bits.
        public double NextDouble() => (NextUInt64() >> 11) * (1.0 / (1UL << 53));
    }
}
