using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace Study.Lesson
{
    // Logs frame timing every few seconds so the frame rate can be read from the log
    // (Quest: `adb logcat -s Unity`; desktop: Player.log) during a full lesson, and a
    // frame_rate summary to the session file every minute (spec, Telemetry: a check that
    // the display rate held, not tracking). Logs only; draws nothing.
    public class FrameStats : MonoBehaviour
    {
        [SerializeField] float reportSeconds = 10f;
        [SerializeField] float summarySeconds = 60f;

        float windowStart;
        int frames;
        float worst;
        int slow;
        readonly List<XRDisplaySubsystem> displays = new List<XRDisplaySubsystem>();
        float summaryStart;
        int summaryFrames, summarySlow;
        float summaryWorst;

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            frames++;
            worst = Mathf.Max(worst, dt);
            float refresh = DisplayRefreshRate();
            bool missed = refresh > 0 && dt > 1.5f / refresh; // missed at least one display refresh
            if (missed) slow++;
            Summarize(dt, missed, refresh);
            float elapsed = Time.unscaledTime - windowStart;
            if (elapsed < reportSeconds) return;
            Debug.Log($"[Frames] t={Time.unscaledTime:F0}s fps={frames / elapsed:F1} worst={worst * 1000:F1}ms " +
                      $"missed={slow}/{frames} display={refresh:F0}Hz");
            windowStart = Time.unscaledTime;
            frames = 0;
            worst = 0;
            slow = 0;
        }

        void Summarize(float dt, bool missed, float refresh)
        {
            summaryFrames++;
            if (missed) summarySlow++;
            summaryWorst = Mathf.Max(summaryWorst, dt);
            float elapsed = Time.unscaledTime - summaryStart;
            if (elapsed < summarySeconds) return;
            StudyEvents.Log("frame_rate", $"fps={summaryFrames / elapsed:F1} worst_ms={summaryWorst * 1000:F1} " +
                                          $"missed={summarySlow} frames={summaryFrames} display_hz={refresh:F0}");
            summaryStart = Time.unscaledTime;
            summaryFrames = 0;
            summarySlow = 0;
            summaryWorst = 0;
        }

        // Headset refresh rate in Hz; with no headset, the monitor's.
        float DisplayRefreshRate()
        {
            SubsystemManager.GetSubsystems(displays);
            foreach (XRDisplaySubsystem display in displays)
                if (display.running && display.TryGetDisplayRefreshRate(out float rate)) return rate;
            return (float)Screen.currentResolution.refreshRateRatio.value;
        }
    }
}
