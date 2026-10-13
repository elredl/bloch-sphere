using System;
using System.Numerics;
using NUnit.Framework;
using Study.Quantum;

namespace Study.Tests
{
    public class QubitTests
    {
        const double Eps = 1e-12;

        static void AssertBloch(Qubit q, double x, double y, double z)
        {
            BlochVector b = q.Bloch;
            Assert.AreEqual(x, b.X, Eps, $"x of {q}");
            Assert.AreEqual(y, b.Y, Eps, $"y of {q}");
            Assert.AreEqual(z, b.Z, Eps, $"z of {q}");
        }

        [Test]
        public void CardinalStates_HaveExpectedBlochCoordinates()
        {
            AssertBloch(Qubit.Zero, 0, 0, 1);
            AssertBloch(Qubit.One, 0, 0, -1);
            AssertBloch(Qubit.Plus, 1, 0, 0);
            AssertBloch(Qubit.Minus, -1, 0, 0);
            AssertBloch(Qubit.PlusI, 0, 1, 0);
            AssertBloch(Qubit.MinusI, 0, -1, 0);
        }

        [Test]
        public void CardinalStates_HaveExpectedAngles()
        {
            Assert.AreEqual(0, Qubit.Zero.Theta, Eps);
            Assert.AreEqual(Math.PI, Qubit.One.Theta, Eps);
            Assert.AreEqual(Math.PI / 2, Qubit.Plus.Theta, Eps);
            Assert.AreEqual(0, Qubit.Plus.Phi, Eps);
            Assert.AreEqual(Math.PI, Qubit.Minus.Phi, Eps);
            Assert.AreEqual(Math.PI / 2, Qubit.PlusI.Phi, Eps);
            Assert.AreEqual(3 * Math.PI / 2, Qubit.MinusI.Phi, Eps);
        }

        [Test]
        public void FromBlochAngles_RoundTripsThetaAndPhi()
        {
            for (double theta = 0.1; theta < Math.PI; theta += 0.37)
            for (double phi = 0; phi < 2 * Math.PI; phi += 0.53)
            {
                Qubit q = Qubit.FromBlochAngles(theta, phi);
                Assert.AreEqual(theta, q.Theta, 1e-9);
                Assert.AreEqual(phi, q.Phi, 1e-9);
                Assert.AreEqual(1, q.Bloch.Length, Eps);
            }
        }

        [Test]
        public void Constructor_Normalizes()
        {
            var q = new Qubit(new Complex(3, 0), new Complex(0, 4));
            Assert.AreEqual(1, q.ProbabilityZero + q.ProbabilityOne, Eps);
            Assert.AreEqual(9.0 / 25, q.ProbabilityZero, Eps);
            Assert.AreEqual(1, q.Bloch.Length, Eps);
        }

        [Test]
        public void Constructor_RejectsZeroVector()
        {
            Assert.Throws<ArgumentException>(() => new Qubit(0, 0));
        }

        [Test]
        public void FromName_KnowsAllCardinalStates()
        {
            Assert.IsTrue(Qubit.FromName("0").IsSameStateAs(Qubit.Zero));
            Assert.IsTrue(Qubit.FromName("1").IsSameStateAs(Qubit.One));
            Assert.IsTrue(Qubit.FromName("+").IsSameStateAs(Qubit.Plus));
            Assert.IsTrue(Qubit.FromName("-").IsSameStateAs(Qubit.Minus));
            Assert.IsTrue(Qubit.FromName("+i").IsSameStateAs(Qubit.PlusI));
            Assert.IsTrue(Qubit.FromName("-i").IsSameStateAs(Qubit.MinusI));
            Assert.Throws<ArgumentException>(() => Qubit.FromName("2"));
        }

        [Test]
        public void SameState_IgnoresGlobalPhase()
        {
            var phased = new Qubit(Complex.FromPolarCoordinates(Math.Sqrt(0.5), 1.234), Complex.FromPolarCoordinates(Math.Sqrt(0.5), 1.234));
            Assert.IsTrue(phased.IsSameStateAs(Qubit.Plus));
            Assert.IsFalse(Qubit.Plus.IsSameStateAs(Qubit.Minus));
        }
    }
}
