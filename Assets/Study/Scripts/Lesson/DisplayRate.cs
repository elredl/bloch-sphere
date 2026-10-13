using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
#if UNITY_ANDROID
using Unity.Collections;
using UnityEngine.XR.OpenXR.Features.Meta;
#endif

namespace Study.Lesson
{
    // Quest 3 at 90 Hz from the first frame (spec, Glow, performance, comfort). The headset
    // starts at whatever the system setting says (often 72 Hz), so the app asks for 90 Hz
    // through Meta's display utilities and then reads back what the display actually runs
    // at. The result is shown on the launch screen and logged in the session file
    // (display_rate), so a session that ran at the wrong rate is visible in the data.
    // Also keeps the desktop screen from sleeping. On the desktop the rate is the
    // monitor's (DesktopDisplay).
    public class DisplayRate : MonoBehaviour
    {
        public const float TargetHz = 90f;

        // The headset rate after the request, 0 until known (or with no headset).
        public static float CurrentHz { get; private set; }
        public static string Status { get; private set; } = "display: no headset";
        public static bool Known => CurrentHz > 0;
        public static bool Ok => Mathf.Abs(CurrentHz - TargetHz) < 1f;

        static bool logged;
        readonly List<XRDisplaySubsystem> displays = new List<XRDisplaySubsystem>();

        void Start()
        {
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            if (XRSettings.isDeviceActive) StartCoroutine(Request());
        }

        IEnumerator Request()
        {
            XRDisplaySubsystem display = null;
            for (int i = 0; i < 120 && display == null; i++)
            {
                SubsystemManager.GetSubsystems(displays);
                foreach (XRDisplaySubsystem d in displays)
                    if (d.running) display = d;
                if (display == null) yield return null;
            }
            if (display == null)
            {
                Status = "display: no running XR display";
                Debug.LogError("[Display] " + Status);
                yield break;
            }

            string supported = "?";
            bool requested = false;
#if UNITY_ANDROID
            if (display.TryGetSupportedDisplayRefreshRates(Allocator.Temp, out NativeArray<float> rates))
            {
                supported = string.Join("/", rates.ToArray());
                bool has90 = false;
                foreach (float r in rates) has90 |= Mathf.Abs(r - TargetHz) < 0.5f;
                if (has90) requested = display.TryRequestDisplayRefreshRate(TargetHz);
                rates.Dispose();
            }
#endif
            // The runtime switches on a later frame.
            for (int i = 0; i < 90; i++)
            {
                yield return null;
                if (display.TryGetDisplayRefreshRate(out float hz) && Mathf.Abs(hz - TargetHz) < 1f) break;
            }
            display.TryGetDisplayRefreshRate(out float now);
            CurrentHz = now;
            Status = $"display {now:F0} Hz" + (Ok ? "" : $" (wanted {TargetHz:F0}; supported {supported})");
            string line = $"[Display] headset refresh {now:F1} Hz, requested {TargetHz:F0} ok={requested}, supported {supported}";
            if (Ok) Debug.Log(line);
            else Debug.LogError(line + " -- NOT 90 Hz");
        }

        // Called once a session exists, so the rate lands in the session file.
        public static void LogToSession()
        {
            if (logged || !XRSettings.isDeviceActive) return;
            logged = true;
            StudyEvents.Log("display_rate", $"hz={CurrentHz:F1} target={TargetHz:F0} ok={(Ok ? 1 : 0)}");
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            CurrentHz = 0;
            logged = false;
            Status = "display: no headset";
        }
    }
}
