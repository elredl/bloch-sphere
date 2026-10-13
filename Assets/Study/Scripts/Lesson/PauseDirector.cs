using Study.Quantum;
using Study.Visuals;
using UnityEngine;

namespace Study.Lesson
{
    // What happens inside each 60 s pause. The pause's start and end come from
    // LessonController (identical in every condition); this only decides what
    // fills it, using the pause-relative times in the timeline's pauseSchedule.
    //
    //   Cond 1 / 2: at scriptedArcStart the gate axis appears and the arc plays.
    //   Cond 3:     sphere dims at onset; the learner's prediction and rotation
    //               come in Checkpoint 4. Until then the learner is treated as
    //               never acting: at rotationDeadline the rotation plays as the
    //               agreed timeout arc. Either way the end state is the same.
    //   All:        pause 4 runs the measurement demo at measurementDemoStart.
    //   Nothing else is shown in Cond 1 / 2: no dimming, no prompt, no cues.
    public class PauseDirector : MonoBehaviour
    {
        [SerializeField] LessonController lesson;
        [SerializeField] GateArcAnimator animator;
        [SerializeField] SphereDimmer dimmer;
        [SerializeField] MeasurementDemo demo;

        LessonTimeline.PauseTask task;
        int pauseIndex = -1;
        double onset;
        bool arcDone;
        bool demoDone;

        void OnEnable()
        {
            lesson.PauseStarted += OnPauseStarted;
            lesson.PauseEnded += OnPauseEnded;
        }

        void OnDisable()
        {
            lesson.PauseStarted -= OnPauseStarted;
            lesson.PauseEnded -= OnPauseEnded;
        }

        void OnPauseStarted(int index, LessonTimeline.Segment segment, LessonTimeline.PauseTask pauseTask, double pauseOnset)
        {
            pauseIndex = index;
            task = pauseTask;
            onset = pauseOnset;
            arcDone = false;
            demoDone = !task.measurementDemo;
            if (lesson.Config.DimsAtPauses) dimmer.SetDimmed(true);
        }

        void OnPauseEnded(int index)
        {
            if (index != pauseIndex) return;
            // Safety net: the pause must never end without the operation applied.
            if (!arcDone) PlayArc(onset + lesson.Timeline.pauseSchedule.pauseLength, 0.001, "late arc (should not happen)");
            if (animator.IsPlaying) animator.Complete();
            demo.Finish();
            demo.HidePanel();
            if (lesson.Config.DimsAtPauses) dimmer.SetDimmed(false);
            pauseIndex = -1;
        }

        void Update()
        {
            if (pauseIndex < 0) return;
            LessonTimeline.PauseSchedule p = lesson.Timeline.pauseSchedule;
            double sincePause = lesson.Time - onset;

            if (!arcDone)
            {
                if (!lesson.Config.LearnerRotates && sincePause >= p.scriptedArcStart)
                    PlayArc(onset + p.scriptedArcStart, p.arcDuration, "scripted arc");
                else if (lesson.Config.LearnerRotates && sincePause >= p.rotationDeadline)
                    PlayArc(onset + p.rotationDeadline, p.timeoutArcMaxDuration, "timeout arc (no learner yet)");
            }

            if (!demoDone && sincePause >= p.measurementDemoStart)
            {
                demoDone = true;
                lesson.Record($"pause{pauseIndex + 1} measurement demo", onset + p.measurementDemoStart);
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
