using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace Study.Session
{
    // One CSV file per session (spec, Telemetry): one row per event, flushed to disk
    // after every row so a crash or a pulled battery loses nothing already logged.
    // Never overwrites: the file is created with CreateNew. No names or other
    // identifying information, only the participant code.
    //
    //   utc          wall-clock time of the event, ISO 8601
    //   session_s    seconds since the session started (launch screen confirmed)
    //   lesson_s     lesson time (narration clock), blank outside the lesson
    //   event        event type; fields: "key=value key=value"
    public sealed class SessionLog : IDisposable
    {
        public const string Header = "utc,session_s,lesson_s,participant,condition,stratum,event,fields";

        readonly FileStream stream;
        readonly StreamWriter writer;
        readonly string code, stratum;
        readonly int condition;

        public string FilePath { get; }
        public string FileName => Path.GetFileName(FilePath);
        public int EventCount { get; private set; }
        public bool IsOpen { get; private set; }

        // e.g. P07_C3_20261020-1432.csv (local time, so it matches the session day).
        public static string FileNameFor(string code, int condition, DateTime localTime) =>
            $"{code}_C{condition}_{localTime.ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture)}.csv";

        // Throws IOException if the file already exists.
        public SessionLog(string directory, string code, int condition, string stratum, DateTime localTime)
        {
            this.code = code;
            this.condition = condition;
            this.stratum = stratum;
            Directory.CreateDirectory(directory);
            FilePath = Path.Combine(directory, FileNameFor(code, condition, localTime));
            stream = new FileStream(FilePath, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
            writer = new StreamWriter(stream, new UTF8Encoding(false));
            IsOpen = true;
            WriteLine(Header);
        }

        // lessonSeconds: NaN outside the lesson (written as a blank cell).
        public void Write(DateTime utc, double sessionSeconds, double lessonSeconds, string type, string fields)
        {
            if (!IsOpen) return;
            var row = new StringBuilder();
            row.Append(utc.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture)).Append(',');
            row.Append(sessionSeconds.ToString("F3", CultureInfo.InvariantCulture)).Append(',');
            if (!double.IsNaN(lessonSeconds)) row.Append(lessonSeconds.ToString("F3", CultureInfo.InvariantCulture));
            row.Append(',').Append(Cell(code));
            row.Append(',').Append(condition.ToString(CultureInfo.InvariantCulture));
            row.Append(',').Append(Cell(stratum));
            row.Append(',').Append(Cell(type));
            row.Append(',').Append(Cell(fields ?? ""));
            WriteLine(row.ToString());
            EventCount++;
        }

        void WriteLine(string line)
        {
            writer.Write(line);
            writer.Write('\n');
            writer.Flush();
            stream.Flush(true); // through the OS cache to the disk
        }

        // RFC 4180: quote cells holding a comma, quote or line break; double the quotes.
        public static string Cell(string value)
        {
            if (value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) < 0) return value;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        public void Dispose()
        {
            if (!IsOpen) return;
            IsOpen = false;
            writer.Dispose();
        }
    }
}
