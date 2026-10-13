using UnityEngine;

namespace Study.Visuals
{
    // Turns a label to face whichever camera is about to draw it, so it stays
    // readable from the desktop camera, the headset, and offscreen captures alike.
    [ExecuteAlways]
    public class Billboard : MonoBehaviour
    {
        void OnWillRenderObject()
        {
            Camera cam = Camera.current;
            if (cam == null) return;
            transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position, cam.transform.up);
        }
    }
}
