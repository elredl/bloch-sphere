using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Study.Visuals
{
    // The narration as text, lower left of the sphere (below the prompt panel). Built by the
    // editor scene setup; NarrationCaptions decides what it shows. Not interactive.
    [ExecuteAlways]
    public class CaptionPanelView : MonoBehaviour
    {
        [SerializeField] StylePalette palette;
        [SerializeField] CanvasGroup group;
        [SerializeField] Image background;
        [SerializeField] Image[] accents;
        [SerializeField] TextMeshProUGUI body;

        void OnEnable()
        {
            StylePalette.Edited += OnPaletteEdited;
            ApplyStyle();
            if (Application.isPlaying) Hide();
        }

        void OnDisable() => StylePalette.Edited -= OnPaletteEdited;

        void OnPaletteEdited(StylePalette edited)
        {
            if (edited == palette) ApplyStyle();
        }

        public bool IsVisible => group.alpha > 0;
        public string Text => body.text;

        public void Show(string text)
        {
            body.text = text;
            group.alpha = 1;
        }

        public void Hide()
        {
            if (group == null) return; // scene teardown
            group.alpha = 0;
        }

        public void ApplyStyle()
        {
            if (palette == null || background == null) return;
            background.color = palette.panelBackground;
            foreach (Image accent in accents) accent.color = palette.panelAccent;
            body.font = palette.bodyFont;
            body.color = palette.panelText;
        }
    }
}
