using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Study.Visuals
{
    // The "Next" button to the right of the sphere, shown during every pause in all
    // conditions: pressing it ends the pause early. Built by the editor scene setup;
    // this only shows, hides and styles it. Controller ray in the headset, mouse on
    // the desktop (PointerSupport on the canvas).
    [ExecuteAlways]
    public class NextButtonView : MonoBehaviour
    {
        [SerializeField] StylePalette palette;
        [SerializeField] CanvasGroup group;
        [SerializeField] Button button;
        [SerializeField] Image frame;
        [SerializeField] Image fill;
        [SerializeField] TextMeshProUGUI label;

        public event Action Pressed;

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

        void Awake()
        {
            if (!Application.isPlaying) return;
            button.onClick.AddListener(Press);
            button.gameObject.AddComponent<PositionMarkersView.HoverRelay>().Changed = SetHover;
        }

        public bool IsVisible => group.alpha > 0;

        public void Show() => SetVisible(true);

        public void Hide() => SetVisible(false);

        // A hidden button takes no ray hits, so it can't swallow a pick meant for the sphere.
        void SetVisible(bool on)
        {
            if (group == null) return; // scene teardown
            group.alpha = on ? 1 : 0;
            group.interactable = on;
            group.blocksRaycasts = on;
            button.interactable = on;
            SetHover(false);
        }

        // Editor and dev runs: the same path as a click.
        public void Press()
        {
            if (!IsVisible || !button.interactable) return;
            Pressed?.Invoke(); // PauseDirector hides the button once the skip is accepted
        }

        void SetHover(bool over)
        {
            if (palette == null || frame == null) return;
            frame.color = palette.panelAccent * new Color(1, 1, 1, over && button.interactable ? 1f : 0.6f);
        }

        public void ApplyStyle()
        {
            if (palette == null || fill == null) return;
            fill.color = palette.buttonBackground;
            frame.color = palette.panelAccent * new Color(1, 1, 1, 0.6f);
            label.font = palette.titleFont;
            label.color = palette.panelAccent;
        }
    }
}
