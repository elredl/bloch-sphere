using System;
using UnityEngine;

namespace Study.Lesson
{
    // The study's event stream: one call per telemetry event (spec, Telemetry).
    // For now each event goes to the log as an "[Event]" line; Checkpoint 6
    // subscribes a CSV writer to Logged. Event-driven only, never per frame.
    public static class StudyEvents
    {
        public static event Action<double, string, string> Logged; // lesson time, type, fields

        // fields: "key=value key=value", no names or identifying information.
        public static void Log(double lessonTime, string type, string fields = "")
        {
            Debug.Log($"[Event] t={lessonTime:F3} {type} {fields}");
            Logged?.Invoke(lessonTime, type, fields);
        }
    }
}
