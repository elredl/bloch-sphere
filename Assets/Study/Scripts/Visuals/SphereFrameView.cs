using System.Collections.Generic;
using Study.Quantum;
using UnityEngine;

namespace Study.Visuals
{
    // The static instrument: lat/long wireframe, the two brighter reference
    // rings (equator and prime meridian), the faint shell, the three axes and
    // their labels. Rebuilt only when the palette changes.
    public class SphereFrameView : SphereView
    {
        const int CircleSegments = 128;
        const int TubeSides = 8;

        public override bool IsDimmable => true;

        protected override void Build()
        {
            StylePalette p = Palette;

            AddLine("Wireframe", p.wireframe, (b, r) =>
            {
                foreach (float latitude in new[] { 30f, 60f, 120f, 150f })
                    b.AddPolyline(Latitude(latitude * Mathf.Deg2Rad), r, TubeSides, true);
                foreach (float longitude in new[] { 30f, 60f, 90f, 120f, 150f })
                    b.AddPolyline(Meridian(longitude * Mathf.Deg2Rad), r, TubeSides, true);
            });

            AddLine("Reference Rings", p.referenceRings, (b, r) =>
            {
                b.AddPolyline(Latitude(Mathf.PI / 2), r, TubeSides, true); // equator
                b.AddPolyline(Meridian(0), r, TubeSides, true);            // prime meridian, through x and z
            });

            var shell = new Material(p.shellShader) { hideFlags = HideFlags.DontSave };
            shell.SetColor("_Color", p.shellColor);
            shell.SetFloat("_BaseAlpha", p.shellAlpha);
            shell.SetFloat("_RimAlpha", p.shellRimAlpha);
            AddMesh("Shell", TubeMeshBuilder.Sphere(Radius, 64, 32), shell);

            AddAxis("Axis X", p.axisX, BlochVector.UnitX);
            AddAxis("Axis Y", p.axisY, BlochVector.UnitY);
            AddAxis("Axis Z", p.axisZ, BlochVector.UnitZ);

            // Labels pair every axis color with a name, so color is never the only cue.
            float labelOut = p.axisExtent + 0.12f;
            AddLabel("|0⟩", p.axisZ.color, Rig.LocalPoint(BlochVector.UnitZ, labelOut));
            AddLabel("|1⟩", p.axisZ.color, Rig.LocalPoint(BlochVector.UnitZ * -1, labelOut));
            AddLabel("x", p.axisX.color, Rig.LocalPoint(BlochVector.UnitX, labelOut));
            AddLabel("y", p.axisY.color, Rig.LocalPoint(BlochVector.UnitY, labelOut));
        }

        void AddAxis(string name, StylePalette.Line style, BlochVector direction)
        {
            Vector3 end = Rig.LocalPoint(direction, Palette.axisExtent);
            AddLine(name, style, (b, r) => b.AddSegment(-end, end, r, TubeSides));
        }

        // Circle of constant polar angle theta (from +z).
        List<Vector3> Latitude(float theta)
        {
            var points = new List<Vector3>(CircleSegments);
            for (int i = 0; i < CircleSegments; i++)
            {
                double phi = i * 2 * System.Math.PI / CircleSegments;
                points.Add(Rig.LocalPoint(new BlochVector(System.Math.Sin(theta) * System.Math.Cos(phi),
                    System.Math.Sin(theta) * System.Math.Sin(phi), System.Math.Cos(theta))));
            }
            return points;
        }

        // Great circle through both poles at azimuth phi (and phi + 180 deg).
        List<Vector3> Meridian(float phi)
        {
            var points = new List<Vector3>(CircleSegments);
            for (int i = 0; i < CircleSegments; i++)
            {
                double t = i * 2 * System.Math.PI / CircleSegments;
                points.Add(Rig.LocalPoint(new BlochVector(System.Math.Sin(t) * System.Math.Cos(phi),
                    System.Math.Sin(t) * System.Math.Sin(phi), System.Math.Cos(t))));
            }
            return points;
        }
    }
}
