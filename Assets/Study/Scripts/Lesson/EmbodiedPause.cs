using System;
using Study.Quantum;
using Study.Visuals;
using UnityEngine;

namespace Study.Lesson
{
    // Condition 3 inside a pause, driven by PauseDirector with pause-relative time:
    //
    //   0 s       question 1: a dim prediction vector appears on the current state; the
    //             learner grabs its tip and moves it anywhere on the sphere (or points at
    //             a marker to snap it there), then presses Lock in
    //   20 s      not locked: a moved vector locks where it is, an untouched one times out
    //   lock      question 2: the five P(0) buttons; the first answer locks
    //   32 s      no answer: prediction_timeout
    //   answer    the gate button appears; the press plays the gate as an arc, while the
    //             prediction vector stays in place to compare against
    //   35 s      not pressed: the gate plays anyway, rotation_timeout
    //   done      feedback on both answers until 60 s (pause 4: panel hidden through the
    //             demo, feedback from 51 s)
    //
    // Every path applies the same gate, so the pause always ends in the same state
    // as in the other conditions, and nothing here changes when the pause ends.
    public class EmbodiedPause : MonoBehaviour
    {
        public enum Phase { Idle, Place, Predict, Gate, Finishing, Done }

        [SerializeField] LessonController lesson;
        [SerializeField] GateArcAnimator animator;
        [SerializeField] QubitState state;
        [SerializeField] PredictionPanelView panel;
        [SerializeField] PositionMarkersView markers;
        [SerializeField] GateDirectionView arrow;
        [SerializeField] QubitGrabTarget grab;
        [SerializeField] StateVectorView predictionVector;
        [SerializeField] TextAsset feedbackJson;
        [Tooltip("A placement this close to the true end state counts as correct (degrees; tilt only for height tasks).")]
        [SerializeField] float placementToleranceDegrees = 20f;
        [Tooltip("A wrong placement this close to a named state gets that state's misconception feedback.")]
        [SerializeField] float namedStateSnapDegrees = 30f;

        public Phase Current => phase;
        public int PauseNumber => index + 1;
        public double SincePause => lesson.Time - onset;
        public LessonTimeline.PauseTask Task => task;
        public PauseFeedback Feedback => feedback;
        public bool RotationStarted => phase >= Phase.Finishing;

        // Where the grab handle sits: the prediction vector's tip while placing, else null (the state's tip).
        public BlochVector? HandleTip => phase == Phase.Place ? placed : (BlochVector?)null;

        PauseFeedback feedback;
        LessonTimeline.PauseTask task;
        int index = -1;
        double onset;
        Phase phase = Phase.Idle;
        string answer;
        BlochVector placed;
        bool touched, locked, placementCorrect;
        string placedNear;
        Func<BlochVector> hand;
        bool feedbackShown;

        void Awake()
        {
            feedback = PauseFeedback.FromJson(feedbackJson.text);
        }

        void OnEnable()
        {
            panel.ChoicePicked += OnAnswer;
            panel.GatePressed += OnButton;
            markers.Picked += OnMarker;
        }

        void OnDisable()
        {
            panel.ChoicePicked -= OnAnswer;
            panel.GatePressed -= OnButton;
            markers.Picked -= OnMarker;
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
            hand = null;
            touched = locked = placementCorrect = false;
            placedNear = null;
            feedbackShown = false;
            phase = Phase.Place;

            placed = state.Current.Bloch;
            predictionVector.ShowDirection(state.Current);
            predictionVector.SetVisible(true);
            panel.ShowPrediction(feedback.ui.placeTitle, feedback.Task(task.id).placement.prompt, null, null, null);
            panel.ShowGateButton(feedback.ui.lockLabel, feedback.ui.placeHint);
            markers.Show();
            grab.SetGrabbable(true);
        }

        public void Tick(LessonTimeline.PauseSchedule p)
        {
            if (phase == Phase.Idle) return;
            double since = SincePause;

            if (phase == Phase.Place && hand != null)
            {
                BlochVector h = hand();
                if (h.Length > 1e-6) SetPlaced(h.Normalized());
            }

            if (phase == Phase.Place && since >= p.placementDeadline) TimeOutPlacement(p);
            if (phase == Phase.Predict && since >= p.predictionDeadline) TimeOutPrediction(p);
            if ((phase == Phase.Place || phase == Phase.Predict || phase == Phase.Gate) && since >= p.rotationDeadline) TimeOutRotation(p);
            if (phase == Phase.Finishing && !animator.IsPlaying) phase = Phase.Done;
            if (phase == Phase.Done && !feedbackShown && (!task.measurementDemo || since >= p.measurementDemoEnd)) ShowFeedback();
        }

        public void End()
        {
            if (phase == Phase.Idle) return;
            phase = Phase.Idle;
            hand = null;
            grab.SetGrabbable(false);
            predictionVector.SetVisible(false);
            panel.Hide();
            markers.Hide();
            arrow.Hide();
            index = -1;
        }

        // The panel's one button: Lock in while placing, the gate afterwards.
        void OnButton()
        {
            if (phase == Phase.Place) LockPlacement(false);
            else if (phase == Phase.Gate) PlayGate();
        }

        // A marker pick snaps the prediction vector to that state; it still has to be locked in.
        void OnMarker(string named)
        {
            if (phase != Phase.Place) return;
            touched = true;
            SetPlaced(Qubit.FromName(named).Bloch);
            Log("placement_snap", $"state={named}");
        }

        void SetPlaced(BlochVector direction)
        {
            placed = direction;
            predictionVector.ShowDirection(QubitAt(direction));
        }

        void LockPlacement(bool auto)
        {
            hand = null;
            grab.SetGrabbable(false);
            markers.Hide();
            locked = touched;
            if (locked)
            {
                double error = PauseFeedback.PlacementErrorDegrees(task, placed);
                placementCorrect = error <= placementToleranceDegrees;
                placedNear = PauseFeedback.NearestNamedState(placed, namedStateSnapDegrees);
                Log("placement_locked", $"x={placed.X:F3} y={placed.Y:F3} z={placed.Z:F3} error={error:F1} " +
                                        $"correct={(placementCorrect ? 1 : 0)} near={placedNear ?? "none"} auto={(auto ? 1 : 0)} rt={SincePause:F3}");
            }
            else
            {
                predictionVector.SetVisible(false); // nothing was predicted, so nothing to compare against
                Log("placement_timeout", "");
            }
            EnterPredict();
        }

        void TimeOutPlacement(LessonTimeline.PauseSchedule p)
        {
            lesson.Record($"pause{PauseNumber} placement deadline", onset + p.placementDeadline);
            LockPlacement(true);
        }

        void EnterPredict()
        {
            phase = Phase.Predict;
            string[] labels = Array.ConvertAll(task.choices, c => AnswerText(task, c));
            panel.ShowPrediction(feedback.ui.predictTitle, feedback.Task(task.id).prompt, task.choices, labels, null);
        }

        void OnAnswer(string picked)
        {
            if (phase != Phase.Predict) return;
            answer = picked;
            Log("prediction_response", $"answer={picked} rt={SincePause:F3}");
            panel.LockChoice(picked, string.Format(feedback.ui.lockedLine, AnswerText(task, picked)));
            EnterGate(feedback.ui.gateHint);
        }

        void TimeOutPrediction(LessonTimeline.PauseSchedule p)
        {
            Log("prediction_timeout", "");
            lesson.Record($"pause{PauseNumber} prediction timeout", onset + p.predictionDeadline);
            panel.RemovePrompt(feedback.ui.turnTitle);
            EnterGate(feedback.ui.timeoutHint);
        }

        void EnterGate(string hintText)
        {
            phase = Phase.Gate;
            panel.ShowGateButton(task.operationLabel, hintText);
        }

        void PlayGate()
        {
            Log("gate_press", "");
            BeginArc(lesson.Timeline.pauseSchedule.arcDuration, lesson.Time);
            Log("rotation_complete", "by=gate_press");
            lesson.Record($"pause{PauseNumber} gate pressed", lesson.Time);
        }

        void TimeOutRotation(LessonTimeline.PauseSchedule p)
        {
            double scheduled = onset + p.rotationDeadline;
            if (phase == Phase.Place) LockPlacement(true);
            Log("rotation_timeout", "gate_pressed=0");
            lesson.Record($"pause{PauseNumber} timeout arc", scheduled);
            BeginArc(p.timeoutArcMaxDuration, scheduled);
        }

        void BeginArc(double duration, double startTime)
        {
            phase = Phase.Finishing;
            grab.SetGrabbable(false);
            arrow.Hide();
            // Pause 4: nothing to say until the demo has run. Otherwise the panel says what to watch.
            if (task.measurementDemo) panel.Hide();
            else panel.ShowRotateHint(feedback.ui.turnTitle, feedback.ui.rotateHint);
            animator.Play(Gate.FromName(task.gate), duration, startTime);
        }

        // Both answers: the title is correct only when both are, neutral when neither was given.
        void ShowFeedback()
        {
            feedbackShown = true;
            PauseFeedback.Choice place = feedback.ForPlacement(task, locked, placementCorrect, placedNear);
            PauseFeedback.Choice chance = feedback.For(task, answer);
            bool anyWrong = place.Tone == PauseFeedback.Tone.Incorrect || chance.Tone == PauseFeedback.Tone.Incorrect;
            bool allRight = place.Tone == PauseFeedback.Tone.Correct && chance.Tone == PauseFeedback.Tone.Correct;
            var tone = allRight ? PredictionPanelView.Tone.Correct : anyWrong ? PredictionPanelView.Tone.Incorrect : PredictionPanelView.Tone.Neutral;
            string title = allRight ? feedback.ui.correctTitle : anyWrong ? feedback.ui.incorrectTitle : feedback.ui.neutralTitle;
            string lockedText = answer == null ? null : string.Format(feedback.ui.lockedLine, AnswerText(task, answer));
            panel.ShowFeedback(tone, title, place.Text + "\n\n" + chance.Text, lockedText);
            Log("feedback_shown", $"type={chance.Type} id={chance.Id} place_type={place.Type} place_id={place.Id}");
        }

        // From QubitGrabTarget (or the dev simulator). handPoint: the grab point in Bloch
        // coordinates, sphere center = origin, radius = 1. While placing, the prediction
        // vector points at it.
        public void GrabStarted(string mode, Func<BlochVector> handPoint)
        {
            if (phase != Phase.Place || hand != null) return;
            hand = handPoint;
            touched = true;
            Log("grab_start", $"mode={mode}");
        }

        public void GrabEnded()
        {
            if (phase != Phase.Place || hand == null) return;
            hand = null;
            Log("grab_end", $"error={PauseFeedback.PlacementErrorDegrees(task, placed):F1}");
        }

        void Log(string type, string fields) =>
            StudyEvents.Log(lesson.Time, type, $"pause={PauseNumber} {fields}".TrimEnd());

        // The state whose Bloch vector points along a unit direction (also used by MeasurementSandbox).
        internal static Qubit QubitAt(BlochVector direction) =>
            Qubit.FromBlochAngles(Math.Acos(Math.Max(-1, Math.Min(1, direction.Z))), Math.Atan2(direction.Y, direction.X));

        // How an answer reads on screen: "50%" for P(0) tasks, "|+i⟩" for positions.
        public static string AnswerText(LessonTimeline.PauseTask task, string answer)
        {
            if (task.prediction == "p0") return answer + "%";
            return "|" + answer.Replace("-", "−") + "⟩";
        }
    }
}
