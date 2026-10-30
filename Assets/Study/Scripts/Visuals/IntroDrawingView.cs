using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Study.Visuals
{
    // Base for the intro's drawings (IntroBoardView, IntroSphereView): neon strokes that
    // draw themselves in, text that writes itself in, and groups ("parts") that fade out
    // together. Strokes use the sphere's line styles, so a drawing looks like the sphere.
    // Everything runs on scaled time, so a break (time scale 0) freezes a drawing.
    public abstract class IntroDrawingView : SphereView
    {
        protected const int TubeSides = 8;

        public override bool IsDimmable => false;

        protected class Stroke
        {
            public List<Vector3> Points;
            public bool Closed;
            public float Radius, HaloRadius;
            public Mesh Core, Halo;
            public float Fraction;
        }

        protected class Part
        {
            public readonly List<Stroke> Strokes = new List<Stroke>();
            public readonly List<Material> Materials = new List<Material>();
            public readonly List<TextMeshPro> Texts = new List<TextMeshPro>();
            public readonly List<Color> TextColors = new List<Color>();
        }

        readonly TubeMeshBuilder builder = new TubeMeshBuilder();

        // A stroke that starts invisible. Draw it in with Grow or SetFraction.
        protected Stroke AddStroke(Part part, string name, StylePalette.Line style, List<Vector3> points, bool closed, Transform parent = null)
        {
            var stroke = new Stroke
            {
                Points = points,
                Closed = closed,
                Radius = style.widthMm * 0.0005f,
                HaloRadius = style.widthMm * 0.0005f * Palette.haloWidthMultiplier,
                Core = Track(new Mesh { name = name }),
            };
            Material core = Track(StyledMaterials.LineCore(Palette, style));
            Track(StyledMaterials.CreateChild(parent != null ? parent : transform, name, stroke.Core, core));
            part.Materials.Add(core);
            if (style.halo)
            {
                stroke.Halo = Track(new Mesh { name = name + " Halo" });
                Material halo = Track(StyledMaterials.LineHalo(Palette, style));
                Track(StyledMaterials.CreateChild(parent != null ? parent : transform, name + " Halo", stroke.Halo, halo));
                part.Materials.Add(halo);
            }
            part.Strokes.Add(stroke);
            return stroke;
        }

        protected static List<Vector3> Segment(Vector3 from, Vector3 to) => new List<Vector3> { from, to };

        // Draws the first fraction of the stroke's length.
        protected void SetFraction(Stroke stroke, float fraction)
        {
            stroke.Fraction = Mathf.Clamp01(fraction);
            List<Vector3> partial = Partial(stroke.Points, stroke.Closed, stroke.Fraction);
            Fill(stroke.Core, partial, stroke.Radius);
            if (stroke.Halo != null) Fill(stroke.Halo, partial, stroke.HaloRadius);
        }

        void Fill(Mesh mesh, List<Vector3> points, float radius)
        {
            builder.Clear();
            if (points.Count >= 2) builder.AddPolyline(points, radius, TubeSides, false);
            builder.ToMesh(mesh);
        }

        static List<Vector3> Partial(List<Vector3> points, bool closed, float fraction)
        {
            var path = new List<Vector3>(points);
            if (closed) path.Add(points[0]);
            float total = 0;
            for (int i = 1; i < path.Count; i++) total += Vector3.Distance(path[i - 1], path[i]);
            float left = total * fraction;
            var result = new List<Vector3> { path[0] };
            for (int i = 1; i < path.Count && left > 0; i++)
            {
                float d = Vector3.Distance(path[i - 1], path[i]);
                result.Add(d <= left ? path[i] : Vector3.Lerp(path[i - 1], path[i], left / d));
                left -= d;
            }
            return result;
        }

        // Starts slow, ends slow, with no jolt at either end (smootherstep).
        protected static float Ease(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * t * (t * (6 * t - 15) + 10);
        }

        // Grows strokes together from 0 to 1 over the given seconds.
        protected IEnumerator Grow(float seconds, params Stroke[] strokes)
        {
            for (float t = 0; t < seconds; t += Time.deltaTime)
            {
                float f = Ease(t / seconds);
                foreach (Stroke s in strokes) SetFraction(s, f);
                yield return null;
            }
            foreach (Stroke s in strokes) SetFraction(s, 1);
        }

        // Grows strokes one after another, each starting gap seconds after the last, so they
        // flow into each other instead of appearing at once.
        protected IEnumerator Stagger(float each, float gap, params Stroke[] strokes)
        {
            var routines = new IEnumerator[strokes.Length];
            for (int i = 0; i < strokes.Length; i++) routines[i] = Delayed(i * gap, Grow(each, strokes[i]));
            return Together(routines);
        }

        // Runs the steps at the same time and finishes when the last one does.
        protected IEnumerator Together(params IEnumerator[] steps)
        {
            int running = steps.Length;
            foreach (IEnumerator step in steps) StartCoroutine(Count(step, () => running--));
            while (running > 0) yield return null;
        }

        protected static IEnumerator Delayed(float seconds, IEnumerator step)
        {
            yield return Wait(seconds);
            yield return step;
        }

        static IEnumerator Count(IEnumerator step, System.Action done)
        {
            yield return step;
            done();
        }

        // World-space text in the drawing's plane, facing the viewer; starts unwritten.
        protected TextMeshPro AddText(Part part, string text, Color color, Vector3 localPosition, float heightMetres,
            TextAlignmentOptions alignment = TextAlignmentOptions.Center, Transform parent = null, bool billboard = false)
        {
            var go = Track(new GameObject("Text " + text) { hideFlags = HideFlags.DontSave });
            go.transform.SetParent(parent != null ? parent : transform, false);
            go.transform.localPosition = localPosition;
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.font = Palette.bodyFont;
            tmp.richText = true;
            tmp.text = text;
            tmp.fontSize = heightMetres * 10f; // TMP: font size 10 is about 1 m tall at scale 1
            tmp.alignment = alignment;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.rectTransform.sizeDelta = new Vector2(1f, heightMetres * 1.6f);
            tmp.color = color;
            tmp.maxVisibleCharacters = 0;
            if (billboard) go.AddComponent<Billboard>();
            part.Texts.Add(tmp);
            part.TextColors.Add(color);
            return tmp;
        }

        // Writes the text in from left to right: each character fades in over CharacterFade
        // seconds, starting a little after the one before it, so the text flows in.
        protected static IEnumerator Write(TextMeshPro text, float seconds)
        {
            const float CharacterFade = 0.4f;
            text.maxVisibleCharacters = int.MaxValue;
            text.ForceMeshUpdate();
            TMP_TextInfo info = text.textInfo;
            int count = info.characterCount;
            float spacing = count > 1 ? Mathf.Max(0, seconds - CharacterFade) / (count - 1) : 0;
            var full = new byte[info.meshInfo.Length][];
            for (int m = 0; m < full.Length; m++)
            {
                Color32[] colors = info.meshInfo[m].colors32;
                full[m] = new byte[colors.Length];
                for (int v = 0; v < colors.Length; v++) full[m][v] = colors[v].a;
            }
            for (float t = 0; ; t += Time.deltaTime)
            {
                bool done = t >= seconds;
                for (int c = 0; c < count; c++)
                {
                    TMP_CharacterInfo ch = info.characterInfo[c];
                    if (!ch.isVisible) continue;
                    float a = done ? 1 : Ease((t - c * spacing) / CharacterFade);
                    Color32[] colors = info.meshInfo[ch.materialReferenceIndex].colors32;
                    byte[] alphas = full[ch.materialReferenceIndex];
                    for (int v = ch.vertexIndex; v < ch.vertexIndex + 4; v++) colors[v].a = (byte)(alphas[v] * a);
                }
                text.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
                if (done) yield break;
                yield return null;
            }
        }

        protected static IEnumerator Wait(float seconds)
        {
            for (float t = 0; t < seconds; t += Time.deltaTime) yield return null;
        }

        // Fades every stroke and text of the part out (or back in).
        protected IEnumerator Fade(Part part, float from, float to, float seconds)
        {
            for (float t = 0; t < seconds; t += Time.deltaTime)
            {
                SetOpacity(part, Mathf.Lerp(from, to, t / seconds));
                yield return null;
            }
            SetOpacity(part, to);
        }

        protected static void SetOpacity(Part part, float value)
        {
            foreach (Material m in part.Materials) m.SetFloat(StyledMaterials.DimId, value);
            for (int i = 0; i < part.Texts.Count; i++)
            {
                Color c = part.TextColors[i];
                part.Texts[i].color = new Color(c.r, c.g, c.b, c.a * value);
            }
        }

        protected static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);
    }
}
