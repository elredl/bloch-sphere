using System;
using Study.Quantum;
using Study.Visuals;
using UnityEngine;

namespace Study.Lesson
{
    // Condition 3's measurement segment: instead of the segment's scripted events
    // (LessonController skips them), the learner explores freely until shortly
    // before the pause:
    //
    //   start     the vector fades to |0>; the prediction panel becomes the gate
    //             palette (X, Z, H, S, |0> to start over, Measure) and the
    //             measurement panel shows the chance of 0 and 1 live
    //   gates     each press plays the gate (icon, then the turn)
    //   grab      the vector's tip turns freely to any state
    //   measure   the sphere collapses to the outcome (Born rule) with the flash;
    //             the panel tallies the learner's results
    //   end       2 s before the pause: panels close and the vector fades to the
    //             pause task's start state, so the pause starts as in Cond 1 / 2
    //
    // Everything is logged as sandbox_* events.
    public class MeasurementSandbox : MonoBehaviour
    {
        const double EndLeadSeconds = 2.0;
        const double GateSeconds = 2.5;
        const string Reset = "reset";
        static readonly string[] Buttons = { "X", "Z", "H", "S", Reset };
        static readonly string[] ButtonLabels = { "X", "Z", "H", "S", "|0⟩" };

        [SerializeField] LessonController lesson;
        [SerializeField] QubitState state;
        [SerializeField] GateArcAnimator animator;
        [SerializeField] VectorEffects effects;
        [SerializeField] PredictionPanelView palette;
        [SerializeField] MeasurementPanelView chart;
        [SerializeField] QubitGrabTarget grab;
        [SerializeField] string title = "EXPLORE";
        [SerializeField] string instructions = "Apply gates, grab the vector to turn it, and measure. |0⟩ starts over.";
        [SerializeField] string hint = "Watch how the chance of 0 and 1 changes.";
        [SerializeField] string measureLabel = "Measure";
        [SerializeField] string chartTitle = "CHANCE";

        public bool IsActive => active;

        readonly System.Random random = new System.Random();
        bool active;
        double endTime;
        Qubit endState;
        Func<BlochVector> hand;
        int zeros, ones, gates, turns;

        void OnEnable()
        {
            lesson.SegmentStarted += OnSegmentStarted;
            palette.ChoicePicked += OnButton;
            palette.GatePressed += OnMeasure;
        }

        void OnDisable()
        {
            lesson.SegmentStarted -= OnSegmentStarted;
            palette.ChoicePicked -= OnButton;
            palette.GatePressed -= OnMeasure;
        }

        void OnSegmentStarted(LessonTimeline.Segment s)
        {
            if (!s.sandbox || !lesson.Config.ExploresSandbox) return;
            active = true;
            endTime = s.pauseStart - EndLeadSeconds;
            endState = Qubit.FromName(lesson.Timeline.Task(s.task).startState);
            hand = null;
            zeros = ones = gates = turns = 0;
            effects.Reprepare(Qubit.Zero, lesson.Time, lesson.Timeline.reprepareDuration);
            palette.ShowPrediction(title, instructions, Buttons, ButtonLabels, null);
            palette.ShowGateButton(measureLabel, hint);
            chart.ShowLive(chartTitle);
            grab.SetGrabbable(true);
            Log("sandbox_start", $"segment={s.id} until={endTime:F1}");
        }

        void Update()
        {
            if (!active || lesson.OnBreak) return;
            if (lesson.Time >= endTime)
            {
                Finish();
                return;
            }
            if (hand != null)
            {
                BlochVector h = hand();
                if (h.Length > 1e-6) state.Set(EmbodiedPause.QubitAt(h.Normalized()));
            }
            chart.SetLive(state.Current.ProbabilityZero, $"You measured  0: {zeros}   1: {ones}");
        }

        // A turn, re-preparation or grab is still under way: wait for it.
        bool Busy => animator.IsPlaying || effects.IsBusy || hand != null;

        void OnButton(string pressed)
        {
            if (!active || Busy) return;
            if (pressed == Reset)
            {
                effects.Reprepare(Qubit.Zero, lesson.Time, lesson.Timeline.reprepareDuration);
                Log("sandbox_reset", "");
                return;
            }
            gates++;
            animator.Play(Gate.FromName(pressed), GateSeconds, lesson.Time);
            Log("sandbox_gate", $"gate={pressed}");
        }

        void OnMeasure()
        {
            if (!active) return;
            palette.ShowGateButton(measureLabel, hint); // the button turns itself off on press; keep it usable
            if (Busy) return;
            double p0 = state.Current.ProbabilityZero;
            int outcome = random.NextDouble() < p0 ? 0 : 1;
            if (outcome == 0) zeros++;
            else ones++;
            effects.Collapse(outcome, lesson.Time);
            Log("sandbox_measure", $"outcome={outcome} p0={p0:F3}");
        }

        // From QubitGrabTarget while the sandbox is active. handPoint: Bloch coordinates.
        public void GrabStarted(string mode, Func<BlochVector> handPoint)
        {
            if (!active || hand != null || animator.IsPlaying || effects.IsBusy) return;
            hand = handPoint;
            Log("sandbox_grab_start", $"mode={mode}");
        }

        public void GrabEnded()
        {
            if (!active || hand == null) return;
            hand = null;
            turns++;
            BlochVector b = state.Current.Bloch;
            Log("sandbox_turn", $"x={b.X:F3} y={b.Y:F3} z={b.Z:F3} p0={state.Current.ProbabilityZero:F3}");
        }

        void Finish()
        {
            active = false;
            hand = null;
            grab.SetGrabbable(false);
            palette.Hide();
            chart.Hide();
            if (animator.IsPlaying) animator.Complete();
            effects.Complete();
            effects.Reprepare(endState, lesson.Time, lesson.Timeline.reprepareDuration);
            Log("sandbox_end", $"gates={gates} turns={turns} measures={zeros + ones} zeros={zeros} ones={ones}");
        }

        void Log(string type, string fields) => StudyEvents.Log(lesson.Time, type, fields);
    }
}
