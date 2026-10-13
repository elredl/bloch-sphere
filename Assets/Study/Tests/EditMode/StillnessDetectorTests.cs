using NUnit.Framework;
using Study.Session;
using UnityEngine;

namespace Study.Tests
{
    public class StillnessDetectorTests
    {
        static void Feed(StillnessDetector d, float from, float to, System.Func<float, Vector3> position, System.Func<float, Vector3> forward)
        {
            for (float t = from; t <= to + 1e-4f; t += 1f / 90) d.Add(t, position(t), forward(t));
        }

        [Test]
        public void StillOnlyAfterAFullSecondWithinTolerance()
        {
            var d = new StillnessDetector(1f, 0.015f, 3f);
            Feed(d, 0, 0.5f, t => new Vector3(0, 1.2f, 0), t => Vector3.forward);
            Assert.IsFalse(d.IsStill, "half a second is not enough");
            Feed(d, 0.5f + 1f / 90, 1.1f, t => new Vector3(0.004f * Mathf.Sin(t * 20), 1.2f, 0), t => Vector3.forward);
            Assert.IsTrue(d.IsStill, "small sway (4 mm) for over a second counts as still");
        }

        [Test]
        public void MovementOrTurningInTheLastSecondIsNotStill()
        {
            var d = new StillnessDetector(1f, 0.015f, 3f);
            Feed(d, 0, 2f, t => new Vector3(t < 1.5f ? 0 : 0.03f, 1.2f, 0), t => Vector3.forward);
            Assert.IsFalse(d.IsStill, "a 3 cm move half a second ago");
            d.Clear();
            Feed(d, 0, 2f, t => new Vector3(0, 1.2f, 0), t => Quaternion.Euler(0, t * 5, 0) * Vector3.forward);
            Assert.IsFalse(d.IsStill, "turning 5 deg per second");
            Feed(d, 2f + 1f / 90, 3.2f, t => new Vector3(0, 1.2f, 0), t => Quaternion.Euler(0, 10, 0) * Vector3.forward);
            Assert.IsTrue(d.IsStill, "still again for over a second");
        }
    }
}
