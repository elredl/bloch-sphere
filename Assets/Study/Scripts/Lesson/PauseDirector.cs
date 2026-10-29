using Study.Quantum;
using Study.Visuals;
using UnityEngine;

namespace Study.Lesson
{
    // What happens inside each 60 s pause. The pause's start and end come from
    // LessonController (identical in every condition); this only decides what
    // fills it, using the pause-relative times in the timeline's pauseSchedule.
    //
    //   All:        sphere dims at onset; EmbodiedPause runs the prediction and
    //               the feedback. In between, Cond 3 presses the gate and turns
    //               the qubit (timeout arc at rotationDeadline) and Cond 1/2
    //               watch the scripted arc. Either way the end state is the same.
    //   All:        pause 4 runs the measurement demo at measurementDemoStart.
    //               The Next button shows once the prediction is over (answered
    //               or timed out), so nobody skips the question; pressing it ends
    //               the pause early (whatever has not run yet is skipped, and the
    //               operation snaps to its result so the end state is the same).
    //   Nothing else is shown in Cond 1 / 2: no dimming, no prompt, no cues.
    public class PauseDirector : MonoBehaviour
    {
        [SerializeField] LessonController lesson;
        [SerializeField] GateArcAnimator animator;
        [SerializeField] SphereDimmer dimmer;
        [SerializeField] MeasurementDemo demo;
        [SerializeField] EmbodiedPause embodied;
        [SerializeField] NextButtonView nextButton;

        LessonTimeline.PauseTask task;
        int pauseIndex = -1;
        double onset;
        bool arcDone;
        bool demoDone;
        bool skipped;

        void OnEnable()
        {
            lesson.PauseStarted += OnPauseStarted;
            lesson.PauseEnded += OnPauseEnded;
            nextButton.Pressed += OnNextPressed;
        }

        void OnDisable()
        {
            lesson.PauseStarted -= OnPauseStarted;
            lesson.PauseEnded -= OnPauseEnded;
            if (nextButton == null) return; // scene teardown
            nextButton.Pressed -= OnNextPressed;
            nextButton.Hide(); // an aborted lesson leaves no button behind
        }

        void OnNextPressed()
        {
            if (pauseIndex < 0 || !lesson.SkipPause()) return;
            skipped = true;
            nextButton.Hide();
        }

        void OnPauseStarted(int index, LessonTimeline.Segment segment, LessonTimeline.PauseTask pauseTask, double pauseOnset)
        {
            pauseIndex = index;
            task = pauseTask;
            onset = pauseOnset;
            arcDone = false;
            demoDone = !task.measurementDemo;
            skipped = false;
            dimmer.SetDimmed(true);
            if (index == 0) embodied.Validate(lesson.Timeline);
            embodied.Begin(index, task, pauseOnset);
        }

        void OnPauseEnded(int index)
        {
            if (index != pauseIndex) return;
            arcDone |= embodied.RotationStarted;
            embodied.End();
            nextButton.Hide();
            // The pause must never end without the operation applied: after Next, it snaps
            // to its result; otherwise this is a safety net that should never be needed.
            if (!arcDone) PlayArc(onset + lesson.Timeline.pauseSchedule.pauseLength, 0.001,
                skipped ? "arc snapped (Next)" : "late arc (should not happen)");
            if (animator.IsPlaying) animator.Complete();
            demo.Finish();
            demo.HidePanel();
            dimmer.SetDimmed(false);
            pauseIndex = -1;
        }

        void Update()
        {
            if (pauseIndex < 0 || lesson.OnBreak || skipped) return;
            LessonTimeline.PauseSchedule p = lesson.Timeline.pauseSchedule;
            double sincePause = lesson.Time - onset;

            embodied.Tick(p);
            if (!nextButton.IsVisible && embodied.Current != EmbodiedPause.Phase.Predict) nextButton.Show();

            if (!demoDone && sincePause >= p.measurementDemoStart)
            {
                demoDone = true;
                lesson.Record($"pause{pauseIndex + 1} measurement demo", onset + p.measurementDemoStart);
                StudyEvents.Log(lesson.Time, "measurement_demo", $"pause={pauseIndex + 1} shots={task.demoShots} seed={task.demoSeed}");
                // Demo on the state the pause operation produced (the same in every condition).
                Qubit prepared = Gate.FromName(task.gate).Apply(Qubit.FromName(task.startState));
                demo.Run(prepared, task.demoShots, task.demoSeed, onset + p.measurementDemoStart);
            }
        }

        void PlayArc(double scheduledStart, double duration, string what)
        {
            arcDone = true;
            lesson.Record($"pause{pauseIndex + 1} {what}", scheduledStart);
            animator.Play(Gate.FromName(task.gate), duration, scheduledStart);
        }
    }
}
