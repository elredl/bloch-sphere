using System;
using Study.Quantum;
using Study.Visuals;
using UnityEngine;

namespace Study.Lesson
{
    // Every condition inside a pause, driven by PauseDirector. Pauses are untimed: each
    // step waits for the learner, and the Next button (PauseDirector) ends the pause
    // once the feedback shows.
    //
    //   question 1  where the turn will take the vector. Cond 1/3: a dim prediction
    //               vector appears on the current state; the learner drags its tip
    //               anywhere on the sphere (mouse or controller) or picks a marker to
    //               snap it there, then presses Lock in. Cond 2: a list of states (or
    //               of angles from |0>); a pick lights up and can be changed until Lock
    //               in, which shows the dim vector there
    //   question 2  the P(0) buttons: pick, then Lock in
    //   turn        Cond 1/3: the gate button; the press plays the gate as an arc.
    //               Cond 2: WatchDelay after locking, the arc plays by itself. Either
    //               way the prediction vector stays in place to compare against
    //   feedback    on both answers (pause 4: after the measurement demo)
    //
    // (The name is historical: this used to run in Condition 3 only.)
    //
    // Every path applies the same gate, so the pause always ends in the same state
    // as in the other conditions, and nothing here changes when the pause ends.
    public class EmbodiedPause : MonoBehaviour
    {
        public enum Phase { Idle, Place, Predict, Gate, Watch, Finishing, Done }

        // Cond 2: seconds between locking the answer and the scripted arc, so the
        // locked answer reads before the vector moves.
        public const double WatchDelay = 1.0;

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
        // Seconds since the pause began, on lesson.Now (lesson time stands still in a pause).
        public double SincePause => lesson.Now - onset;
        public LessonTimeline.PauseTask Task => task;
        public PauseFeedback Feedback => feedback;
        public bool RotationStarted => phase >= Phase.Finishing;
        public bool TurnFinished => phase == Phase.Done;
        // Everything is done and the feedback is up: the Next button may show.
        public bool FeedbackShown => feedbackShown;
        bool HandsOn => lesson.Config.LearnerRotates;

        // Where the grab handle sits: the prediction vector's tip while placing, else null (the state's tip).
        public BlochVector? HandleTip => phase == Phase.Place ? placed : (BlochVector?)null;

        PauseFeedback feedback;
        LessonTimeline.PauseTask task;
        int index = -1;
        double onset;
        Phase phase = Phase.Idle;
        string answer;
        string selected; // picked on the panel, not yet locked in
        BlochVector placed;
        bool touched, placementCorrect;
        string placedNear;
        Func<BlochVector> hand;
        bool feedbackShown;
        bool feedbackHeld; // pause 4: until PauseDirector's measurement demo has run
        double watchAt;    // Cond 2: SincePause at which the scripted arc starts

        void Awake()
        {
            feedback = PauseFeedback.FromJson(feedbackJson.text);
        }

        void OnEnable()
        {
            panel.ChoicePicked += OnChoice;
            panel.GatePressed += OnButton;
            markers.Picked += OnMarker;
        }

        void OnDisable()
        {
            panel.ChoicePicked -= OnChoice;
            panel.GatePressed -= OnButton;
            markers.Picked -= OnMarker;
        }

        // Called once the timeline is loaded. Errors here mean the feedback file and timeline disagree.
        public void Validate(LessonTimeline timeline)
        {
            foreach (string error in feedback.Validate(timeline)) Debug.LogError("[Pause] pause_feedback.json: " + error);
        }

        // pauseOnset: lesson.Now at the pause onset.
        public void Begin(int pauseIndex, LessonTimeline.PauseTask pauseTask, double pauseOnset)
        {
            index = pauseIndex;
            task = pauseTask;
            onset = pauseOnset;
            answer = null;
            selected = null;
            hand = null;
            touched = placementCorrect = false;
            placedNear = null;
            feedbackShown = false;
            feedbackHeld = task.measurementDemo;
            phase = Phase.Place;

            placed = state.Current.Bloch;
            string prompt = feedback.Task(task.id).placement.prompt;
            if (!HandsOn)
            {
                // Cond 2: nothing to move; the dim vector appears once a choice is locked in.
                string[] choices = PauseFeedback.PlacementChoices(task);
                string[] labels = Array.ConvertAll(choices, c => PauseFeedback.PlacementChoiceText(task, c));
                string hint = task.placementMatch == "height" ? feedback.ui.placeHintListHeight : feedback.ui.placeHintList;
                panel.ShowPrediction(feedback.ui.placeTitle, prompt, choices, labels, hint);
                return;
            }
            predictionVector.ShowDirection(state.Current);
            predictionVector.SetVisible(true);
            panel.ShowPrediction(feedback.ui.placeTitle, prompt, null, null, null);
            panel.ShowGateButton(feedback.ui.lockLabel, lesson.Config.UsesHeadset ? feedback.ui.placeHint : feedback.ui.placeHintDesktop);
            markers.Show();
            grab.SetGrabbable(true);
        }

        public void Tick()
        {
            if (phase == Phase.Idle) return;

            if (phase == Phase.Place && hand != null)
            {
                BlochVector h = hand();
                if (h.Length > 1e-6) SetPlaced(h.Normalized());
            }

            if (phase == Phase.Watch && SincePause >= watchAt) PlayScriptedArc();
            if (phase == Phase.Finishing && !animator.IsPlaying) phase = Phase.Done;
            if (phase == Phase.Done && !feedbackShown && !feedbackHeld) ShowFeedback();
        }

        // Pause 4: the measurement demo has run; the feedback can follow.
        public void ReleaseFeedback() => feedbackHeld = false;

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

        // The panel's one button: Lock in for either question, the gate afterwards.
        void OnButton()
        {
            if (phase == Phase.Place && HandsOn) LockPlacement();
            else if (phase == Phase.Place && selected != null) PickPlacement(selected);
            else if (phase == Phase.Predict && selected != null) LockAnswer(selected);
            else if (phase == Phase.Gate) PlayGate();
        }

        // A pick on the panel (Cond 2's placement list, or the P(0) answers): it lights up
        // and can still be changed; Lock in commits it.
        void OnChoice(string picked)
        {
            if (phase != Phase.Predict && !(phase == Phase.Place && !HandsOn)) return;
            selected = picked;
            panel.SelectChoice(picked);
            panel.ShowGateButton(feedback.ui.lockLabel, feedback.ui.chooseHint);
            Log(phase == Phase.Place ? "placement_select" : "prediction_select", $"choice={picked}");
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

        // Cond 2: the locked list choice is the placement. The dim vector shows it.
        void PickPlacement(string choice)
        {
            touched = true;
            SetPlaced(PauseFeedback.PlacementChoiceVector(task, choice));
            predictionVector.SetVisible(true);
            Log("placement_pick", $"choice={choice}");
            LockPlacement();
        }

        // Locks wherever the vector points, moved or not (not moving it is an answer too).
        void LockPlacement()
        {
            hand = null;
            grab.SetGrabbable(false);
            markers.Hide();
            double error = PauseFeedback.PlacementErrorDegrees(task, placed);
            placementCorrect = error <= placementToleranceDegrees;
            // A list pick is exact: only a named state itself gets that state's misconception.
            placedNear = PauseFeedback.NearestNamedState(placed, HandsOn ? namedStateSnapDegrees : 1);
            Log("placement_locked", $"x={placed.X:F3} y={placed.Y:F3} z={placed.Z:F3} error={error:F1} " +
                                    $"correct={(placementCorrect ? 1 : 0)} near={placedNear ?? "none"} moved={(touched ? 1 : 0)} rt={SincePause:F3}");
            EnterPredict();
        }

        void EnterPredict()
        {
            phase = Phase.Predict;
            selected = null;
            string[] labels = Array.ConvertAll(task.choices, c => AnswerText(task, c));
            panel.ShowPrediction(feedback.ui.predictTitle, feedback.Task(task.id).prompt, task.choices, labels, null);
        }

        void LockAnswer(string picked)
        {
            answer = picked;
            Log("prediction_response", $"answer={picked} rt={SincePause:F3}");
            panel.LockChoice(picked, string.Format(feedback.ui.lockedLine, AnswerText(task, picked)));
            if (HandsOn) EnterGate();
            else EnterWatch();
        }

        // Cond 2: no gate button; the turn plays by itself.
        void EnterWatch()
        {
            phase = Phase.Watch;
            watchAt = SincePause + WatchDelay;
        }

        void PlayScriptedArc()
        {
            lesson.Record($"pause{PauseNumber} scripted arc", lesson.Time);
            BeginArc(onset + watchAt);
        }

        void EnterGate()
        {
            phase = Phase.Gate;
            panel.ShowGateButton(task.operationLabel, feedback.ui.gateHint);
        }

        void PlayGate()
        {
            Log("gate_press", $"rt={SincePause:F3}");
            lesson.Record($"pause{PauseNumber} gate pressed", lesson.Time);
            BeginArc(lesson.Now);
        }

        // startTime: on lesson.Now.
        void BeginArc(double startTime)
        {
            phase = Phase.Finishing;
            grab.SetGrabbable(false);
            arrow.Hide();
            // Pause 4: nothing to say until the demo has run. Otherwise the panel says what to watch.
            if (task.measurementDemo) panel.Hide();
            else panel.ShowRotateHint(feedback.ui.turnTitle, feedback.ui.rotateHint);
            animator.Play(Gate.FromName(task.gate), lesson.Timeline.pauseSchedule.arcDuration, startTime);
        }

        // Both answers: the title is correct only when both are.
        void ShowFeedback()
        {
            feedbackShown = true;
            PauseFeedback.Choice place = feedback.ForPlacement(task, true, placementCorrect, placedNear);
            PauseFeedback.Choice chance = feedback.For(task, answer);
            bool anyWrong = place.Tone == PauseFeedback.Tone.Incorrect || chance.Tone == PauseFeedback.Tone.Incorrect;
            bool allRight = place.Tone == PauseFeedback.Tone.Correct && chance.Tone == PauseFeedback.Tone.Correct;
            var tone = allRight ? PredictionPanelView.Tone.Correct : anyWrong ? PredictionPanelView.Tone.Incorrect : PredictionPanelView.Tone.Neutral;
            string title = allRight ? feedback.ui.correctTitle : anyWrong ? feedback.ui.incorrectTitle : feedback.ui.neutralTitle;
            string lockedText = answer == null ? null : string.Format(feedback.ui.lockedLine, AnswerText(task, answer));
            panel.ShowFeedback(tone, title, place.Text + "\n\n" + chance.Text, lockedText);
            Log("feedback_shown", $"type={chance.Type} id={chance.Id} place_type={place.Type} place_id={place.Id} rt={SincePause:F3}");
        }

        // From QubitGrabTarget (or the dev simulator). handPoint: the grab point in Bloch
        // coordinates, sphere center = origin, radius = 1. While placing, the prediction
        // vector points at it.
        public void GrabStarted(string mode, Func<BlochVector> handPoint)
        {
            if (phase != Phase.Place || hand != null || !HandsOn) return;
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

        // The state whose Bloch vector points along a unit direction.
        static Qubit QubitAt(BlochVector direction) =>
            Qubit.FromBlochAngles(Math.Acos(Math.Max(-1, Math.Min(1, direction.Z))), Math.Atan2(direction.Y, direction.X));

        // How an answer reads on screen: "50%" for P(0) tasks, "|+i⟩" for positions.
        public static string AnswerText(LessonTimeline.PauseTask task, string answer)
        {
            if (task.prediction == "p0") return answer + "%";
            return "|" + answer.Replace("-", "−") + "⟩";
        }
    }
}
