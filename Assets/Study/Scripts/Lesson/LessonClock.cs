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
    //
    // Editor dry runs above 3x (the most audio pitch allows) use a frame-time
    // clock instead: no audio, time = frame time x speed, with each frame's step
    // capped so an Editor hitch (a screenshot) slows the run rather than skipping
    // lesson time. Builds never do this.
    public class LessonClock
    {
        const double SnapThreshold = 0.05;  // seconds
        const double CorrectionRate = 0.1;  // fraction of the error removed per frame
        const double FrameClockMaxStep = 1.0 / 30; // real seconds per frame, frame-time clock only
        const double EndWindow = 1.0;       // audio that stops this close to the clip's end has finished

        readonly AudioSource source;
        readonly double frameClockSpeed; // 0 = locked to audio
        double time;
        bool finished;

        public LessonClock(AudioSource source, double frameClockSpeed = 0)
        {
            this.source = source;
            this.frameClockSpeed = frameClockSpeed;
        }

        public bool IsFrameClock => frameClockSpeed > 0;

        public double Time => time;

        // Raw audio position. Used for the pacing report.
        public double AudioTime => IsFrameClock ? time : source.clip == null ? 0 : source.timeSamples / (double)source.clip.frequency;

        // Jump forward to a time the audio was just moved to (a skipped pause, an Editor seek).
        // Never backwards. The audio clock would snap there by itself on the next Tick; setting
        // it here means items due at that time fire on the very next frame.
        public void Jump(double seconds)
        {
            if (seconds > time) time = seconds;
        }

        public bool Finished => finished;

        // Call once per frame. paused = the lesson is on a break (audio paused by us).
        public void Tick(float unscaledDeltaTime, bool paused)
        {
            if (finished || paused) return;

            if (IsFrameClock)
            {
                time += System.Math.Min(unscaledDeltaTime, FrameClockMaxStep) * frameClockSpeed;
                if (time >= source.clip.length)
                {
                    time = source.clip.length;
                    finished = true;
                }
                return;
            }

            // When a clip plays to its end Unity stops it and resets timeSamples to 0. Audio that
            // stops anywhere else (the system paused it, say, before a headset-off break began)
            // is a stall: lesson time waits for it instead of jumping to the end.
            if (!source.isPlaying)
            {
                if (time > 0 && time >= source.clip.length - EndWindow)
                {
                    time = System.Math.Max(time, source.clip.length); // a skip may already be past the clip's end
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
