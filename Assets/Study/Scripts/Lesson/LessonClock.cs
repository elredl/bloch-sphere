using UnityEngine;

namespace Study.Lesson
{
    // Lesson time in seconds, locked to the narration audio's playback position.
    //
    // The audio position (timeSamples) only advances in chunks of one audio
    // buffer (~20-40 ms), which would make arcs judder at 90 Hz. So each frame
    // the clock predicts time from the frame delta and then pulls toward the
    // measured audio position. It can never drift away from the audio: if the
    // two disagree by more than SnapThreshold it jumps to the audio position.
    // It never runs backwards.
    public class LessonClock
    {
        const double SnapThreshold = 0.05;  // seconds
        const double CorrectionRate = 0.1;  // fraction of the error removed per frame

        readonly AudioSource source;
        double time;
        bool finished;

        public LessonClock(AudioSource source)
        {
            this.source = source;
        }

        public double Time => time;

        // Raw audio position. Used for the pacing report.
        public double AudioTime => source.clip == null ? 0 : source.timeSamples / (double)source.clip.frequency;

        public bool Finished => finished;

        // Call once per frame. paused = the lesson is on a break (audio paused by us).
        public void Tick(float unscaledDeltaTime, bool paused)
        {
            if (finished || paused) return;

            // When a clip plays to its end Unity stops it and resets timeSamples to 0.
            if (!source.isPlaying)
            {
                if (time > 0)
                {
                    time = source.clip.length;
                    finished = true;
                }
                return;
            }

            double measured = AudioTime;
            double predicted = time + unscaledDeltaTime * source.pitch;
            double error = measured - predicted;
            double next = System.Math.Abs(error) > SnapThreshold ? measured : predicted + error * CorrectionRate;
            if (next > time) time = next;
        }
    }
}
