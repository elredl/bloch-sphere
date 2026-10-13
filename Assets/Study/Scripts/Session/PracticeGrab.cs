using System;
using Study.Quantum;
using Study.Visuals;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Study.Session
{
    // Orientation: the grab on the practice cube's knob. In the headset it works like the
    // lesson's QubitGrabTarget (grip, near or by ray, the grab point fixed to the controller
    // like the end of a stick). On the desktop the learner presses the mouse on the knob and
    // drags; the grab point is where the mouse ray meets the sphere the knob moves on.
    // Either way the grab point goes to Orientation, whose ratchet turns the cube.
    public class PracticeGrab : MonoBehaviour
    {
        const float NearGrabMetres = 0.15f;

        [SerializeField] PracticeCubeView cube;
        [SerializeField] BlochSphereRig rig;
        [SerializeField] XRSimpleInteractable interactable;
        [SerializeField] Collider grabCollider;
        [SerializeField] float tickAmplitude = 0.25f;
        [SerializeField] float tickSeconds = 0.03f;

        // mode (near, ray or mouse) and the grab point in Bloch coordinates (rig center, radius 1).
        public event Action<string, Func<BlochVector>> Started;
        public event Action Ended;

        bool grabbable;
        bool mouseMode;
        IXRSelectInteractor holder;
        Vector3 leverLocal;
        bool mouseHeld;

        public bool IsHeld => holder != null || mouseHeld;

        void Awake() => SetGrabbable(false, false);

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

        // mouse: Condition 1 (no controllers). Turning it off mid-grab releases the grab.
        public void SetGrabbable(bool on, bool mouse)
        {
            grabbable = on;
            mouseMode = mouse;
            interactable.enabled = on && !mouse;
            grabCollider.enabled = on;
            if (!on && mouseHeld) ReleaseMouse();
        }

        void OnSelectEntered(SelectEnterEventArgs args)
        {
            if (holder != null) return;
            holder = args.interactorObject;
            leverLocal = holder.transform.InverseTransformPoint(transform.position);
            Started?.Invoke(leverLocal.magnitude <= NearGrabMetres ? "near" : "ray", ControllerPoint);
        }

        void OnSelectExited(SelectExitEventArgs args)
        {
            if (args.interactorObject != holder) return;
            holder = null;
            Ended?.Invoke();
        }

        BlochVector ControllerPoint() => ToBloch(holder.transform.TransformPoint(leverLocal));

        BlochVector ToBloch(Vector3 world) => BlochSpace.FromLocal(rig.transform.InverseTransformPoint(world) / rig.Radius);

        void Update()
        {
            if (!grabbable || !mouseMode) return;
            Mouse mouse = Mouse.current;
            Camera cam = Camera.main;
            if (mouse == null || cam == null) return;
            if (!mouseHeld && mouse.leftButton.wasPressedThisFrame &&
                grabCollider.Raycast(cam.ScreenPointToRay(mouse.position.ReadValue()), out _, 10f))
            {
                mouseHeld = true;
                Started?.Invoke("mouse", MousePoint);
            }
            else if (mouseHeld && !mouse.leftButton.isPressed) ReleaseMouse();
        }

        void ReleaseMouse()
        {
            mouseHeld = false;
            Ended?.Invoke();
        }

        // Where the mouse ray meets the sphere through the knob; past its edge, the ray's closest point.
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
            return ToBloch(point);
        }

        public void WrongWayTick()
        {
            if (holder is XRBaseInputInteractor input) input.SendHapticImpulse(tickAmplitude, tickSeconds);
        }

        void LateUpdate()
        {
            transform.localPosition = cube.KnobLocalPoint;
        }
    }
}
