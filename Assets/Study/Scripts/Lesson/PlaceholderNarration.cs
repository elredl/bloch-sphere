using UnityEngine;

namespace Study.Lesson
{
    // Stand-in for the narration until the real recording exists. Generated
    // from the timeline so its length always matches: a soft beep every 5 s
    // while "narration" runs, a double beep at each segment start, and true
    // silence during the pauses (like the real file's built-in silences).
    // Generated at runtime to keep a 15-minute WAV out of the repository.
    public static class PlaceholderNarration
    {
        const int SampleRate = 8000;
        const float BeepSeconds = 0.12f;
        const float Volume = 0.15f;

        public static AudioClip Create(LessonTimeline timeline)
        {
            int length = (int)System.Math.Round(timeline.lessonDuration * SampleRate);
            var samples = new float[length];
            foreach (LessonTimeline.Segment s in timeline.segments)
            {
                Beep(samples, s.start, 660f);
                Beep(samples, s.start + 0.2, 660f);
                for (double t = s.start + 5; t < s.pauseStart - BeepSeconds; t += 5) Beep(samples, t, 440f);
            }
            var clip = AudioClip.Create("Placeholder Narration", length, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        static void Beep(float[] samples, double start, float frequency)
        {
            int first = (int)(start * SampleRate);
            int count = (int)(BeepSeconds * SampleRate);
            for (int i = 0; i < count && first + i < samples.Length; i++)
            {
                float envelope = Mathf.Sin(Mathf.PI * i / count); // no clicks
                samples[first + i] = Volume * envelope * Mathf.Sin(2 * Mathf.PI * frequency * i / SampleRate);
            }
        }
    }
}
