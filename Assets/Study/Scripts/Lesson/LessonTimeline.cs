using System;
using System.Collections.Generic;
using System.Globalization;
using Study.Quantum;
using UnityEngine;

namespace Study.Lesson
{
    // The lesson as data: segment and pause times, scripted events, and the
    // pause tasks, all in seconds on the narration clip. Loaded from
    // Assets/Study/Data/lesson_timeline.json. Swapping in the real narration
    // means replacing the clip and editing the times in that file.
    //
    // Shared by all three conditions. Nothing here may depend on the condition:
    // only the pause behavior (PauseDirector) differs between conditions.
    [Serializable]
    public class LessonTimeline
    {
        public double lessonDuration;
        // A set_state is shown as a re-preparation: the vector fades out and back in at
        // the new state (never a turn, so it can't be mistaken for a rotation).
        public double reprepareDuration;
        public PauseSchedule pauseSchedule;
        public Segment[] segments;
        public LessonEvent[] events;
        public PauseTask[] pauseTasks;

        [Serializable]
        public class Segment
        {
            public string id;
            public double start;      // narration for this segment begins
            public double pauseStart; // narration ends, silent pause begins
            public double pauseEnd;   // pause ends, next segment's narration begins
            public string task;       // id of the PauseTask run in this pause
        }

        // Settings shared by every pause.
        [Serializable]
        public class PauseSchedule
        {
            public double pauseLength;            // silent gap in the narration; Next jumps over it (pauses are untimed)
            public double arcDuration;            // the turn after the prediction (Cond 3/1: gate press; Cond 2: scripted)
        }

        // One scripted moment during narration. Fields used depend on type:
        //   set_state   state                (re-prepare the qubit: fade out, fade in)
        //   apply_gate  gate, duration       (arc animation)
        //   show / hide target               ("angle_arcs", "measurement_panel")
        //   measure_demo state, shots, seed  (repeated-measurement panel)
        [Serializable]
        public class LessonEvent
        {
            public double t;
            public string type;
            public string state;
            public string gate;
            public double duration;
            public string target;
            public int shots;
            public ulong seed;

            public override string ToString() => $"{t:F2}s {type} {state}{gate}{target}";
        }

        [Serializable]
        public class PauseTask
        {
            public string id;
            public string startState;      // checked against the live state at pause onset
            public string gate;
            public string operationLabel;  // Cond 3 gate-press button text
            // Cond 3 question 1, how a placement is judged: "point" (the exact end state) or
            // "height" (only the tilt from |0>, for operations whose side the lesson leaves open).
            public string placementMatch;
            // Cond 3 question 2 (question 1, placing the end state, needs no data: it is
            // the gate applied to startState). "p0" (five buttons) or "position" (six markers).
            public string prediction;
            public string[] choices;
            public string correct;
            public bool measurementDemo;   // pause 4: demo on the post-rotation state
            public int demoShots;
            public ulong demoSeed;
        }

        public static readonly string[] KnownEventTypes = { "set_state", "apply_gate", "show", "hide", "measure_demo" };
        public static readonly string[] KnownTargets = { "angle_arcs", "measurement_panel" };

        public static LessonTimeline FromJson(string json) => JsonUtility.FromJson<LessonTimeline>(json);

        public PauseTask Task(string id) => Array.Find(pauseTasks, t => t.id == id);

        // Every rule the timeline must satisfy. Empty list = valid.
        // The equal-pause and no-events-in-pauses rules protect the experimental invariant.
        public List<string> Validate()
        {
            var errors = new List<string>();
            if (segments == null || segments.Length != 4) errors.Add("expected exactly 4 segments");
            if (pauseSchedule == null) { errors.Add("missing pauseSchedule"); return errors; }
            if (segments == null || events == null || pauseTasks == null) { errors.Add("missing segments, events or pauseTasks"); return errors; }

            PauseSchedule p = pauseSchedule;
            if (p.arcDuration <= 0) errors.Add("arcDuration must be positive");
            if (reprepareDuration <= 0) errors.Add("reprepareDuration must be positive");

            double previousEnd = 0;
            for (int i = 0; i < segments.Length; i++)
            {
                Segment s = segments[i];
                if (Math.Abs(s.start - previousEnd) > 1e-6) errors.Add($"segment {s.id} starts at {s.start}, expected {previousEnd}");
                if (!(s.start < s.pauseStart && s.pauseStart < s.pauseEnd)) errors.Add($"segment {s.id} times out of order");
                if (Math.Abs(s.pauseEnd - s.pauseStart - p.pauseLength) > 1e-6)
                    errors.Add($"pause after {s.id} is {s.pauseEnd - s.pauseStart}s, all pauses must be {p.pauseLength}s");
                if (Task(s.task) == null) errors.Add($"segment {s.id} names unknown task '{s.task}'");
                previousEnd = s.pauseEnd;
            }
            if (Math.Abs(previousEnd - lessonDuration) > 1e-6) errors.Add($"last pause ends at {previousEnd}, lessonDuration is {lessonDuration}");

            double previousTime = -1;
            double busyUntil = 0; // end of the last arc or demo; scripted events must not overlap
            foreach (LessonEvent e in events)
            {
                if (e.t < previousTime) errors.Add($"events out of order at {e}");
                previousTime = e.t;
                if (Array.IndexOf(KnownEventTypes, e.type) < 0) { errors.Add($"unknown event type at {e}"); continue; }
                if (e.t < 0 || e.t > lessonDuration) errors.Add($"event outside the lesson: {e}");
                if (e.t < busyUntil - 1e-6 && e.type != "show" && e.type != "hide") errors.Add($"event starts before the previous animation ends: {e}");

                double end = e.t;
                if (e.type == "set_state" || e.type == "measure_demo") CheckState(e.state, e.ToString(), errors);
                if (e.type == "set_state") end = e.t + reprepareDuration;
                if (e.type == "apply_gate")
                {
                    CheckGate(e.gate, e.ToString(), errors);
                    if (e.duration <= 0) errors.Add($"apply_gate needs a positive duration: {e}");
                    end = e.t + e.duration;
                }
                if (e.type == "measure_demo")
                {
                    if (e.shots <= 0) errors.Add($"measure_demo needs shots: {e}");
                    end = e.t + MeasurementDemo.Duration;
                }
                if ((e.type == "show" || e.type == "hide") && Array.IndexOf(KnownTargets, e.target) < 0)
                    errors.Add($"unknown target at {e}");
                if (e.type == "show" && e.target == "measurement_panel")
                    errors.Add($"the measurement panel is shown by measure_demo, not by show: {e}");
                busyUntil = Math.Max(busyUntil, end);

                // Pauses belong to the pause behavior, not to scripted narration events.
                foreach (Segment s in segments)
                    if (end > s.pauseStart + 1e-6 && e.t < s.pauseEnd - 1e-6)
                        errors.Add($"event overlaps the pause after {s.id}: {e}");
            }

            foreach (PauseTask task in pauseTasks)
            {
                CheckState(task.startState, task.id, errors);
                CheckGate(task.gate, task.id, errors);
                if (task.prediction != "p0" && task.prediction != "position") errors.Add($"{task.id}: prediction must be p0 or position");
                if (task.placementMatch != "point" && task.placementMatch != "height") errors.Add($"{task.id}: placementMatch must be point or height");
                if (task.choices == null || Array.IndexOf(task.choices, task.correct) < 0) errors.Add($"{task.id}: correct answer not among choices");
                if (task.measurementDemo && task.demoShots <= 0) errors.Add($"{task.id}: measurement demo needs shots");
            }
            return errors;
        }

        // Plays the scripted state changes and pause operations forward without
        // animation and returns the state at each pause onset. Lets tests (and the
        // app at load time) catch a timeline whose events don't lead to the
        // start state each pause task expects.
        public List<Qubit> StatesAtPauseOnsets()
        {
            var states = new List<Qubit>();
            Qubit q = Qubit.Zero;
            int nextEvent = 0;
            foreach (Segment s in segments)
            {
                while (nextEvent < events.Length && events[nextEvent].t < s.pauseStart)
                {
                    LessonEvent e = events[nextEvent++];
                    if (e.type == "set_state") q = Qubit.FromName(e.state);
                    if (e.type == "apply_gate") q = Gate.FromName(e.gate).Apply(q);
                }
                states.Add(q);
                q = Gate.FromName(Task(s.task).gate).Apply(q);
            }
            return states;
        }

        static void CheckState(string name, string where, List<string> errors)
        {
            try { Qubit.FromName(name); }
            catch (ArgumentException) { errors.Add($"unknown state '{name}' at {where}"); }
        }

        static void CheckGate(string name, string where, List<string> errors)
        {
            try { Gate.FromName(name); }
            catch (Exception) { errors.Add($"unknown gate '{name}' at {where}"); }
        }

        public static string Seconds(double t) => t.ToString("F3", CultureInfo.InvariantCulture);
    }
}
