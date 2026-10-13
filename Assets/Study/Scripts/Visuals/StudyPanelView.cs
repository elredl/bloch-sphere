using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Study.Visuals
{
    // A styled world-space panel for the session screens (launch, calibration, practice,
    // break, end): title, body, an optional footer, a fixed set of buttons laid out by the
    // editor scene setup, and an optional hold-progress ring. This only switches what is
    // shown; the screens decide what it says. Same look as the lesson panels: neon on the
    // frame and accents only, plain off-white text.
    [ExecuteAlways]
    public class StudyPanelView : MonoBehaviour
    {
        [SerializeField] StylePalette palette;
        [SerializeField] CanvasGroup group;
        [SerializeField] Image background;
        [SerializeField] Image[] accents;
        [SerializeField] TextMeshProUGUI title;
        [SerializeField] TextMeshProUGUI body;
        [SerializeField] TextMeshProUGUI footer;
        [SerializeField] Button[] buttons;
        [SerializeField] Image[] frames;
        [SerializeField] Image[] fills;
        [SerializeField] TextMeshProUGUI[] labels;
        [SerializeField] Image ring;
        [SerializeField] bool startHidden = true;
        [Tooltip("Opaque quad behind the panel (overlay and end screen): hides the glowing sphere lines behind it.")]
        [SerializeField] GameObject backing;

        public event Action<int> Pressed;

        public bool IsVisible => group.alpha > 0;
        public int ButtonCount => buttons.Length;

        void OnEnable()
        {
            StylePalette.Edited += OnPaletteEdited;
            ApplyStyle();
        }

        void OnDisable() => StylePalette.Edited -= OnPaletteEdited;

        void OnPaletteEdited(StylePalette edited)
        {
            if (edited == palette) ApplyStyle();
        }

        void Awake()
        {
            if (!Application.isPlaying) return;
            for (int i = 0; i < buttons.Length; i++)
            {
                int index = i;
                buttons[i].onClick.AddListener(() => Press(index));
                buttons[i].gameObject.AddComponent<PositionMarkersView.HoverRelay>().Changed = over => SetHover(index, over);
            }
            if (ring != null)
            {
                ring.sprite = RingSprite();
                ring.type = Image.Type.Filled;
                ring.fillMethod = Image.FillMethod.Radial360;
                ring.fillOrigin = (int)Image.Origin360.Top;
                ring.fillClockwise = true;
                SetRing(-1);
            }
            if (startHidden) Hide();
        }

        public void Show(string titleText, string bodyText)
        {
            title.text = titleText ?? "";
            body.text = bodyText ?? "";
            SetVisible(true);
        }

        public void SetBody(string text) => body.text = text ?? "";

        public void SetFooter(string text)
        {
            if (footer != null) footer.text = text ?? "";
        }

        // label null hides the button. Shown buttons are enabled.
        public void SetButton(int index, string label)
        {
            bool on = label != null;
            buttons[index].gameObject.SetActive(on);
            buttons[index].interactable = on;
            if (on) labels[index].text = label;
            SetHover(index, false);
        }

        public void HideButtons()
        {
            for (int i = 0; i < buttons.Length; i++) SetButton(i, null);
        }

        // progress 0-1 shows the ring that far round; below 0 hides it.
        public void SetRing(float progress)
        {
            if (ring == null) return;
            ring.gameObject.SetActive(progress >= 0);
            ring.fillAmount = Mathf.Clamp01(progress);
        }

        public void Hide() => SetVisible(false);

        // A hidden panel takes no ray or mouse hits.
        void SetVisible(bool on)
        {
            group.alpha = on ? 1 : 0;
            group.interactable = on;
            group.blocksRaycasts = on;
            if (backing != null) backing.SetActive(on);
        }

        // Editor and dev runs: the same path as a trigger pull or click on the button.
        public void Press(int index)
        {
            if (!IsVisible || index < 0 || index >= buttons.Length) return;
            if (!buttons[index].gameObject.activeSelf || !buttons[index].interactable) return;
            Pressed?.Invoke(index);
        }

        // Dev runs and logs: what the panel shows, on one line.
        public string Describe()
        {
            var shown = new System.Collections.Generic.List<string>();
            for (int i = 0; i < buttons.Length; i++)
                if (buttons[i].gameObject.activeSelf) shown.Add($"{i}:{labels[i].text.Replace('\n', ' ')}");
            return $"visible={IsVisible} title='{title.text}' body='{body.text.Replace("\n", " | ")}' " +
                   $"footer='{(footer != null ? footer.text.Replace("\n", " | ") : "")}' buttons=[{string.Join(", ", shown)}]";
        }

        public string ButtonLabel(int index) => buttons[index].gameObject.activeSelf ? labels[index].text : null;

        void SetHover(int index, bool over)
        {
            if (palette == null) return;
            frames[index].color = palette.panelAccent * new Color(1, 1, 1, over ? 1f : 0.6f);
        }

        public void ApplyStyle()
        {
            if (palette == null || background == null) return;
            background.color = palette.panelBackground;
            foreach (Image image in accents) image.color = palette.panelAccent;
            title.font = palette.titleFont;
            title.color = palette.panelAccent;
            foreach (TextMeshProUGUI text in new[] { body, footer })
            {
                if (text == null) continue;
                text.font = palette.bodyFont;
                text.color = palette.panelText;
            }
            for (int i = 0; i < buttons.Length; i++)
            {
                fills[i].color = palette.buttonBackground;
                frames[i].color = palette.panelAccent * new Color(1, 1, 1, 0.6f);
                labels[i].font = palette.bodyFont;
                labels[i].color = palette.panelText;
            }
            if (ring != null) ring.color = palette.panelAccent;
        }

        // A thin anti-aliased ring for the hold-progress indicator, made once at run time.
        static Sprite ringSprite;

        static Sprite RingSprite()
        {
            if (ringSprite != null) return ringSprite;
            const int size = 128;
            const float outer = 62f, inner = 54f;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { hideFlags = HideFlags.DontSave };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float r = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size / 2f, size / 2f));
                float a = Mathf.Clamp01(outer - r) * Mathf.Clamp01(r - inner);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            ringSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            ringSprite.hideFlags = HideFlags.DontSave;
            return ringSprite;
        }
    }
}
