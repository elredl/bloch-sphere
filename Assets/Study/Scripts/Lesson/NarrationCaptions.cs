using System;
using Study.Visuals;
using UnityEngine;

namespace Study.Lesson
{
    // Until the narration is recorded, the lesson is shown as text: the line the narration
    // would be speaking, by lesson time, from Assets/Study/Data/narration_captions.json
    // (written by tools/narration_script.py). Hidden in pauses and breaks, and never shown
    // once a recorded clip is assigned. Same in every condition.
    public class NarrationCaptions : MonoBehaviour
    {
        [SerializeField] LessonController lesson;
        [SerializeField] CaptionPanelView panel;
        [SerializeField] TextAsset captionsJson;

        [Serializable]
        class CaptionFile
        {
            public Line[] lines;
        }

        [Serializable]
        class Line
        {
            public double start;
            public double end;
            public string text;
        }

        Line[] lines;
        int current = -1;

        void Start()
        {
            lines = JsonUtility.FromJson<CaptionFile>(captionsJson.text).lines;
            Array.Sort(lines, (a, b) => a.start.CompareTo(b.start));
        }

        void Update()
        {
            if (!lesson.IsRunning || lesson.OnBreak || lesson.InPause || !lesson.UsesPlaceholderNarration)
            {
                panel.Hide();
                return;
            }
            // The latest line that has started stays up through the short gaps between lines.
            // Lesson time only moves forward, except when a lesson starts over after an abort.
            double t = lesson.Time;
            if (current >= 0 && lines[current].start > t) current = -1;
            int line = current;
            while (line + 1 < lines.Length && lines[line + 1].start <= t) line++;
            if (line < 0)
            {
                panel.Hide();
                return;
            }
            if (line != current || !panel.IsVisible) panel.Show(lines[line].text);
            current = line;
        }
    }
}
