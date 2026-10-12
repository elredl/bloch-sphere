using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Study.Visuals
{
    // Base for every piece of the sphere that is generated from the palette.
    // Handles the boring lifecycle in one place:
    //  - build on enable, tear everything down on disable (nothing generated is saved),
    //  - rebuild when the palette asset is edited (live tuning in the Editor),
    //  - dim support for the Condition 3 pause (only views that opt in).
    [ExecuteAlways]
    public abstract class SphereView : MonoBehaviour
    {
        protected BlochSphereRig Rig { get; private set; }
        protected StylePalette Palette => Rig != null ? Rig.Palette : null;
        protected float Radius => Rig.Radius;

        // Sphere grid, rings, shell, axes, labels and angle arcs dim; the vector, gate axis and trail don't.
        public abstract bool IsDimmable { get; }

        readonly List<Object> generated = new List<Object>();
        readonly List<Material> materials = new List<Material>();
        readonly List<(TextMeshPro label, Color color)> labels = new List<(TextMeshPro, Color)>();
        float dim = 1f;

        protected virtual void OnEnable()
        {
            Rig = GetComponentInParent<BlochSphereRig>();
            StylePalette.Edited += OnPaletteEdited;
            Rebuild();
        }

        protected virtual void OnDisable()
        {
            StylePalette.Edited -= OnPaletteEdited;
            Teardown();
        }

        void OnPaletteEdited(StylePalette edited)
        {
            if (edited != Palette) return;
#if UNITY_EDITOR
            // Unity forbids destroying objects inside OnValidate, so rebuild just after it.
            UnityEditor.EditorApplication.delayCall += () => { if (this != null && isActiveAndEnabled) Rebuild(); };
#endif
        }

        public void Rebuild()
        {
            Teardown();
            if (Palette == null || Palette.neonLineShader == null) return;
            Build();
            SetDim(dim);
        }

        protected abstract void Build();

        public void SetDim(float value)
        {
            dim = value;
            foreach (Material m in materials) m.SetFloat(StyledMaterials.DimId, value);
            foreach (var (label, color) in labels)
                label.color = new Color(color.r * value, color.g * value, color.b * value, color.a);
        }

        void Teardown()
        {
            for (int i = generated.Count - 1; i >= 0; i--) StyledMaterials.Destroy(generated[i]);
            generated.Clear();
            materials.Clear();
            labels.Clear();
        }

        // Registers something this view created so it is destroyed on teardown.
        protected T Track<T>(T o) where T : Object
        {
            generated.Add(o);
            if (o is Material m) materials.Add(m);
            return o;
        }

        protected GameObject AddMesh(string name, Mesh mesh, Material material, Transform parent = null)
        {
            return Track(StyledMaterials.CreateChild(parent != null ? parent : transform, name, Track(mesh), Track(material)));
        }

        // Adds a line (and its halo, if the style asks for one) built by fill at the given tube radius.
        protected void AddLine(string name, StylePalette.Line style, System.Action<TubeMeshBuilder, float> fill, Transform parent = null)
        {
            var builder = new TubeMeshBuilder();
            fill(builder, style.widthMm * 0.0005f);
            AddMesh(name, builder.ToMesh(), StyledMaterials.LineCore(Palette, style), parent);
            if (!style.halo) return;
            builder.Clear();
            fill(builder, style.widthMm * 0.0005f * Palette.haloWidthMultiplier);
            AddMesh(name + " Halo", builder.ToMesh(), StyledMaterials.LineHalo(Palette, style), parent);
        }

        // A camera-facing text label (axis names, pole states, angle symbols).
        protected TextMeshPro AddLabel(string text, Color color, Vector3 localPosition, Transform parent = null)
        {
            var go = Track(new GameObject("Label " + text) { hideFlags = HideFlags.DontSave });
            go.transform.SetParent(parent != null ? parent : transform, false);
            go.transform.localPosition = localPosition;
            var label = go.AddComponent<TextMeshPro>();
            label.font = Palette.bodyFont;
            label.text = text;
            label.fontSize = Palette.sphereLabelSize * 10f; // TMP: font size 10 is about 1 m tall at scale 1
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.rectTransform.sizeDelta = new Vector2(0.2f, 0.06f);
            label.color = color;
            go.AddComponent<Billboard>();
            labels.Add((label, color));
            return label;
        }
    }
}
