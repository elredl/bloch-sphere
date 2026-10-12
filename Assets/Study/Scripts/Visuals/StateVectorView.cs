using Study.Quantum;
using UnityEngine;

namespace Study.Visuals
{
    // Points a rod from the sphere center to the state's Bloch point.
    // The rod is a unit-height-2 cylinder (Unity primitive), so its length
    // is always exactly the sphere radius: length never encodes probability.
    public class StateVectorView : MonoBehaviour
    {
        [SerializeField] QubitState state;
        [SerializeField] Transform sphereCenter;
        [SerializeField] float sphereRadius = 0.5f;

        void OnEnable()
        {
            if (state != null) state.Changed += Show;
        }

        void OnDisable()
        {
            if (state != null) state.Changed -= Show;
        }

        void Show(Qubit q)
        {
            Vector3 direction = sphereCenter.TransformDirection(BlochSpace.ToLocal(q.Bloch)).normalized;
            transform.position = sphereCenter.position + direction * (sphereRadius / 2f);
            transform.rotation = Quaternion.FromToRotation(Vector3.up, direction);
            Vector3 scale = transform.localScale;
            transform.localScale = new Vector3(scale.x, sphereRadius / 2f, scale.z);
        }
    }
}
