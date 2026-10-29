using System;
using System.Collections.Generic;
using Study.Quantum;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Study.Visuals
{
    // The "where will the vector point?" answers (all conditions): six glowing, labeled
    // rings on the sphere at |0>, |1>, |+>, |->, |+i>, |-i>. Each ring takes the
    // color of its axis and its own ket label at full brightness (the sphere's
    // labels are dimmed in the pause). The learner points a controller ray at one and pulls the
    // trigger; each ring has an invisible world-space UI button for that, so it
    // works with the same XR UI input as the panel buttons.
    public class PositionMarkersView : SphereView
    {
        const int TubeSides = 8;
        const int RingSegments = 40;
        const float RingRadiusMetres = 0.016f;
        const float HitSizeMm = 56f;
        const float HoverScale = 1.4f;

        public override bool IsDimmable => false;

        public event Action<string> Picked;

        static readonly (string answer, string label, BlochVector direction)[] Positions =
        {
            ("0", "|0⟩", BlochVector.UnitZ),
            ("1", "|1⟩", BlochVector.UnitZ * -1),
            ("+", "|+⟩", BlochVector.UnitX),
            ("-", "|−⟩", BlochVector.UnitX * -1),
            ("+i", "|+i⟩", BlochVector.UnitY),
            ("-i", "|−i⟩", BlochVector.UnitY * -1),
        };

        readonly List<Transform> rings = new List<Transform>();
        readonly List<Transform> hitTargets = new List<Transform>();
        Transform root;
        bool visible;

        protected override void Build()
        {
            StylePalette p = Palette;
            rings.Clear();
            hitTargets.Clear();
            root = Track(new GameObject("Position Markers") { hideFlags = HideFlags.DontSave }).transform;
            root.SetParent(transform, false);

            foreach (var (answer, label, direction) in Positions)
            {
                StylePalette.Line style = Mathf.Abs((float)direction.Z) > 0.5f ? p.axisZ : Mathf.Abs((float)direction.X) > 0.5f ? p.axisX : p.axisY;
                Vector3 point = Rig.LocalPoint(direction);

                var ring = Track(new GameObject("Marker " + answer) { hideFlags = HideFlags.DontSave }).transform;
                ring.SetParent(root, false);
                ring.localPosition = point;
                AddLine("Ring " + answer, style, (b, r) => b.AddPolyline(Circle(), r, TubeSides, true), ring);
                foreach (Transform child in ring) child.gameObject.AddComponent<Billboard>();
                rings.Add(ring);

                // Above the ring; at the poles beside it, clear of the z axis and the pole labels.
                bool pole = Mathf.Abs((float)direction.Z) > 0.5f;
                Vector3 offset = pole ? Vector3.right * 0.05f : Vector3.up * 0.035f;
                AddLabel(label, style.color, point + transform.InverseTransformVector(offset), root);
                hitTargets.Add(AddHitTarget(answer, point, rings.Count - 1));
            }
            root.gameObject.SetActive(visible);
        }

        public void Show()
        {
            visible = true;
            if (root == null) return;
            // Face the hit targets toward the viewer so the ray meets them square on.
            Camera cam = Camera.main;
            if (cam != null)
                foreach (Transform hit in hitTargets)
                    hit.rotation = Quaternion.LookRotation(hit.position - cam.transform.position);
            foreach (Transform ring in rings) ring.localScale = Vector3.one;
            root.gameObject.SetActive(true);
        }

        public void Hide()
        {
            visible = false;
            if (root != null) root.gameObject.SetActive(false);
        }

        // Editor and dev runs: the same path as a trigger pull on the marker.
        public void Pick(string answer)
        {
            if (visible) Picked?.Invoke(answer);
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

        Transform AddHitTarget(string answer, Vector3 localPoint, int ringIndex)
        {
            var go = Track(new GameObject("Hit " + answer, typeof(RectTransform)) { hideFlags = HideFlags.DontSave });
            var rect = (RectTransform)go.transform;
            rect.SetParent(root, false);
            rect.localPosition = localPoint;
            rect.sizeDelta = new Vector2(HitSizeMm, HitSizeMm);
            rect.localScale = Vector3.one * 0.001f;
            go.AddComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            PointerSupport.Add(go); // controller ray, or the mouse in Condition 1

            var image = new GameObject("Area", typeof(RectTransform), typeof(Image));
            var imageRect = (RectTransform)image.transform;
            imageRect.SetParent(rect, false);
            imageRect.anchorMin = Vector2.zero;
            imageRect.anchorMax = Vector2.one;
            imageRect.offsetMin = imageRect.offsetMax = Vector2.zero;
            image.GetComponent<Image>().color = new Color(0, 0, 0, 0); // invisible; the ring is the visual
            var button = image.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => Pick(answer));
            var hover = image.AddComponent<HoverRelay>();
            hover.Changed = over => rings[ringIndex].localScale = Vector3.one * (over ? HoverScale : 1f);
            return rect;
        }

        // Forwards pointer enter/exit from a UI element (hover highlight on the ring).
        public class HoverRelay : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
        {
            public Action<bool> Changed;
            public void OnPointerEnter(PointerEventData eventData) => Changed?.Invoke(true);
            public void OnPointerExit(PointerEventData eventData) => Changed?.Invoke(false);
        }
    }
}
