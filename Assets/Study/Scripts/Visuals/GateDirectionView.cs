using System.Collections.Generic;
using Study.Quantum;
using UnityEngine;

namespace Study.Visuals
{
    // Condition 3: a short curved amber arrow just outside the sphere, running
    // from the vector's tip along the way the gate turns it. It tells the learner
    // which way to push. It shows only the next stretch of the path (never the end
    // point) and shrinks to nothing as the vector reaches the gate's angle.
    // The geometry is computed in Bloch space with the gate's own rotation, so
    // the arrow always points the way the arc will actually go.
    public class GateDirectionView : SphereView
    {
        const int TubeSides = 8;
        const int ArcSegments = 24;
        const float SurfaceOffset = 1.08f;            // fraction of the radius
        const double LeadDegrees = 6, SpanDegrees = 36;
        const float HeadLengthMetres = 0.022f;

        public override bool IsDimmable => false;

        readonly TubeMeshBuilder builder = new TubeMeshBuilder();
        readonly List<Vector3> arc = new List<Vector3>();
        Mesh coreMesh, haloMesh;
        Transform root;
        Gate gate;
        BlochVector start;
        double angle;
        bool visible;

        protected override void Build()
        {
            StylePalette p = Palette;
            root = Track(new GameObject("Direction Arrow") { hideFlags = HideFlags.DontSave }).transform;
            root.SetParent(transform, false);
            coreMesh = new Mesh { name = "Direction Arrow", hideFlags = HideFlags.DontSave };
            haloMesh = new Mesh { name = "Direction Arrow Halo", hideFlags = HideFlags.DontSave };
            AddMesh("Arrow", coreMesh, StyledMaterials.LineCore(p, p.gateAxis), root);
            AddMesh("Arrow Halo", haloMesh, StyledMaterials.LineHalo(p, p.gateAxis), root);
            RebuildMesh();
        }

        // startState: where the gate began; angle: radians turned so far.
        public void Show(Gate gate, Qubit startState, double angle)
        {
            this.gate = gate;
            start = startState.Bloch;
            visible = true;
            SetAngle(angle);
        }

        public void SetAngle(double radians)
        {
            angle = radians;
            RebuildMesh();
        }

        public void Hide()
        {
            visible = false;
            RebuildMesh();
        }

        void RebuildMesh()
        {
            if (coreMesh == null) return;
            builder.Clear();
            double from = angle + LeadDegrees * Mathf.Deg2Rad;
            double to = gate == null ? 0 : System.Math.Min(angle + (LeadDegrees + SpanDegrees) * Mathf.Deg2Rad, gate.Angle);
            bool show = visible && gate != null && to - from > 2 * Mathf.Deg2Rad;
            root.gameObject.SetActive(show);
            if (!show)
            {
                builder.ToMesh(coreMesh);
                builder.ToMesh(haloMesh);
                return;
            }

            arc.Clear();
            for (int i = 0; i <= ArcSegments; i++)
                arc.Add(Rig.LocalPoint(start.RotatedAbout(gate.Axis, from + (to - from) * i / ArcSegments), SurfaceOffset));

            // Arrowhead: two short strokes back from the tip, either side of the arc.
            Vector3 tip = arc[arc.Count - 1];
            Vector3 back = (arc[arc.Count - 2] - tip).normalized;
            Vector3 side = Vector3.Cross(back, tip.normalized).normalized;
            Vector3 wingA = tip + (back + side * 0.75f).normalized * HeadLengthMetres;
            Vector3 wingB = tip + (back - side * 0.75f).normalized * HeadLengthMetres;

            float radius = Palette.gateAxis.widthMm * 0.0005f;
            Fill(radius, tip, wingA, wingB);
            builder.ToMesh(coreMesh);
            builder.Clear();
            Fill(radius * Palette.haloWidthMultiplier, tip, wingA, wingB);
            builder.ToMesh(haloMesh);
        }

        void Fill(float radius, Vector3 tip, Vector3 wingA, Vector3 wingB)
        {
            builder.AddPolyline(arc, radius, TubeSides, false);
            builder.AddSegment(tip, wingA, radius, TubeSides);
            builder.AddSegment(tip, wingB, radius, TubeSides);
        }
    }
}
