using UnityEngine;

namespace Study.Visuals
{
    // Root of the Bloch sphere. Holds the two things every sphere view needs:
    // the style palette and the sphere radius. Views find it with GetComponentInParent.
    [ExecuteAlways]
    public class BlochSphereRig : MonoBehaviour
    {
        [SerializeField] StylePalette palette;
        [Tooltip("Metres. 0.3 m gives the 0.6 m diameter in the spec.")]
        [SerializeField] float radius = 0.3f;

        public StylePalette Palette => palette;
        public float Radius => radius;

        // Converts a Bloch direction to a point at the given fraction of the radius, in rig-local space.
        public Vector3 LocalPoint(Study.Quantum.BlochVector direction, float radiusFraction = 1f) =>
            BlochSpace.ToLocal(direction) * (radius * radiusFraction);
    }
}
