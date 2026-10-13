using UnityEngine;
using UnityEngine.InputSystem;

namespace Study.Session
{
    // The session's own buttons, read straight from the Input System so they work whatever
    // the XRI interactors are doing (Condition 2 has no rays during the lesson):
    //
    //   participant    trigger (either hand) or mouse click: begin, continue after a break
    //   experimenter   Quest: hold A (right controller) 1 s to calibrate;
    //                  hold both grips and click in both thumbsticks 3 s to abort
    //                  desktop: B pauses / resumes; hold Esc 2 s to abort
    //   headset        userPresence (proximity sensor), where the runtime reports it
    public class SessionInput : MonoBehaviour
    {
        [SerializeField] float calibrateHoldSeconds = 1f;
        [SerializeField] float headsetAbortHoldSeconds = 3f;
        [SerializeField] float desktopAbortHoldSeconds = 2f;

        InputAction trigger, calibrate, leftGrip, rightGrip, leftStick, rightStick, presence;
        float calibrateHeld, abortHeld;
        bool calibrateFired, abortFired;

        public bool TriggerPressed { get; private set; }  // this frame
        public bool MouseClicked { get; private set; }    // this frame
        public bool BreakKeyPressed { get; private set; } // this frame
        public bool CalibratePressed { get; private set; } // this frame, after the hold
        public bool AbortTriggered { get; private set; }  // this frame, after the hold
        public float AbortProgress { get; private set; }  // 0-1 while the abort hold is in progress
        public string AbortInput { get; private set; }    // controllers or esc

        // False only when the runtime says nobody is wearing the headset. Trusted only after it
        // has once reported someone present, so a runtime that never reports it can't start a break.
        public bool UserPresent => !presenceSeen || presence.IsPressed();

        bool presenceSeen;

        void Awake()
        {
            trigger = new InputAction("Trigger", InputActionType.Button);
            trigger.AddBinding("<XRController>{LeftHand}/{TriggerButton}");
            trigger.AddBinding("<XRController>{RightHand}/{TriggerButton}");
            calibrate = Button("<XRController>{RightHand}/{PrimaryButton}");
            leftGrip = Button("<XRController>{LeftHand}/{GripButton}");
            rightGrip = Button("<XRController>{RightHand}/{GripButton}");
            leftStick = Button("<XRController>{LeftHand}/{Primary2DAxisClick}");
            rightStick = Button("<XRController>{RightHand}/{Primary2DAxisClick}");
            presence = Button("<XRHMD>/userPresence");
        }

        static InputAction Button(string path) => new InputAction(path, InputActionType.Button, path);

        void OnEnable()
        {
            foreach (InputAction a in All()) a.Enable();
        }

        void OnDisable()
        {
            foreach (InputAction a in All()) a.Disable();
        }

        void OnDestroy()
        {
            foreach (InputAction a in All()) a.Dispose();
        }

        InputAction[] All() => new[] { trigger, calibrate, leftGrip, rightGrip, leftStick, rightStick, presence };

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            presenceSeen |= presence.IsPressed();
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;

            TriggerPressed = trigger.WasPressedThisFrame();
            MouseClicked = mouse != null && mouse.leftButton.wasPressedThisFrame;
            BreakKeyPressed = keyboard != null && keyboard.bKey.wasPressedThisFrame;

            CalibratePressed = Hold(calibrate.IsPressed(), ref calibrateHeld, ref calibrateFired, calibrateHoldSeconds, dt);

            bool controllers = leftGrip.IsPressed() && rightGrip.IsPressed() && leftStick.IsPressed() && rightStick.IsPressed();
            bool esc = keyboard != null && keyboard.escapeKey.isPressed;
            float needed = controllers ? headsetAbortHoldSeconds : desktopAbortHoldSeconds;
            if (controllers || esc) AbortInput = controllers ? "controllers" : "esc";
            AbortTriggered = Hold(controllers || esc, ref abortHeld, ref abortFired, needed, dt);
            AbortProgress = abortFired ? 1f : Mathf.Clamp01(abortHeld / needed);
        }

        // True on the frame the hold reaches its time; the hold must be released before it fires again.
        static bool Hold(bool down, ref float held, ref bool fired, float seconds, float dt)
        {
            if (!down)
            {
                held = 0;
                fired = false;
                return false;
            }
            held += dt;
            if (fired || held < seconds) return false;
            fired = true;
            return true;
        }
    }
}
