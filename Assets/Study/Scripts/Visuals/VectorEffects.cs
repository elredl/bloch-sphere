using System;
using Study.Quantum;
using UnityEngine;

namespace Study.Visuals
{
    // State changes that are not rotations, each with its own look so none can
    // be mistaken for a turn:
    //   Re-prepare: the vector fades out, the state changes while it is
    //               invisible, and it fades back in. No arc, no trail.
    //   Collapse:   the vector snaps to a pole with one short flash.
    // Timed on Clock (lesson time), like the arc animator.
    public class VectorEffects : MonoBehaviour
    {
        [SerializeField] QubitState state;
        [SerializeField] GateArcAnimator animator;
        [SerializeField] StateVectorView vector;
        [SerializeField] CollapseFlashView flash;

        public Func<double> Clock = () => Time.timeAsDouble;

        bool repreparing;
        Qubit target;
        double start;
        double duration;
        bool swapped;

        public bool IsBusy => repreparing;
        public double ScheduledEnd => start + duration;

        // Fade out over the first half of duration, switch state, fade in over the second half.
        // start may be in the future; nothing happens until then.
        public void Reprepare(Qubit newState, double startTime, double durationSeconds)
        {
            if (animator.IsPlaying) animator.Complete();
            if (repreparing) Complete();
            target = newState;
            start = startTime;
            duration = Math.Max(durationSeconds, 1e-3);
            swapped = false;
            repreparing = true;
        }

        public void Collapse(int outcome, double atTime)
        {
            if (repreparing) Complete();
            if (animator.IsPlaying) animator.Complete();
            Qubit collapsed = Measurement.Collapse(outcome);
            state.Set(collapsed);
            vector.SetOpacity(1f);
            flash.Pulse(collapsed.Bloch, atTime);
        }

        // Jumps to the end of a re-preparation in progress.
        public void Complete()
        {
            if (!repreparing) return;
            state.Set(target);
            vector.SetOpacity(1f);
            repreparing = false;
        }

        void Update()
        {
            if (!repreparing) return;
            double f = (Clock() - start) / duration;
            if (f < 0) return;
            if (f >= 1)
            {
                Complete();
                return;
            }
            if (f < 0.5)
            {
                vector.SetOpacity((float)(1 - 2 * f));
            }
            else
            {
                if (!swapped)
                {
                    state.Set(target); // turns the vector while it is invisible
                    swapped = true;
                }
                vector.SetOpacity((float)(2 * f - 1));
            }
        }
    }
}
