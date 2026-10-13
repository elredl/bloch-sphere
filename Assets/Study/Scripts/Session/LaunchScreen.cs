using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Study.Lesson;
using Study.Visuals;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.XR;

namespace Study.Session
{
    // The experimenter's launch screen (spec, Session flow), before the participant puts on
    // the headset or sits at the monitor. Lists the codes from allocation.csv that can start
    // a session on this device: not used yet (no session file, not in session_index.csv) and
    // allocated to a condition this device runs (Quest: 2 and 3, desktop: 1). The experimenter
    // picks one and confirms. Only the code is shown, never the condition or stratum.
    //
    // Manual entry is the fallback (no usable allocation.csv, or a code outside it): exactly
    // two digits on a keypad (the number keys work on the desktop), then condition, stratum
    // and a confirmation; logged as source=manual. Codes in allocation.csv must be picked
    // from the list instead.
    //
    // Experimenter-only screen, so its wording lives here rather than in session_text.json.
    public class LaunchScreen : MonoBehaviour
    {
        const int Grid = 12;        // buttons 0-11: code list or keypad
        const int Actions = 4;      // buttons 12-15: action row
        const int Backspace = 9, Zero = 10, Clear = 11;
        const string LessonScene = "Lesson";
        const string Prefix = "P";

        [SerializeField] StudyPanelView panel;

        enum View { List, Confirm, ManualCode, ManualCondition, ManualStratum, ManualConfirm }

        View view;
        Allocation allocation;
        string allocationStatus;
        readonly List<Allocation.Entry> available = new List<Allocation.Entry>();
        int page;
        string chosenCode;
        string digits = "";
        Condition chosenCondition;
        string chosenStratum;
        readonly List<Action> actions = new List<Action>();
        float footerRefresh;

        public StudyPanelView Panel => panel;

        static Condition[] AllowedConditions =>
            Application.isEditor ? new[] { Condition.Desktop2D, Condition.PassiveVR, Condition.EmbodiedVR }
            : Application.platform == RuntimePlatform.Android ? new[] { Condition.PassiveVR, Condition.EmbodiedVR }
            : new[] { Condition.Desktop2D };

        int Pages => Math.Max(1, (available.Count + Grid - 1) / Grid);

        void OnEnable() => panel.Pressed += OnPressed;
        void OnDisable() => panel.Pressed -= OnPressed;

        void Start()
        {
            LoadAllocation();
            ShowList("");
        }

        void LoadAllocation()
        {
            string path = Path.Combine(StudySession.DataDirectory, StudySession.AllocationFile);
            allocation = null;
            available.Clear();
            if (!File.Exists(path))
            {
                allocationStatus = $"allocation.csv not found in {StudySession.DataDirectory}";
            }
            else
            {
                var parsed = Allocation.Parse(File.ReadAllText(path));
                if (parsed.IsValid)
                {
                    allocation = parsed;
                    foreach (Allocation.Entry e in parsed.Entries)
                        if (Array.IndexOf(AllowedConditions, e.Condition) >= 0 && StudySession.AlreadyUsed(e.Code, StudySession.DataDirectory) == null)
                            available.Add(e);
                    allocationStatus = $"allocation.csv: {parsed.Count} codes, {available.Count} available on this device";
                }
                else allocationStatus = $"allocation.csv REJECTED: {string.Join("; ", parsed.Errors.Take(2))}";
            }
            Debug.Log("[Launch] " + allocationStatus);
        }

        void Update()
        {
            footerRefresh += Time.unscaledDeltaTime;
            if (footerRefresh > 1f)
            {
                footerRefresh = 0;
                RefreshFooter();
            }
            if (view != View.ManualCode || Keyboard.current == null) return;
            Keyboard k = Keyboard.current;
            for (int d = 0; d <= 9; d++)
                if (k[Key.Digit0 + d].wasPressedThisFrame || k[Key.Numpad0 + d].wasPressedThisFrame) Type(d.ToString());
            if (k.backspaceKey.wasPressedThisFrame) Erase();
            if (k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame) ManualNext();
        }

        // ---- views ----

        void Show(View next, string body)
        {
            view = next;
            panel.Show("SESSION", body);
            RefreshFooter();
        }

        void RefreshFooter() => panel.SetFooter($"{allocationStatus}\nData: {StudySession.DataDirectory}\n{DeviceStatus()}");

        void ShowList(string note)
        {
            page = Mathf.Clamp(page, 0, Pages - 1);
            for (int i = 0; i < Grid; i++)
            {
                int index = page * Grid + i;
                panel.SetButton(i, index < available.Count ? available[index].Code : null);
            }
            var row = new List<(string, Action)>();
            if (Pages > 1)
            {
                row.Add(("◀", () => { page = (page + Pages - 1) % Pages; ShowList(""); }));
                row.Add(("▶", () => { page = (page + 1) % Pages; ShowList(""); }));
            }
            row.Add(("MANUAL", StartManual));
            SetActions(row.ToArray());
            string body = allocation == null ? "No usable allocation.csv: use MANUAL, or fix the file and restart."
                : available.Count == 0 ? "No unused codes left for this device."
                : "Choose the participant code." + (Pages > 1 ? $"   (page {page + 1} of {Pages})" : "");
            Show(View.List, body + (note.Length > 0 ? "\n" + note : ""));
        }

        void ShowChoice(View next, string text, params (string, Action)[] choices)
        {
            for (int i = 0; i < Grid; i++) panel.SetButton(i, null);
            SetActions(choices);
            Show(next, text);
        }

        void SetActions(params (string label, Action action)[] list)
        {
            actions.Clear();
            for (int i = 0; i < Actions; i++)
            {
                bool used = i < list.Length;
                panel.SetButton(Grid + i, used ? list[i].label : null);
                actions.Add(used ? list[i].action : null);
            }
        }

        void OnPressed(int index)
        {
            if (index >= Grid)
            {
                actions[index - Grid]?.Invoke();
                return;
            }
            if (view == View.List)
            {
                int pick = page * Grid + index;
                if (pick < available.Count) Pick(available[pick]);
            }
            else if (view == View.ManualCode)
            {
                if (index == Backspace) Erase();
                else if (index == Clear) { digits = ""; ShowManualCode(""); }
                else Type(index == Zero ? "0" : (index + 1).ToString());
            }
        }

        // ---- allocation path: the code only ----

        void Pick(Allocation.Entry entry)
        {
            ShowChoice(View.Confirm, $"<size=150%>{entry.Code}</size>\nStart the session for {entry.Code}?",
                ("CONFIRM", () => Begin(entry.Code, entry.Condition, entry.Stratum, "allocation")), ("BACK", () => ShowList("")));
        }

        // ---- manual fallback ----

        void StartManual()
        {
            digits = "";
            ShowManualCode("");
        }

        void ShowManualCode(string note)
        {
            for (int i = 0; i < Grid; i++)
                panel.SetButton(i, i == Backspace ? "⌫" : i == Zero ? "0" : i == Clear ? "CLR" : (i + 1).ToString());
            SetActions(("NEXT", ManualNext), ("BACK", () => ShowList("")));
            string cursor = new string('_', Allocation.CodeDigits - digits.Length);
            Show(View.ManualCode, $"MANUAL ENTRY\n<size=150%>{Prefix}{digits}{cursor}</size>" + (note.Length > 0 ? "\n" + note : ""));
        }

        void Type(string digit)
        {
            if (digits.Length >= Allocation.CodeDigits) return;
            digits += digit;
            ShowManualCode("");
        }

        void Erase()
        {
            if (digits.Length > 0) digits = digits.Substring(0, digits.Length - 1);
            ShowManualCode("");
        }

        void ManualNext()
        {
            string code = Prefix + digits;
            if (digits.Length != Allocation.CodeDigits) { ShowManualCode(Red($"Type exactly {Allocation.CodeDigits} digits ({Prefix}01 to {Prefix}99).")); return; }
            string used = StudySession.AlreadyUsed(code, StudySession.DataDirectory);
            if (used != null) { ShowManualCode(Red(used)); return; }
            if (allocation != null && allocation.TryGet(code, out _)) { ShowManualCode(Red($"{code} is in allocation.csv: pick it from the list.")); return; }
            chosenCode = code;
            var choices = new List<(string, Action)>();
            foreach (Condition c in AllowedConditions)
            {
                Condition condition = c;
                choices.Add(($"COND {(int)c}", () => ChooseStratum(condition)));
            }
            choices.Add(("BACK", () => ShowManualCode("")));
            ShowChoice(View.ManualCondition, $"MANUAL ENTRY  <size=150%>{chosenCode}</size>\nCondition?", choices.ToArray());
        }

        void ChooseStratum(Condition condition)
        {
            chosenCondition = condition;
            ShowChoice(View.ManualStratum, $"MANUAL ENTRY  <size=150%>{chosenCode}</size>\nCondition {(int)condition}. Stratum?",
                ("MATH\nLITERATE", () => ConfirmManual("math_literate")), ("NOVICE", () => ConfirmManual("novice")),
                ("BACK", ManualNext));
        }

        void ConfirmManual(string stratum)
        {
            chosenStratum = stratum;
            ShowChoice(View.ManualConfirm,
                $"{Red("MANUAL")}  <size=150%>{chosenCode}</size>\nCondition {(int)chosenCondition}, {stratum}. Logged as manual. Start?",
                ("CONFIRM", () => Begin(chosenCode, chosenCondition, chosenStratum, "manual")), ("BACK", () => ChooseStratum(chosenCondition)));
        }

        // ---- start ----

        void Begin(string code, Condition condition, string stratum, string source)
        {
            string used = StudySession.AlreadyUsed(code, StudySession.DataDirectory); // a file may have appeared since
            if (used != null) { LoadAllocation(); ShowList(Red(used)); return; }
            try
            {
                StudySession.Start(code, condition, stratum, source);
            }
            catch (Exception e)
            {
                Debug.LogError("[Launch] could not start the session: " + e);
                ShowList(Red("Could not create the session file: " + e.Message));
                return;
            }
            panel.Hide();
            SceneManager.LoadScene(LessonScene);
        }

        // Build, display rate and battery, so the experimenter sees a 72 Hz headset or a low
        // battery before the session starts.
        static string DeviceStatus()
        {
            string rate = DisplayRate.Status;
            if (XRSettings.isDeviceActive && DisplayRate.Known && !DisplayRate.Ok) rate = Red(rate);
            float battery = SystemInfo.batteryLevel;
            string power = battery < 0 ? "" : $"   battery {battery * 100:F0}%";
            if (battery >= 0 && battery < 0.4f && SystemInfo.batteryStatus != BatteryStatus.Charging) power = Red(power);
            return $"{Application.productName} {Application.version}   {rate}{power}";
        }

        static string Red(string s) => $"<color=#FF6B6B>{s}</color>";

#if UNITY_EDITOR
        // Dev runs: type the manual-entry digits through the same path as the keypad.
        public string DevType(string number)
        {
            if (view != View.ManualCode) return "not on the manual code view";
            digits = "";
            foreach (char c in number) Type(c.ToString());
            return Prefix + digits;
        }
#endif
    }
}
