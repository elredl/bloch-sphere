using System;
using Study.Quantum;
using UnityEngine;

namespace Study.Visuals
{
    // Animates a gate as a continuous arc about the gate's rotation axis by
    // advancing the rotation angle and writing each intermediate state to
    // QubitState. Time comes from a swappable clock, so the lesson can drive
    // arcs from narration audio time and they cannot drift from the audio.
    public class GateArcAnimator : MonoBehaviour
    {
        [SerializeField] QubitState state;

        public event Action<Gate> Started;
        public event Action<Gate> Finished;

        // Seconds. Defaults to game time; the lesson controller replaces it with audio time.
        public Func<double> Clock = () => Time.timeAsDouble;

        public bool IsPlaying => activeGate != null;
        public Gate ActiveGate => activeGate;

        Gate activeGate;
        Qubit startState;
        double startTime;
        double duration;

        public void Play(Gate gate, double durationSeconds)
        {
            if (IsPlaying) Complete(); // never leave a gate half-applied
            activeGate = gate;
            startState = state.Current;
            startTime = Clock();
            duration = Math.Max(durationSeconds, 1e-3);
            Started?.Invoke(gate);
            Debug.Log($"[GateArc] start {gate} from {startState.Bloch}");
        }

        void Update()
        {
            if (!IsPlaying) return;
            double fraction = (Clock() - startTime) / duration;
            if (fraction >= 1)
            {
                Complete();
                return;
            }
            state.Set(GateArc.StateAt(startState, activeGate, Ease(fraction)));
        }

        // Snaps to the exact gate result and ends the animation.
        public void Complete()
        {
            Gate gate = activeGate;
            state.Set(gate.Apply(startState));
            activeGate = null;
            Debug.Log($"[GateArc] end {gate.Name} at {state.Current.Bloch}, |v|={state.Current.Bloch.Length:F6}");
            Finished?.Invoke(gate);
        }

        // Smoothstep: starts and ends gently so the turn reads as one motion.
        // Only the speed along the arc is eased; the path is always the true arc.
        static double Ease(double t) => t * t * (3 - 2 * t);
    }
}
