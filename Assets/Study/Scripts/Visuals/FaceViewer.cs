using UnityEngine;

namespace Study.Visuals
{
    // Keeps a session panel in front of the viewer at a fixed distance, upright. Used for the
    // screens that appear before calibration or wherever the viewer happens to look (launch,
    // calibration, break, end), never for the lesson panels, which stay beside the sphere.
    //   follow:  stays in front every frame, easing after the head (break and hold overlay)
    //   default: placed when shown, and again only if the viewer turns well away
    public class FaceViewer : MonoBehaviour
    {
        [SerializeField] float distance = 0.7f;
        [SerializeField] float heightOffset = -0.05f;
        [SerializeField] bool follow;
        [SerializeField] float replaceAngle = 40f;
        [SerializeField] float followSpeed = 6f;

        void OnEnable() => Place(true);

        void LateUpdate() => Place(false);

        public void Place(bool snap)
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            Transform head = cam.transform;
            Vector3 target = head.position + head.forward * distance + head.up * heightOffset;

            if (!snap && !follow)
            {
                Vector3 toPanel = transform.position - head.position;
                if (Vector3.Angle(toPanel, head.forward) < replaceAngle && Mathf.Abs(toPanel.magnitude - distance) < 0.3f) return;
                snap = true;
            }
            float t = snap ? 1f : 1f - Mathf.Exp(-followSpeed * Time.unscaledDeltaTime);
            transform.position = Vector3.Lerp(transform.position, target, t);
            transform.rotation = Quaternion.LookRotation(transform.position - head.position, Vector3.up);
        }
    }
}
