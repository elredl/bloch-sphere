using System;
using Study.Quantum;
using UnityEngine;

namespace Study.Visuals
{
    // Animates a gate as a continuous arc about the gate's rotation axis by
    // advancing the rotation angle and writing each intermediate state to
    // QubitState. Time comes from a swappable clock, so the lesson can drive
    // arcs from narration audio time and they cannot drift from the audio.
    //
    // Condition 3 "held" mode: BeginHeld starts the gate (axis shows, trail
    // records) and the learner's grab sets the fraction directly; FinishFrom
    // then plays the rest of the arc on the clock. Same arc, same events.
    public class GateArcAnimator : MonoBehaviour
    {
        [SerializeField] QubitState state;

        public event Action<Gate> Started;
        public event Action<Gate> Finished;

        // Seconds. Defaults to game time; the lesson controller replaces it with audio time.
        public Func<double> Clock = () => Time.timeAsDouble;

        public bool IsPlaying => activeGate != null;
        public bool IsHeld => held;
        // A held gate has no scheduled end until it is let go.
        public double ScheduledEnd => held ? double.MaxValue : startTime + duration;
        public Gate ActiveGate => activeGate;
        public double HeldFraction => heldFraction;

        Gate activeGate;
        Qubit startState;
        double startTime;
        double duration;
        double fromFraction;
        bool held;
        double heldFraction;

        // startTime: when the arc was scheduled to begin, on Clock. Defaults to now.
        // Passing the scheduled time means a frame that fires late doesn't shift
        // the arc's end; the arc just catches up.
        public void Play(Gate gate, double durationSeconds, double? startTime = null)
        {
            if (IsPlaying) Complete(); // never leave a gate half-applied
            activeGate = gate;
            startState = state.Current;
            held = false;
            fromFraction = 0;
            this.startTime = startTime ?? Clock();
            duration = Math.Max(durationSeconds, 1e-3);
            Started?.Invoke(gate);
            Debug.Log($"[GateArc] start {gate} from {startState.Bloch}");
        }

        // Starts a gate whose progress the learner controls (Condition 3).
        public void BeginHeld(Gate gate)
        {
            if (IsPlaying) Complete();
            activeGate = gate;
            startState = state.Current;
            held = true;
            heldFraction = 0;
            Started?.Invoke(gate);
            Debug.Log($"[GateArc] held start {gate} from {startState.Bloch}");
        }

        // fraction of the gate's angle, 0..1. Only while held.
        public void SetHeldFraction(double fraction)
        {
            if (!held) return;
            heldFraction = Math.Max(0, Math.Min(1, fraction));
            state.Set(GateArc.StateAt(startState, activeGate, heldFraction));
        }

        // Lets go of a held gate: the rest of the arc plays from where it is.
        public void FinishFrom(double durationSeconds, double? startTime = null)
        {
            if (!held) return;
            held = false;
            fromFraction = heldFraction;
            this.startTime = startTime ?? Clock();
            duration = Math.Max(durationSeconds, 1e-3);
        }

        void Update()
        {
            if (!IsPlaying || held) return;
            double fraction = (Clock() - startTime) / duration;
            if (fraction >= 1)
            {
                Complete();
                return;
            }
            state.Set(GateArc.StateAt(startState, activeGate, fromFraction + (1 - fromFraction) * Ease(Math.Max(0, fraction))));
        }

        // Snaps to the exact gate result and ends the animation.
        public void Complete()
        {
            Gate gate = activeGate;
            state.Set(gate.Apply(startState));
            activeGate = null;
            held = false;
            Debug.Log($"[GateArc] end {gate.Name} at {state.Current.Bloch}, |v|={state.Current.Bloch.Length:F6}");
            Finished?.Invoke(gate);
        }

        // Smoothstep: starts and ends gently so the turn reads as one motion.
        // Only the speed along the arc is eased; the path is always the true arc.
        static double Ease(double t) => t * t * (3 - 2 * t);
    }
}
