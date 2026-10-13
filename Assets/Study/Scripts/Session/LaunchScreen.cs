using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Study.Lesson;
using Study.Visuals;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Study.Session
{
    // The experimenter's launch screen (spec, Session flow), before the participant puts on
    // the headset or sits at the monitor. Takes the participant code on a keypad (codes are
    // the prefix plus digits, e.g. P07; on the desktop the number keys work too), looks up
    // condition and stratum in allocation.csv and asks for confirmation. Refused: codes not
    // in the file, codes that already have a session on this device, and codes allocated to
    // a condition this device doesn't run (Quest: 2 and 3, desktop: 1). Manual entry is the
    // fallback, with its own confirmation, and is logged as source=manual.
    //
    // Experimenter-only screen, so its wording lives here rather than in session_text.json.
    public class LaunchScreen : MonoBehaviour
    {
        const int Keys = 12;        // buttons 0-11: keypad
        const int Actions = 4;      // buttons 12-15: action row
        const int Backspace = 9, Zero = 10, Clear = 11;
        const string LessonScene = "Lesson";

        [SerializeField] StudyPanelView panel;
        [SerializeField] string codePrefix = "P";
        [SerializeField] int maxDigits = 3;

        enum View { Entry, Confirm, ManualCondition, ManualStratum, ManualConfirm }

        View view;
        string digits = "";
        string message = "";
        Allocation allocation;
        string allocationStatus;
        Condition chosenCondition;
        string chosenStratum;
        readonly List<Action> actions = new List<Action>();

        public StudyPanelView Panel => panel;

        string Code => codePrefix + digits;

        static Condition[] AllowedConditions =>
            Application.isEditor ? new[] { Condition.Desktop2D, Condition.PassiveVR, Condition.EmbodiedVR }
            : Application.platform == RuntimePlatform.Android ? new[] { Condition.PassiveVR, Condition.EmbodiedVR }
            : new[] { Condition.Desktop2D };

        void OnEnable() => panel.Pressed += OnPressed;
        void OnDisable() => panel.Pressed -= OnPressed;

        void Start()
        {
            LoadAllocation();
            ShowEntry("");
        }

        void LoadAllocation()
        {
            string path = Path.Combine(StudySession.DataDirectory, StudySession.AllocationFile);
            if (!File.Exists(path))
            {
                allocation = null;
                allocationStatus = $"allocation.csv not found in {StudySession.DataDirectory}";
            }
            else
            {
                allocation = Allocation.Parse(File.ReadAllText(path));
                allocationStatus = allocation.IsValid
                    ? $"allocation.csv: {allocation.Count} codes"
                    : $"allocation.csv REJECTED: {string.Join("; ", allocation.Errors.Take(2))}";
                if (!allocation.IsValid) allocation = null;
            }
            Debug.Log("[Launch] " + allocationStatus);
        }

        void Update()
        {
            if (view != View.Entry || Keyboard.current == null) return;
            Keyboard k = Keyboard.current;
            for (int d = 0; d <= 9; d++)
                if (k[Key.Digit0 + d].wasPressedThisFrame || k[Key.Numpad0 + d].wasPressedThisFrame) Type(d.ToString());
            if (k.backspaceKey.wasPressedThisFrame) Erase();
            if (k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame) Lookup();
        }

        // ---- views ----

        void ShowEntry(string note)
        {
            view = View.Entry;
            message = note;
            for (int i = 0; i < Keys; i++)
                panel.SetButton(i, i == Backspace ? "⌫" : i == Zero ? "0" : i == Clear ? "CLR" : (i + 1).ToString());
            SetActions(("START", Lookup), ("MANUAL", StartManual));
            Refresh();
        }

        void Refresh()
        {
            string cursor = digits.Length < maxDigits ? "_" : "";
            string body = view == View.Entry ? $"<size=150%>{Code}{cursor}</size>\n{message}" : message;
            panel.Show("SESSION", body);
            panel.SetFooter($"{allocationStatus}\nData: {StudySession.DataDirectory}");
        }

        void ShowChoice(View next, string text, params (string, Action)[] choices)
        {
            view = next;
            message = text;
            for (int i = 0; i < Keys; i++) panel.SetButton(i, null);
            SetActions(choices);
            Refresh();
        }

        void SetActions(params (string label, Action action)[] list)
        {
            actions.Clear();
            for (int i = 0; i < Actions; i++)
            {
                bool used = i < list.Length;
                panel.SetButton(Keys + i, used ? list[i].label : null);
                actions.Add(used ? list[i].action : null);
            }
        }

        void OnPressed(int index)
        {
            if (index >= Keys)
            {
                actions[index - Keys]?.Invoke();
                return;
            }
            if (view != View.Entry) return;
            if (index == Backspace) Erase();
            else if (index == Clear) { digits = ""; message = ""; Refresh(); }
            else Type(index == Zero ? "0" : (index + 1).ToString());
        }

        void Type(string digit)
        {
            if (digits.Length >= maxDigits) return;
            digits += digit;
            message = "";
            Refresh();
        }

        void Erase()
        {
            if (digits.Length > 0) digits = digits.Substring(0, digits.Length - 1);
            message = "";
            Refresh();
        }

        // ---- allocation path ----

        string Refusal()
        {
            if (digits.Length == 0) return "Type the participant number.";
            return StudySession.AlreadyUsed(Code, StudySession.DataDirectory);
        }

        void Lookup()
        {
            string refusal = Refusal();
            if (refusal != null) { ShowEntry(Red(refusal)); return; }
            if (allocation == null) { ShowEntry(Red("No usable allocation.csv. Fix it, or use MANUAL.")); return; }
            if (!allocation.TryGet(Code, out Allocation.Entry entry)) { ShowEntry(Red($"{Code} is not in allocation.csv.")); return; }
            if (Array.IndexOf(AllowedConditions, entry.Condition) < 0)
            {
                ShowEntry(Red($"{Code} is allocated to condition {(int)entry.Condition}, which runs on the " +
                              $"{(entry.Condition == Condition.Desktop2D ? "desktop" : "Quest")}."));
                return;
            }
            ShowChoice(View.Confirm, $"<size=150%>{Code}</size>\nCondition {(int)entry.Condition}, {entry.Stratum}\nStart this session?",
                ("CONFIRM", () => Begin(entry.Condition, entry.Stratum, "allocation")), ("BACK", () => ShowEntry("")));
        }

        // ---- manual fallback ----

        void StartManual()
        {
            string refusal = Refusal();
            if (refusal != null) { ShowEntry(Red(refusal)); return; }
            var choices = new List<(string, Action)>();
            foreach (Condition c in AllowedConditions)
            {
                Condition condition = c;
                choices.Add(($"COND {(int)c}", () => ChooseStratum(condition)));
            }
            choices.Add(("BACK", () => ShowEntry("")));
            string inFile = allocation != null && allocation.TryGet(Code, out Allocation.Entry e)
                ? $" allocation.csv says condition {(int)e.Condition}, {e.Stratum}." : "";
            ShowChoice(View.ManualCondition, $"<size=150%>{Code}</size>\nMANUAL ENTRY.{inFile}\nCondition?", choices.ToArray());
        }

        void ChooseStratum(Condition condition)
        {
            chosenCondition = condition;
            ShowChoice(View.ManualStratum, $"<size=150%>{Code}</size>\nMANUAL ENTRY, condition {(int)condition}.\nStratum?",
                ("MATH\nLITERATE", () => ConfirmManual("math_literate")), ("NOVICE", () => ConfirmManual("novice")),
                ("BACK", StartManual));
        }

        void ConfirmManual(string stratum)
        {
            chosenStratum = stratum;
            ShowChoice(View.ManualConfirm,
                $"<size=150%>{Code}</size>\n{Red("MANUAL")} condition {(int)chosenCondition}, {stratum}.\nLogged as manual. Start?",
                ("CONFIRM", () => Begin(chosenCondition, chosenStratum, "manual")), ("BACK", () => ChooseStratum(chosenCondition)));
        }

        // ---- start ----

        void Begin(Condition condition, string stratum, string source)
        {
            string refusal = Refusal(); // a file may have appeared since the lookup
            if (refusal != null) { ShowEntry(Red(refusal)); return; }
            try
            {
                StudySession.Start(Code, condition, stratum, source);
            }
            catch (Exception e)
            {
                Debug.LogError("[Launch] could not start the session: " + e);
                ShowEntry(Red("Could not create the session file: " + e.Message));
                return;
            }
            panel.Hide();
            SceneManager.LoadScene(LessonScene);
        }

        static string Red(string s) => $"<color=#FF6B6B>{s}</color>";

#if UNITY_EDITOR
        // Dev runs: type a number and press START (or CONFIRM) through the same paths.
        public string DevType(string number)
        {
            if (view != View.Entry) return "not on the code entry view";
            digits = "";
            foreach (char c in number) Type(c.ToString());
            return Code;
        }
#endif
    }
}
