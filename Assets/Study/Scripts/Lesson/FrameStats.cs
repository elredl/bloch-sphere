using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace Study.Lesson
{
    // Logs frame timing every few seconds so the frame rate can be read from the log
    // (Quest: `adb logcat -s Unity`; desktop: Player.log) during a full lesson.
    // Logs only; draws nothing.
    public class FrameStats : MonoBehaviour
    {
        [SerializeField] float reportSeconds = 10f;

        float windowStart;
        int frames;
        float worst;
        int slow;
        readonly List<XRDisplaySubsystem> displays = new List<XRDisplaySubsystem>();

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            frames++;
            worst = Mathf.Max(worst, dt);
            float refresh = DisplayRefreshRate();
            if (refresh > 0 && dt > 1.5f / refresh) slow++; // missed at least one display refresh
            float elapsed = Time.unscaledTime - windowStart;
            if (elapsed < reportSeconds) return;
            Debug.Log($"[Frames] t={Time.unscaledTime:F0}s fps={frames / elapsed:F1} worst={worst * 1000:F1}ms " +
                      $"missed={slow}/{frames} display={refresh:F0}Hz");
            windowStart = Time.unscaledTime;
            frames = 0;
            worst = 0;
            slow = 0;
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
