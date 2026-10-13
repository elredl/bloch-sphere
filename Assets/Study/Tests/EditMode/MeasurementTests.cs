using System;
using NUnit.Framework;
using Study.Quantum;

namespace Study.Tests
{
    public class MeasurementTests
    {
        [Test]
        public void SeededRandom_MatchesReferenceSplitMix64()
        {
            // Reference outputs of SplitMix64 for seed 0. Pins the algorithm so the
            // shot sequence can't silently change between platforms or refactors.
            var rng = new SeededRandom(0);
            Assert.AreEqual(0xE220A8397B1DCDAFUL, rng.NextUInt64());
            Assert.AreEqual(0x6E789E6AA1B965F4UL, rng.NextUInt64());
            Assert.AreEqual(0x06C45D188009454FUL, rng.NextUInt64());
        }

        [Test]
        public void SameSeed_GivesSameShots_DifferentSeed_Differs()
        {
            int[] a = Measurement.Shots(Qubit.Plus, 100, 42);
            int[] b = Measurement.Shots(Qubit.Plus, 100, 42);
            int[] c = Measurement.Shots(Qubit.Plus, 100, 43);
            CollectionAssert.AreEqual(a, b);
            CollectionAssert.AreNotEqual(a, c);
        }

        [Test]
        public void BasisStates_AreDeterministic()
        {
            Assert.AreEqual(1000, Measurement.CountZeros(Measurement.Shots(Qubit.Zero, 1000, 1)));
            Assert.AreEqual(0, Measurement.CountZeros(Measurement.Shots(Qubit.One, 1000, 1)));
        }

        [TestCase(0.0, 0.0)]                     // |0>
        [TestCase(Math.PI / 2, 0.0)]             // |+>
        [TestCase(Math.PI / 2, Math.PI / 2)]     // |+i>, phase must not matter
        [TestCase(Math.PI / 3, 0.0)]             // pause 4 state, P(0) = 0.75
        [TestCase(2.5, 4.0)]
        public void Statistics_ConvergeToBornRule(double theta, double phi)
        {
            Qubit q = Qubit.FromBlochAngles(theta, phi);
            const int shots = 200000;
            double observed = Measurement.CountZeros(Measurement.Shots(q, shots, 2026)) / (double)shots;
            double expected = q.ProbabilityZero;
            double fiveSigma = 5 * Math.Sqrt(expected * (1 - expected) / shots) + 1e-9;
            Assert.AreEqual(expected, observed, fiveSigma);
        }

        [Test]
        public void Collapse_GoesToThePoles()
        {
            Assert.IsTrue(Measurement.Collapse(0).IsSameStateAs(Qubit.Zero));
            Assert.IsTrue(Measurement.Collapse(1).IsSameStateAs(Qubit.One));
        }
    }
}
