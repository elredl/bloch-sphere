using Study.Quantum;
using UnityEngine;

namespace Study.Visuals
{
    // The state vector: white-hot core, magenta glow, tip marker on the surface.
    // Built once along +Y with length exactly the sphere radius, then only
    // rotated. Length, thickness and brightness never change with the state,
    // because "vector length = probability" is a misconception we target.
    //
    // A second instance with followsState off is the measurement demo's
    // "ghost": a dim, constant marker of the prepared state.
    public class StateVectorView : SphereView
    {
        const int TubeSides = 10;

        [Tooltip("Off for the ghost vector, which is pointed explicitly with ShowDirection.")]
        [SerializeField] bool followsState = true;
        [Tooltip("The measurement demo's dim marker of the prepared state (palette ghost intensity).")]
        [SerializeField] bool isGhost;

        public override bool IsDimmable => false;

        QubitState state;
        Transform pivot;
        Qubit shown = Qubit.Zero;
        bool visible = true;
        float length = 1;
        Transform[] tips;

        protected override void OnEnable()
        {
            if (isGhost) visible = false; // shown only while a measurement demo runs
            if (followsState)
            {
                state = GetComponentInParent<QubitState>();
                if (state != null) state.Changed += ShowDirection;
            }
            base.OnEnable();
        }

        protected override void OnDisable()
        {
            if (state != null) state.Changed -= ShowDirection;
            base.OnDisable();
        }

        protected override void Build()
        {
            StylePalette p = Palette;
            pivot = Track(new GameObject("Vector Pivot") { hideFlags = HideFlags.DontSave }).transform;
            pivot.SetParent(transform, false);

            float scale = isGhost ? p.ghostVectorIntensity : 1f;
            var glow = new StylePalette.Line(p.vectorGlow, p.vectorGlowIntensity * scale, p.vectorCoreWidthMm, true);
            Vector3 tip = Vector3.up * Radius;
            float coreRadius = p.vectorCoreWidthMm * 0.0005f;

            var core = new TubeMeshBuilder();
            core.AddSegment(Vector3.zero, tip, coreRadius, TubeSides);
            AddMesh("Vector Core", core.ToMesh(), StyledMaterials.Neon(p, p.vectorCore, p.vectorGlowIntensity * scale, 0f, 1f), pivot);

            var halo = new TubeMeshBuilder();
            halo.AddSegment(Vector3.zero, tip, coreRadius * p.haloWidthMultiplier, TubeSides);
            AddMesh("Vector Glow", halo.ToMesh(), StyledMaterials.LineHalo(p, glow), pivot);

            float tipRadius = p.vectorTipDiameterMm * 0.0005f;
            Transform tipCore = AddMesh("Tip Core", TubeMeshBuilder.Sphere(tipRadius * 0.5f, 16, 8),
                StyledMaterials.Neon(p, p.vectorCore, p.vectorGlowIntensity * scale, 0f, 1f), pivot).transform;
            Transform tipGlow = AddMesh("Tip Glow", TubeMeshBuilder.Sphere(tipRadius, 16, 8),
                StyledMaterials.Neon(p, p.vectorGlow, p.vectorGlowIntensity * scale, 1.5f, 0f), pivot).transform;
            tipCore.localPosition = tip;
            tipGlow.localPosition = tip;
            tips = new[] { tipCore, tipGlow };
            SetLength(length);

            ShowDirection(state != null ? state.Current : shown);
            pivot.gameObject.SetActive(visible);
        }

        public void ShowDirection(Qubit q)
        {
            shown = q;
            if (pivot == null) return;
            pivot.localRotation = Quaternion.FromToRotation(Vector3.up, BlochSpace.ToLocal(q.Bloch));
        }

        public void SetVisible(bool value)
        {
            visible = value;
            if (pivot != null) pivot.gameObject.SetActive(value);
        }

        // The intro only: draws the vector out from the center (0 = none, 1 = the full
        // radius). The lesson never changes it, so its length always stays the radius.
        public void SetLength(float fraction)
        {
            length = Mathf.Clamp01(fraction);
            if (pivot == null) return;
            float f = Mathf.Max(length, 1e-3f);
            pivot.localScale = new Vector3(1, f, 1);
            foreach (Transform t in tips) t.localScale = new Vector3(1, 1 / f, 1); // the tip stays round
        }

        // 0 = invisible, 1 = full. Used for re-preparation and measurement fades.
        // Changes brightness only; length and width stay fixed.
        public void SetOpacity(float opacity) => SetDim(Mathf.Clamp01(opacity));
    }
}
