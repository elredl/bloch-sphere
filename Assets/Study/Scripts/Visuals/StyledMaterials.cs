using UnityEngine;

namespace Study.Visuals
{
    // Creates the runtime materials for palette styles, and the generated child
    // objects that carry generated meshes. Generated objects are DontSave: the
    // scene stores only the components, and geometry is rebuilt from the
    // palette on enable, so nothing stale can be saved into a scene.
    public static class StyledMaterials
    {
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        static readonly int CoreWhiteId = Shader.PropertyToID("_CoreWhite");
        static readonly int FalloffId = Shader.PropertyToID("_Falloff");
        static readonly int DashLengthId = Shader.PropertyToID("_DashLength");
        static readonly int GapLengthId = Shader.PropertyToID("_GapLength");
        public static readonly int DimId = Shader.PropertyToID("_Dim");

        // Solid core of a line.
        public static Material LineCore(StylePalette palette, StylePalette.Line line, float coreWhite = 0.35f) =>
            Neon(palette, line.color, line.intensity, 0f, coreWhite);

        // Soft halo tube around a line: bright at its center, fading at its silhouette.
        public static Material LineHalo(StylePalette palette, StylePalette.Line line) =>
            Neon(palette, line.color, line.intensity * palette.haloIntensity, 2.5f, 0f);

        public static Material Neon(StylePalette palette, Color color, float intensity, float falloff, float coreWhite)
        {
            var material = new Material(palette.neonLineShader) { hideFlags = HideFlags.DontSave };
            material.SetColor(ColorId, color);
            material.SetFloat(IntensityId, intensity);
            material.SetFloat(FalloffId, falloff);
            material.SetFloat(CoreWhiteId, coreWhite);
            material.SetFloat(DimId, 1f);
            return material;
        }

        public static void SetDash(Material material, float dashMetres, float gapMetres)
        {
            material.SetFloat(DashLengthId, dashMetres);
            material.SetFloat(GapLengthId, gapMetres);
        }

        public static GameObject CreateChild(Transform parent, string name, Mesh mesh, Material material)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return go;
        }

        // Destroys a generated object or asset in both Edit and Play Mode.
        public static void Destroy(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Object.Destroy(o);
            else Object.DestroyImmediate(o);
        }
    }
}
