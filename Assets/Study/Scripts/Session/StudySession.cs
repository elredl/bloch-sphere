using System;
using System.Globalization;
using System.IO;
using Study.Lesson;
using UnityEngine;

namespace Study.Session
{
    // The running session: who (code only), which condition and stratum, and the CSV log
    // every StudyEvents event goes to. Started by the launch screen (or, in the Editor, by
    // a dev run of the Lesson scene) and kept across the scene change.
    //
    // Data folder: Application.persistentDataPath in builds (Quest:
    // /sdcard/Android/data/<package>/files), DevData/ in the project in the Editor, so
    // dry runs never mix with real sessions. It holds allocation.csv, the session files
    // and session_index.csv, a list of every session started on this device. pull_data
    // removes the session files but keeps the index, so a code can't be reused later
    // in the study either.
    public sealed class StudySession
    {
        public const string AllocationFile = "allocation.csv";
        public const string IndexFile = "session_index.csv";
        const string IndexHeader = "participant_code,condition,stratum,source,file,started_utc";

        public static StudySession Current { get; private set; }

        public string Code { get; }
        public Condition Condition { get; }
        public string Stratum { get; }
        public string Source { get; } // allocation, manual or dev
        public SessionLog Log { get; }
        public bool Finished { get; private set; }

        readonly double startRealtime;

        public double SessionSeconds => Time.realtimeSinceStartupAsDouble - startRealtime;

        public static string DataDirectory => Application.isEditor
            ? Path.Combine(Path.GetDirectoryName(Application.dataPath), "DevData")
            : Application.persistentDataPath;

        StudySession(string code, Condition condition, string stratum, string source, string directory)
        {
            Code = code;
            Condition = condition;
            Stratum = stratum;
            Source = source;
            Log = new SessionLog(directory, code, (int)condition, stratum, DateTime.Now);
            startRealtime = Time.realtimeSinceStartupAsDouble;
        }

        // Domain reload may be off in the Editor: never carry a session into the next Play.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Current = null;
            ConditionConfig.Current = null;
        }

        public static StudySession Start(string code, Condition condition, string stratum, string source)
        {
            if (Current != null && !Current.Finished) throw new InvalidOperationException("a session is already running");
            string directory = DataDirectory;
            var session = new StudySession(code, condition, stratum, source, directory);
            AppendIndex(directory, session);
            Current = session;
            ConditionConfig.Current = new ConditionConfig(condition);
            StudyEvents.Logged += session.OnLogged;
            Application.quitting += session.OnQuitting;
            StudyEvents.Log("session_start",
                $"source={source} app={Application.version} unity={Application.unityVersion} " +
                $"platform={Application.platform} device={Sanitize(SystemInfo.deviceModel)}");
            Debug.Log($"[Session] {code} C{(int)condition} {stratum} ({source}) logging to {session.Log.FilePath}");
            return session;
        }

        // status: completed or withdrawn (abort). Logs session_end and closes the file.
        public void Finish(string status)
        {
            if (Finished) return;
            StudyEvents.Log("session_end", $"status={status} events={Log.EventCount + 1}");
            Finished = true;
            StudyEvents.Logged -= OnLogged;
            Application.quitting -= OnQuitting;
            Log.Dispose();
            Debug.Log($"[Session] {Code} ended ({status}), {Log.EventCount} events in {Log.FileName}");
        }

        void OnQuitting() => Finish("quit");

        void OnLogged(double lessonTime, string type, string fields)
        {
            try
            {
                Log.Write(DateTime.UtcNow, SessionSeconds, lessonTime, type, fields);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Session] could not write {type} to {Log.FileName}: {e.Message}");
            }
        }

        // Why a code can't start a session, or null if it can: a session file for it is still on
        // the device, or the index shows it was used before (spec: refuse codes with a session).
        public static string AlreadyUsed(string code, string directory)
        {
            if (Directory.Exists(directory) && Directory.GetFiles(directory, code + "_C*.csv").Length > 0)
                return $"{code} already has a session file on this device";
            string index = Path.Combine(directory, IndexFile);
            if (File.Exists(index))
                foreach (string line in File.ReadAllLines(index))
                    if (line.StartsWith(code + ",", StringComparison.Ordinal))
                        return $"{code} already had a session on this device ({IndexFile})";
            return null;
        }

        static void AppendIndex(string directory, StudySession s)
        {
            string path = Path.Combine(directory, IndexFile);
            string line = string.Join(",", s.Code, ((int)s.Condition).ToString(CultureInfo.InvariantCulture), s.Stratum, s.Source,
                s.Log.FileName, DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture));
            if (!File.Exists(path)) File.WriteAllText(path, IndexHeader + "\n");
            File.AppendAllText(path, line + "\n");
        }

        static string Sanitize(string s) => (s ?? "").Replace(' ', '_').Replace('=', '_');
    }
}
