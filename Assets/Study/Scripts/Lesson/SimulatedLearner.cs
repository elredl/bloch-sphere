using System;
using Study.Quantum;
using Study.Visuals;
using UnityEngine;

namespace Study.Lesson
{
    // Development only: plays a learner through each pause (in Cond 1/2 only the answer
    // applies; there is nothing to press or grab) so the whole
    // flow (prediction, gate press, ratchet grab, timeouts, feedback) can be run in
    // the Editor without a headset. It uses the same entry points as the real
    // controls: the panel and marker pick handlers, the gate button, and a grab
    // point fed to EmbodiedPause exactly as QubitGrabTarget feeds it.
    // Added by LessonDevTools.SimulateLearner; never part of the saved scene.
    public class SimulatedLearner : MonoBehaviour
    {
        // One learner behavior, times in seconds from pause onset (negative = never).
        public struct Profile
        {
            public string Name;
            public string Answer;        // "correct", "misconception", "generic", or null = no answer
            public double AnswerAt, PressAt, GrabAt;
            public double DegreesPerSecond;
            public double PushPastDegrees;    // hand goes this far past the target before letting go
            public double ReleaseShortDegrees; // > 0: first release this far short of the target, regrab 1.5 s later
            public bool WrongWayWobble;        // pulls back 25 deg halfway through
        }

        public static Profile Named(string name)
        {
            switch (name)
            {
                case "fast": return new Profile { Name = name, Answer = "correct", AnswerAt = 5, PressAt = 8, GrabAt = 9, DegreesPerSecond = 45, PushPastDegrees = 15 };
                case "wrong": return new Profile { Name = name, Answer = "misconception", AnswerAt = 7, PressAt = 10, GrabAt = 11, DegreesPerSecond = 25, WrongWayWobble = true };
                case "generic": return new Profile { Name = name, Answer = "generic", AnswerAt = 9, PressAt = 12, GrabAt = 13, DegreesPerSecond = 20, ReleaseShortDegrees = 25 };
                case "slow": return new Profile { Name = name, Answer = "correct", AnswerAt = 22, PressAt = 26, GrabAt = 28, DegreesPerSecond = 6 };
                case "late": return new Profile { Name = name, Answer = null, PressAt = 30, GrabAt = 31, DegreesPerSecond = 40 };
                case "never": return new Profile { Name = name, Answer = null, PressAt = -1, GrabAt = -1 };
                default: throw new ArgumentException("unknown learner profile " + name + " (fast, wrong, generic, slow, late, never)");
            }
        }

        Profile[] profiles;
        EmbodiedPause pause;
        PredictionPanelView panel;
        PositionMarkersView markers;
        int pauseSeen = -1;
        bool answered, pressed, grabbing, released, regrabbed, letGoNext, finished;
        double grabStart, handAngle, baseAngle, releaseShortAt;
        BlochVector start;
        Gate gate;

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

            if (!answered && p.Answer != null && t >= p.AnswerAt && pause.Current == EmbodiedPause.Phase.Predict)
            {
                answered = true;
                Pick(ChooseAnswer(p.Answer));
            }
            if (!pressed && p.PressAt >= 0 && t >= p.PressAt && pause.Current == EmbodiedPause.Phase.Gate)
            {
                pressed = true;
                panel.PressGate();
            }
            if (pause.Current != EmbodiedPause.Phase.Rotate) return;

            if (finished) return;
            if (!grabbing && !released && p.GrabAt >= 0 && t >= p.GrabAt) BeginGrab(t);
            if (released && !regrabbed && t >= releaseShortAt + 1.5)
            {
                regrabbed = true;
                BeginGrab(t);
            }
            if (!grabbing) return;
            if (letGoNext)
            {
                // Let go one frame after the hand got there, so the ratchet has seen the final position.
                letGoNext = grabbing = false;
                finished = !released || regrabbed;
                pause.GrabEnded();
                return;
            }

            double target = gate.Angle * 180 / Math.PI;
            double moved = (t - grabStart) * p.DegreesPerSecond;
            double wobble = p.WrongWayWobble ? Wobble(moved, target) : 0;
            handAngle = Math.Min(baseAngle + moved - wobble, target + p.PushPastDegrees);

            bool shortRelease = p.ReleaseShortDegrees > 0 && !released && handAngle >= target - p.ReleaseShortDegrees;
            if (shortRelease || handAngle >= target + p.PushPastDegrees - 1e-6)
            {
                letGoNext = true;
                if (shortRelease)
                {
                    released = true;
                    releaseShortAt = t;
                }
            }
        }

        void StartPause()
        {
            pauseSeen = pause.PauseNumber;
            answered = pressed = grabbing = released = regrabbed = letGoNext = finished = false;
            gate = Gate.FromName(pause.Task.gate);
            start = Qubit.FromName(pause.Task.startState).Bloch;
            handAngle = 0;
        }

        // A regrab picks up where the hand let go; the ratchet measures hand motion from there.
        void BeginGrab(double t)
        {
            grabbing = true;
            grabStart = t;
            baseAngle = handAngle;
            // The hand sits on the vector tip: the grab point is the start state turned by the hand angle.
            pause.GrabStarted("sim", () => start.RotatedAbout(gate.Axis, handAngle * Math.PI / 180));
        }

        // Halfway through, the hand goes 25 deg back and then forward again.
        static double Wobble(double moved, double target)
        {
            double half = target / 2;
            if (moved < half) return 0;
            double into = moved - half;
            return into < 25 ? into : into < 50 ? 50 - into : 0;
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

        void Pick(string answer)
        {
            LessonTimeline.PauseTask task = pause.Task;
            if (task.prediction == "position") markers.Pick(answer);
            else panel.PickChoice(Array.IndexOf(task.choices, answer));
        }
    }
}
