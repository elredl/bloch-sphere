using Study.Quantum;
using Study.Visuals;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Study.Lesson
{
    // The grab handle at the tip of the state vector (Condition 3 only; present
    // but switched off in the shared Condition 2/3 build when the learner doesn't
    // rotate). Grabbing works up close and by ray, so the seated learner never
    // leans: the grab point is fixed to the controller like the end of a stick,
    // so with a ray it sits out at the sphere and pointing moves it. Each frame
    // the grab point goes to EmbodiedPause, whose ratchet decides the rotation.
    public class QubitGrabTarget : MonoBehaviour
    {
        // A grab that starts with the controller this close to the tip counts as "near".
        const float NearGrabMetres = 0.15f;

        [SerializeField] LessonController lesson;
        [SerializeField] QubitState state;
        [SerializeField] BlochSphereRig rig;
        [SerializeField] XRSimpleInteractable interactable;
        [SerializeField] Collider grabCollider;
        [SerializeField] EmbodiedPause pause;
        [Tooltip("Wrong-way haptic tick: light and short.")]
        [SerializeField] float tickAmplitude = 0.25f;
        [SerializeField] float tickSeconds = 0.03f;

        IXRSelectInteractor holder;
        Vector3 leverLocal; // grab point in the holder's local space

        void Awake()
        {
            SetGrabbable(false);
        }

        void Start()
        {
            Debug.Log($"[Grab] {lesson.Config}: grab handle present, usable={lesson.Config.LearnerRotates} (only after a gate press)");
        }

        void OnEnable()
        {
            interactable.selectEntered.AddListener(OnSelectEntered);
            interactable.selectExited.AddListener(OnSelectExited);
        }

        void OnDisable()
        {
            interactable.selectEntered.RemoveListener(OnSelectEntered);
            interactable.selectExited.RemoveListener(OnSelectExited);
        }

        // Never grabbable outside Condition 3. Turning it off mid-grab releases the grab.
        public void SetGrabbable(bool on)
        {
            on &= lesson.Config != null && lesson.Config.LearnerRotates;
            interactable.enabled = on;
            grabCollider.enabled = on;
        }

        void OnSelectEntered(SelectEnterEventArgs args)
        {
            if (holder != null) return; // one hand at a time
            holder = args.interactorObject;
            Transform hand = holder.transform;
            leverLocal = hand.InverseTransformPoint(transform.position);
            string mode = leverLocal.magnitude <= NearGrabMetres ? "near" : "ray";
            pause.GrabStarted(mode, HandPoint);
        }

        void OnSelectExited(SelectExitEventArgs args)
        {
            if (args.interactorObject != holder) return;
            holder = null;
            pause.GrabEnded();
        }

        // The grab point in Bloch coordinates (sphere center = origin, radius = 1).
        BlochVector HandPoint()
        {
            Vector3 world = holder.transform.TransformPoint(leverLocal);
            return BlochSpace.FromLocal(rig.transform.InverseTransformPoint(world) / rig.Radius);
        }

        public void WrongWayTick()
        {
            if (holder is XRBaseInputInteractor input) input.SendHapticImpulse(tickAmplitude, tickSeconds);
        }

        void LateUpdate()
        {
            transform.localPosition = rig.LocalPoint(state.Current.Bloch);
        }
    }
}
