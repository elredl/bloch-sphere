using UnityEngine;
using UnityEngine.XR;

namespace Study.Lesson
{
    // Turns on the right viewpoint for the condition: the XR rig in the headset
    // conditions, the fixed desktop camera in Condition 1. Both look at the
    // same sphere from the same eye point. The XR rig is inactive in the saved
    // scene so it never renders a stray frame in the desktop condition.
    // In the launch scene (no lesson) it uses the headset whenever one is active.
    public class ViewSetup : MonoBehaviour
    {
        [Tooltip("Empty in the launch scene.")]
        [SerializeField] LessonController lesson;
        [SerializeField] GameObject desktopCamera;
        [SerializeField] GameObject xrOrigin;
        [Tooltip("Controller ray/grab interactors. Off unless the learner manipulates the qubit.")]
        [SerializeField] GameObject[] interactors;

        void Awake()
        {
            bool headsetActive = XRSettings.isDeviceActive;
            bool wantsHeadset = lesson == null ? headsetActive : lesson.Config.UsesHeadset;
            bool useXr = wantsHeadset && headsetActive;
            xrOrigin.SetActive(useXr);
            desktopCamera.SetActive(!useXr);
            // Lesson scene: SessionFlow switches the rays on for orientation and the end screen; in the
            // lesson passive learners keep their controllers (same as Cond 3) but get no pointer rays.
            foreach (GameObject interactor in interactors) interactor.SetActive(lesson == null || lesson.Config.LearnerRotates);
            string who = lesson == null ? "launch" : lesson.Config.ToString();

            if (wantsHeadset && !headsetActive)
            {
                // Expected in the Linux Editor (no Quest Link). A headset build must never get here.
                if (Application.isEditor) Debug.LogWarning($"[View] {who}: no headset active, using the desktop camera as a dev view.");
                else Debug.LogError($"[View] {who}: headset condition but no XR device is active.");
            }
            Debug.Log($"[View] {who}: {(useXr ? "XR rig" : "desktop camera")}");
        }
    }
}
