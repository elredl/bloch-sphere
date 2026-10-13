using System;
using NUnit.Framework;
using Study.Quantum;

namespace Study.Tests
{
    public class GateArcTests
    {
        static readonly Gate[] Gates = { Gate.X, Gate.Z, Gate.H, Gate.Ry(Math.PI / 2), Gate.Rz(Math.PI / 2), Gate.Ry(Math.PI / 3) };
        static readonly Qubit[] Starts = { Qubit.Zero, Qubit.Plus, Qubit.One, Qubit.FromBlochAngles(0.7, 1.9) };

        [Test]
        public void Endpoints_AreStartAndGateResult()
        {
            foreach (Gate gate in Gates)
            foreach (Qubit start in Starts)
            {
                Assert.IsTrue(GateArc.StateAt(start, gate, 0).IsSameStateAs(start));
                Assert.IsTrue(GateArc.StateAt(start, gate, 1).IsSameStateAs(gate.Apply(start)));
            }
        }

        [Test]
        public void Path_StaysOnSphere_AtConstantAngleFromAxis()
        {
            // An arc about the axis keeps |v| = 1 and keeps the same angle to the
            // axis all the way round. A lerp between end points would do neither.
            foreach (Gate gate in Gates)
            foreach (Qubit start in Starts)
            {
                double coneAngle = BlochVector.AngleBetween(start.Bloch, gate.Axis);
                for (int i = 0; i <= 100; i++)
                {
                    BlochVector v = GateArc.StateAt(start, gate, i / 100.0).Bloch;
                    Assert.AreEqual(1, v.Length, 1e-12, $"{gate.Name} length at {i}%");
                    Assert.AreEqual(coneAngle, BlochVector.AngleBetween(v, gate.Axis), 1e-9, $"{gate.Name} cone at {i}%");
                }
            }
        }

        [Test]
        public void Path_MatchesRodriguesRotation_AtEveryFraction()
        {
            foreach (Gate gate in Gates)
            foreach (Qubit start in Starts)
            for (int i = 0; i <= 20; i++)
            {
                double f = i / 20.0;
                BlochVector expected = start.Bloch.RotatedAbout(gate.Axis, gate.Angle * f);
                BlochVector actual = GateArc.StateAt(start, gate, f).Bloch;
                Assert.AreEqual(0, (expected - actual).Length, 1e-9, $"{gate.Name} at {f}");
            }
        }

        [Test]
        public void HadamardOnZero_MidpointSwingsThroughMinusY()
        {
            // H takes +z to +x by a half turn about (x+z)/sqrt2. Halfway, the vector is
            // at (1/2, -1/sqrt2, 1/2), well away from the straight-line midpoint (0.71, 0, 0.71).
            BlochVector mid = GateArc.StateAt(Qubit.Zero, Gate.H, 0.5).Bloch;
            Assert.AreEqual(0.5, mid.X, 1e-12);
            Assert.AreEqual(-1 / Math.Sqrt(2), mid.Y, 1e-12);
            Assert.AreEqual(0.5, mid.Z, 1e-12);
        }

        [Test]
        public void PositiveRotation_FollowsRightHandRule()
        {
            // A small positive turn about z moves +x toward +y (this is the only direction the
            // Condition 3 ratchet allows, and the one the scripted arc must show).
            BlochVector early = GateArc.StateAt(Qubit.Plus, Gate.Rz(Math.PI / 2), 0.1).Bloch;
            Assert.Greater(early.Y, 0);
        }
    }
}
