using System.Collections.Generic;
using Study.Quantum;
using UnityEngine;

namespace Study.Visuals
{
    // Amber trail on the sphere surface tracing the path the vector tip took
    // during a gate, with a fading tail, then fading out after the gate ends.
    // It records only while GateArcAnimator plays, so measurement collapses
    // (which set the state directly) never leave a trail, by construction.
    public class ArcTrailView : SphereView
    {
        const int TubeSides = 8;
        const float TailAlpha = 0.12f;
        const float MinStepMetres = 0.001f;

        public override bool IsDimmable => false;

        QubitState state;
        GateArcAnimator animator;
        readonly List<Vector3> points = new List<Vector3>();
        readonly List<float> alphas = new List<float>();
        readonly TubeMeshBuilder builder = new TubeMeshBuilder();
        Mesh coreMesh, haloMesh;
        Material coreMaterial, haloMaterial;
        float fade = 1f;
        bool recording;

        protected override void OnEnable()
        {
            state = GetComponentInParent<QubitState>();
            animator = GetComponentInParent<GateArcAnimator>();
            if (state != null) state.Changed += OnStateChanged;
            if (animator != null)
            {
                animator.Started += OnGateStarted;
                animator.Finished += OnGateFinished;
            }
            base.OnEnable();
        }

        protected override void OnDisable()
        {
            if (state != null) state.Changed -= OnStateChanged;
            if (animator != null)
            {
                animator.Started -= OnGateStarted;
                animator.Finished -= OnGateFinished;
            }
            base.OnDisable();
        }

        protected override void Build()
        {
            StylePalette p = Palette;
            coreMesh = new Mesh { name = "Trail", hideFlags = HideFlags.DontSave };
            haloMesh = new Mesh { name = "Trail Halo", hideFlags = HideFlags.DontSave };
            coreMaterial = StyledMaterials.LineCore(p, p.gateTrail);
            haloMaterial = StyledMaterials.LineHalo(p, p.gateTrail);
            AddMesh("Trail", coreMesh, coreMaterial);
            AddMesh("Trail Halo", haloMesh, haloMaterial);
            RebuildMeshes();
        }

        void OnGateStarted(Gate gate)
        {
            points.Clear();
            fade = 1f;
            recording = true;
            AddPoint(state.Current);
        }

        void OnGateFinished(Gate gate)
        {
            AddPoint(state.Current);
            recording = false;
        }

        void OnStateChanged(Qubit q)
        {
            if (recording) AddPoint(q);
        }

        void AddPoint(Qubit q)
        {
            if (Rig == null) return;
            Vector3 point = Rig.LocalPoint(q.Bloch);
            if (points.Count > 0 && Vector3.Distance(points[points.Count - 1], point) < MinStepMetres) return;
            points.Add(point);
            RebuildMeshes();
        }

        void Update()
        {
            if (recording || points.Count == 0 || Palette == null) return;
            fade -= Time.deltaTime / Mathf.Max(Palette.trailFadeSeconds, 0.01f);
            if (fade <= 0)
            {
                points.Clear();
                RebuildMeshes();
                return;
            }
            coreMaterial.SetFloat(StyledMaterials.DimId, fade);
            haloMaterial.SetFloat(StyledMaterials.DimId, fade);
        }

        void RebuildMeshes()
        {
            if (coreMesh == null) return;
            if (recording || points.Count > 0)
            {
                coreMaterial.SetFloat(StyledMaterials.DimId, fade);
                haloMaterial.SetFloat(StyledMaterials.DimId, fade);
            }
            alphas.Clear();
            for (int i = 0; i < points.Count; i++)
                alphas.Add(Mathf.Lerp(TailAlpha, 1f, points.Count == 1 ? 1f : i / (points.Count - 1f)));

            float radius = Palette.gateTrail.widthMm * 0.0005f;
            builder.Clear();
            builder.AddPolyline(points, radius, TubeSides, false, alphas);
            builder.ToMesh(coreMesh);
            builder.Clear();
            builder.AddPolyline(points, radius * Palette.haloWidthMultiplier, TubeSides, false, alphas);
            builder.ToMesh(haloMesh);
        }
    }
}
