using System;
using System.IO;
using NUnit.Framework;
using Study.Lesson;
using Study.Session;

namespace Study.Tests
{
    public class SessionDataTests
    {
        const string Good = "participant_code,condition,stratum\nP01,1,novice\np02, 3 ,Math_Literate\n\n# comment\nP03,2,novice\n";

        [Test]
        public void AllocationReadsRowsAndNormalizesCodes()
        {
            Allocation a = Allocation.Parse(Good);
            Assert.IsTrue(a.IsValid, string.Join("; ", a.Errors));
            Assert.AreEqual(3, a.Count);
            Assert.IsTrue(a.TryGet("P02", out Allocation.Entry e));
            Assert.AreEqual(Condition.EmbodiedVR, e.Condition);
            Assert.AreEqual("math_literate", e.Stratum);
            Assert.IsTrue(a.TryGet(" p01 ", out e));
            Assert.AreEqual(Condition.Desktop2D, e.Condition);
            Assert.IsFalse(a.TryGet("P04", out _));
        }

        [Test]
        public void AllocationAcceptsWindowsLineEndingsAndBom()
        {
            Allocation a = Allocation.Parse("﻿participant_code,condition,stratum\r\nP01,2,novice\r\n");
            Assert.IsTrue(a.IsValid, string.Join("; ", a.Errors));
            Assert.AreEqual(1, a.Count);
        }

        [TestCase("participant,condition,stratum\nP01,1,novice\n", "header")]
        [TestCase("participant_code,condition,stratum\nP01,4,novice\n", "condition")]
        [TestCase("participant_code,condition,stratum\nP01,1,expert\n", "stratum")]
        [TestCase("participant_code,condition,stratum\nP01,1,novice\nP01,2,novice\n", "twice")]
        [TestCase("participant_code,condition,stratum\nP 01,1,novice\n", "code")]
        [TestCase("participant_code,condition,stratum\nP01,1\n", "columns")]
        [TestCase("participant_code,condition,stratum\n", "no participant rows")]
        [TestCase("", "empty")]
        public void AllocationRejectsTheWholeFileOnAnyError(string text, string expected)
        {
            Allocation a = Allocation.Parse(text);
            Assert.IsFalse(a.IsValid);
            Assert.AreEqual(0, a.Count, "no rows are usable from a rejected file");
            StringAssert.Contains(expected, string.Join("; ", a.Errors));
        }

        [Test]
        public void SessionFileNameHasCodeConditionAndLocalTime()
        {
            Assert.AreEqual("P07_C3_20261020-1432.csv", SessionLog.FileNameFor("P07", 3, new DateTime(2026, 10, 20, 14, 32, 59)));
        }

        [Test]
        public void SessionLogWritesHeaderAndQuotedRowsAndNeverOverwrites()
        {
            string dir = Path.Combine(Path.GetTempPath(), "study_log_test_" + Guid.NewGuid().ToString("N"));
            var time = new DateTime(2026, 10, 20, 14, 32, 0);
            try
            {
                using (var log = new SessionLog(dir, "P07", 3, "novice", time))
                {
                    log.Write(new DateTime(2026, 10, 20, 12, 32, 1, 250, DateTimeKind.Utc), 1.25, double.NaN, "session_start", "a=1 b=2");
                    log.Write(new DateTime(2026, 10, 20, 12, 33, 0, DateTimeKind.Utc), 60, 12.5, "note", "text=\"x,y\"");
                    Assert.AreEqual(2, log.EventCount);
                    // Readable while open (flushed after every row).
                    string[] live = File.ReadAllLines(log.FilePath);
                    Assert.AreEqual(3, live.Length);
                }
                string[] lines = File.ReadAllLines(Path.Combine(dir, "P07_C3_20261020-1432.csv"));
                Assert.AreEqual(SessionLog.Header, lines[0]);
                Assert.AreEqual("2026-10-20T12:32:01.250Z,1.250,,P07,3,novice,session_start,a=1 b=2", lines[1]);
                Assert.AreEqual("2026-10-20T12:33:00.000Z,60.000,12.500,P07,3,novice,note,\"text=\"\"x,y\"\"\"", lines[2]);
                Assert.Throws<IOException>(() => new SessionLog(dir, "P07", 3, "novice", time));
                Assert.AreEqual(3, File.ReadAllLines(Path.Combine(dir, "P07_C3_20261020-1432.csv")).Length, "the existing file is untouched");
            }
            finally
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
        }

        [Test]
        public void ACodeWithASessionFileOrIndexEntryIsRefused()
        {
            string dir = Path.Combine(Path.GetTempPath(), "study_index_test_" + Guid.NewGuid().ToString("N"));
            try
            {
                Assert.IsNull(StudySession.AlreadyUsed("P07", dir), "no folder yet");
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "P070_C1_20261020-1000.csv"), "");
                Assert.IsNull(StudySession.AlreadyUsed("P07", dir), "P070 is a different code");
                File.WriteAllText(Path.Combine(dir, "P07_C3_20261020-1432.csv"), "");
                StringAssert.Contains("session file", StudySession.AlreadyUsed("P07", dir));
                File.WriteAllText(Path.Combine(dir, StudySession.IndexFile),
                    "participant_code,condition,stratum,source,file,started_utc\nP08,2,novice,allocation,P08_C2_x.csv,t\n");
                StringAssert.Contains(StudySession.IndexFile, StudySession.AlreadyUsed("P08", dir));
                Assert.IsNull(StudySession.AlreadyUsed("P0", dir));
            }
            finally
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
        }
    }
}
