using System;
using UnityEngine;

namespace Study.Lesson
{
    // The study's event stream: one call per telemetry event (spec, Telemetry).
    // Each event goes to the log as an "[Event]" line and to Logged, which the
    // session's CSV writer (StudySession) subscribes to. Event-driven only, never per frame.
    public static class StudyEvents
    {
        public static event Action<double, string, string> Logged; // lesson time (NaN outside the lesson), type, fields

        // Lesson time for events logged without one; NaN while no lesson is running. Set by LessonController.
        public static Func<double> LessonTime = () => double.NaN;

        // fields: "key=value key=value", no names or identifying information.
        public static void Log(double lessonTime, string type, string fields = "")
        {
            string t = double.IsNaN(lessonTime) ? "-" : lessonTime.ToString("F3");
            Debug.Log($"[Event] t={t} {type} {fields}");
            Logged?.Invoke(lessonTime, type, fields);
        }

        // Stamped with the current lesson time, if a lesson is running.
        public static void Log(string type, string fields = "") => Log(LessonTime(), type, fields);
    }
}
