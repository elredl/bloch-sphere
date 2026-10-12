using System.Globalization;
using Study.Lesson;
using UnityEditor;
using UnityEngine;

namespace Study.EditorTools
{
    // Editor helpers for lesson dry runs, called through the live command runner.
    public static class LessonDevTools
    {
        // Edit Mode, before entering Play Mode. arg = "condition,speed", e.g. "2,3".
        public static string SetDevRun(string arg)
        {
            string[] parts = arg.Split(',');
            var lesson = Object.FindAnyObjectByType<LessonController>() ?? throw new System.Exception("no LessonController in scene");
            var so = new SerializedObject(lesson);
            so.FindProperty("devCondition").enumValueIndex = int.Parse(parts[0]) - 1;
            so.FindProperty("devPlaybackSpeed").floatValue = float.Parse(parts[1], CultureInfo.InvariantCulture);
            so.ApplyModifiedProperties();
            return $"dev run: condition {parts[0]}, speed {parts[1]}x";
        }

        // Play Mode. Reports lesson time, audio position and running state.
        public static string Status()
        {
            var lesson = Object.FindAnyObjectByType<LessonController>();
            if (lesson == null || lesson.Config == null) return "no lesson running";
            return $"{lesson.Config} running={lesson.IsRunning} lessonTime={lesson.Time:F2}";
        }
    }
}
