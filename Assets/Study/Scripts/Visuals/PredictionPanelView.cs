using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Study.Visuals
{
    // Condition 3 panel, upper left of the sphere (the histogram's mirror image):
    // the prediction prompt, the five P(0) buttons, the gate button, the rotation
    // hint and the feedback. Built by the editor scene setup; this only switches
    // what is shown and styles it from the palette. Neon on frames and accents
    // only; text is plain off-white. Feedback tone sets the title and accent
    // color (cyan correct, amber incorrect, off-white neutral), always with text.
    [ExecuteAlways]
    public class PredictionPanelView : MonoBehaviour
    {
        public enum Tone { Prompt, Correct, Incorrect, Neutral }

        [SerializeField] StylePalette palette;
        [SerializeField] CanvasGroup group;
        [SerializeField] Image background;
        [SerializeField] Image[] accents;
        [SerializeField] TextMeshProUGUI title;
        [SerializeField] TextMeshProUGUI body;
        [SerializeField] TextMeshProUGUI lockedLine;
        [SerializeField] TextMeshProUGUI hint;
        [SerializeField] Button[] choiceButtons;
        [SerializeField] Image[] choiceFrames;
        [SerializeField] Image[] choiceFills;
        [SerializeField] TextMeshProUGUI[] choiceLabels;
        [SerializeField] Button gateButton;
        [SerializeField] Image gateFrame;
        [SerializeField] Image gateFill;
        [SerializeField] TextMeshProUGUI gateLabel;
        [Tooltip("Body text rect while prompting (offset from the top, offset from the bottom, mm) and while showing feedback.")]
        [SerializeField] Vector2 promptBodyRange = new Vector2(36, 140);
        [SerializeField] Vector2 feedbackBodyRange = new Vector2(36, 48);
        [Tooltip("'Your prediction' line, bottom and top in mm: in the answer row while prompting, at the foot during feedback.")]
        [SerializeField] Vector2 lockedPromptY = new Vector2(100, 134);
        [SerializeField] Vector2 lockedFeedbackY = new Vector2(12, 40);

        public event Action<string> ChoicePicked;
        public event Action GatePressed;

        string[] choices = new string[0];
        Tone tone = Tone.Prompt;
        int lockedIndex = -1;

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
            for (int i = 0; i < choiceButtons.Length; i++)
            {
                int index = i;
                choiceButtons[i].onClick.AddListener(() => PickChoice(index));
                AddHover(choiceButtons[i].gameObject, over => SetFrameHover(choiceFrames[index], over && lockedIndex < 0));
            }
            gateButton.onClick.AddListener(PressGate);
            AddHover(gateButton.gameObject, over => SetFrameHover(gateFrame, over));
        }

        public bool IsVisible => group.alpha > 0;

        // choiceTexts: the five P(0) answers, or null for a six-position prompt (answers are on the sphere).
        public void ShowPrediction(string titleText, string prompt, string[] choiceValues, string[] choiceTexts, string hintText)
        {
            tone = Tone.Prompt;
            lockedIndex = -1;
            choices = choiceValues ?? new string[0];
            title.text = titleText;
            SetBody(prompt, promptBodyRange);
            for (int i = 0; i < choiceButtons.Length; i++)
            {
                bool used = choiceTexts != null && i < choiceTexts.Length;
                choiceButtons[i].gameObject.SetActive(used);
                choiceButtons[i].interactable = used;
                if (used) choiceLabels[i].text = choiceTexts[i];
            }
            Show(lockedLine, false);
            hint.text = hintText ?? "";
            Show(hint, !string.IsNullOrEmpty(hintText));
            gateButton.gameObject.SetActive(false);
            ApplyStyle();
            SetVisible(true);
        }

        // The first answer locks: the chosen button stays lit, the others fade, none respond.
        public void LockChoice(string answer, string lockedText)
        {
            lockedIndex = Array.IndexOf(choices, answer);
            foreach (Button button in choiceButtons) button.interactable = false;
            if (lockedIndex < 0)
            {
                // Position answers: the row is empty, so say what was chosen.
                lockedLine.text = lockedText;
                SetY(lockedLine.rectTransform, lockedPromptY);
                Show(lockedLine, true);
            }
            Show(hint, false);
            ApplyStyle();
        }

        // Prediction deadline passed with no answer: the prompt goes away.
        public void RemovePrompt(string titleText)
        {
            title.text = titleText;
            SetBody("", promptBodyRange);
            foreach (Button button in choiceButtons) button.gameObject.SetActive(false);
            Show(lockedLine, false);
            Show(hint, false);
        }

        public void ShowGateButton(string label, string hintText)
        {
            gateLabel.text = label;
            gateButton.gameObject.SetActive(true);
            gateButton.interactable = true;
            hint.text = hintText;
            Show(hint, true);
            SetFrameHover(gateFrame, false);
        }

        // After the gate press: the button goes, the rotation hint takes the body.
        public void ShowRotateHint(string titleText, string hintText)
        {
            title.text = titleText;
            gateButton.gameObject.SetActive(false);
            foreach (Button button in choiceButtons) button.gameObject.SetActive(false);
            Show(hint, false);
            SetBody(hintText, promptBodyRange);
        }

        public void ShowFeedback(Tone feedbackTone, string titleText, string text, string lockedText)
        {
            tone = feedbackTone;
            title.text = titleText;
            SetBody(text, feedbackBodyRange);
            foreach (Button button in choiceButtons) button.gameObject.SetActive(false);
            gateButton.gameObject.SetActive(false);
            Show(hint, false);
            lockedLine.text = lockedText ?? "";
            SetY(lockedLine.rectTransform, lockedFeedbackY);
            Show(lockedLine, !string.IsNullOrEmpty(lockedText));
            ApplyStyle();
            SetVisible(true);
        }

        public void Hide()
        {
            SetVisible(false);
            if (gateButton != null) gateButton.gameObject.SetActive(false);
        }

        // A hidden panel also takes no ray hits, so it can't swallow a pick meant for the sphere.
        void SetVisible(bool on)
        {
            group.alpha = on ? 1 : 0;
            group.interactable = on;
            group.blocksRaycasts = on;
        }

        // Editor and dev runs: the same paths as a trigger pull on a button.
        public void PickChoice(int index)
        {
            if (lockedIndex >= 0 || index >= choices.Length || !choiceButtons[index].interactable) return;
            ChoicePicked?.Invoke(choices[index]);
        }

        public void PressGate()
        {
            if (!gateButton.gameObject.activeSelf || !gateButton.interactable) return;
            gateButton.interactable = false;
            GatePressed?.Invoke();
        }

        void SetBody(string text, Vector2 range)
        {
            body.text = text;
            body.rectTransform.offsetMax = new Vector2(body.rectTransform.offsetMax.x, -range.x);
            body.rectTransform.offsetMin = new Vector2(body.rectTransform.offsetMin.x, range.y);
        }

        static void SetY(RectTransform rect, Vector2 bottomTop)
        {
            rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, bottomTop.x);
            rect.sizeDelta = new Vector2(rect.sizeDelta.x, bottomTop.y - bottomTop.x);
        }

        static void Show(Component c, bool on) => c.gameObject.SetActive(on);

        void SetFrameHover(Image frame, bool over)
        {
            if (palette == null) return;
            frame.color = palette.panelAccent * new Color(1, 1, 1, over ? 1f : 0.6f);
        }

        static void AddHover(GameObject target, Action<bool> changed) =>
            target.AddComponent<PositionMarkersView.HoverRelay>().Changed = changed;

        public void ApplyStyle()
        {
            if (palette == null || background == null) return;
            Color accent = tone == Tone.Correct ? palette.feedbackCorrect
                : tone == Tone.Incorrect ? palette.feedbackIncorrect
                : tone == Tone.Neutral ? palette.panelText
                : palette.panelAccent;
            background.color = palette.panelBackground;
            foreach (Image image in accents) image.color = accent;
            title.font = palette.titleFont;
            title.color = accent;
            foreach (TextMeshProUGUI text in new[] { body, lockedLine, hint, gateLabel })
            {
                text.font = palette.bodyFont;
                text.color = palette.panelText;
            }
            for (int i = 0; i < choiceButtons.Length; i++)
            {
                bool faded = lockedIndex >= 0 && i != lockedIndex;
                choiceFills[i].color = palette.buttonBackground;
                choiceFrames[i].color = palette.panelAccent * new Color(1, 1, 1, i == lockedIndex ? 1f : faded ? 0.15f : 0.6f);
                choiceLabels[i].font = palette.bodyFont;
                choiceLabels[i].color = palette.panelText * new Color(1, 1, 1, faded ? 0.35f : 1f);
            }
            gateFill.color = palette.buttonBackground;
            gateFrame.color = palette.panelAccent * new Color(1, 1, 1, 0.6f);
        }
    }
}
