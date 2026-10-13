using System;
using System.Collections;
using UnityEngine;

namespace Study.Lesson
{
    // Condition 1 display: 1920 x 1080 borderless fullscreen at 60 Hz with vsync, the
    // cursor hidden for the lesson (the mouse is only for orientation and the end screen,
    // Checkpoint 6). Logs what the monitor actually gave, so a wrong monitor setting
    // shows up in the session log instead of silently changing the framing or pacing.
    // Does nothing in the headset conditions.
    //
    // Dev flags for builds: -dev-windowed (leave the window as launched), -dev-mute.
    public class DesktopDisplay : MonoBehaviour
    {
        [SerializeField] LessonController lesson;
        [SerializeField] int width = 1920;
        [SerializeField] int height = 1080;
        [SerializeField] int refreshHz = 60;

        void Start()
        {
            if (lesson.Config.UsesHeadset) return;
            bool windowed = HasArg("-dev-windowed");
            if (!Application.isEditor && !windowed)
                Screen.SetResolution(width, height, FullScreenMode.FullScreenWindow,
                    new RefreshRate { numerator = (uint)refreshHz, denominator = 1 });
            QualitySettings.vSyncCount = 1; // one frame per monitor refresh
            Application.targetFrameRate = -1;
            if (!Application.isEditor) SetCursorHidden(true);
            if (HasArg("-dev-mute")) AudioListener.volume = 0; // the narration still runs the clock
            lesson.LessonEnded += OnLessonEnded;
            StartCoroutine(ReportDisplay(windowed));
        }

        void OnDestroy()
        {
            if (lesson != null) lesson.LessonEnded -= OnLessonEnded;
        }

        void OnLessonEnded()
        {
            if (!Application.isEditor) SetCursorHidden(false);
        }

        static void SetCursorHidden(bool hidden)
        {
            Cursor.visible = !hidden;
            Cursor.lockState = hidden ? CursorLockMode.Locked : CursorLockMode.None;
        }

        // A resolution change lands a frame or two later.
        IEnumerator ReportDisplay(bool windowed)
        {
            yield return null;
            yield return null;
            double hz = Screen.currentResolution.refreshRateRatio.value;
            string report = $"[Display] {lesson.Config} window {Screen.width}x{Screen.height} {Screen.fullScreenMode}, " +
                            $"monitor {Screen.currentResolution.width}x{Screen.currentResolution.height} @ {hz:F2} Hz, vSync={QualitySettings.vSyncCount}" +
                            (windowed ? " (dev windowed)" : "");
            bool wrong = !Application.isEditor && !windowed &&
                         (Screen.width != width || Screen.height != height || Math.Abs(hz - refreshHz) > 1);
            if (wrong) Debug.LogWarning(report + $" -- expected {width}x{height} @ {refreshHz} Hz: check the monitor settings");
            else Debug.Log(report);
        }

        static bool HasArg(string flag) => Array.IndexOf(Environment.GetCommandLineArgs(), flag) >= 0;
    }
}
