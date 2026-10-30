using Study.Quantum;
using Study.Visuals;
using UnityEngine;

namespace Study.Lesson
{
    // What happens inside each pause. The pause starts on LessonController's
    // schedule (identical in every condition) and lasts until the learner presses
    // Next; this decides what fills it. Nothing in it is timed.
    //
    //   All:        sphere dims at onset; EmbodiedPause runs the prediction and
    //               the feedback. In between, Cond 1/3 press the gate and Cond 2
    //               watches the scripted arc. Either way the end state is the same.
    //   All:        pause 4 runs the measurement demo once the turn is done, then
    //               the feedback. The Next button shows with the feedback, so
    //               every step has happened; pressing it ends the pause.
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
        bool arcDone;
        bool demoDone;
        bool demoStarted;
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
            arcDone = false;
            demoDone = !task.measurementDemo;
            demoStarted = false;
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
            // The pause must never end without the operation applied. Next only shows once
            // it has been, so this is a safety net (an aborted or dev-skipped pause).
            if (!arcDone) PlayArc(lesson.Now, 0.001, "arc snapped (should not happen)");
            if (animator.IsPlaying) animator.Complete();
            demo.Finish();
            demo.HidePanel();
            dimmer.SetDimmed(false);
            pauseIndex = -1;
        }

        void Update()
        {
            if (pauseIndex < 0 || lesson.OnBreak || skipped) return;
            embodied.Tick();
            if (!nextButton.IsVisible && embodied.FeedbackShown) nextButton.Show();

            if (!demoStarted && !demoDone && embodied.TurnFinished)
            {
                demoStarted = true;
                lesson.Record($"pause{pauseIndex + 1} measurement demo", lesson.Time);
                StudyEvents.Log(lesson.Time, "measurement_demo", $"pause={pauseIndex + 1} shots={task.demoShots} seed={task.demoSeed}");
                // Demo on the state the pause operation produced (the same in every condition).
                Qubit prepared = Gate.FromName(task.gate).Apply(Qubit.FromName(task.startState));
                demo.Run(prepared, task.demoShots, task.demoSeed, lesson.Now);
            }
            else if (demoStarted && !demoDone && !demo.IsRunning)
            {
                demoDone = true;
                embodied.ReleaseFeedback();
            }
        }

        void PlayArc(double scheduledStart, double duration, string what)
        {
            arcDone = true;
            lesson.Record($"pause{pauseIndex + 1} {what}", lesson.Time);
            animator.Play(Gate.FromName(task.gate), duration, scheduledStart);
        }
    }
}
