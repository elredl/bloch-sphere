using System.Collections.Generic;
using Study.Quantum;
using TMPro;
using UnityEngine;

namespace Study.Visuals
{
    // The gate's icon on the vector: a small amber frame with the gate's letter just
    // past the tip, shown while a named gate (GateArcAnimator.HasIcon) is applied.
    // It appears on the still vector during the animator's lead time, rides the tip
    // through the turn, and clears shortly after. Same in every condition.
    public class GateBadgeView : SphereView
    {
        const int TubeSides = 6;
        const float HalfSizeMetres = 0.026f;
        const float BeyondTipMetres = 0.06f;
        const float LingerSeconds = 0.5f;

        public override bool IsDimmable => false;

        GateArcAnimator animator;
        QubitState state;
        Transform root;
        TextMeshPro letter;
        Gate shown;
        float hideAt = -1;

        protected override void OnEnable()
        {
            animator = GetComponentInParent<GateArcAnimator>();
            state = GetComponentInParent<QubitState>();
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
            root = Track(new GameObject("Gate Badge") { hideFlags = HideFlags.DontSave }).transform;
            root.SetParent(transform, false);

            var frame = Track(new GameObject("Badge Frame") { hideFlags = HideFlags.DontSave }).transform;
            frame.SetParent(root, false);
            var square = new List<Vector3>
            {
                new Vector3(-HalfSizeMetres, -HalfSizeMetres, 0), new Vector3(HalfSizeMetres, -HalfSizeMetres, 0),
                new Vector3(HalfSizeMetres, HalfSizeMetres, 0), new Vector3(-HalfSizeMetres, HalfSizeMetres, 0),
            };
            AddLine("Badge Frame", p.gateAxis, (b, r) => b.AddPolyline(square, r, TubeSides, true), frame);
            foreach (Transform child in frame) child.gameObject.AddComponent<Billboard>();

            letter = AddLabel("", p.gateAxis.color, Vector3.zero, root);
            letter.font = p.titleFont;
            letter.fontSize *= 1.6f;

            if (shown != null) Show(shown);
            else root.gameObject.SetActive(false);
        }

        public void Show(Gate gate)
        {
            if (!GateArcAnimator.HasIcon(gate)) return;
            shown = gate;
            hideAt = -1;
            if (root == null) return;
            letter.text = gate.Name;
            FollowTip();
            root.gameObject.SetActive(true);
        }

        void OnGateFinished(Gate gate)
        {
            if (shown != null) hideAt = Time.time + LingerSeconds;
        }

        void LateUpdate()
        {
            if (shown == null || root == null) return;
            if (hideAt >= 0 && Time.time >= hideAt)
            {
                shown = null;
                root.gameObject.SetActive(false);
                return;
            }
            FollowTip();
        }

        // Just past the tip, along the vector, so it reads as stamped onto it.
        void FollowTip()
        {
            if (state == null) return;
            Vector3 tip = Rig.LocalPoint(state.Current.Bloch);
            root.localPosition = tip.normalized * (tip.magnitude + BeyondTipMetres / transform.lossyScale.x);
        }
    }
}
