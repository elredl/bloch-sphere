using NUnit.Framework;
using Study.Quantum;
using Study.Visuals;
using UnityEngine;

namespace Study.Tests
{
    public class BlochSpaceMappingTests
    {
        [Test]
        public void CardinalStates_MapToAgreedUnityDirections()
        {
            // Participant faces +Z: |0> up, |+> toward the participant, |+i> to their right.
            Assert.AreEqual(Vector3.up, BlochSpace.ToLocal(Qubit.Zero.Bloch));
            Assert.AreEqual(Vector3.down, BlochSpace.ToLocal(Qubit.One.Bloch));
            Assert.That(Vector3.Distance(Vector3.back, BlochSpace.ToLocal(Qubit.Plus.Bloch)), Is.LessThan(1e-6f));
            Assert.That(Vector3.Distance(Vector3.right, BlochSpace.ToLocal(Qubit.PlusI.Bloch)), Is.LessThan(1e-6f));
        }

        [Test]
        public void ToLocal_FromLocal_RoundTrip()
        {
            var v = new BlochVector(0.3, -0.5, 0.8);
            BlochVector back = BlochSpace.FromLocal(BlochSpace.ToLocal(v));
            Assert.AreEqual(0, (v - back).Length, 1e-6);
        }
    }
}
