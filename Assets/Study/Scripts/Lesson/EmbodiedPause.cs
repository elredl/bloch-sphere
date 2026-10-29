using System;
using Study.Quantum;
using Study.Visuals;
using UnityEngine;

namespace Study.Lesson
{
    // Every condition inside a pause, driven by PauseDirector with pause-relative time.
    // The prediction and the feedback are the same in all three conditions; only the
    // turn in between differs (Cond 3 turns the qubit by hand, Cond 1/2 watch it):
    //
    //   0 s       prompt (panel; for position tasks also the six sphere markers)
    //   answer    first answer locks, prediction_response logged
    //   25 s      no answer: prompt removed, prediction_timeout
    //   Cond 1/2  WatchDelay after the lock (or the timeout) the scripted arc plays
    //   Cond 3    gate button appears once the prediction is over
    //   press     gate_press: axis and direction arrow show, the grab unlocks
    //   grab      ratchet rotation about the gate axis; release within tolerance completes
    //             and the vector eases to the exact gate result
    //   35 s      not done: the remaining rotation plays as a scripted arc, rotation_timeout
    //   done      feedback until 60 s (pause 4: panel hidden through the demo, feedback from 51 s)
    //
    // (The name is historical: this used to run in Condition 3 only.)
    //
    // Every path applies the same gate, so the pause always ends in the same state
    // as in the other conditions, and nothing here changes when the pause ends.
    public class EmbodiedPause : MonoBehaviour
    {
        public enum Phase { Idle, Predict, Gate, Watch, Rotate, Finishing, Done }

        // Cond 1/2: seconds between the prediction locking (or timing out) and the scripted
        // arc, so the locked answer reads before the vector moves.
        public const double WatchDelay = 1.0;

        [SerializeField] LessonController lesson;
        [SerializeField] GateArcAnimator animator;
        [SerializeField] QubitState state;
        [SerializeField] PredictionPanelView panel;
        [SerializeField] PositionMarkersView markers;
        [SerializeField] GateDirectionView arrow;
        [SerializeField] QubitGrabTarget grab;
        [SerializeField] TextAsset feedbackJson;
        [Tooltip("A release this close to the target angle completes the gate (spec: 10, widen to 15 only after dry runs).")]
        [SerializeField] float toleranceDegrees = 10f;

        public Phase Current => phase;
        public int PauseNumber => index + 1;
        public double SincePause => lesson.Time - onset;
        public LessonTimeline.PauseTask Task => task;
        public PauseFeedback Feedback => feedback;
        public bool RotationStarted => phase >= Phase.Finishing || animator.IsHeld;

        PauseFeedback feedback;
        LessonTimeline.PauseTask task;
        int index = -1;
        double onset;
        Phase phase = Phase.Idle;
        string answer;
        RatchetRotation ratchet;
        Func<BlochVector> hand;
        bool feedbackShown;
        double watchAt; // Cond 1/2: pause-relative time the scripted arc starts

        void Awake()
        {
            feedback = PauseFeedback.FromJson(feedbackJson.text);
        }

        void OnEnable()
        {
            panel.ChoicePicked += OnAnswer;
            panel.GatePressed += OnGatePressed;
            markers.Picked += OnAnswer;
        }

        void OnDisable()
        {
            panel.ChoicePicked -= OnAnswer;
            panel.GatePressed -= OnGatePressed;
            markers.Picked -= OnAnswer;
        }

        // Called once the timeline is loaded. Errors here mean the feedback file and timeline disagree.
        public void Validate(LessonTimeline timeline)
        {
            foreach (string error in feedback.Validate(timeline)) Debug.LogError("[Pause] pause_feedback.json: " + error);
        }

        public void Begin(int pauseIndex, LessonTimeline.PauseTask pauseTask, double pauseOnset)
        {
            index = pauseIndex;
            task = pauseTask;
            onset = pauseOnset;
            answer = null;
            ratchet = null;
            hand = null;
            feedbackShown = false;
            phase = Phase.Predict;

            PauseFeedback.TaskText text = feedback.Task(task.id);
            bool position = task.prediction == "position";
            string[] labels = position ? null : Array.ConvertAll(task.choices, c => AnswerText(task, c));
            string positionHint = lesson.Config.UsesHeadset ? feedback.ui.positionHint : feedback.ui.positionHintDesktop;
            panel.ShowPrediction(feedback.ui.predictTitle, text.prompt, task.choices, labels, position ? positionHint : null);
            if (position) markers.Show();
            grab.SetGrabbable(false);
        }

        public void Tick(LessonTimeline.PauseSchedule p)
        {
            if (phase == Phase.Idle) return;
            double since = SincePause;

            if (phase == Phase.Rotate && ratchet.IsGrabbing)
            {
                if (ratchet.Move(hand())) grab.WrongWayTick();
                animator.SetHeldFraction(ratchet.Fraction);
                arrow.SetAngle(ratchet.Angle);
            }

            if (phase == Phase.Predict && since >= p.predictionDeadline) TimeOutPrediction(p);
            if (phase == Phase.Watch && since >= watchAt) PlayScriptedArc(p);
            if ((phase == Phase.Gate || phase == Phase.Rotate) && since >= p.rotationDeadline) TimeOutRotation(p);
            if (phase == Phase.Finishing && !animator.IsPlaying) phase = Phase.Done;
            if (phase == Phase.Done && !feedbackShown && (!task.measurementDemo || since >= p.measurementDemoEnd)) ShowFeedback();
        }

        public void End()
        {
            if (phase == Phase.Idle) return;
            phase = Phase.Idle;
            grab.SetGrabbable(false);
            panel.Hide();
            markers.Hide();
            arrow.Hide();
            index = -1;
        }

        void OnAnswer(string picked)
        {
            if (phase != Phase.Predict) return;
            answer = picked;
            Log("prediction_response", $"answer={picked} rt={SincePause:F3}");
            markers.Hide();
            panel.LockChoice(picked, string.Format(feedback.ui.lockedLine, AnswerText(task, picked)));
            if (lesson.Config.LearnerRotates) EnterGate(feedback.ui.gateHint);
            else EnterWatch(SincePause);
        }

        void TimeOutPrediction(LessonTimeline.PauseSchedule p)
        {
            Log("prediction_timeout", "");
            lesson.Record($"pause{PauseNumber} prediction timeout", onset + p.predictionDeadline);
            markers.Hide();
            panel.RemovePrompt(feedback.ui.turnTitle);
            if (lesson.Config.LearnerRotates) EnterGate(feedback.ui.timeoutHint);
            else EnterWatch(p.predictionDeadline);
        }

        // Cond 1/2: no gate button and no grab; the turn plays by itself.
        void EnterWatch(double predictionOver)
        {
            phase = Phase.Watch;
            watchAt = predictionOver + WatchDelay;
        }

        void PlayScriptedArc(LessonTimeline.PauseSchedule p)
        {
            double scheduled = onset + watchAt;
            phase = Phase.Finishing;
            if (task.measurementDemo) panel.Hide(); // nothing to say until the demo has run
            else panel.ShowRotateHint(feedback.ui.turnTitle, feedback.ui.watchHint);
            lesson.Record($"pause{PauseNumber} scripted arc", scheduled);
            animator.Play(Gate.FromName(task.gate), p.arcDuration, scheduled);
        }

        void EnterGate(string hintText)
        {
            phase = Phase.Gate;
            panel.ShowGateButton(task.operationLabel, hintText);
        }

        void OnGatePressed()
        {
            if (phase != Phase.Gate) return;
            Log("gate_press", "");
            Qubit start = state.Current;
            Gate gate = Gate.FromName(task.gate);
            ratchet = new RatchetRotation(start, gate, toleranceDegrees * Math.PI / 180);
            animator.BeginHeld(gate);
            arrow.Show(gate, start, 0);
            panel.ShowRotateHint(feedback.ui.turnTitle, feedback.ui.rotateHint);
            phase = Phase.Rotate;
            grab.SetGrabbable(true);
        }

        // From QubitGrabTarget (or the dev simulator). handPoint: the grab point in Bloch
        // coordinates, sphere center = origin, radius = 1.
        public void GrabStarted(string mode, Func<BlochVector> handPoint)
        {
            if (phase != Phase.Rotate || ratchet.IsGrabbing) return;
            hand = handPoint;
            ratchet.Begin(hand());
            Log("grab_start", $"mode={mode} angle={Degrees(ratchet.Angle):F1}");
        }

        public void GrabEnded()
        {
            if (phase != Phase.Rotate || ratchet == null || !ratchet.IsGrabbing) return;
            bool complete = ratchet.End();
            Log("grab_end", $"angle={Degrees(ratchet.Angle):F1} target={Degrees(ratchet.Gate.Angle):F1} complete={(complete ? 1 : 0)}");
            if (!complete) return;
            phase = Phase.Finishing;
            grab.SetGrabbable(false);
            arrow.Hide();
            if (task.measurementDemo) panel.Hide(); // nothing to say until the demo has run
            // The last few degrees ease in at the scripted arc's speed.
            double remaining = 1 - ratchet.Fraction;
            animator.FinishFrom(Math.Max(0.15, lesson.Timeline.pauseSchedule.arcDuration * remaining));
            Log("rotation_complete", $"by=learner");
            lesson.Record($"pause{PauseNumber} learner rotation done", lesson.Time);
        }

        void TimeOutRotation(LessonTimeline.PauseSchedule p)
        {
            double scheduled = onset + p.rotationDeadline;
            bool pressed = phase == Phase.Rotate;
            if (!pressed) animator.BeginHeld(Gate.FromName(task.gate)); // never pressed: the whole turn plays
            if (ratchet != null && ratchet.IsGrabbing)
            {
                ratchet.End();
                Log("grab_end", $"angle={Degrees(ratchet.Angle):F1} target={Degrees(ratchet.Gate.Angle):F1} complete=0 forced=1");
            }
            phase = Phase.Finishing;
            grab.SetGrabbable(false);
            arrow.Hide();
            panel.Hide(); // the scripted arc plays; feedback brings the panel back
            double remaining = 1 - animator.HeldFraction;
            Log("rotation_timeout", $"gate_pressed={(pressed ? 1 : 0)} remaining={Degrees(remaining * Gate.FromName(task.gate).Angle):F1}");
            lesson.Record($"pause{PauseNumber} timeout arc", scheduled);
            animator.FinishFrom(Math.Max(0.15, p.timeoutArcMaxDuration * remaining), scheduled);
        }

        void ShowFeedback()
        {
            feedbackShown = true;
            PauseFeedback.Choice choice = feedback.For(task, answer);
            var tone = choice.Tone == PauseFeedback.Tone.Correct ? PredictionPanelView.Tone.Correct
                : choice.Tone == PauseFeedback.Tone.Incorrect ? PredictionPanelView.Tone.Incorrect
                : PredictionPanelView.Tone.Neutral;
            string locked = answer == null ? null : string.Format(feedback.ui.lockedLine, AnswerText(task, answer));
            panel.ShowFeedback(tone, choice.Title, choice.Text, locked);
            Log("feedback_shown", $"type={choice.Type} id={choice.Id}");
        }

        void Log(string type, string fields) =>
            StudyEvents.Log(lesson.Time, type, $"pause={PauseNumber} {fields}".TrimEnd());

        static double Degrees(double radians) => radians * 180 / Math.PI;

        // How an answer reads on screen: "50%" for P(0) tasks, "|+i⟩" for positions.
        public static string AnswerText(LessonTimeline.PauseTask task, string answer)
        {
            if (task.prediction == "p0") return answer + "%";
            return "|" + answer.Replace("-", "−") + "⟩";
        }
    }
}
