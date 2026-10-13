using Study.Quantum;
using UnityEngine;

namespace Study.Visuals
{
    // The one place where physics Bloch coordinates meet Unity coordinates.
    // Textbook drawing, as seen by a participant facing +Z:
    //   Bloch +z (|0>)  -> Unity +Y (up)
    //   Bloch +x (|+>)  -> Unity -Z (toward the participant)
    //   Bloch +y (|+i>) -> Unity +X (participant's right)
    // Viewed from above this keeps x -> y counterclockwise, so the right-hand
    // rule looks right on screen even though Unity itself is left-handed.
    public static class BlochSpace
    {
        public static Vector3 ToLocal(BlochVector v) => new Vector3((float)v.Y, (float)v.Z, (float)-v.X);

        public static BlochVector FromLocal(Vector3 u) => new BlochVector(-u.z, u.x, u.y);
    }
}
