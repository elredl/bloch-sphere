using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Study.Visuals
{
    // Orientation: a row of rings above the practice cube; one is lit and the learner
    // selects it with the controller ray and trigger (or a mouse click in Condition 1),
    // the same way the lesson's position markers are picked. Each ring has an invisible
    // world-space UI button, like PositionMarkersView.
    public class PracticeTargetsView : SphereView
    {
        public const int Count = 5;
        const int TubeSides = 8;
        const int RingSegments = 40;
        const float RingRadiusMetres = 0.024f;
        const float SpacingMetres = 0.12f;
        const float HeightMetres = 0.2f; // above the rig center
        const float HitSizeMm = 64f;
        const float HoverScale = 1.3f;

        public override bool IsDimmable => false;

        public event Action<int> Picked;

        readonly List<GameObject> litRings = new List<GameObject>();
        readonly List<GameObject> dimRings = new List<GameObject>();
        readonly List<Transform> rings = new List<Transform>();
        readonly List<Transform> hitTargets = new List<Transform>();
        Transform root;
        int lit = -1;
        bool visible;

        public int Lit => lit;

        protected override void Build()
        {
            StylePalette p = Palette;
            litRings.Clear();
            dimRings.Clear();
            rings.Clear();
            hitTargets.Clear();
            root = Track(new GameObject("Practice Targets") { hideFlags = HideFlags.DontSave }).transform;
            root.SetParent(transform, false);
            var dim = new StylePalette.Line(p.panelAccent, 0.3f, 2f, false);

            for (int i = 0; i < Count; i++)
            {
                int index = i;
                var ring = Track(new GameObject("Target " + i) { hideFlags = HideFlags.DontSave }).transform;
                ring.SetParent(root, false);
                ring.localPosition = new Vector3((i - (Count - 1) / 2f) * SpacingMetres, HeightMetres, 0);
                var litGroup = Track(new GameObject("Lit") { hideFlags = HideFlags.DontSave });
                litGroup.transform.SetParent(ring, false);
                AddLine("Ring Lit", p.axisZ, (b, r) => b.AddPolyline(Circle(), r, TubeSides, true), litGroup.transform);
                var dimGroup = Track(new GameObject("Dim") { hideFlags = HideFlags.DontSave });
                dimGroup.transform.SetParent(ring, false);
                AddLine("Ring Dim", dim, (b, r) => b.AddPolyline(Circle(), r, TubeSides, true), dimGroup.transform);
                foreach (Transform child in litGroup.transform) child.gameObject.AddComponent<Billboard>();
                foreach (Transform child in dimGroup.transform) child.gameObject.AddComponent<Billboard>();
                litRings.Add(litGroup);
                dimRings.Add(dimGroup);
                rings.Add(ring);
                hitTargets.Add(AddHitTarget(ring, () => Pick(index), over => ring.localScale = Vector3.one * (over ? HoverScale : 1f)));
            }
            Apply();
        }

        // litIndex: which ring to select.
        public void Show(int litIndex)
        {
            lit = litIndex;
            visible = true;
            Apply();
        }

        public void Hide()
        {
            visible = false;
            Apply();
        }

        // Editor and dev runs: the same path as a trigger pull on a ring.
        public void Pick(int index)
        {
            if (visible) Picked?.Invoke(index);
        }

        void Apply()
        {
            if (root == null) return;
            root.gameObject.SetActive(visible);
            for (int i = 0; i < rings.Count; i++)
            {
                litRings[i].SetActive(i == lit);
                dimRings[i].SetActive(i != lit);
                rings[i].localScale = Vector3.one;
            }
            // Face the hit targets toward the viewer so the ray meets them square on.
            Camera cam = Camera.main;
            if (visible && cam != null)
                foreach (Transform hit in hitTargets)
                    hit.rotation = Quaternion.LookRotation(hit.position - cam.transform.position);
        }

        static List<Vector3> Circle()
        {
            var points = new List<Vector3>(RingSegments);
            for (int i = 0; i < RingSegments; i++)
            {
                float a = i * 2 * Mathf.PI / RingSegments;
                points.Add(new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * RingRadiusMetres);
            }
            return points;
        }

        Transform AddHitTarget(Transform ring, Action click, Action<bool> hover)
        {
            var go = Track(new GameObject("Hit", typeof(RectTransform)) { hideFlags = HideFlags.DontSave });
            var rect = (RectTransform)go.transform;
            rect.SetParent(ring, false);
            rect.sizeDelta = new Vector2(HitSizeMm, HitSizeMm);
            rect.localScale = Vector3.one * 0.001f;
            go.AddComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            PointerSupport.Add(go);

            var image = new GameObject("Area", typeof(RectTransform), typeof(Image));
            var imageRect = (RectTransform)image.transform;
            imageRect.SetParent(rect, false);
            imageRect.anchorMin = Vector2.zero;
            imageRect.anchorMax = Vector2.one;
            imageRect.offsetMin = imageRect.offsetMax = Vector2.zero;
            image.GetComponent<Image>().color = new Color(0, 0, 0, 0); // invisible; the ring is the visual
            var button = image.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => click());
            image.AddComponent<PositionMarkersView.HoverRelay>().Changed = hover;
            return rect;
        }
    }
}
