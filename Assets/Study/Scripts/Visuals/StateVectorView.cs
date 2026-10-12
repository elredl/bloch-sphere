using Study.Quantum;
using UnityEngine;

namespace Study.Visuals
{
    // The state vector: white-hot core, magenta glow, tip marker on the surface.
    // Built once along +Y with length exactly the sphere radius, then only
    // rotated. Length, thickness and brightness never change with the state,
    // because "vector length = probability" is a misconception we target.
    public class StateVectorView : SphereView
    {
        const int TubeSides = 10;

        public override bool IsDimmable => false;

        QubitState state;
        Transform pivot;

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
            pivot = Track(new GameObject("Vector Pivot") { hideFlags = HideFlags.DontSave }).transform;
            pivot.SetParent(transform, false);

            var glow = new StylePalette.Line(p.vectorGlow, p.vectorGlowIntensity, p.vectorCoreWidthMm, true);
            Vector3 tip = Vector3.up * Radius;
            float coreRadius = p.vectorCoreWidthMm * 0.0005f;

            var core = new TubeMeshBuilder();
            core.AddSegment(Vector3.zero, tip, coreRadius, TubeSides);
            AddMesh("Vector Core", core.ToMesh(), StyledMaterials.Neon(p, p.vectorCore, p.vectorGlowIntensity, 0f, 1f), pivot);

            var halo = new TubeMeshBuilder();
            halo.AddSegment(Vector3.zero, tip, coreRadius * p.haloWidthMultiplier, TubeSides);
            AddMesh("Vector Glow", halo.ToMesh(), StyledMaterials.LineHalo(p, glow), pivot);

            float tipRadius = p.vectorTipDiameterMm * 0.0005f;
            AddMesh("Tip Core", TubeMeshBuilder.Sphere(tipRadius * 0.5f, 16, 8),
                StyledMaterials.Neon(p, p.vectorCore, p.vectorGlowIntensity, 0f, 1f), pivot).transform.localPosition = tip;
            AddMesh("Tip Glow", TubeMeshBuilder.Sphere(tipRadius, 16, 8),
                StyledMaterials.Neon(p, p.vectorGlow, p.vectorGlowIntensity, 1.5f, 0f), pivot).transform.localPosition = tip;

            if (state != null) Show(state.Current);
        }

        void Show(Qubit q)
        {
            if (pivot == null) return;
            pivot.localRotation = Quaternion.FromToRotation(Vector3.up, BlochSpace.ToLocal(q.Bloch));
        }
    }
}
