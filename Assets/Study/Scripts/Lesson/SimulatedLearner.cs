using System;
using Study.Quantum;
using Study.Visuals;
using UnityEngine;

namespace Study.Lesson
{
    // Development only: plays a learner through each pause so the whole flow (placement,
    // lock in, prediction, gate press, feedback, Next) can be run in the Editor without
    // a headset. It uses the same entry points as the real controls: a grab point fed to
    // EmbodiedPause exactly as QubitGrabTarget feeds it, the panel's buttons and answer
    // picks, and the Next button. Cond 2 has no grab or gate button: it picks its
    // placement from the list and watches the turn.
    // Added by LessonDevTools.SimulateLearner; never part of the saved scene.
    public class SimulatedLearner : MonoBehaviour
    {
        const double DragSeconds = 1.5;
        const double LockDelay = 1.0; // a list pick is locked in this long after it is made

        // One learner behavior, times in seconds from pause onset. Pauses are untimed, so
        // each step waits until both its time has come and the pause has reached it.
        public struct Profile
        {
            public string Name;
            public string Place;         // "correct", "misconception", "generic", or null = locks the vector unmoved
            public string Answer;        // same kinds, for the P(0) question
            public string FirstAnswer;   // picked first and changed before locking in; null = none
            public double PlaceAt, LockAt, AnswerAt, PressAt, NextAt;
        }

        public static Profile Named(string name)
        {
            switch (name)
            {
                case "fast": return new Profile { Name = name, Place = "correct", PlaceAt = 3, LockAt = 7, Answer = "correct", AnswerAt = 10, PressAt = 13, NextAt = 20 };
                case "wrong": return new Profile { Name = name, Place = "misconception", PlaceAt = 4, LockAt = 9, Answer = "misconception", AnswerAt = 14, PressAt = 18, NextAt = 26 };
                case "generic": return new Profile { Name = name, Place = "generic", PlaceAt = 5, LockAt = 11, Answer = "generic", AnswerAt = 16, PressAt = 20, NextAt = 28 };
                case "slow": return new Profile { Name = name, Place = "correct", PlaceAt = 40, LockAt = 60, Answer = "correct", AnswerAt = 75, PressAt = 90, NextAt = 110 };
                case "change": return new Profile { Name = name, Place = "correct", PlaceAt = 3, LockAt = 7, FirstAnswer = "misconception", Answer = "correct", AnswerAt = 10, PressAt = 16, NextAt = 22 };
                case "still": return new Profile { Name = name, Place = null, LockAt = 5, Answer = "generic", AnswerAt = 8, PressAt = 11, NextAt = 18 };
                default: throw new ArgumentException("unknown learner profile " + name + " (fast, wrong, generic, slow, change, still)");
            }
        }

        Profile[] profiles;
        EmbodiedPause pause;
        PredictionPanelView panel;
        PositionMarkersView markers;
        NextButtonView next;
        LessonController lesson;
        int pauseSeen = -1;
        bool dragging, dragged, placePicked, lockPressed, firstPicked, answered, answerLocked, pressed, nextPressed;
        double dragStart, answeredAt;
        BlochVector start, target, axis;
        double arc;

        public void Configure(Profile[] perPause, EmbodiedPause embodied, PredictionPanelView predictionPanel, PositionMarkersView positionMarkers)
        {
            profiles = perPause;
            pause = embodied;
            panel = predictionPanel;
            markers = positionMarkers;
            next = FindAnyObjectByType<NextButtonView>();
            lesson = FindAnyObjectByType<LessonController>();
        }

        void Update()
        {
            if (pause == null || pause.Current == EmbodiedPause.Phase.Idle) return;
            if (pause.PauseNumber != pauseSeen) StartPause();
            Profile p = profiles[Math.Min(pause.PauseNumber - 1, profiles.Length - 1)];
            double t = pause.SincePause;
            bool placing = pause.Current == EmbodiedPause.Phase.Place;
            bool list = !lesson.Config.LearnerRotates; // Cond 2 places from a list

            if (placing && list && !placePicked && t >= p.PlaceAt)
            {
                placePicked = true;
                string[] choices = PauseFeedback.PlacementChoices(pause.Task);
                panel.PickChoice(Array.IndexOf(choices, ChooseListPlacement(p.Place ?? "generic")));
            }
            if (placing && !list && !dragged && !dragging && p.Place != null && t >= p.PlaceAt)
                BeginDrag(p.Place, t);
            if (dragging && t - dragStart >= DragSeconds)
            {
                dragging = false;
                dragged = true;
                pause.GrabEnded();
            }
            if (placing && !lockPressed && !dragging && (dragged || p.Place == null || placePicked) && t >= p.LockAt)
            {
                lockPressed = true;
                panel.PressGate();
            }

            bool predicting = pause.Current == EmbodiedPause.Phase.Predict;
            if (predicting && p.FirstAnswer != null && !firstPicked && t >= p.AnswerAt - 2)
            {
                firstPicked = true;
                panel.PickChoice(Array.IndexOf(pause.Task.choices, ChooseAnswer(p.FirstAnswer)));
            }
            if (predicting && !answered && t >= p.AnswerAt)
            {
                answered = true;
                answeredAt = t;
                panel.PickChoice(Array.IndexOf(pause.Task.choices, ChooseAnswer(p.Answer)));
            }
            if (predicting && answered && !answerLocked && t >= answeredAt + LockDelay)
            {
                answerLocked = true;
                panel.PressGate();
            }
            if (!pressed && t >= p.PressAt && pause.Current == EmbodiedPause.Phase.Gate)
            {
                pressed = true;
                panel.PressGate();
            }
            if (!nextPressed && t >= p.NextAt && pause.FeedbackShown && next != null && next.IsVisible)
            {
                nextPressed = true;
                next.Press();
            }
        }

        void StartPause()
        {
            pauseSeen = pause.PauseNumber;
            dragging = dragged = placePicked = lockPressed = firstPicked = answered = answerLocked = pressed = nextPressed = false;
        }

        // The hand starts on the vector tip and sweeps along a great circle to the target.
        void BeginDrag(string kind, double t)
        {
            LessonTimeline.PauseTask task = pause.Task;
            start = Qubit.FromName(task.startState).Bloch;
            target = ChoosePlacement(kind);
            arc = BlochVector.AngleBetween(start, target);
            BlochVector normal = BlochVector.Cross(start, target);
            axis = normal.Length > 1e-6 ? normal.Normalized() : Math.Abs(start.X) < 0.9 ? BlochVector.UnitX : BlochVector.UnitY;
            dragStart = t;
            dragging = true;
            pause.GrabStarted("sim", () => start.RotatedAbout(axis, arc * Math.Min(1, (pause.SincePause - dragStart) / DragSeconds)));
        }

        BlochVector ChoosePlacement(string kind)
        {
            LessonTimeline.PauseTask task = pause.Task;
            if (kind == "correct") return Gate.FromName(task.gate).Apply(Qubit.FromName(task.startState)).Bloch;
            if (kind == "misconception") return Qubit.FromName(pause.Feedback.Task(task.id).placement.misconceptions[0].answers[0]).Bloch;
            // Below the equator between +x and +y: wrong for every pause and near no named state.
            return Qubit.FromBlochAngles(120 * Math.PI / 180, 45 * Math.PI / 180).Bloch;
        }

        // Cond 2: the list entry of the wanted kind, judged the way EmbodiedPause judges it.
        string ChooseListPlacement(string kind)
        {
            LessonTimeline.PauseTask task = pause.Task;
            PauseFeedback.PlacementText text = pause.Feedback.Task(task.id).placement;
            string fallback = null;
            foreach (string choice in PauseFeedback.PlacementChoices(task))
            {
                BlochVector v = PauseFeedback.PlacementChoiceVector(task, choice);
                bool correct = PauseFeedback.PlacementErrorDegrees(task, v) <= 20;
                string near = PauseFeedback.NearestNamedState(v, 1);
                bool mapped = near != null && Array.Exists(text.misconceptions, m => Array.IndexOf(m.answers, near) >= 0);
                if (kind == "correct" && correct) return choice;
                if (kind == "misconception" && !correct && mapped) return choice;
                if (kind == "generic" && !correct && !mapped) return choice;
                if (!correct) fallback = fallback ?? choice;
            }
            return fallback;
        }

        string ChooseAnswer(string kind)
        {
            LessonTimeline.PauseTask task = pause.Task;
            if (kind == "correct") return task.correct;
            PauseFeedback.TaskText text = pause.Feedback.Task(task.id);
            if (kind == "misconception") return text.misconceptions[0].answers[0];
            foreach (string choice in task.choices)
            {
                if (choice == task.correct) continue;
                bool mapped = false;
                foreach (PauseFeedback.Misconception m in text.misconceptions) mapped |= Array.IndexOf(m.answers, choice) >= 0;
                if (!mapped) return choice;
            }
            return text.misconceptions[text.misconceptions.Length - 1].answers[0]; // every wrong answer is mapped here
        }
    }
}
