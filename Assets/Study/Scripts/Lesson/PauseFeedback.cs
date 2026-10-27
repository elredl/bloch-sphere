using System;
using System.Collections.Generic;
using Study.Quantum;
using UnityEngine;

namespace Study.Lesson
{
    // Condition 3 prompt and feedback text, and the mapping from wrong answers to
    // misconceptions. Loaded from Assets/Study/Data/pause_feedback.json so the
    // wording can be edited without touching code. Answers use the same strings
    // as the timeline's pause task choices ("50", "+i", ...).
    [Serializable]
    public class PauseFeedback
    {
        public UiText ui;
        public TaskText[] tasks;

        [Serializable]
        public class UiText
        {
            public string placeTitle;    // question 1: place the predicted end state
            public string placeHint;
            public string lockLabel;     // the button that locks the placement in
            public string predictTitle;
            public string turnTitle;     // once the prediction is over: gate button, grab
            public string positionHint;
            public string lockedLine;   // {0} = the learner's answer
            public string gateHint;
            public string timeoutHint;
            public string rotateHint;
            public string correctTitle;
            public string incorrectTitle;
            public string neutralTitle;
        }

        [Serializable]
        public class TaskText
        {
            public string id;
            public PlacementText placement; // question 1
            public string prompt;           // question 2 from here down
            public string correct;
            public string noPrediction;
            public string genericIncorrect; // wrong answers with no mapped misconception
            public Misconception[] misconceptions;
        }

        // Question 1. A placement is correct within the pause's tolerance of the true end
        // state; a wrong one is matched to the nearest named state ("0", "+i", ...) if it
        // is close to one, and that name picks the misconception.
        [Serializable]
        public class PlacementText
        {
            public string prompt;
            public string correct;
            public string noPlacement;
            public string genericIncorrect;
            public Misconception[] misconceptions;
        }

        [Serializable]
        public class Misconception
        {
            public string id;
            public string[] answers;
            public string text;
        }

        public enum Tone { Correct, Incorrect, Neutral }

        // What to show after the rotation, and how to log it.
        public readonly struct Choice
        {
            public readonly Tone Tone;
            public readonly string Type;  // telemetry feedback type: correct, misconception, generic_incorrect, no_prediction
            public readonly string Id;    // misconception id, or the type again
            public readonly string Title;
            public readonly string Text;

            public Choice(Tone tone, string type, string id, string title, string text)
            {
                Tone = tone; Type = type; Id = id; Title = title; Text = text;
            }
        }

        public static PauseFeedback FromJson(string json) => JsonUtility.FromJson<PauseFeedback>(json);

        public TaskText Task(string id) => Array.Find(tasks, t => t.id == id);

        // answer = null when the prediction timed out.
        public Choice For(LessonTimeline.PauseTask task, string answer)
        {
            TaskText text = Task(task.id);
            if (answer == null) return new Choice(Tone.Neutral, "no_prediction", "no_prediction", ui.neutralTitle, text.noPrediction);
            if (answer == task.correct) return new Choice(Tone.Correct, "correct", "correct", ui.correctTitle, text.correct);
            foreach (Misconception m in text.misconceptions)
                if (Array.IndexOf(m.answers, answer) >= 0)
                    return new Choice(Tone.Incorrect, "misconception", m.id, ui.incorrectTitle, m.text);
            return new Choice(Tone.Incorrect, "generic_incorrect", "generic_incorrect", ui.incorrectTitle, text.genericIncorrect);
        }

        // placed = false when the placement timed out untouched; nearest = the named state
        // the placement is close to, or null.
        public Choice ForPlacement(LessonTimeline.PauseTask task, bool placed, bool correct, string nearest)
        {
            PlacementText text = Task(task.id).placement;
            if (!placed) return new Choice(Tone.Neutral, "no_prediction", "no_prediction", ui.neutralTitle, text.noPlacement);
            if (correct) return new Choice(Tone.Correct, "correct", "correct", ui.correctTitle, text.correct);
            if (nearest != null)
                foreach (Misconception m in text.misconceptions)
                    if (Array.IndexOf(m.answers, nearest) >= 0)
                        return new Choice(Tone.Incorrect, "misconception", m.id, ui.incorrectTitle, m.text);
            return new Choice(Tone.Incorrect, "generic_incorrect", "generic_incorrect", ui.incorrectTitle, text.genericIncorrect);
        }

        // Every rule the file must satisfy against the timeline's tasks. Empty list = valid.
        public List<string> Validate(LessonTimeline timeline)
        {
            var errors = new List<string>();
            if (ui == null || tasks == null) { errors.Add("missing ui or tasks"); return errors; }
            foreach (string field in new[] { ui.placeTitle, ui.placeHint, ui.lockLabel, ui.predictTitle, ui.turnTitle, ui.positionHint, ui.lockedLine, ui.gateHint, ui.timeoutHint,
                         ui.rotateHint, ui.correctTitle, ui.incorrectTitle, ui.neutralTitle })
                if (string.IsNullOrWhiteSpace(field)) errors.Add("a ui text is empty");
            foreach (LessonTimeline.PauseTask task in timeline.pauseTasks)
            {
                TaskText text = Task(task.id);
                if (text == null) { errors.Add($"no feedback for {task.id}"); continue; }
                if (string.IsNullOrWhiteSpace(text.prompt) || string.IsNullOrWhiteSpace(text.correct) ||
                    string.IsNullOrWhiteSpace(text.noPrediction) || string.IsNullOrWhiteSpace(text.genericIncorrect))
                    errors.Add($"{task.id}: prompt, correct, noPrediction and genericIncorrect are all required");
                var seen = new HashSet<string>();
                foreach (Misconception m in text.misconceptions ?? new Misconception[0])
                    foreach (string answer in m.answers)
                    {
                        if (Array.IndexOf(task.choices, answer) < 0) errors.Add($"{task.id}: misconception {m.id} names '{answer}', not a choice");
                        if (answer == task.correct) errors.Add($"{task.id}: misconception {m.id} includes the correct answer");
                        if (!seen.Add(answer)) errors.Add($"{task.id}: answer '{answer}' is mapped twice");
                    }
                ValidatePlacement(task, text.placement, errors);
            }
            return errors;
        }

        public static readonly string[] NamedStates = { "0", "1", "+", "-", "+i", "-i" };

        static void ValidatePlacement(LessonTimeline.PauseTask task, PlacementText text, List<string> errors)
        {
            if (text == null || string.IsNullOrWhiteSpace(text.prompt) || string.IsNullOrWhiteSpace(text.correct) ||
                string.IsNullOrWhiteSpace(text.noPlacement) || string.IsNullOrWhiteSpace(text.genericIncorrect))
            {
                errors.Add($"{task.id}: placement prompt, correct, noPlacement and genericIncorrect are all required");
                return;
            }
            // A named state that matches the true end state is a correct placement, never a misconception.
            var seen = new HashSet<string>();
            foreach (Misconception m in text.misconceptions ?? new Misconception[0])
                foreach (string state in m.answers)
                {
                    if (Array.IndexOf(NamedStates, state) < 0) { errors.Add($"{task.id}: placement misconception {m.id} names '{state}', not a named state"); continue; }
                    if (PlacementErrorDegrees(task, Qubit.FromName(state).Bloch) < 1e-6)
                        errors.Add($"{task.id}: placement misconception {m.id} includes a correct end state");
                    if (!seen.Add(state)) errors.Add($"{task.id}: placement state '{state}' is mapped twice");
                }
        }

        // How far a placement is from the true end state, in degrees: the angle between
        // the two for "point", the difference in tilt from |0> for "height".
        public static double PlacementErrorDegrees(LessonTimeline.PauseTask task, BlochVector placed)
        {
            BlochVector end = Gate.FromName(task.gate).Apply(Qubit.FromName(task.startState)).Bloch;
            double radians = task.placementMatch == "height"
                ? Math.Abs(Tilt(placed) - Tilt(end))
                : BlochVector.AngleBetween(placed, end);
            return radians * 180 / Math.PI;
        }

        static double Tilt(BlochVector v) => Math.Acos(Math.Max(-1, Math.Min(1, v.Normalized().Z)));

        // The named state within maxDegrees of the placement (always the closest one), or null.
        public static string NearestNamedState(BlochVector placed, double maxDegrees)
        {
            string best = null;
            double bestDegrees = maxDegrees;
            foreach (string state in NamedStates)
            {
                double degrees = BlochVector.AngleBetween(placed, Qubit.FromName(state).Bloch) * 180 / Math.PI;
                if (degrees <= bestDegrees) { best = state; bestDegrees = degrees; }
            }
            return best;
        }
    }
}
