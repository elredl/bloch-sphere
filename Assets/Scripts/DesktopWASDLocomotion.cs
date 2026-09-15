using UnityEngine;
using UnityEngine.InputSystem;

// Lets the XR rig also be driven by keyboard WASD, so the scene is
// playable without a headset. VR locomotion (thumbstick/teleport, wired
// on the rig's own components) keeps working the same time since both
// just move this same transform.
[RequireComponent(typeof(CharacterController))]
public class DesktopWASDLocomotion : MonoBehaviour
{
    public float moveSpeed = 3f;
    public float gravity = -9.81f;

    CharacterController controller;
    float verticalVelocity;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null || controller == null) return;

        Vector2 input = Vector2.zero;
        if (kb.wKey.isPressed) input.y += 1f;
        if (kb.sKey.isPressed) input.y -= 1f;
        if (kb.dKey.isPressed) input.x += 1f;
        if (kb.aKey.isPressed) input.x -= 1f;
        input = Vector2.ClampMagnitude(input, 1f);

        Vector3 move = transform.right * input.x + transform.forward * input.y;

        if (controller.isGrounded) verticalVelocity = -0.5f;
        else verticalVelocity += gravity * Time.deltaTime;

        controller.Move((move * moveSpeed + Vector3.up * verticalVelocity) * Time.deltaTime);
    }
}
