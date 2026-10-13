using System.Collections.Generic;
using UnityEngine;

namespace Study.Session
{
    // Calibration waits for the head to be still: the last `window` seconds of head poses
    // must stay within `positionTolerance` metres and `angleTolerance` degrees of each other.
    // Plain C# so it can be unit-tested.
    public sealed class StillnessDetector
    {
        readonly float window, positionTolerance, angleTolerance;
        readonly List<(float time, Vector3 position, Vector3 forward)> samples = new List<(float, Vector3, Vector3)>();

        public StillnessDetector(float windowSeconds = 1f, float positionToleranceMetres = 0.015f, float angleToleranceDegrees = 3f)
        {
            window = windowSeconds;
            positionTolerance = positionToleranceMetres;
            angleTolerance = angleToleranceDegrees;
        }

        public void Clear() => samples.Clear();

        public void Add(float time, Vector3 position, Vector3 forward)
        {
            samples.Add((time, position, forward.normalized));
            // Keep one sample older than the window, so a full window is always covered.
            while (samples.Count > 2 && time - samples[1].time >= window) samples.RemoveAt(0);
        }

        // True once the samples cover the whole window and none moved more than the tolerances.
        public bool IsStill
        {
            get
            {
                if (samples.Count < 2 || samples[samples.Count - 1].time - samples[0].time < window) return false;
                for (int i = 0; i < samples.Count; i++)
                for (int j = i + 1; j < samples.Count; j++)
                {
                    if (Vector3.Distance(samples[i].position, samples[j].position) > positionTolerance) return false;
                    if (Vector3.Angle(samples[i].forward, samples[j].forward) > angleTolerance) return false;
                }
                return true;
            }
        }
    }
}
