using Study.Quantum;
using Study.Visuals;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Study.Lesson
{
    // The grab handle at the tip of the prediction vector while the learner places a
    // prediction (Conditions 1 and 3; present but switched off in Condition 2). In the
    // headset grabbing works up close and by ray, so the seated learner never leans:
    // the grab point is fixed to the controller like the end of a stick, so with a ray
    // it sits out at the sphere and pointing moves it. On the desktop the learner
    // presses the mouse on the tip and drags; the grab point is where the mouse ray
    // meets the sphere (as PracticeGrab). Each frame the grab point goes to
    // EmbodiedPause, which points the prediction vector at it.
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
        bool grabbable;
        bool mouseHeld;

        void Awake()
        {
            SetGrabbable(false);
        }

        void Start()
        {
            Debug.Log($"[Grab] {lesson.Config}: grab handle present, usable={lesson.Config.LearnerRotates} (only while placing a prediction)");
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

        // Never grabbable in Condition 2. Turning it off mid-grab releases the grab.
        public void SetGrabbable(bool on)
        {
            on &= lesson.Config != null && lesson.Config.LearnerRotates;
            grabbable = on;
            interactable.enabled = on && lesson.Config.UsesHeadset;
            grabCollider.enabled = on;
            if (!on && mouseHeld) ReleaseMouse();
        }

        void OnSelectEntered(SelectEnterEventArgs args)
        {
            if (holder != null) return; // one hand at a time
            holder = args.interactorObject;
            Transform hand = holder.transform;
            leverLocal = hand.InverseTransformPoint(transform.position);
            Started(leverLocal.magnitude <= NearGrabMetres ? "near" : "ray", HandPoint);
        }

        void OnSelectExited(SelectExitEventArgs args)
        {
            if (args.interactorObject != holder) return;
            holder = null;
            Ended();
        }

        void Started(string mode, System.Func<BlochVector> point)
        {
            pause.GrabStarted(mode, point);
        }

        void Ended()
        {
            pause.GrabEnded();
        }

        // Condition 1: press on the tip and drag.
        void Update()
        {
            if (!grabbable || lesson.Config.UsesHeadset) return;
            Mouse mouse = Mouse.current;
            Camera cam = Camera.main;
            if (mouse == null || cam == null) return;
            if (!mouseHeld && mouse.leftButton.wasPressedThisFrame &&
                grabCollider.Raycast(cam.ScreenPointToRay(mouse.position.ReadValue()), out _, 10f))
            {
                mouseHeld = true;
                Started("mouse", MousePoint);
            }
            else if (mouseHeld && !mouse.leftButton.isPressed) ReleaseMouse();
        }

        void ReleaseMouse()
        {
            mouseHeld = false;
            Ended();
        }

        // Where the mouse ray meets the sphere; past its edge, the ray's closest point.
        BlochVector MousePoint()
        {
            Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
            Vector3 center = rig.transform.position;
            float radius = rig.transform.TransformVector(Vector3.right * rig.Radius).magnitude;
            Vector3 toCenter = center - ray.origin;
            float along = Vector3.Dot(toCenter, ray.direction);
            Vector3 closest = ray.origin + ray.direction * along;
            float missSq = (closest - center).sqrMagnitude;
            Vector3 point = missSq <= radius * radius
                ? closest - ray.direction * Mathf.Sqrt(radius * radius - missSq)
                : center + (closest - center).normalized * radius;
            return BlochSpace.FromLocal(rig.transform.InverseTransformPoint(point) / rig.Radius);
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
            transform.localPosition = rig.LocalPoint(pause.HandleTip ?? state.Current.Bloch);
        }
    }
}
