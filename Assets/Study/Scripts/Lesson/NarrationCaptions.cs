using Study.Visuals;
using UnityEngine;

namespace Study.Lesson
{
    // The lesson's text boxes, in the same box as the intro (IntroPanelView, lesson pose)
    // with NEXT inside it, and the Next button after each step (turn or demo), standing
    // where the box's NEXT sits, so Next is always in one place. Outside the pauses only
    // (PauseDirector has that button in a pause). LessonController holds lesson time
    // at each box and after each step; this shows the box while it holds. In Condition 3
    // the box shows the guide text, without NEXT, while a turn waits for the learner.
    // Same in every condition. Texts come from narration_captions.json, via LessonController.
    public class NarrationCaptions : MonoBehaviour
    {
        [SerializeField] LessonController lesson;
        [SerializeField] IntroPanelView panel;
        [SerializeField] NextButtonView stepNext;
        [SerializeField] GuidedTurn turn;
        [SerializeField] EmbodiedPause pause; // the guide's title and NEXT label (pause_feedback.json ui)
        [SerializeField] TextAsset introTextJson; // the intro's NEXT label, so both read the same

        string nextLabel;
        string shownTitle, shownBody; // what the panel shows, or is fading in; null = nothing
        bool shownHasNext;

        [System.Serializable]
        class IntroLabel
        {
            public string nextLabel;
        }

        void Awake()
        {
            nextLabel = JsonUtility.FromJson<IntroLabel>(introTextJson.text).nextLabel;
        }

        void OnEnable()
        {
            panel.NextPressed += OnBoxNext;
            stepNext.Pressed += OnStepNext;
        }

        void OnDisable()
        {
            if (panel != null) panel.NextPressed -= OnBoxNext;
            if (stepNext != null) stepNext.Pressed -= OnStepNext;
        }

        // The panel is the intro's too: only a lesson box's NEXT counts here.
        void OnBoxNext()
        {
            if (lesson.Holding == LessonController.Hold.Box) lesson.NextStep();
        }

        void OnStepNext()
        {
            if (lesson.InPause || lesson.Holding != LessonController.Hold.Step || !lesson.NextStep()) return;
            stepNext.Hide();
        }

        void Update()
        {
            if (!lesson.IsRunning && shownTitle == null) return; // the intro has the panel
            bool live = lesson.IsRunning && !lesson.OnBreak && !lesson.InPause;
            string title = null, body = null;
            bool hasNext = false;
            if (live && lesson.Box != null)
            {
                title = lesson.Box.title;
                body = lesson.Box.text;
                hasNext = true;
            }
            else if (live && turn != null && turn.Hint != null)
            {
                title = pause.Feedback.ui.turnTitle;
                body = turn.Hint;
            }
            ShowPanel(title, body, hasNext);

            if (!live) return; // in a pause the button is PauseDirector's
            bool stepReady = lesson.ReadyForNext && lesson.Holding == LessonController.Hold.Step;
            if (stepReady && !stepNext.IsVisible && !panel.IsShowing && panel.IsFaded) stepNext.ShowAt(panel.NextAnchor);
            else if (!stepReady && stepNext.IsVisible) stepNext.Hide();
        }

        // A change of text fades the old box out first, then the new one in.
        void ShowPanel(string title, string body, bool hasNext)
        {
            if (title == shownTitle && body == shownBody && hasNext == shownHasNext) return;
            if (panel.IsShowing)
            {
                panel.Hide();
                return;
            }
            if (!panel.IsFaded) return;
            shownTitle = title;
            shownBody = body;
            shownHasNext = hasNext;
            if (title != null) panel.Show(title, body, null, IntroPanelView.Pose.Lesson, hasNext ? nextLabel : null);
        }
    }
}
