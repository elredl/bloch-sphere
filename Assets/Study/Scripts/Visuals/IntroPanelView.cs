using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Study.Visuals
{
    // The intro's text box: a title, a body, an optional answer below a divider (shown
    // later with RevealAnswer), and a NEXT button. It stands either in front of the
    // learner (center) or beside a drawing (side); both poses are set by the editor
    // scene setup. Fades in and out. Controller ray or mouse (PointerSupport).
    [ExecuteAlways]
    public class IntroPanelView : MonoBehaviour
    {
        const float FadeSeconds = 0.5f;

        [SerializeField] StylePalette palette;
        [SerializeField] CanvasGroup group;
        [SerializeField] Image background;
        [SerializeField] Image[] accents;
        [SerializeField] TextMeshProUGUI title;
        [SerializeField] TextMeshProUGUI body;
        [SerializeField] Image divider;
        [SerializeField] TextMeshProUGUI answer;
        [SerializeField] Button nextButton;
        [SerializeField] Image nextFrame;
        [SerializeField] Image nextFill;
        [SerializeField] TextMeshProUGUI nextLabel;
        [SerializeField] Vector3 centerPosition;
        [SerializeField] Quaternion centerRotation = Quaternion.identity;
        [SerializeField] Vector3 sidePosition;
        [SerializeField] Quaternion sideRotation = Quaternion.identity;

        public event Action NextPressed;

        public bool IsShowing => target > 0;
        public bool IsFaded => Mathf.Approximately(group.alpha, target);

        float target;

        void OnEnable()
        {
            StylePalette.Edited += OnPaletteEdited;
            ApplyStyle();
            if (Application.isPlaying) SetAlpha(0);
        }

        void OnDisable() => StylePalette.Edited -= OnPaletteEdited;

        void OnPaletteEdited(StylePalette edited)
        {
            if (edited == palette) ApplyStyle();
        }

        void Awake()
        {
            if (!Application.isPlaying) return;
            nextButton.onClick.AddListener(Press);
            nextButton.gameObject.AddComponent<PositionMarkersView.HoverRelay>().Changed = SetHover;
        }

        // answerText: null for none; otherwise hidden until RevealAnswer.
        public void Show(string titleText, string bodyText, string answerText, bool side, string nextText)
        {
            transform.localPosition = side ? sidePosition : centerPosition;
            transform.localRotation = side ? sideRotation : centerRotation;
            title.text = titleText;
            body.text = bodyText;
            answer.text = answerText ?? "";
            answer.gameObject.SetActive(false);
            divider.gameObject.SetActive(false);
            nextLabel.text = nextText;
            nextButton.gameObject.SetActive(answerText == null);
            target = 1;
        }

        public void RevealAnswer()
        {
            answer.gameObject.SetActive(true);
            divider.gameObject.SetActive(true);
            nextButton.gameObject.SetActive(true);
        }

        public void Hide() => target = 0;

        void Update()
        {
            if (!Application.isPlaying || group == null) return;
            float step = Time.deltaTime / FadeSeconds;
            SetAlpha(Mathf.MoveTowards(group.alpha, target, step));
        }

        // Interactive only when fully shown, so a fading box takes no clicks.
        void SetAlpha(float alpha)
        {
            group.alpha = alpha;
            bool on = alpha >= 1 && target >= 1;
            group.interactable = on;
            group.blocksRaycasts = on;
        }

        // Editor and dev runs: the same path as a click.
        public void Press()
        {
            if (!IsShowing || group.alpha < 1 || !nextButton.gameObject.activeSelf) return;
            NextPressed?.Invoke();
        }

        void SetHover(bool over)
        {
            if (palette == null || nextFrame == null) return;
            nextFrame.color = palette.panelAccent * new Color(1, 1, 1, over ? 1f : 0.6f);
        }

        public void ApplyStyle()
        {
            if (palette == null || background == null) return;
            background.color = palette.panelBackground;
            foreach (Image image in accents) image.color = palette.panelAccent;
            divider.color = palette.panelAccent * new Color(1, 1, 1, 0.4f);
            title.font = palette.titleFont;
            title.color = palette.panelAccent;
            foreach (TextMeshProUGUI text in new[] { body, answer })
            {
                text.font = palette.bodyFont;
                text.color = palette.panelText;
            }
            nextFill.color = palette.buttonBackground;
            nextFrame.color = palette.panelAccent * new Color(1, 1, 1, 0.6f);
            nextLabel.font = palette.titleFont;
            nextLabel.color = palette.panelAccent;
        }
    }
}
