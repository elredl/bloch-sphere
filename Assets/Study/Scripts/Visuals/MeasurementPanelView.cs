using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Study.Visuals
{
    // Running histogram of measurement outcomes. Each bar is paired with its
    // label (|0>, |1>) and a count, so color is never the only cue. Bars
    // grow from a fixed baseline with height = fraction of shots so far.
    //
    // Live mode (Condition 3's measurement sandbox): the bars show the chance of
    // 0 and 1 for the current state instead, and the counter line tallies the
    // learner's own measurements.
    [ExecuteAlways]
    public class MeasurementPanelView : MonoBehaviour
    {
        [SerializeField] StylePalette palette;
        [SerializeField] CanvasGroup group;
        [SerializeField] Image background;
        [SerializeField] Image[] accents;
        [SerializeField] TextMeshProUGUI title;
        [SerializeField] TextMeshProUGUI counter;
        [SerializeField] RectTransform barZero;
        [SerializeField] RectTransform barOne;
        [SerializeField] TextMeshProUGUI labelZero;
        [SerializeField] TextMeshProUGUI labelOne;
        [SerializeField] TextMeshProUGUI countZero;
        [SerializeField] TextMeshProUGUI countOne;

        int zeros;
        int ones;
        int total;
        string demoTitle; // the scene's title, restored when the demo uses the panel after live mode

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

        public void Show(int totalShots)
        {
            if (demoTitle != null) title.text = demoTitle;
            total = totalShots;
            zeros = 0;
            ones = 0;
            Refresh();
            group.alpha = 1;
        }

        public void Hide() => group.alpha = 0;

        public void Add(int outcome)
        {
            if (outcome == 0) zeros++;
            else ones++;
            Refresh();
        }

        void Refresh()
        {
            int shots = zeros + ones;
            counter.text = $"{shots} / {total}";
            SetBar(barZero, shots == 0 ? 0 : zeros / (float)shots);
            SetBar(barOne, shots == 0 ? 0 : ones / (float)shots);
            countZero.text = Count(zeros, shots);
            countOne.text = Count(ones, shots);
        }

        public void ShowLive(string titleText)
        {
            if (demoTitle == null) demoTitle = title.text;
            title.text = titleText;
            group.alpha = 1;
        }

        // probabilityZero: of the current state. tally: e.g. "You measured 0: 3   1: 2".
        public void SetLive(double probabilityZero, string tally)
        {
            float p0 = Mathf.Clamp01((float)probabilityZero);
            SetBar(barZero, p0);
            SetBar(barOne, 1 - p0);
            countZero.text = $"{Mathf.RoundToInt(100 * p0)}%";
            countOne.text = $"{Mathf.RoundToInt(100 * (1 - p0))}%";
            counter.text = tally;
        }

        static string Count(int n, int shots) => shots == 0 ? "0" : $"{n}  ({Mathf.RoundToInt(100f * n / shots)}%)";

        // The bar's rect is anchored to the bottom of its column; height follows the fraction.
        static void SetBar(RectTransform bar, float fraction)
        {
            bar.anchorMax = new Vector2(bar.anchorMax.x, Mathf.Clamp01(fraction));
        }

        public void ApplyStyle()
        {
            if (palette == null || background == null) return;
            background.color = palette.panelBackground;
            foreach (Image accent in accents) accent.color = palette.panelAccent;
            title.font = palette.titleFont;
            title.color = palette.panelAccent;
            foreach (TextMeshProUGUI text in new[] { counter, countZero, countOne })
            {
                text.font = palette.bodyFont;
                text.color = palette.panelText;
            }
            labelZero.font = palette.bodyFont;
            labelOne.font = palette.bodyFont;
            labelZero.color = palette.histogramZero;
            labelOne.color = palette.histogramOne;
            barZero.GetComponent<Image>().color = palette.histogramZero;
            barOne.GetComponent<Image>().color = palette.histogramOne;
        }
    }
}
