using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Study.Visuals
{
    // Applies the palette to a world-space UI panel. Neon goes on the frame
    // and accents only; body text is plain off-white on a dark translucent
    // panel, with no glow on the letters, so it stays crisp to read.
    [ExecuteAlways]
    public class StyledPanel : MonoBehaviour
    {
        [SerializeField] StylePalette palette;
        [SerializeField] Image background;
        [SerializeField] Image[] accents;
        [SerializeField] TextMeshProUGUI title;
        [SerializeField] TextMeshProUGUI[] bodyTexts;
        [SerializeField] Image[] buttonBackgrounds;
        [SerializeField] Image[] buttonFrames;

        void OnEnable()
        {
            StylePalette.Edited += OnPaletteEdited;
            Apply();
        }

        void OnDisable() => StylePalette.Edited -= OnPaletteEdited;

        void OnPaletteEdited(StylePalette edited)
        {
            if (edited == palette) Apply();
        }

        public void Apply()
        {
            if (palette == null) return;
            if (background != null) background.color = palette.panelBackground;
            foreach (Image accent in accents) accent.color = palette.panelAccent;
            foreach (Image button in buttonBackgrounds) button.color = palette.buttonBackground;
            foreach (Image frame in buttonFrames) frame.color = palette.panelAccent * new Color(1, 1, 1, 0.6f);
            if (title != null)
            {
                title.font = palette.titleFont;
                title.color = palette.panelAccent;
            }
            foreach (TextMeshProUGUI text in bodyTexts)
            {
                text.font = palette.bodyFont;
                text.color = palette.panelText;
            }
        }
    }
}
