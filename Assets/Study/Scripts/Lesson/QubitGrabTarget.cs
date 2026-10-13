using Study.Visuals;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Study.Lesson
{
    // The grab handle at the tip of the state vector. Present in the shared
    // Condition 2/3 build and only switched on when the condition lets the
    // learner rotate (Condition 3). The constrained-axis ratchet itself comes
    // in Checkpoint 4; this keeps the handle following the vector.
    public class QubitGrabTarget : MonoBehaviour
    {
        [SerializeField] LessonController lesson;
        [SerializeField] QubitState state;
        [SerializeField] BlochSphereRig rig;
        [SerializeField] XRSimpleInteractable interactable;
        [SerializeField] Collider grabCollider;

        void Start()
        {
            bool enabledForCondition = lesson.Config.LearnerRotates;
            interactable.enabled = enabledForCondition;
            grabCollider.enabled = enabledForCondition;
            Debug.Log($"[Grab] {lesson.Config}: grab handle present, enabled={enabledForCondition}");
        }

        void LateUpdate()
        {
            transform.localPosition = rig.LocalPoint(state.Current.Bloch);
        }
    }
}
