using System.Collections.Generic;
using Study.Quantum;
using UnityEngine;

namespace Study.Visuals
{
    // Orientation: the glowing practice cube with a handle the learner turns about a fixed
    // vertical axis, the same ratchet grab as the lesson's state vector (spec, Session flow).
    // It sits on its own BlochSphereRig so the rotation uses the lesson's Bloch frame and
    // its arrow and axis views: the handle points along Bloch +x (toward the viewer) and
    // turns about Bloch z.
    public class PracticeCubeView : SphereView
    {
        const int TubeSides = 8;
        const float HalfEdgeMetres = 0.07f;
        const float KnobRadiusMetres = 0.018f;

        public static readonly BlochVector HandleStart = BlochVector.UnitX;
        public static readonly BlochVector Axis = BlochVector.UnitZ;

        public override bool IsDimmable => false;

        Transform spin;
        double angle;

        // Where the handle's knob is now, in rig-local space.
        public Vector3 KnobLocalPoint => Rig.LocalPoint(HandleStart.RotatedAbout(Axis, angle));

        protected override void Build()
        {
            StylePalette p = Palette;
            spin = Track(new GameObject("Spin") { hideFlags = HideFlags.DontSave }).transform;
            spin.SetParent(transform, false);

            AddLine("Cube", p.referenceRings, (b, r) =>
            {
                foreach ((Vector3 from, Vector3 to) in Edges()) b.AddSegment(from, to, r, TubeSides);
            }, spin);

            var handle = new StylePalette.Line(p.vectorGlow, p.vectorGlowIntensity, p.vectorCoreWidthMm * 0.6f, true);
            Vector3 knob = Rig.LocalPoint(HandleStart);
            Vector3 face = knob.normalized * HalfEdgeMetres;
            AddLine("Handle", handle, (b, r) => b.AddSegment(face, knob, r, TubeSides), spin);
            GameObject knobMesh = AddMesh("Knob", TubeMeshBuilder.Sphere(KnobRadiusMetres, 16, 8),
                StyledMaterials.Neon(p, p.vectorCore, p.vectorGlowIntensity, 0f, 0.6f), spin);
            knobMesh.transform.localPosition = knob;
            SetAngle(angle);
        }

        // radians turned so far about the axis
        public void SetAngle(double radians)
        {
            angle = radians;
            if (spin == null) return;
            Vector3 from = BlochSpace.ToLocal(HandleStart);
            Vector3 to = BlochSpace.ToLocal(HandleStart.RotatedAbout(Axis, radians));
            spin.localRotation = Quaternion.FromToRotation(from, to);
        }

        static IEnumerable<(Vector3, Vector3)> Edges()
        {
            float h = HalfEdgeMetres;
            for (int a = 0; a < 3; a++)
            for (int s1 = -1; s1 <= 1; s1 += 2)
            for (int s2 = -1; s2 <= 1; s2 += 2)
            {
                Vector3 from = Vector3.zero, to = Vector3.zero;
                from[a] = -h;
                to[a] = h;
                from[(a + 1) % 3] = to[(a + 1) % 3] = s1 * h;
                from[(a + 2) % 3] = to[(a + 2) % 3] = s2 * h;
                yield return (from, to);
            }
        }
    }
}
