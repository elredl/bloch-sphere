using UnityEngine;

namespace Study.Visuals
{
    // The surroundings: near-black background on every camera, no skybox, no
    // ambient light, and a dim grid floor at the real floor (y = floorHeight in
    // the tracking space) that fades out with distance from the sphere.
    // Completely static: nothing here moves.
    [ExecuteAlways]
    public class EnvironmentStyler : MonoBehaviour
    {
        [SerializeField] StylePalette palette;
        [Tooltip("World height of the physical floor. 0 with Quest floor-level tracking.")]
        [SerializeField] float floorHeight = 0f;
        [Tooltip("The grid fades out around the point on the floor below this transform (the sphere).")]
        [SerializeField] Transform fadeCenter;
        [SerializeField] float floorSize = 16f;

        GameObject floor;
        Material floorMaterial;

        void OnEnable()
        {
            StylePalette.Edited += OnPaletteEdited;
            Build();
        }

        void OnDisable()
        {
            StylePalette.Edited -= OnPaletteEdited;
            StyledMaterials.Destroy(floor);
            StyledMaterials.Destroy(floorMaterial);
        }

        void OnPaletteEdited(StylePalette edited)
        {
            if (edited == palette) Apply();
        }

        void Build()
        {
            if (palette == null || palette.gridFloorShader == null) return;
            floorMaterial = new Material(palette.gridFloorShader) { hideFlags = HideFlags.DontSave };
            Mesh quad = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            floor = StyledMaterials.CreateChild(transform, "Grid Floor", quad, floorMaterial);
            floor.transform.position = new Vector3(0, floorHeight, 0);
            floor.transform.rotation = Quaternion.Euler(90, 0, 0);
            floor.transform.localScale = Vector3.one * floorSize;
            Apply();
        }

        public void Apply()
        {
            if (palette == null) return;

            // A camera added later (e.g. the XR rig) is styled when it calls Apply again.
            foreach (Camera cam in FindObjectsByType<Camera>(FindObjectsInactive.Include))
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = palette.background;
            }
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = Color.black;
            RenderSettings.fog = false;

            if (floorMaterial == null) return;
            floorMaterial.SetColor("_Color", palette.floorGridColor);
            floorMaterial.SetFloat("_Intensity", palette.floorGridIntensity);
            floorMaterial.SetFloat("_CellSize", palette.floorGridCellSize);
            floorMaterial.SetFloat("_LineWidth", palette.floorGridLineMm * 0.001f);
            floorMaterial.SetFloat("_FadeStart", palette.floorGridFade.x);
            floorMaterial.SetFloat("_FadeEnd", palette.floorGridFade.y);
            Vector3 center = fadeCenter != null ? fadeCenter.position : Vector3.zero;
            floorMaterial.SetVector("_FadeCenter", new Vector4(center.x, floorHeight, center.z, 0));
        }
    }
}
