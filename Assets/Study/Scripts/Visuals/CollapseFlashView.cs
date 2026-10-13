using System;
using Study.Quantum;
using UnityEngine;

namespace Study.Visuals
{
    // The measurement collapse flash: one short glow burst at the pole the
    // vector snaps to. Localized to the sphere (never full-field) and only
    // ever triggered once per shot, at most 1 Hz, so it stays well under the
    // 3 Hz flashing limit. Rotation never uses it; that's what makes a
    // measurement look different from a turn.
    public class CollapseFlashView : SphereView
    {
        const float BurstRadius = 0.035f; // metres, at full size

        public override bool IsDimmable => false;

        public Func<double> Clock = () => Time.timeAsDouble;

        Transform burst;
        double startTime = double.NegativeInfinity;

        protected override void Build()
        {
            StylePalette p = Palette;
            Material material = StyledMaterials.Neon(p, p.collapseFlashColor, p.collapseFlashIntensity, 1.2f, 0.6f);
            burst = AddMesh("Collapse Burst", TubeMeshBuilder.Sphere(BurstRadius, 24, 12), material).transform;
            burst.gameObject.SetActive(false);
        }

        public void Pulse(BlochVector pole, double atTime)
        {
            if (burst == null) return;
            burst.localPosition = Rig.LocalPoint(pole);
            startTime = atTime;
            burst.gameObject.SetActive(true);
            Apply(0);
        }

        void Update()
        {
            if (burst == null || !burst.gameObject.activeSelf) return;
            double elapsed = Clock() - startTime;
            if (elapsed >= Palette.collapseFlashSeconds || elapsed < 0)
            {
                burst.gameObject.SetActive(false);
                return;
            }
            Apply((float)(elapsed / Palette.collapseFlashSeconds));
        }

        // progress 0..1: grows a little and fades out, a single pulse.
        void Apply(float progress)
        {
            burst.localScale = Vector3.one * Mathf.Lerp(0.6f, 1.3f, progress);
            SetDim(1f - progress * progress);
        }
    }
}
