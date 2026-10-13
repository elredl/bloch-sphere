using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Study.Lesson;

namespace Study.Session
{
    // allocation.csv: which condition and stratum each participant code was randomised to
    // (spec, Session flow). Columns participant_code,condition,stratum; condition 1/2/3;
    // stratum math_literate or novice. Blank lines and lines starting with # are skipped.
    // Any error rejects the whole file: a half-read allocation could start a participant
    // in the wrong condition, so the launch screen shows the errors instead.
    public sealed class Allocation
    {
        public const string Header = "participant_code,condition,stratum";
        public static readonly string[] Strata = { "math_literate", "novice" };

        public readonly struct Entry
        {
            public readonly string Code;
            public readonly Condition Condition;
            public readonly string Stratum;

            public Entry(string code, Condition condition, string stratum)
            {
                Code = code;
                Condition = condition;
                Stratum = stratum;
            }
        }

        readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>();
        readonly List<string> errors = new List<string>();

        public IReadOnlyList<string> Errors => errors;
        public bool IsValid => errors.Count == 0;
        public int Count => entries.Count;

        // Codes go into file names, so only letters, digits and '-' (e.g. P07).
        static readonly Regex CodePattern = new Regex("^[A-Z0-9-]{1,16}$");

        public static string NormalizeCode(string code) => (code ?? "").Trim().ToUpperInvariant();

        public static bool IsValidCode(string code) => CodePattern.IsMatch(NormalizeCode(code));

        public static Allocation Parse(string text)
        {
            var allocation = new Allocation();
            string[] lines = (text ?? "").Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            bool headerSeen = false;
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim().TrimStart('﻿'); // spreadsheet exports may start with a BOM
                if (line.Length == 0 || line.StartsWith("#")) continue;
                int lineNumber = i + 1;
                if (!headerSeen)
                {
                    headerSeen = true;
                    if (line.Replace(" ", "").ToLowerInvariant() != Header)
                        allocation.errors.Add($"line {lineNumber}: header must be '{Header}'");
                    continue;
                }
                allocation.ParseRow(line, lineNumber);
            }
            if (!headerSeen) allocation.errors.Add("the file is empty");
            else if (allocation.entries.Count == 0 && allocation.errors.Count == 0) allocation.errors.Add("no participant rows");
            if (!allocation.IsValid) allocation.entries.Clear();
            return allocation;
        }

        void ParseRow(string line, int lineNumber)
        {
            string[] cells = line.Split(',');
            if (cells.Length != 3)
            {
                errors.Add($"line {lineNumber}: expected 3 columns, found {cells.Length}");
                return;
            }
            string code = NormalizeCode(cells[0]);
            string conditionText = cells[1].Trim();
            string stratum = cells[2].Trim().ToLowerInvariant();
            if (!IsValidCode(code)) errors.Add($"line {lineNumber}: code '{cells[0].Trim()}' (letters, digits and '-' only)");
            else if (entries.ContainsKey(code)) errors.Add($"line {lineNumber}: {code} appears twice");
            if (conditionText != "1" && conditionText != "2" && conditionText != "3")
                errors.Add($"line {lineNumber}: condition '{conditionText}' (must be 1, 2 or 3)");
            if (Array.IndexOf(Strata, stratum) < 0)
                errors.Add($"line {lineNumber}: stratum '{cells[2].Trim()}' (must be {string.Join(" or ", Strata)})");
            if (errors.Count == 0)
                entries[code] = new Entry(code, (Condition)int.Parse(conditionText), stratum);
        }

        public bool TryGet(string code, out Entry entry) => entries.TryGetValue(NormalizeCode(code), out entry);
    }
}
