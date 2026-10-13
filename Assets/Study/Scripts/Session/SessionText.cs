using System;
using UnityEngine;

namespace Study.Session
{
    // Everything the participant reads outside the lesson (Data/session_text.json), so the
    // wording can be reviewed and changed in one place. Headset and desktop variants differ
    // only in how to point and click. Experimenter-only text stays in code.
    [Serializable]
    public class SessionText
    {
        public string calibrationTitle, calibrationVr, readyTitle, readyVr, readyDesktop;
        public string practiceTitle, selectVr, selectDesktop, pressVr, pressDesktop, pressLabel;
        public string rotateVr, rotateDesktop, roundDone;
        public string breakTitle, breakKey, breakResumeVr, abortHold;
        public string endTitle, endCompletedVr, endCompletedDesktop, endStoppedVr, endStoppedDesktop, experimenterButton;

        public static SessionText FromJson(string json)
        {
            var text = JsonUtility.FromJson<SessionText>(json);
            foreach (var field in typeof(SessionText).GetFields())
                if (string.IsNullOrEmpty((string)field.GetValue(text)))
                    Debug.LogError("[Session] session_text.json: missing " + field.Name);
            return text;
        }
    }
}
