using System;
using System.Collections.Generic;
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
            public string predictTitle;
            public string turnTitle;     // once the prediction is over: gate button, grab
            public string positionHint;
            public string positionHintDesktop; // Cond 1: mouse instead of controller
            public string lockedLine;   // {0} = the learner's answer
            public string gateHint;
            public string timeoutHint;
            public string rotateHint;
            public string watchHint;     // Cond 1/2: while the scripted arc plays
            public string correctTitle;
            public string incorrectTitle;
            public string neutralTitle;
        }

        [Serializable]
        public class TaskText
        {
            public string id;
            public string prompt;
            public string correct;
            public string noPrediction;
            public string genericIncorrect; // wrong answers with no mapped misconception
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

        // Every rule the file must satisfy against the timeline's tasks. Empty list = valid.
        public List<string> Validate(LessonTimeline timeline)
        {
            var errors = new List<string>();
            if (ui == null || tasks == null) { errors.Add("missing ui or tasks"); return errors; }
            foreach (string field in new[] { ui.predictTitle, ui.turnTitle, ui.positionHint, ui.positionHintDesktop, ui.lockedLine, ui.gateHint, ui.timeoutHint,
                         ui.rotateHint, ui.watchHint, ui.correctTitle, ui.incorrectTitle, ui.neutralTitle })
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
            }
            return errors;
        }
    }
}
