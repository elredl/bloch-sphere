using System.IO;
using NUnit.Framework;
using Study.Lesson;

namespace Study.Tests
{
    public class MeasurementDemoTests
    {
        [Test]
        public void ShotTimes_SlowShotsAtOneHz_ThenFastFill_InsideTheWindow()
        {
            double[] times = MeasurementDemo.ShotTimes(100);
            for (int i = 0; i < MeasurementDemo.SlowShots; i++)
                Assert.AreEqual(MeasurementDemo.LeadIn + i, times[i], 1e-9, $"slow shot {i}");
            for (int i = 1; i < times.Length; i++) Assert.Greater(times[i], times[i - 1], "shot times must increase");
            Assert.AreEqual(MeasurementDemo.Duration, times[times.Length - 1], 1e-9, "last shot lands at the end of the fill");

            // A slow shot's fade-back must finish before the next shot, so every flash
            // is a single, separate pulse (at most 1 Hz, far below the 3 Hz limit).
            Assert.Less(MeasurementDemo.SnapHold + MeasurementDemo.ReprepareTime, MeasurementDemo.SlowShotInterval);
        }

        [Test]
        public void Demo_FitsTheTimelineWindow()
        {
            var t = LessonTimeline.FromJson(File.ReadAllText("Assets/Study/Data/lesson_timeline.json"));
            Assert.LessOrEqual(MeasurementDemo.Duration, t.pauseSchedule.measurementDemoEnd - t.pauseSchedule.measurementDemoStart);
        }

        [Test]
        public void Validate_RejectsShowingThePanelWithoutADemo()
        {
            var t = LessonTimeline.FromJson(File.ReadAllText("Assets/Study/Data/lesson_timeline.json"));
            System.Array.Find(t.events, e => e.type == "hide" && e.target == "measurement_panel").type = "show";
            Assert.That(t.Validate(), Has.Some.Contains("shown by measure_demo"));
        }
    }
}
