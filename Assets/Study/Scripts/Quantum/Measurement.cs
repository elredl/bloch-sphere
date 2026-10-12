namespace Study.Quantum
{
    // Computational-basis (z) measurement.
    public static class Measurement
    {
        // Returns 0 or 1 with the Born-rule probabilities of q.
        public static int Sample(Qubit q, SeededRandom rng) => rng.NextDouble() < q.ProbabilityZero ? 0 : 1;

        // The state right after measuring the given outcome.
        public static Qubit Collapse(int outcome) => outcome == 0 ? Qubit.Zero : Qubit.One;

        // Repeated measurement of freshly prepared copies of q: the outcome of
        // each shot, in order. Same seed, same sequence, on every platform.
        public static int[] Shots(Qubit q, int shots, ulong seed)
        {
            var rng = new SeededRandom(seed);
            var outcomes = new int[shots];
            for (int i = 0; i < shots; i++) outcomes[i] = Sample(q, rng);
            return outcomes;
        }

        public static int CountZeros(int[] outcomes)
        {
            int zeros = 0;
            foreach (int outcome in outcomes) if (outcome == 0) zeros++;
            return zeros;
        }
    }
}
