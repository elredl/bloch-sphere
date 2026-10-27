using System;
using Study.Quantum;
using Study.Visuals;
using UnityEngine;

namespace Study.Lesson
{
    // Development only: plays a Condition 3 learner through each pause so the whole
    // flow (placement, lock in, prediction, gate press, timeouts, feedback) can be run
    // in the Editor without a headset. It uses the same entry points as the real
    // controls: a grab point fed to EmbodiedPause exactly as QubitGrabTarget feeds it,
    // the panel's buttons and the answer pick handler.
    // Added by LessonDevTools.SimulateLearner; never part of the saved scene.
    public class SimulatedLearner : MonoBehaviour
    {
        const double DragSeconds = 1.5;

        // One learner behavior, times in seconds from pause onset (negative = never).
        public struct Profile
        {
            public string Name;
            public string Place;         // "correct", "misconception", "generic", or null = never touches the vector
            public string Answer;        // same kinds, for the P(0) question; null = no answer
            public double PlaceAt, LockAt, AnswerAt, PressAt;
        }

        public static Profile Named(string name)
        {
            switch (name)
            {
                case "fast": return new Profile { Name = name, Place = "correct", PlaceAt = 3, LockAt = 7, Answer = "correct", AnswerAt = 10, PressAt = 13 };
                case "wrong": return new Profile { Name = name, Place = "misconception", PlaceAt = 4, LockAt = 9, Answer = "misconception", AnswerAt = 14, PressAt = 18 };
                case "generic": return new Profile { Name = name, Place = "generic", PlaceAt = 5, LockAt = 11, Answer = "generic", AnswerAt = 16, PressAt = 20 };
                case "slow": return new Profile { Name = name, Place = "correct", PlaceAt = 12, LockAt = -1, Answer = "correct", AnswerAt = 28, PressAt = 31 };
                case "late": return new Profile { Name = name, Place = null, PlaceAt = -1, LockAt = -1, Answer = null, PressAt = 34 };
                case "never": return new Profile { Name = name, Place = null, PlaceAt = -1, LockAt = -1, Answer = null, PressAt = -1 };
                default: throw new ArgumentException("unknown learner profile " + name + " (fast, wrong, generic, slow, late, never)");
            }
        }

        Profile[] profiles;
        EmbodiedPause pause;
        PredictionPanelView panel;
        PositionMarkersView markers;
        int pauseSeen = -1;
        bool dragging, dragged, lockPressed, answered, pressed;
        double dragStart;
        BlochVector start, target, axis;
        double arc;

        public void Configure(Profile[] perPause, EmbodiedPause embodied, PredictionPanelView predictionPanel, PositionMarkersView positionMarkers)
        {
            profiles = perPause;
            pause = embodied;
            panel = predictionPanel;
            markers = positionMarkers;
        }

        void Update()
        {
            if (pause == null || pause.Current == EmbodiedPause.Phase.Idle) return;
            if (pause.PauseNumber != pauseSeen) StartPause();
            Profile p = profiles[Math.Min(pause.PauseNumber - 1, profiles.Length - 1)];
            double t = pause.SincePause;

            if (!dragged && !dragging && p.Place != null && p.PlaceAt >= 0 && t >= p.PlaceAt && pause.Current == EmbodiedPause.Phase.Place)
                BeginDrag(p.Place, t);
            if (dragging && t - dragStart >= DragSeconds)
            {
                dragging = false;
                dragged = true;
                pause.GrabEnded();
            }
            if (!lockPressed && dragged && p.LockAt >= 0 && t >= p.LockAt && pause.Current == EmbodiedPause.Phase.Place)
            {
                lockPressed = true;
                panel.PressGate();
            }
            if (!answered && p.Answer != null && t >= p.AnswerAt && pause.Current == EmbodiedPause.Phase.Predict)
            {
                answered = true;
                panel.PickChoice(Array.IndexOf(pause.Task.choices, ChooseAnswer(p.Answer)));
            }
            if (!pressed && p.PressAt >= 0 && t >= p.PressAt && pause.Current == EmbodiedPause.Phase.Gate)
            {
                pressed = true;
                panel.PressGate();
            }
        }

        void StartPause()
        {
            pauseSeen = pause.PauseNumber;
            dragging = dragged = lockPressed = answered = pressed = false;
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
