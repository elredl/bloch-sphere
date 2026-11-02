using System;
using Study.Quantum;
using Study.Visuals;
using UnityEngine;

namespace Study.Lesson
{
    // Condition 3: the learner makes every turn of the lesson (LessonController hands each
    // apply_gate here instead of playing it). The gate's axis shows (GateAxisView, on the
    // animator's start) with the amber direction guide at the tip; the learner grabs the
    // tip and turns it. The grab is the ratchet of orientation: the arrow follows the hand
    // forward along the gate's own arc only, and the turn is done when it is let go within
    // tolerance of the end. The rest then plays on the clock, so the end state is exactly
    // the gate's result, as in the other conditions.
    //
    // Logged as turn_start, turn_grab, turn_release (let go short of the end) and turn_done.
    public class GuidedTurn : MonoBehaviour
    {
        const double FinishSeconds = 0.3; // the rest of the arc, after a release within tolerance

        [SerializeField] LessonController lesson;
        [SerializeField] GateArcAnimator animator;
        [SerializeField] QubitState state;
        [SerializeField] GateDirectionView arrow;
        [SerializeField] QubitGrabTarget grab;
        [SerializeField] EmbodiedPause pause; // for the guide text (pause_feedback.json ui)
        [SerializeField] float toleranceDegrees = 10f;

        public bool Active { get; private set; }
        // The guide text while a turn waits for the learner's hand, else null.
        public string Hint => Active ? pause.Feedback.ui.turnGuide : null;
        public Gate Gate => ratchet?.Gate;

        RatchetRotation ratchet;
        Func<BlochVector> hand;
        double startedAt;
        int grabs;

        // duration: the arc's length when it plays by itself (fallback only).
        public void Begin(Gate gate, double duration)
        {
            Cancel();
            try
            {
                ratchet = new RatchetRotation(state.Current, gate, toleranceDegrees * Math.PI / 180);
            }
            catch (ArgumentException)
            {
                // The state lies on the gate's axis: there is nothing to turn. The timeline never
                // does this (tools/narration_script.py checks), so just play it.
                Debug.LogError($"[Turn] {gate.Name} from {state.Current.Bloch}: start state on the axis, playing it instead");
                animator.Play(gate, duration, lesson.Now);
                return;
            }
            Active = true;
            hand = null;
            grabs = 0;
            startedAt = lesson.Now;
            animator.BeginHeld(gate);
            arrow.Show(gate, state.Current, 0);
            grab.SetGrabbable(true);
            Log("turn_start", "");
        }

        // An aborted lesson: nothing is left in the learner's hands.
        public void Cancel()
        {
            if (!Active) return;
            Active = false;
            hand = null;
            grab.SetGrabbable(false);
            arrow.Hide();
            if (animator.IsHeld) animator.Complete();
        }

        void Update()
        {
            if (!Active) return;
            if (!lesson.IsRunning)
            {
                Cancel();
                return;
            }
            if (lesson.OnBreak || hand == null || !ratchet.IsGrabbing) return;
            if (ratchet.Move(hand())) grab.WrongWayTick();
            animator.SetHeldFraction(ratchet.Fraction);
            arrow.SetAngle(ratchet.Angle);
        }

        // From QubitGrabTarget (or the dev simulator), while a turn is active.
        public void GrabStarted(string mode, Func<BlochVector> handPoint)
        {
            if (!Active || ratchet.IsGrabbing || lesson.OnBreak) return;
            hand = handPoint;
            grabs++;
            ratchet.Begin(hand());
            Log("turn_grab", $"mode={mode} at={ratchet.Fraction:F2}");
        }

        public void GrabEnded()
        {
            if (!Active || !ratchet.IsGrabbing) return;
            hand = null;
            if (!ratchet.End())
            {
                Log("turn_release", $"at={ratchet.Fraction:F2}");
                return; // short of the end: grab again
            }
            Active = false;
            grab.SetGrabbable(false);
            arrow.Hide();
            animator.FinishFrom(FinishSeconds, lesson.Now);
            Log("turn_done", $"grabs={grabs} in_turn={lesson.Now - startedAt:F3}");
        }

        void Log(string type, string fields) =>
            StudyEvents.Log(lesson.Time, type, $"gate={ratchet.Gate.Name} {fields}".TrimEnd());
    }
}
