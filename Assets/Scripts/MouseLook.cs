using UnityEngine;
using UnityEngine.InputSystem;

// Desktop-only mouse look: yaw rotates the rig (shared with WASD movement),
// pitch rotates just the head camera so it doesn't fight VR head tracking,
// which drives the camera's rotation directly whenever a headset is active.
public class MouseLook : MonoBehaviour
{
    public Transform pitchTarget;
    public float sensitivity = 2f;
    public float minPitch = -80f;
    public float maxPitch = 80f;

    float pitch;

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        var mouse = Mouse.current;
        if (mouse != null && Cursor.lockState != CursorLockMode.Locked && mouse.leftButton.wasPressedThisFrame)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        if (mouse == null || Cursor.lockState != CursorLockMode.Locked) return;

        Vector2 delta = mouse.delta.ReadValue() * sensitivity * 0.02f;
        transform.Rotate(Vector3.up, delta.x, Space.World);

        if (pitchTarget != null)
        {
            pitch = Mathf.Clamp(pitch - delta.y, minPitch, maxPitch);
            pitchTarget.localEulerAngles = new Vector3(pitch, 0f, 0f);
        }
    }
}
