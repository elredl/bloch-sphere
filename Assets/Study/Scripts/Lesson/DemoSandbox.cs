using System;
using Study.Quantum;
using Study.Visuals;
using UnityEngine;

namespace Study.Lesson
{
    // The in-person demo's sandbox (Demo scene only, never in the study). It fills the demo
    // lesson's one pause and lasts until the visitor presses Done:
    //
    //   start     the vector fades to |0>; the prediction panel becomes the gate palette
    //             (X, Z, H, S, |0> to start over, Measure) and the measurement panel shows
    //             the chance of 0 and 1 live
    //   gates     each press plays the gate (icon, then the turn)
    //   grab      the vector's tip turns freely to any state
    //   measure   the sphere collapses to the outcome (Born rule) with the flash; the
    //             palette's hint line tallies the visitor's results
    //   done      the pause ends, so the demo lesson ends (SessionFlow starts it over)
    //
    // Restored from the study's earlier Condition 3 measurement sandbox. Logged as sandbox_*.
    public class DemoSandbox : MonoBehaviour
    {
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
        [SerializeField] NextButtonView done;
        [SerializeField] string title = "EXPLORE";
        [SerializeField] string instructions = "Press a gate to apply it, grab the tip of the arrow to turn it anywhere, and measure. |0⟩ starts over.";
        [SerializeField] string hint = "Watch how the chance of 0 and 1 follows the height of the arrow.";
        [SerializeField] string measureLabel = "Measure";
        [SerializeField] string chartTitle = "CHANCE";
        [SerializeField] string doneLabel = "DONE";

        public bool IsActive => active;

        readonly System.Random random = new System.Random();
        bool active;
        Func<BlochVector> hand;
        int zeros, ones, gates, turns;

        void OnEnable()
        {
            lesson.PauseStarted += OnPauseStarted;
            palette.ChoicePicked += OnButton;
            palette.GatePressed += OnMeasure;
            done.Pressed += OnDone;
        }

        void OnDisable()
        {
            lesson.PauseStarted -= OnPauseStarted;
            palette.ChoicePicked -= OnButton;
            palette.GatePressed -= OnMeasure;
            if (done != null) done.Pressed -= OnDone;
        }

        void OnPauseStarted(int index, LessonTimeline.Segment segment, LessonTimeline.PauseTask task, double onset)
        {
            if (!lesson.IsDemo) return;
            active = true;
            hand = null;
            zeros = ones = gates = turns = 0;
            effects.Reprepare(Qubit.Zero, lesson.Now, lesson.Timeline.reprepareDuration);
            palette.ShowPrediction(title, instructions, Buttons, ButtonLabels, null);
            palette.ShowGateButton(measureLabel, hint);
            chart.ShowLive(chartTitle);
            grab.SetGrabbable(true);
            done.SetLabel(doneLabel);
            done.Show();
            Log("sandbox_start", "");
        }

        void Update()
        {
            if (!active) return;
            if (!lesson.IsRunning)
            {
                Close(); // aborted
                return;
            }
            if (lesson.OnBreak) return;
            if (hand != null)
            {
                BlochVector h = hand();
                if (h.Length > 1e-6) state.Set(EmbodiedPause.QubitAt(h.Normalized()));
            }
            chart.SetLive(state.Current.ProbabilityZero, ""); // the tally is on the palette: the counter slot is short
        }

        // A turn, re-preparation or grab is still under way: wait for it.
        bool Busy => animator.IsPlaying || effects.IsBusy || hand != null;

        void OnButton(string pressed)
        {
            if (!active || Busy) return;
            if (pressed == Reset)
            {
                effects.Reprepare(Qubit.Zero, lesson.Now, lesson.Timeline.reprepareDuration);
                Log("sandbox_reset", "");
                return;
            }
            gates++;
            animator.Play(Gate.FromName(pressed), GateSeconds, lesson.Now);
            Log("sandbox_gate", $"gate={pressed}");
        }

        void OnMeasure()
        {
            if (!active) return;
            if (Busy)
            {
                palette.ShowGateButton(measureLabel, Tally); // the button turns itself off on press; keep it usable
                return;
            }
            double p0 = state.Current.ProbabilityZero;
            int outcome = random.NextDouble() < p0 ? 0 : 1;
            if (outcome == 0) zeros++;
            else ones++;
            effects.Collapse(outcome, lesson.Now);
            palette.ShowGateButton(measureLabel, Tally);
            Log("sandbox_measure", $"outcome={outcome} p0={p0:F3}");
        }

        string Tally => zeros + ones == 0 ? hint : $"Results so far: {Count(zeros, "zero")}, {Count(ones, "one")}.";

        static string Count(int n, string word) => n == 1 ? "1 " + word : $"{n} {word}s";

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

        void OnDone()
        {
            if (!active || !lesson.SkipPause()) return;
            Log("sandbox_end", $"gates={gates} turns={turns} measures={zeros + ones} zeros={zeros} ones={ones}");
            Close();
        }

        void Close()
        {
            active = false;
            hand = null;
            grab.SetGrabbable(false);
            palette.Hide();
            chart.Hide();
            done.Hide();
            if (animator.IsPlaying) animator.Complete();
            effects.Complete();
        }

        void Log(string type, string fields) => StudyEvents.Log(lesson.Time, type, fields);
    }
}
