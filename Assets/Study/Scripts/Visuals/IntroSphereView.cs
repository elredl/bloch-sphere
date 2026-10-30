using System.Collections;
using System.Collections.Generic;
using Study.Quantum;
using UnityEngine;

namespace Study.Visuals
{
    // The intro draws the Bloch sphere in front of the learner, stroke by stroke, with
    // the same lines, axes and labels as SphereFrameView (and the same rig orientation),
    // so when the drawing is done IntroSequence swaps in the real sphere unnoticed:
    //
    //   equator, then the prime meridian, then the rest of the wireframe circle by
    //   circle, then the three axes grow out from the center, then |0⟩, |1⟩, x and y
    public class IntroSphereView : IntroDrawingView
    {
        const int CircleSegments = 128;

        // The state the intro's example vector points to: up and to the left, across the view, away from the axes.
        public static readonly Qubit Example = Qubit.FromBlochAngles(50 * System.Math.PI / 180, -60 * System.Math.PI / 180);
        // The intro's example unitary: turns Example across the front of the sphere to the right.
        public static readonly Gate Turn = Gate.FromName("Rz(150)");
        public static Qubit Moved => Turn.Apply(Example);

        Part sphere, vectorLabels;
        TMPro.TextMeshPro psiLabel, lengthLabel, movedLabel;
        Stroke equator, primeMeridian;
        readonly List<Stroke> wireframe = new List<Stroke>();
        readonly List<Stroke> axes = new List<Stroke>();
        readonly List<TMPro.TextMeshPro> labels = new List<TMPro.TextMeshPro>();

        protected override void Build()
        {
            StylePalette p = Palette;
            sphere = new Part();
            equator = AddStroke(sphere, "Equator", p.referenceRings, Latitude(Mathf.PI / 2), true);
            primeMeridian = AddStroke(sphere, "Prime Meridian", p.referenceRings, Meridian(0), true);
            wireframe.Clear();
            foreach (float latitude in new[] { 30f, 60f, 120f, 150f })
                wireframe.Add(AddStroke(sphere, "Latitude", p.wireframe, Latitude(latitude * Mathf.Deg2Rad), true));
            foreach (float longitude in new[] { 30f, 60f, 90f, 120f, 150f })
                wireframe.Add(AddStroke(sphere, "Meridian", p.wireframe, Meridian(longitude * Mathf.Deg2Rad), true));

            // Axes grow both ways from the center, like SphereFrameView's (-end to end).
            axes.Clear();
            foreach (var (style, direction) in new[] { (p.axisX, BlochVector.UnitX), (p.axisY, BlochVector.UnitY), (p.axisZ, BlochVector.UnitZ) })
            {
                Vector3 end = Rig.LocalPoint(direction, p.axisExtent);
                axes.Add(AddStroke(sphere, "Axis", style, Segment(Vector3.zero, end), false));
                axes.Add(AddStroke(sphere, "Axis", style, Segment(Vector3.zero, -end), false));
            }

            float labelOut = p.axisExtent + 0.12f;
            labels.Clear();
            labels.Add(AddText(sphere, "|0⟩", p.axisZ.color, Rig.LocalPoint(BlochVector.UnitZ, labelOut), p.sphereLabelSize, billboard: true));
            labels.Add(AddText(sphere, "|1⟩", p.axisZ.color, Rig.LocalPoint(BlochVector.UnitZ * -1, labelOut), p.sphereLabelSize, billboard: true));
            labels.Add(AddText(sphere, "x", p.axisX.color, Rig.LocalPoint(BlochVector.UnitX, labelOut), p.sphereLabelSize, billboard: true));
            labels.Add(AddText(sphere, "y", p.axisY.color, Rig.LocalPoint(BlochVector.UnitY, labelOut), p.sphereLabelSize, billboard: true));

            // Labels for the real sphere's state vector once IntroSequence points it at Example.
            vectorLabels = new Part();
            BlochVector v = Example.Bloch;
            psiLabel = AddText(vectorLabels, "|ψ⟩", p.vectorGlow, Rig.LocalPoint(v, 1.18f), p.sphereLabelSize * 1.3f, billboard: true);
            Vector3 side = Vector3.Cross(Rig.LocalPoint(v), Vector3.forward).normalized * 0.03f;
            lengthLabel = AddText(vectorLabels, "1", p.panelText, Rig.LocalPoint(v, 0.5f) + side, p.sphereLabelSize, billboard: true);
            movedLabel = AddText(vectorLabels, "|ψ′⟩", p.vectorGlow, Rig.LocalPoint(Moved.Bloch, 1.18f), p.sphereLabelSize * 1.3f, billboard: true);
        }

        // The drawing is done and the real sphere takes over: the strokes go, the vector labels stay usable.
        public void HideSphere() => SetOpacity(sphere, 0);

        public IEnumerator WriteVectorLabels()
        {
            SetOpacity(vectorLabels, 1);
            yield return Together(Write(psiLabel, 1.0f), Delayed(0.6f, Write(lengthLabel, 0.8f)));
        }

        public IEnumerator WriteMovedLabel()
        {
            yield return Write(movedLabel, 1.0f);
        }

        public IEnumerator FadeVectorLabels(float seconds) => Fade(vectorLabels, 1, 0, seconds);

        // About 11 s, each stroke flowing into the next.
        public IEnumerator Draw()
        {
            SetOpacity(sphere, 1);
            yield return Together(Grow(2.2f, equator), Delayed(1.5f, Grow(2.2f, primeMeridian)));
            yield return Stagger(1.6f, 0.25f, wireframe.ToArray());
            yield return Grow(1.8f, axes.ToArray());
            var writes = new IEnumerator[labels.Count];
            for (int i = 0; i < labels.Count; i++) writes[i] = Delayed(i * 0.3f, Write(labels[i], 0.7f));
            yield return Together(writes);
            yield return Wait(0.5f);
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
