using System;
using System.Numerics;
using NUnit.Framework;
using Study.Quantum;

namespace Study.Tests
{
    public class GateTests
    {
        const double Eps = 1e-12;

        static readonly Gate[] NamedGates = { Gate.X, Gate.Y, Gate.Z, Gate.H, Gate.S, Gate.T };

        static void AssertSameState(Qubit expected, Qubit actual, string label)
        {
            Assert.IsTrue(expected.IsSameStateAs(actual), $"{label}: expected {expected}, got {actual}");
        }

        static void AssertAmplitudes(Complex alpha, Complex beta, Qubit q, string label)
        {
            Assert.AreEqual(alpha.Real, q.Alpha.Real, Eps, label + " alpha.re");
            Assert.AreEqual(alpha.Imaginary, q.Alpha.Imaginary, Eps, label + " alpha.im");
            Assert.AreEqual(beta.Real, q.Beta.Real, Eps, label + " beta.re");
            Assert.AreEqual(beta.Imaginary, q.Beta.Imaginary, Eps, label + " beta.im");
        }

        [Test]
        public void KnownResults_NamedGates()
        {
            AssertSameState(Qubit.One, Gate.X.Apply(Qubit.Zero), "X|0>");
            AssertSameState(Qubit.Zero, Gate.X.Apply(Qubit.One), "X|1>");
            AssertSameState(Qubit.Minus, Gate.Z.Apply(Qubit.Plus), "Z|+>");
            AssertSameState(Qubit.Plus, Gate.H.Apply(Qubit.Zero), "H|0>");
            AssertSameState(Qubit.Minus, Gate.H.Apply(Qubit.One), "H|1>");
            AssertSameState(Qubit.Zero, Gate.H.Apply(Qubit.Plus), "H|+>");
            AssertSameState(Qubit.PlusI, Gate.S.Apply(Qubit.Plus), "S|+>");
            AssertSameState(Qubit.MinusI, Gate.X.Apply(Qubit.PlusI), "X|+i>");

            // Exact amplitudes, including phase, for the textbook matrices.
            AssertAmplitudes(0, Complex.ImaginaryOne, Gate.Y.Apply(Qubit.Zero), "Y|0> = i|1>");
            AssertAmplitudes(1 / Math.Sqrt(2), 1 / Math.Sqrt(2), Gate.H.Apply(Qubit.Zero), "H|0>");
            AssertAmplitudes(0, Complex.FromPolarCoordinates(1, Math.PI / 4), Gate.T.Apply(Qubit.One), "T|1>");
        }

        [Test]
        public void KnownResults_LessonRotations()
        {
            // Pause 1: quarter turn toward |1> takes |0> to |+>.
            AssertSameState(Qubit.Plus, Gate.Ry(Math.PI / 2).Apply(Qubit.Zero), "Ry(pi/2)|0>");
            // Pause 2: quarter turn about z takes |+> to |+i> (not |-i>).
            AssertSameState(Qubit.PlusI, Gate.Rz(Math.PI / 2).Apply(Qubit.Plus), "Rz(pi/2)|+>");
            // Pause 4: 60 degree turn toward |1> gives P(0) = cos^2(30 deg) = 0.75.
            Assert.AreEqual(0.75, Gate.Ry(Math.PI / 3).Apply(Qubit.Zero).ProbabilityZero, Eps);
            AssertSameState(Qubit.MinusI, Gate.Rx(Math.PI / 2).Apply(Qubit.Zero), "Rx(pi/2)|0>");
        }

        [Test]
        public void NamedGateMatrix_MatchesItsAxisAngleRotation()
        {
            // The animation draws Axis/Angle; the physics uses the matrix. They must
            // agree on every input state (up to global phase).
            Qubit[] probes = { Qubit.Zero, Qubit.One, Qubit.Plus, Qubit.MinusI, Qubit.FromBlochAngles(1.1, 2.3) };
            foreach (Gate gate in NamedGates)
            {
                Gate rotation = Gate.Rotation("check", gate.Axis, gate.Angle);
                foreach (Qubit probe in probes)
                    AssertSameState(gate.Apply(probe), rotation.Apply(probe), $"{gate.Name} on {probe}");
            }
        }

        [Test]
        public void GateAction_MatchesRodriguesRotationOfBlochVector()
        {
            var rng = new SeededRandom(7);
            Gate[] gates = { Gate.X, Gate.Y, Gate.Z, Gate.H, Gate.S, Gate.T, Gate.Rx(0.4), Gate.Ry(2.0), Gate.Rz(-1.3),
                             Gate.Rotation("n", new BlochVector(0.3, -0.5, 0.8), 1.7) };
            for (int i = 0; i < 50; i++)
            {
                Qubit q = Qubit.FromBlochAngles(rng.NextDouble() * Math.PI, rng.NextDouble() * 2 * Math.PI);
                foreach (Gate gate in gates)
                {
                    BlochVector expected = q.Bloch.RotatedAbout(gate.Axis, gate.Angle);
                    BlochVector actual = gate.Apply(q).Bloch;
                    Assert.AreEqual(0, (expected - actual).Length, 1e-9, $"{gate} on {q}");
                }
            }
        }

        [Test]
        public void Hadamard_AppliedTwice_ReturnsOriginalState()
        {
            var rng = new SeededRandom(11);
            for (int i = 0; i < 100; i++)
            {
                Qubit q = Qubit.FromBlochAngles(rng.NextDouble() * Math.PI, rng.NextDouble() * 2 * Math.PI);
                Qubit twice = Gate.H.Apply(Gate.H.Apply(q));
                // H*H = I exactly, so even the global phase is unchanged.
                Assert.AreEqual(0, (q.Alpha - twice.Alpha).Magnitude, 1e-12);
                Assert.AreEqual(0, (q.Beta - twice.Beta).Magnitude, 1e-12);
            }
        }

        [Test]
        public void GateRelations_SSquaredIsZ_TSquaredIsS()
        {
            Qubit q = Qubit.FromBlochAngles(0.9, 0.4);
            AssertSameState(Gate.Z.Apply(q), Gate.S.Apply(Gate.S.Apply(q)), "SS = Z");
            AssertSameState(Gate.S.Apply(q), Gate.T.Apply(Gate.T.Apply(q)), "TT = S");
        }

        [Test]
        public void Normalization_PreservedOverLongGateSequences()
        {
            var rng = new SeededRandom(3);
            Gate[] gates = { Gate.X, Gate.Y, Gate.Z, Gate.H, Gate.S, Gate.T, Gate.Rx(0.3), Gate.Ry(1.9), Gate.Rz(2.7) };
            Qubit q = Qubit.Zero;
            for (int i = 0; i < 10000; i++)
            {
                q = gates[(int)(rng.NextUInt64() % (ulong)gates.Length)].Apply(q);
                Assert.AreEqual(1, q.ProbabilityZero + q.ProbabilityOne, 1e-12);
                Assert.AreEqual(1, q.Bloch.Length, 1e-12);
            }
        }

        [Test]
        public void FromName_ParsesNamedAndRotationGates()
        {
            Assert.AreSame(Gate.H, Gate.FromName("H"));
            Gate ry = Gate.FromName("Ry(60)");
            Assert.AreEqual(Math.PI / 3, ry.Angle, Eps);
            Assert.AreEqual(1, ry.Axis.Y, Eps);
            AssertSameState(Qubit.PlusI, Gate.FromName("Rz(90)").Apply(Qubit.Plus), "Rz(90)|+>");
            Assert.Throws<ArgumentException>(() => Gate.FromName("Q"));
            Assert.Throws<ArgumentException>(() => Gate.FromName("Rq(90)"));
        }
    }
}
