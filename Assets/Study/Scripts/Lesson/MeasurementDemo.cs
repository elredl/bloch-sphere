using System.Collections.Generic;
using Study.Quantum;
using Study.Visuals;
using UnityEngine;

namespace Study.Lesson
{
    // The repeated-measurement demo. Scripted and identical in every condition:
    // the learner never triggers shots, and a fixed seed fixes the outcomes.
    //
    //   0.0 s         ghost vector appears at the prepared state; panel resets
    //   0.5 - 5.5 s   first 5 shots, one per second: flash, snap to the pole,
    //                 hold, fade out, fade back in at the prepared state
    //   5.5 - 9.5 s   remaining shots fill the histogram, no flash, no motion
    //   9.5 s         ghost hidden; vector is at the prepared state
    // The panel stays up until a "hide measurement_panel" event or the pause ends.
    public class MeasurementDemo : MonoBehaviour
    {
        public const int SlowShots = 5;
        public const double LeadIn = 0.5;
        public const double SlowShotInterval = 1.0;   // 1 Hz: flashes stay far below 3 Hz
        public const double SnapHold = 0.45;          // collapsed state held this long
        public const double ReprepareTime = 0.45;     // fade out + fade in
        public const double FastFillDuration = 4.0;
        public const double Duration = LeadIn + SlowShots * SlowShotInterval + FastFillDuration;

        [SerializeField] LessonController lesson;
        [SerializeField] QubitState state;
        [SerializeField] VectorEffects effects;
        [SerializeField] StateVectorView ghost;
        [SerializeField] MeasurementPanelView panel;

        readonly List<(double time, int shot)> pending = new List<(double, int)>();
        int[] outcomes;
        Qubit prepared;
        double startTime;
        bool running;

        public bool IsRunning => running;

        // Offsets from demo start at which each shot is taken. Pure, so tests can check it.
        public static double[] ShotTimes(int shots)
        {
            var times = new double[shots];
            int fast = shots - SlowShots;
            for (int i = 0; i < shots; i++)
                times[i] = i < SlowShots
                    ? LeadIn + i * SlowShotInterval
                    : LeadIn + SlowShots * SlowShotInterval + (i - SlowShots + 1) * FastFillDuration / fast;
            return times;
        }

        void OnEnable() => lesson.MeasurementDemoRequested += OnNarratedDemo;
        void OnDisable() => lesson.MeasurementDemoRequested -= OnNarratedDemo;

        void OnNarratedDemo(LessonTimeline.LessonEvent e) => Run(Qubit.FromName(e.state), e.shots, e.seed, lesson.ToNow(e.t));

        // scheduledStart: on lesson.Now.
        public void Run(Qubit preparedState, int shots, ulong seed, double scheduledStart)
        {
            if (!state.Current.IsSameStateAs(preparedState, 1e-6))
                Debug.LogError($"[Demo] live state {state.Current.Bloch} differs from the demo's prepared state {preparedState.Bloch}");
            prepared = preparedState;
            outcomes = Measurement.Shots(preparedState, shots, seed);
            startTime = scheduledStart;
            pending.Clear();
            double[] times = ShotTimes(shots);
            for (int i = 0; i < shots; i++) pending.Add((scheduledStart + times[i], i));

            ghost.ShowDirection(prepared);
            ghost.SetVisible(true);
            panel.Show(shots);
            running = true;
            Debug.Log($"[Demo] start at {LessonTimeline.Seconds(scheduledStart)}: {shots} shots, seed {seed}, " +
                      $"P(0)={prepared.ProbabilityZero:F3}, zeros will be {Measurement.CountZeros(outcomes)}");
        }

        public void HidePanel() => panel.Hide();

        void Update()
        {
            if (!running) return;
            double now = lesson.Now;
            // Catch up on every shot that is due, in order, even after a slow frame.
            while (pending.Count > 0 && pending[0].time <= now)
            {
                (double time, int shot) = pending[0];
                pending.RemoveAt(0);
                int outcome = outcomes[shot];
                panel.Add(outcome);
                if (shot < SlowShots)
                {
                    effects.Collapse(outcome, time);
                    effects.Reprepare(prepared, time + SnapHold, ReprepareTime);
                }
            }
            if (pending.Count == 0 && now >= startTime + Duration)
            {
                effects.Complete();
                ghost.SetVisible(false);
                running = false;
                Debug.Log($"[Demo] done at {LessonTimeline.Seconds(now)}; state back at {state.Current.Bloch}");
            }
        }

        // Ends the demo immediately with every shot counted (used if a pause ends early).
        public void Finish()
        {
            if (!running) return;
            foreach ((double _, int shot) in pending) panel.Add(outcomes[shot]);
            pending.Clear();
            effects.Complete();
            if (!state.Current.IsSameStateAs(prepared, 1e-9)) state.Set(prepared);
            ghost.SetVisible(false);
            running = false;
        }
    }
}
