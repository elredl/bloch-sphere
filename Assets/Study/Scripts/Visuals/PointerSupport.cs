using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace Study.Visuals
{
    // Makes a world-space canvas clickable by whatever pointer this session has: controller
    // rays in the headset (TrackedDeviceGraphicRaycaster), the mouse on the desktop
    // (GraphicRaycaster, added only when no headset is active so it never sees ray events).
    public class PointerSupport : MonoBehaviour
    {
        void Awake() => Add(gameObject);

        public static void Add(GameObject canvas)
        {
            if (canvas.GetComponent<TrackedDeviceGraphicRaycaster>() == null)
                canvas.AddComponent<TrackedDeviceGraphicRaycaster>().ignoreReversedGraphics = false;
            if (!XRSettings.isDeviceActive && canvas.GetComponent<GraphicRaycaster>() == null)
                canvas.AddComponent<GraphicRaycaster>().ignoreReversedGraphics = false;
        }
    }
}
