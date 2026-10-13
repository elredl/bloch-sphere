using Study.Lesson;
using Study.Visuals;
using UnityEngine;

namespace Study.Session
{
    // The end screen (spec, Session flow). The participant sees only "take off the headset /
    // look away and tell the experimenter". The experimenter's part opens with its button and
    // shows the data file name and event count; after an abort it asks for the withdrawal
    // reason (discomfort, participant request, equipment failure) and logs it.
    public class EndScreen : MonoBehaviour
    {
        public static readonly string[] Reasons = { "discomfort", "participant_request", "equipment_failure" };
        static readonly string[] ReasonLabels = { "Discomfort", "Participant\nrequest", "Equipment\nfailure" };

        [SerializeField] StudyPanelView panel;

        public StudyPanelView Panel => panel;
        public bool ReasonNeeded => aborted && reason == null;

        enum View { Participant, Experimenter, Saved }

        SessionText text;
        StudySession session;
        bool aborted;
        string reason;
        View view;

        void OnEnable() => panel.Pressed += OnPressed;
        void OnDisable() => panel.Pressed -= OnPressed;

        public void Show(SessionText sessionText, StudySession s, bool wasAborted, bool headset)
        {
            text = sessionText;
            session = s;
            aborted = wasAborted;
            reason = null;
            view = View.Participant;
            string message = aborted ? (headset ? text.endStoppedVr : text.endStoppedDesktop)
                                     : (headset ? text.endCompletedVr : text.endCompletedDesktop);
            panel.Show(text.endTitle, message);
            panel.SetFooter("");
            panel.HideButtons();
            panel.SetButton(0, text.experimenterButton);
        }

        void OnPressed(int index)
        {
            switch (view)
            {
                case View.Participant:
                    ShowExperimenter();
                    break;
                case View.Experimenter when ReasonNeeded:
                    if (index < Reasons.Length) RecordReason(Reasons[index]);
                    break;
                default:
                    Quit();
                    break;
            }
        }

        // Experimenter-only text below here (English, not reviewed as participant wording).
        void ShowExperimenter()
        {
            view = View.Experimenter;
            panel.HideButtons();
            if (ReasonNeeded)
            {
                panel.SetBody("Session stopped. Reason for withdrawal?");
                for (int i = 0; i < ReasonLabels.Length; i++) panel.SetButton(i, ReasonLabels[i]);
            }
            else
            {
                panel.SetBody(aborted ? "Session stopped." : "Lesson completed.");
                panel.SetButton(0, "Quit");
                view = View.Saved;
            }
            UpdateFooter();
        }

        void RecordReason(string picked)
        {
            reason = picked;
            StudyEvents.Log("withdrawal", $"reason={picked}");
            session.Finish("withdrawn");
            view = View.Saved;
            panel.SetBody($"Withdrawal recorded: {picked.Replace('_', ' ')}.");
            panel.HideButtons();
            panel.SetButton(0, "Quit");
            UpdateFooter();
        }

        void UpdateFooter()
        {
            string status = session.Finished ? "file closed" : "file open";
            panel.SetFooter($"{session.Code}   {session.Log.FileName}\n{session.Log.EventCount} events, {status}");
        }

        void Quit()
        {
            if (!session.Finished) session.Finish(aborted ? "withdrawn_no_reason" : "completed");
            Debug.Log("[Session] quit from the end screen");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
