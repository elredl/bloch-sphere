using Study.Quantum;
using UnityEngine;

namespace Study.Visuals
{
    // Dashed amber line through the sphere along the active gate's rotation
    // axis. Shown only while a gate is being applied.
    public class GateAxisView : SphereView
    {
        const int TubeSides = 8;

        public override bool IsDimmable => false;

        GateArcAnimator animator;
        Transform pivot;
        Gate shown;

        protected override void OnEnable()
        {
            animator = GetComponentInParent<GateArcAnimator>();
            if (animator != null)
            {
                animator.Started += Show;
                animator.Finished += OnGateFinished;
            }
            base.OnEnable();
        }

        protected override void OnDisable()
        {
            if (animator != null)
            {
                animator.Started -= Show;
                animator.Finished -= OnGateFinished;
            }
            base.OnDisable();
        }

        protected override void Build()
        {
            StylePalette p = Palette;
            pivot = Track(new GameObject("Axis Pivot") { hideFlags = HideFlags.DontSave }).transform;
            pivot.SetParent(transform, false);

            Vector3 end = Vector3.up * (Radius * p.axisExtent);
            AddLine("Gate Axis", p.gateAxis, (b, r) => b.AddSegment(-end, end, r, TubeSides), pivot);
            // Dashes on both the core and its halo, so the halo doesn't fill in the gaps.
            foreach (Renderer renderer in pivot.GetComponentsInChildren<Renderer>())
                StyledMaterials.SetDash(renderer.sharedMaterial, p.gateAxisDashMm.x * 0.001f, p.gateAxisDashMm.y * 0.001f);

            if (shown != null) Show(shown);
            else pivot.gameObject.SetActive(false);
        }

        public void Show(Gate gate)
        {
            shown = gate;
            if (pivot == null) return;
            pivot.localRotation = Quaternion.FromToRotation(Vector3.up, BlochSpace.ToLocal(gate.Axis));
            pivot.gameObject.SetActive(true);
        }

        public void Hide()
        {
            shown = null;
            if (pivot != null) pivot.gameObject.SetActive(false);
        }

        void OnGateFinished(Gate gate) => Hide();
    }
}
