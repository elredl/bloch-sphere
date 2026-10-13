using System;
using Study.Lesson;
using Study.Visuals;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR;

namespace Study.Session
{
    // The participant's session in the Lesson scene (spec, Session flow):
    //
    //   calibrate    headset: participant sits upright facing forward, the experimenter
    //                holds A; eye height is measured and the sphere placed 0.25 m below it,
    //                0.8 m ahead. Desktop: nothing to measure, the camera is fixed.
    //   ready        "pull the trigger / click when ready" (the controllers are handed over)
    //   orientation  fixed 2:00 practice
    //   lesson       the 15-minute lesson
    //   end          end screen; the experimenter's part records a withdrawal reason
    //
    // Breaks: B on the desktop, or the headset coming off, freezes orientation or the lesson
    // where it is (narration, lesson time and effects); the headset break ends with a trigger
    // pull once it is back on. Abort (hold both grips and both thumbsticks 3 s, or Esc 2 s)
    // ends the session from any step.
    [DefaultExecutionOrder(-50)]
    public class SessionFlow : MonoBehaviour
    {
        public enum Step { Calibrate, Ready, Orientation, Lesson, End }
        enum BreakKind { None, Key, Headset }

        // Stage height that puts the sphere 0.25 m below a 1.2 m eye (the scene's default).
        public const float DefaultEyeHeight = 1.2f;

        [SerializeField] LessonController lesson;
        [SerializeField] PauseDirector pauses;
        [SerializeField] EmbodiedPause embodied;
        [SerializeField] Transform stage;
        [SerializeField] GameObject sphere;
        [SerializeField] XROrigin xrOrigin;
        [SerializeField] GameObject[] interactors;
        [SerializeField] Orientation orientation;
        [SerializeField] SessionInput input;
        [SerializeField] StudyPanelView overlay;
        [SerializeField] EndScreen endScreen;
        [SerializeField] TextAsset textJson;

        [Header("Development only")]
        [Tooltip("Editor only, when the Lesson scene is played directly: start the lesson at once, without calibration and orientation.")]
        [SerializeField] bool devSkipToLesson = true;

        public Step Current => step;
        public Orientation Orientation => orientation;
        public EndScreen EndScreen => endScreen;
        public bool OnBreak => breakKind != BreakKind.None;

        SessionText text;
        StudySession session;
        Step step;
        bool headset;
        BreakKind breakKind;
        bool headsetAway, awaitingResume;
        double breakStart;
        float timeScaleBeforeBreak = 1f;
        bool appFocused = true, appPaused;
        string awayReason;

        void Awake()
        {
            text = SessionText.FromJson(textJson.text);
        }

        void Start()
        {
            session = StudySession.Current;
            bool devSession = session == null;
            if (devSession)
            {
                if (!Application.isEditor)
                {
                    // A build always comes through the launch screen; never run without a session file.
                    Debug.LogError("[Session] Lesson scene loaded without a session: back to the launch screen.");
                    SceneManager.LoadScene(0);
                    return;
                }
                // Editor dev run of the Lesson scene: a throwaway session in DevData/.
                session = StudySession.Start("DEV" + DateTime.Now.ToString("HHmmss"), lesson.Config.Condition, "novice", "dev");
            }
            headset = lesson.Config.UsesHeadset && XRSettings.isDeviceActive;
            lesson.LessonEnded += OnLessonEnded;
            orientation.Finished += StartLesson;
            overlay.Hide();
            sphere.SetActive(false);
            SetInteractors(false);
            if (Application.isEditor) Time.timeScale = lesson.DevPlaybackSpeed; // dev runs: orientation keeps pace too
            if (devSession && devSkipToLesson)
            {
                Debug.LogWarning("[Session] Dev run: straight to the lesson (no calibration or orientation).");
                StartLesson();
                return;
            }
            if (headset) EnterCalibrate();
            else
            {
                StudyEvents.Log("calibration", $"source=desktop_fixed eye_height={DefaultEyeHeight:F3}");
                EnterReady();
            }
        }

        void OnDestroy()
        {
            if (lesson != null) lesson.LessonEnded -= OnLessonEnded;
            if (orientation != null) orientation.Finished -= StartLesson;
        }

        void EnterCalibrate()
        {
            step = Step.Calibrate;
            DesktopDisplay.SetCursorHidden(false);
        }

        void EnterReady()
        {
            step = Step.Ready;
            DesktopDisplay.SetCursorHidden(false);
        }

        void Calibrate()
        {
            Transform head = xrOrigin.Camera.transform;
            float eye = head.position.y - xrOrigin.transform.position.y; // the origin is the tracked floor
            float yawBefore = Vector3.SignedAngle(Vector3.forward, Vector3.ProjectOnPlane(head.forward, Vector3.up), Vector3.up);
            Vector3 headBefore = head.position;
            xrOrigin.MatchOriginUpCameraForward(Vector3.up, Vector3.forward);
            xrOrigin.MoveCameraToWorldLocation(new Vector3(0, head.position.y, 0));
            stage.position = new Vector3(0, eye - DefaultEyeHeight, 0);
            bool plausible = eye > 0.9f && eye < 1.6f; // seated eye heights
            StudyEvents.Log("calibration", $"source=headset eye_height={eye:F3} yaw_corrected={yawBefore:F1} " +
                                           $"moved={new Vector2(headBefore.x, headBefore.z).magnitude:F3} plausible={(plausible ? 1 : 0)}");
            if (!plausible) Debug.LogWarning($"[Session] calibration eye height {eye:F2} m is outside the seated range; check the floor level.");
        }

        void StartOrientation()
        {
            step = Step.Orientation;
            SetInteractors(headset);
            orientation.Begin(text, !headset);
        }

        void StartLesson()
        {
            if (step == Step.End) return;
            step = Step.Lesson;
            sphere.SetActive(true);
            SetInteractors(lesson.Config.LearnerRotates);
            if (!lesson.Config.UsesHeadset) DesktopDisplay.SetCursorHidden(true); // spec: no mouse during the lesson
            lesson.Begin();
        }

        void OnLessonEnded()
        {
            session.Finish("completed");
            EnterEnd(false);
        }

        void Abort(string inputName)
        {
            StudyEvents.Log("abort", $"step={step.ToString().ToLowerInvariant()} input={inputName}");
            if (OnBreak) EndBreak("abort");
            orientation.Stop();
            lesson.Abort();
            pauses.enabled = false;
            embodied.End();
            embodied.enabled = false;
            EnterEnd(true);
        }

        void EnterEnd(bool aborted)
        {
            step = Step.End;
            stage.gameObject.SetActive(false);
            overlay.Hide();
            SetInteractors(headset);
            DesktopDisplay.SetCursorHidden(false);
            endScreen.Show(text, session, aborted, headset);
        }

        void Update()
        {
            if (session == null) return;
            UpdateHeadsetPresence();

            if (step != Step.End && input.AbortTriggered)
            {
                Abort(input.AbortInput);
                return;
            }

            switch (step)
            {
                case Step.Calibrate:
                    if (input.CalibratePressed)
                    {
                        Calibrate();
                        EnterReady();
                    }
                    break;
                case Step.Ready:
                    if (headset && input.CalibratePressed) Calibrate(); // the experimenter may redo it
                    else if (input.TriggerPressed || input.MouseClicked) StartOrientation();
                    break;
                case Step.Orientation:
                case Step.Lesson:
                    if (input.BreakKeyPressed && breakKind != BreakKind.Headset)
                    {
                        if (breakKind == BreakKind.Key) EndBreak("key");
                        else BeginBreak(BreakKind.Key);
                    }
                    if (breakKind == BreakKind.Headset && awaitingResume && input.TriggerPressed) EndBreak("trigger");
                    break;
            }
            UpdateOverlay();
        }

        // ---- headset off / on ----

        void OnApplicationFocus(bool focused)
        {
            appFocused = focused;
            if (!headset && session != null && !session.Finished) StudyEvents.Log(focused ? "focus_back" : "focus_lost");
            UpdateHeadsetPresence();
        }

        void OnApplicationPause(bool paused)
        {
            appPaused = paused;
            UpdateHeadsetPresence();
        }

        // On Quest, taking the headset off pauses the app (and the system menu takes focus);
        // where the runtime reports it, the proximity sensor says so directly.
        void UpdateHeadsetPresence()
        {
            if (!headset || session == null || session.Finished) return;
            string reason = appPaused ? "app_paused" : !appFocused ? "focus_lost" : !input.UserPresent ? "user_absent" : null;
            bool away = reason != null;
            if (away == headsetAway) return;
            headsetAway = away;
            if (away)
            {
                awayReason = reason;
                StudyEvents.Log("hmd_removed", $"reason={reason} step={step.ToString().ToLowerInvariant()}");
                if ((step == Step.Orientation || step == Step.Lesson) && breakKind == BreakKind.None) BeginBreak(BreakKind.Headset);
                awaitingResume = false;
            }
            else
            {
                if (breakKind == BreakKind.Headset) awaitingResume = true; // "pull the trigger to continue"
                else StudyEvents.Log("hmd_returned", $"away_reason={awayReason}");
            }
        }

        // ---- breaks ----

        void BeginBreak(BreakKind kind)
        {
            breakKind = kind;
            breakStart = session.SessionSeconds;
            if (kind == BreakKind.Key) StudyEvents.Log("break_start", $"step={step.ToString().ToLowerInvariant()} input=key_b");
            if (step == Step.Lesson) lesson.SetBreak(true);
            else
            {
                timeScaleBeforeBreak = Time.timeScale;
                Time.timeScale = 0;
                orientation.SetPaused(true);
            }
            SetInteractors(false);
        }

        void EndBreak(string endedBy)
        {
            double duration = session.SessionSeconds - breakStart;
            StudyEvents.Log(breakKind == BreakKind.Headset ? "hmd_resume" : "break_end",
                $"duration={duration:F2} ended_by={endedBy} step={step.ToString().ToLowerInvariant()}");
            breakKind = BreakKind.None;
            awaitingResume = false;
            if (step == Step.Lesson) lesson.SetBreak(false);
            else
            {
                Time.timeScale = timeScaleBeforeBreak;
                orientation.SetPaused(false);
            }
            SetInteractors(step == Step.Orientation ? headset : lesson.Config.LearnerRotates);
        }

        // ---- overlay: calibration, ready, break and the abort hold ----

        void UpdateOverlay()
        {
            string title = null, body = null;
            float ring = -1;
            if (step != Step.End && input.AbortProgress > 0)
            {
                title = text.breakTitle;
                body = text.abortHold;
                ring = input.AbortProgress;
            }
            else if (breakKind == BreakKind.Key) (title, body) = (text.breakTitle, text.breakKey);
            else if (breakKind == BreakKind.Headset) (title, body) = (text.breakTitle, awaitingResume ? text.breakResumeVr : text.breakKey);
            else if (step == Step.Calibrate) (title, body) = (text.calibrationTitle, text.calibrationVr);
            else if (step == Step.Ready) (title, body) = (text.readyTitle, headset ? text.readyVr : text.readyDesktop);

            if (title == null)
            {
                if (overlay.IsVisible) overlay.Hide();
                return;
            }
            overlay.Show(title, body);
            overlay.SetRing(ring);
        }

        void SetInteractors(bool on)
        {
            foreach (GameObject interactor in interactors) interactor.SetActive(on);
        }

#if UNITY_EDITOR
        // Dev runs (LessonDevTools): the same paths as the experimenter's buttons.
        public void DevCalibrate() { if (step == Step.Calibrate) { EnterReady(); } }
        public void DevBegin() { if (step == Step.Ready) StartOrientation(); }
        public void DevBreak() { if (step == Step.Orientation || step == Step.Lesson) { if (OnBreak) EndBreak("dev"); else BeginBreak(BreakKind.Key); } }
        public void DevAbort() { if (step != Step.End) Abort("dev"); }
#endif
    }
}
