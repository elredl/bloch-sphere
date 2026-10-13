using UnityEngine;
using UnityEngine.XR;

namespace Study.Lesson
{
    // Turns on the right viewpoint for the condition: the XR rig in the headset
    // conditions, the fixed desktop camera in Condition 1. Both look at the
    // same sphere from the same eye point. The XR rig is inactive in the saved
    // scene so it never renders a stray frame in the desktop condition.
    public class ViewSetup : MonoBehaviour
    {
        [SerializeField] LessonController lesson;
        [SerializeField] GameObject desktopCamera;
        [SerializeField] GameObject xrOrigin;
        [Tooltip("Controller ray/grab interactors. Off unless the learner manipulates the qubit.")]
        [SerializeField] GameObject[] interactors;

        void Awake()
        {
            bool headsetActive = XRSettings.isDeviceActive;
            bool useXr = lesson.Config.UsesHeadset && headsetActive;
            xrOrigin.SetActive(useXr);
            desktopCamera.SetActive(!useXr);
            // Passive learners keep their controllers (same as Cond 3) but get no pointer rays.
            foreach (GameObject interactor in interactors) interactor.SetActive(lesson.Config.LearnerRotates);

            if (lesson.Config.UsesHeadset && !headsetActive)
            {
                // Expected in the Linux Editor (no Quest Link). A headset build must never get here.
                if (Application.isEditor) Debug.LogWarning($"[View] {lesson.Config}: no headset active, using the desktop camera as a dev view.");
                else Debug.LogError($"[View] {lesson.Config}: headset condition but no XR device is active.");
            }
            Debug.Log($"[View] {lesson.Config}: {(useXr ? "XR rig" : "desktop camera")}");
        }
    }
}
