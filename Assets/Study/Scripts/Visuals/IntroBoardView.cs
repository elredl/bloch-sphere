using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Study.Visuals
{
    // The intro's "board" in front of the learner, in this object's x-y plane (facing
    // the viewer), centered where the sphere will be:
    //
    //   Formula   in four steps, each explained before the next: ℂ²; |ψ⟩ = (α, β) as a
    //             column with drawn brackets; α = a + b i and β = c + d i; i² = −1
    //             (i in amber throughout). Each step stays on the board.
    //   Plane     a grid, x and y axes, and the vector to (a, b) with its components:
    //             ℝ², two real numbers, drawn in
    //
    //   Unitary   U|ψ⟩ = |ψ′⟩ and its matrix form, left of the (real) sphere
    //
    // All are watched, not touched; IntroSequence runs them and fades them out.
    public class IntroBoardView : IntroDrawingView
    {
        const float GridHalf = 0.25f;  // metres
        const float GridStep = 0.05f;
        static readonly Vector2 Tip = new Vector2(0.15f, 0.10f); // the (a, b) vector

        Part formula, plane, unitary;
        TextMeshPro uEquation, equals;
        readonly List<TextMeshPro> matrixEntries = new List<TextMeshPro>();
        readonly List<TextMeshPro> columnEntries = new List<TextMeshPro>();
        readonly List<TextMeshPro> resultEntries = new List<TextMeshPro>();
        readonly List<Stroke> matrixBrackets = new List<Stroke>();
        readonly List<Stroke> columnBrackets = new List<Stroke>();
        readonly List<Stroke> resultBrackets = new List<Stroke>();
        TextMeshPro title, ket, alpha, beta, alphaDef, betaDef, iSquared;
        Stroke leftBracket, rightBracket;
        readonly List<Stroke> grid = new List<Stroke>();
        Stroke xAxis, yAxis, arrow, arrowHeadA, arrowHeadB, toX, toY;
        TextMeshPro xLabel, yLabel, aLabel, bLabel, tipLabel, planeTitle;

        protected override void Build()
        {
            StylePalette p = Palette;
            string i = $"<color={Hex(p.gateAxis.color)}>i</color>";

            formula = new Part();
            title = AddText(formula, "ℂ²", p.panelAccent, new Vector3(0, 0.22f, 0), 0.09f);
            ket = AddText(formula, "|ψ⟩ =", p.panelText, new Vector3(-0.20f, 0.025f, 0), 0.06f);
            leftBracket = AddStroke(formula, "Left Bracket", p.referenceRings, new List<Vector3>
                { new Vector3(-0.05f, 0.10f, 0), new Vector3(-0.08f, 0.10f, 0), new Vector3(-0.08f, -0.05f, 0), new Vector3(-0.05f, -0.05f, 0) }, false);
            rightBracket = AddStroke(formula, "Right Bracket", p.referenceRings, new List<Vector3>
                { new Vector3(0.05f, 0.10f, 0), new Vector3(0.08f, 0.10f, 0), new Vector3(0.08f, -0.05f, 0), new Vector3(0.05f, -0.05f, 0) }, false);
            alpha = AddText(formula, "α", p.panelText, new Vector3(0, 0.06f, 0), 0.055f);
            beta = AddText(formula, "β", p.panelText, new Vector3(0, -0.01f, 0), 0.055f);
            alphaDef = AddText(formula, $"α = a + b{i}", p.panelText, new Vector3(0, -0.14f, 0), 0.05f);
            betaDef = AddText(formula, $"β = c + d{i}", p.panelText, new Vector3(0, -0.22f, 0), 0.05f);
            iSquared = AddText(formula, $"{i}² = −1", p.gateAxis.color, new Vector3(0, -0.32f, 0), 0.05f);

            plane = new Part();
            grid.Clear();
            for (float v = -GridHalf; v <= GridHalf + 1e-4f; v += GridStep)
            {
                if (Mathf.Abs(v) < 1e-4f) continue; // the axes go there
                grid.Add(AddStroke(plane, "Grid V", p.wireframe, Segment(new Vector3(v, -GridHalf, 0), new Vector3(v, GridHalf, 0)), false));
                grid.Add(AddStroke(plane, "Grid H", p.wireframe, Segment(new Vector3(-GridHalf, v, 0), new Vector3(GridHalf, v, 0)), false));
            }
            xAxis = AddStroke(plane, "X Axis", p.referenceRings, Segment(new Vector3(-GridHalf - 0.02f, 0, 0), new Vector3(GridHalf + 0.02f, 0, 0)), false);
            yAxis = AddStroke(plane, "Y Axis", p.referenceRings, Segment(new Vector3(0, -GridHalf - 0.02f, 0), new Vector3(0, GridHalf + 0.02f, 0)), false);
            var vectorStyle = new StylePalette.Line(p.vectorGlow, p.vectorGlowIntensity, 3.5f, true);
            Vector3 tip = Tip;
            arrow = AddStroke(plane, "Vector", vectorStyle, Segment(Vector3.zero, tip), false);
            Vector3 back = -tip.normalized * 0.025f;
            Vector3 side = new Vector3(-tip.y, tip.x, 0).normalized * 0.014f;
            arrowHeadA = AddStroke(plane, "Head A", vectorStyle, Segment(tip, tip + back + side), false);
            arrowHeadB = AddStroke(plane, "Head B", vectorStyle, Segment(tip, tip + back - side), false);
            toX = AddStroke(plane, "To X", p.wireframe, Segment(tip, new Vector3(tip.x, 0, 0)), false);
            toY = AddStroke(plane, "To Y", p.wireframe, Segment(tip, new Vector3(0, tip.y, 0)), false);
            planeTitle = AddText(plane, "ℝ²", p.panelAccent, new Vector3(-0.20f, 0.30f, 0), 0.06f);
            xLabel = AddText(plane, "x", p.panelText, new Vector3(GridHalf + 0.05f, 0, 0), 0.035f);
            yLabel = AddText(plane, "y", p.panelText, new Vector3(0, GridHalf + 0.05f, 0), 0.035f);
            aLabel = AddText(plane, "a", p.panelText, new Vector3(tip.x, -0.03f, 0), 0.035f);
            bLabel = AddText(plane, "b", p.panelText, new Vector3(-0.03f, tip.y, 0), 0.035f);
            tipLabel = AddText(plane, "(a, b)", p.vectorGlow, tip + new Vector3(0.05f, 0.03f, 0), 0.035f);

            // Left of the sphere and nearer the viewer, clear of it and inside the desktop frame:
            // U|ψ⟩ = |ψ′⟩ over its matrix form, U (2 x 2) times (α, β) = (α′, β′).
            unitary = new Part();
            const float Z = -0.25f, RowA = 0.0f, RowB = -0.05f, Top = 0.025f, Bottom = -0.075f;
            uEquation = AddText(unitary, "U|ψ⟩ = |ψ′⟩", p.panelText, new Vector3(-0.41f, 0.10f, Z), 0.04f);
            matrixEntries.Clear(); columnEntries.Clear(); resultEntries.Clear();
            matrixBrackets.Clear(); columnBrackets.Clear(); resultBrackets.Clear();
            foreach (var (text, x, y) in new[] { ("u<sub>00</sub>", -0.505f, RowA), ("u<sub>01</sub>", -0.455f, RowA),
                                                 ("u<sub>10</sub>", -0.505f, RowB), ("u<sub>11</sub>", -0.455f, RowB) })
                matrixEntries.Add(AddText(unitary, text, p.panelText, new Vector3(x, y, Z), 0.026f));
            Brackets(unitary, matrixBrackets, -0.54f, -0.43f, Top, Bottom, Z);
            columnEntries.Add(AddText(unitary, "α", p.panelText, new Vector3(-0.38f, RowA, Z), 0.026f));
            columnEntries.Add(AddText(unitary, "β", p.panelText, new Vector3(-0.38f, RowB, Z), 0.026f));
            Brackets(unitary, columnBrackets, -0.40f, -0.36f, Top, Bottom, Z);
            equals = AddText(unitary, "=", p.panelText, new Vector3(-0.335f, (RowA + RowB) / 2, Z), 0.026f);
            resultEntries.Add(AddText(unitary, "α′", p.panelText, new Vector3(-0.29f, RowA, Z), 0.026f));
            resultEntries.Add(AddText(unitary, "β′", p.panelText, new Vector3(-0.29f, RowB, Z), 0.026f));
            Brackets(unitary, resultBrackets, -0.312f, -0.268f, Top, Bottom, Z);
        }

        // A pair of square brackets from left to right, top to bottom, in the plane z.
        void Brackets(Part part, List<Stroke> into, float left, float right, float top, float bottom, float z)
        {
            const float Serif = 0.008f;
            into.Add(AddStroke(part, "Bracket", Palette.referenceRings, new List<Vector3>
                { new Vector3(left + Serif, top, z), new Vector3(left, top, z), new Vector3(left, bottom, z), new Vector3(left + Serif, bottom, z) }, false));
            into.Add(AddStroke(part, "Bracket", Palette.referenceRings, new List<Vector3>
                { new Vector3(right - Serif, top, z), new Vector3(right, top, z), new Vector3(right, bottom, z), new Vector3(right - Serif, bottom, z) }, false));
        }

        // About 8 s: the short form, then the matrix form piece by piece.
        public IEnumerator DrawUnitary()
        {
            SetOpacity(unitary, 1);
            yield return Write(uEquation, 2.0f);
            yield return Wait(0.5f);
            yield return Together(Grow(1.4f, matrixBrackets.ToArray()),
                                  Delayed(0.6f, WriteAll(matrixEntries, 0.35f, 0.8f)));
            yield return Together(Grow(1.0f, columnBrackets.ToArray()),
                                  Delayed(0.4f, WriteAll(columnEntries, 0.3f, 0.7f)));
            yield return Write(equals, 0.6f);
            yield return Together(Grow(1.0f, resultBrackets.ToArray()),
                                  Delayed(0.4f, WriteAll(resultEntries, 0.3f, 0.7f)));
        }

        IEnumerator WriteAll(List<TextMeshPro> texts, float gap, float each)
        {
            var writes = new IEnumerator[texts.Count];
            for (int i = 0; i < texts.Count; i++) writes[i] = Delayed(i * gap, Write(texts[i], each));
            return Together(writes);
        }

        public IEnumerator FadeUnitary(float seconds) => Fade(unitary, 1, 0, seconds);

        // The formula comes in one idea at a time; IntroSequence explains each before the next.
        // Steps 1 to 4: about 2, 4, 4.5 and 2 s.
        public IEnumerator DrawSpace()
        {
            SetOpacity(formula, 1);
            yield return Write(title, 1.8f);
        }

        public IEnumerator DrawState()
        {
            yield return Write(ket, 1.4f);
            yield return Together(Grow(1.6f, leftBracket, rightBracket),
                                  Delayed(0.9f, Write(alpha, 0.9f)),
                                  Delayed(1.5f, Write(beta, 0.9f)));
        }

        public IEnumerator DrawComplex()
        {
            yield return Together(Write(alphaDef, 2.2f), Delayed(1.6f, Write(betaDef, 2.2f)));
        }

        public IEnumerator DrawImaginary()
        {
            yield return Write(iSquared, 1.8f);
        }

        // About 11 s: grid line by line, then the axes, then the vector and its parts.
        public IEnumerator DrawPlane()
        {
            SetOpacity(plane, 1);
            yield return Write(planeTitle, 1.0f);
            yield return Stagger(1.0f, 0.12f, grid.ToArray());
            yield return Together(Grow(1.6f, xAxis, yAxis),
                                  Delayed(1.2f, Write(xLabel, 0.6f)),
                                  Delayed(1.2f, Write(yLabel, 0.6f)));
            yield return Wait(0.3f);
            yield return Grow(2.0f, arrow);
            yield return Grow(0.5f, arrowHeadA, arrowHeadB);
            yield return Together(Grow(1.2f, toX, toY),
                                  Delayed(0.8f, Write(aLabel, 0.6f)),
                                  Delayed(0.8f, Write(bLabel, 0.6f)));
            yield return Write(tipLabel, 1.2f);
        }

        public IEnumerator FadeFormula(float seconds) => Fade(formula, 1, 0, seconds);

        public IEnumerator FadePlane(float seconds) => Fade(plane, 1, 0, seconds);
    }
}
