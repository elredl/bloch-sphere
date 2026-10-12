using System;
using TMPro;
using UnityEngine;

namespace Study.Visuals
{
    // Every color, glow intensity, line width and font in the study lives here.
    // No other script hard-codes a color. Edit the StylePalette asset in the
    // Inspector; in the Editor the scene updates live while you tune.
    //
    // Color carries meaning: an element always takes the color of the axis or
    // quantity it belongs to (theta arc = z cyan, phi arc = x orange), and is
    // always paired with a label or shape so color is never the only cue.
    [CreateAssetMenu(menuName = "Study/Style Palette", fileName = "StylePalette")]
    public class StylePalette : ScriptableObject
    {
        [Serializable]
        public struct Line
        {
            [ColorUsage(false, true)] public Color color;
            [Tooltip("Brightness multiplier. Values above 1 push the core toward white.")]
            public float intensity;
            [Tooltip("Tube diameter in millimetres.")]
            public float widthMm;
            [Tooltip("Add a soft glow tube around the line.")]
            public bool halo;

            public Line(Color color, float intensity, float widthMm, bool halo)
            {
                this.color = color;
                this.intensity = intensity;
                this.widthMm = widthMm;
                this.halo = halo;
            }
        }

        [Header("Environment")]
        public Color background = Hex("#05060A");
        public Color floorGridColor = Hex("#00F0FF");
        [Range(0, 1)] public float floorGridIntensity = 0.07f;
        [Tooltip("Grid cell size in metres.")] public float floorGridCellSize = 0.5f;
        [Tooltip("Grid line width in millimetres.")] public float floorGridLineMm = 4f;
        [Tooltip("Grid fades out between these distances (m) from the point under the sphere.")]
        public Vector2 floorGridFade = new Vector2(1f, 4f);

        [Header("Sphere")]
        public Line wireframe = new Line(Hex("#00F0FF"), 0.22f, 1.4f, false);
        public Line referenceRings = new Line(Hex("#00F0FF"), 0.7f, 2.4f, true);
        public Color shellColor = Hex("#00F0FF");
        [Range(0, 1)] public float shellAlpha = 0.006f;
        [Range(0, 1)] public float shellRimAlpha = 0.06f;

        [Header("Axes")]
        public Line axisX = new Line(Hex("#FF9E1B"), 0.9f, 3f, true);
        public Line axisY = new Line(Hex("#A970FF"), 0.9f, 3f, true);
        public Line axisZ = new Line(Hex("#00F0FF"), 1.3f, 3.6f, true);
        [Tooltip("How far the axes reach past the sphere surface, as a multiple of the radius.")]
        public float axisExtent = 1.18f;

        [Header("State vector")]
        [ColorUsage(false, true)] public Color vectorCore = Color.white;
        [ColorUsage(false, true)] public Color vectorGlow = Hex("#FF2E88");
        public float vectorGlowIntensity = 1.4f;
        public float vectorCoreWidthMm = 5f;
        public float vectorTipDiameterMm = 18f;

        [Header("Gates")]
        public Line gateAxis = new Line(Hex("#FFE14D"), 1.1f, 3f, true);
        [Tooltip("Dash and gap length of the gate axis, in millimetres.")]
        public Vector2 gateAxisDashMm = new Vector2(18f, 10f);
        public Line gateTrail = new Line(Hex("#FFE14D"), 1f, 3.5f, true);
        [Tooltip("Seconds the arc trail takes to fade out after a gate finishes.")]
        public float trailFadeSeconds = 2.5f;

        [Header("Angles")]
        public Line thetaArc = new Line(Hex("#00F0FF"), 1f, 2.4f, true);
        public Line phiArc = new Line(Hex("#FF9E1B"), 1f, 2.4f, true);
        [Tooltip("Radius of the theta and phi arcs as a fraction of the sphere radius.")]
        public float thetaArcRadius = 0.32f;
        public float phiArcRadius = 0.42f;

        [Header("Measurement")]
        public Color histogramZero = Hex("#00F0FF");
        public Color histogramOne = Hex("#FF2E88");

        [Header("Feedback (Condition 3)")]
        public Color feedbackCorrect = Hex("#00F0FF");
        public Color feedbackIncorrect = Hex("#FFE14D");

        [Header("Halo and dimming")]
        [Tooltip("Halo tube diameter as a multiple of the line it surrounds.")]
        public float haloWidthMultiplier = 4f;
        [Range(0, 1)] public float haloIntensity = 0.35f;
        [Tooltip("Condition 3 pause: sphere emission drops to this fraction. Vector and gate axis stay full.")]
        [Range(0, 1)] public float pauseDimFactor = 0.3f;
        [Tooltip("Seconds for the dim fade (slow, never a flash).")]
        public float dimFadeSeconds = 0.6f;

        [Header("Text")]
        public TMP_FontAsset bodyFont;
        [Tooltip("Display font. Short titles only; hard to read in sentences.")]
        public TMP_FontAsset titleFont;
        public Color panelBackground = new Color(0.03f, 0.04f, 0.07f, 0.88f);
        public Color panelText = Hex("#E8ECF2");
        public Color panelAccent = Hex("#00F0FF");
        public Color buttonBackground = new Color(0.07f, 0.09f, 0.14f, 0.95f);
        [Tooltip("Height of the axis and pole labels on the sphere, in metres.")]
        public float sphereLabelSize = 0.028f;

        [Header("Shaders (referenced here so builds include them)")]
        public Shader neonLineShader;
        public Shader shellShader;
        public Shader gridFloorShader;

        // Raised when the asset is edited in the Inspector, so views can restyle live.
        public static event Action<StylePalette> Edited;

        void OnValidate() => Edited?.Invoke(this);

        public static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out Color c) ? c : Color.magenta;
    }
}
