using System.Collections.Generic;
using Study.Quantum;
using TMPro;
using UnityEngine;

namespace Study.Visuals
{
    // Polar (theta) and azimuthal (phi) angles of the current state, drawn as
    // two distinct arcs with their symbols. Theta is measured from z, so it is
    // z cyan; phi is measured from x in the equator plane, so it is x orange.
    // A dashed line from the center to the vector's shadow on the equator
    // shows where phi ends.
    public class AngleArcsView : SphereView
    {
        const int TubeSides = 8;
        const int ArcSegments = 48;

        [SerializeField] bool visible;

        public override bool IsDimmable => true;

        QubitState state;
        readonly TubeMeshBuilder builder = new TubeMeshBuilder();
        readonly List<Vector3> points = new List<Vector3>();
        Mesh thetaCore, thetaHalo, phiCore, phiHalo, shadowLine;
        TextMeshPro thetaLabel, phiLabel;
        Transform root;

        public bool Visible
        {
            get => visible;
            set
            {
                visible = value;
                if (root != null) root.gameObject.SetActive(value);
                if (value && state != null) Show(state.Current);
            }
        }

        protected override void OnEnable()
        {
            state = GetComponentInParent<QubitState>();
            if (state != null) state.Changed += Show;
            base.OnEnable();
        }

        protected override void OnDisable()
        {
            if (state != null) state.Changed -= Show;
            base.OnDisable();
        }

        protected override void Build()
        {
            StylePalette p = Palette;
            root = Track(new GameObject("Angle Arcs") { hideFlags = HideFlags.DontSave }).transform;
            root.SetParent(transform, false);

            thetaCore = MeshFor("Theta Arc", StyledMaterials.LineCore(p, p.thetaArc));
            thetaHalo = MeshFor("Theta Arc Halo", StyledMaterials.LineHalo(p, p.thetaArc));
            phiCore = MeshFor("Phi Arc", StyledMaterials.LineCore(p, p.phiArc));
            phiHalo = MeshFor("Phi Arc Halo", StyledMaterials.LineHalo(p, p.phiArc));
            Material shadowMaterial = StyledMaterials.LineCore(p, p.phiArc);
            StyledMaterials.SetDash(shadowMaterial, 0.012f, 0.008f);
            shadowLine = MeshFor("Phi Shadow Line", shadowMaterial);

            thetaLabel = AddLabel("θ", p.thetaArc.color, Vector3.zero, root);
            phiLabel = AddLabel("φ", p.phiArc.color, Vector3.zero, root);

            root.gameObject.SetActive(visible);
            if (state != null) Show(state.Current);
        }

        Mesh MeshFor(string name, Material material)
        {
            var mesh = new Mesh { name = name, hideFlags = HideFlags.DontSave };
            AddMesh(name, mesh, material, root);
            return mesh;
        }

        void Show(Qubit q)
        {
            if (!visible || root == null) return;
            StylePalette p = Palette;
            double theta = q.Theta;
            double phi = q.Phi;

            // Theta: from +z down to the vector, in the vector's meridian plane.
            points.Clear();
            for (int i = 0; i <= ArcSegments; i++)
            {
                double t = theta * i / ArcSegments;
                points.Add(Rig.LocalPoint(Direction(t, phi), p.thetaArcRadius));
            }
            FillArc(thetaCore, thetaHalo, p.thetaArc);
            thetaLabel.transform.localPosition = Rig.LocalPoint(Direction(theta / 2, phi), p.thetaArcRadius + 0.14f);
            thetaLabel.gameObject.SetActive(theta > 0.05);

            // Phi is undefined at the poles: hide its arc and line there.
            bool phiDefined = theta > 0.05 && theta < System.Math.PI - 0.05;
            points.Clear();
            if (phiDefined)
                for (int i = 0; i <= ArcSegments; i++)
                    points.Add(Rig.LocalPoint(Direction(System.Math.PI / 2, phi * i / ArcSegments), p.phiArcRadius));
            FillArc(phiCore, phiHalo, p.phiArc);
            phiLabel.transform.localPosition = Rig.LocalPoint(Direction(System.Math.PI / 2, phi / 2), p.phiArcRadius + 0.14f);
            phiLabel.gameObject.SetActive(phiDefined && phi > 0.05);

            // Dashed line from the center to the vector's shadow on the equator.
            builder.Clear();
            if (phiDefined)
            {
                BlochVector b = q.Bloch;
                builder.AddSegment(Vector3.zero, Rig.LocalPoint(new BlochVector(b.X, b.Y, 0)), p.phiArc.widthMm * 0.0004f, TubeSides);
            }
            builder.ToMesh(shadowLine);
        }

        void FillArc(Mesh core, Mesh halo, StylePalette.Line style)
        {
            float radius = style.widthMm * 0.0005f;
            builder.Clear();
            builder.AddPolyline(points, radius, TubeSides, false);
            builder.ToMesh(core);
            builder.Clear();
            builder.AddPolyline(points, radius * Palette.haloWidthMultiplier, TubeSides, false);
            builder.ToMesh(halo);
        }

        static BlochVector Direction(double theta, double phi) => new BlochVector(
            System.Math.Sin(theta) * System.Math.Cos(phi), System.Math.Sin(theta) * System.Math.Sin(phi), System.Math.Cos(theta));
    }
}
