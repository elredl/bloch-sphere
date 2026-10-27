using System;
using System.IO;
using NUnit.Framework;
using Study.Lesson;
using Study.Quantum;

namespace Study.Tests
{
    public class LessonTimelineTests
    {
        const string TimelinePath = "Assets/Study/Data/lesson_timeline.json";

        static LessonTimeline Load() => LessonTimeline.FromJson(File.ReadAllText(TimelinePath));

        [Test]
        public void RealTimeline_IsValid()
        {
            var errors = Load().Validate();
            Assert.IsEmpty(errors, string.Join("\n", errors));
        }

        [Test]
        public void RealTimeline_HasFourEqualPauses_AndFifteenMinutes()
        {
            LessonTimeline t = Load();
            Assert.AreEqual(4, t.segments.Length);
            foreach (var s in t.segments) Assert.AreEqual(60.0, s.pauseEnd - s.pauseStart, 1e-9, s.id);
            Assert.AreEqual(900.0, t.lessonDuration, 1e-9);
            Assert.AreEqual(t.lessonDuration, t.segments[3].pauseEnd, 1e-9);
        }

        [Test]
        public void RealTimeline_ScriptedEventsReachEachPauseStartState()
        {
            LessonTimeline t = Load();
            var states = t.StatesAtPauseOnsets();
            for (int i = 0; i < 4; i++)
            {
                var task = t.Task(t.segments[i].task);
                Assert.IsTrue(states[i].IsSameStateAs(Qubit.FromName(task.startState), 1e-9),
                    $"{task.id}: reached {states[i].Bloch}, expected |{task.startState}>");
            }
        }

        [Test]
        public void RealTimeline_PauseTasksMatchTheSpec()
        {
            LessonTimeline t = Load();
            // Correct answers follow from the physics, not from the data file.
            Assert.AreEqual(0.5, Gate.FromName(t.Task("pause1").gate).Apply(Qubit.Zero).ProbabilityZero, 1e-9);
            // End states (question 1, placement) and P(0) (question 2).
            Assert.IsTrue(Gate.FromName(t.Task("pause2").gate).Apply(Qubit.PlusI).IsSameStateAs(Qubit.Minus));
            Assert.IsTrue(Gate.FromName(t.Task("pause3").gate).Apply(Qubit.Minus).IsSameStateAs(Qubit.One));
            Assert.AreEqual(0.5, Gate.FromName(t.Task("pause2").gate).Apply(Qubit.PlusI).ProbabilityZero, 1e-9);
            Assert.AreEqual(0.0, Gate.FromName(t.Task("pause3").gate).Apply(Qubit.Minus).ProbabilityZero, 1e-9);
            Assert.AreEqual(0.75, Gate.FromName(t.Task("pause4").gate).Apply(Qubit.Zero).ProbabilityZero, 1e-9);
            Assert.AreEqual("50", t.Task("pause1").correct);
            Assert.AreEqual("50", t.Task("pause2").correct);
            Assert.AreEqual("0", t.Task("pause3").correct);
            Assert.AreEqual("75", t.Task("pause4").correct);
        }

        // Seeds are chosen so 100 shots land exactly on the true probability and the
        // first 5 (the slow, fully animated shots) show both outcomes. An unlucky seed
        // (e.g. 65/35 for a 75 % state) would teach the wrong thing.
        [Test]
        public void MeasurementSeeds_LandOnTheTrueProbability()
        {
            LessonTimeline t = Load();
            int demos = 0;
            foreach (var e in t.events)
            {
                if (e.type != "measure_demo") continue;
                AssertSeed(Qubit.FromName(e.state), e.shots, e.seed, $"event at {e.t}s");
                demos++;
            }
            var pause4 = t.Task("pause4");
            Qubit prepared = Gate.FromName(pause4.gate).Apply(Qubit.FromName(pause4.startState));
            AssertSeed(prepared, pause4.demoShots, pause4.demoSeed, "pause4");
            Assert.AreEqual(1, demos, "expected one narrated measurement demo (segment 4)");
        }

        static void AssertSeed(Qubit q, int shots, ulong seed, string where)
        {
            int[] outcomes = Measurement.Shots(q, shots, seed);
            int zeros = Measurement.CountZeros(outcomes);
            Assert.AreEqual(Math.Round(q.ProbabilityZero * shots), zeros, 1, $"{where}: seed {seed} gives {zeros}/{shots} zeros");
            int firstFiveZeros = 0;
            for (int i = 0; i < 5; i++) if (outcomes[i] == 0) firstFiveZeros++;
            Assert.That(firstFiveZeros, Is.InRange(1, 4), $"{where}: first five shots should show both outcomes");
        }

        [Test]
        public void Validate_CatchesUnequalPause()
        {
            LessonTimeline t = Load();
            t.segments[1].pauseEnd += 5;
            t.segments[2].start += 5; // keep the segments contiguous so only the pause rule fires
            Assert.That(t.Validate(), Has.Some.Contains("all pauses must be"));
        }

        [Test]
        public void Validate_CatchesEventInsideAPause()
        {
            LessonTimeline t = Load();
            t.events[1].t = t.segments[0].pauseStart + 10; // move the second event into pause 1
            Assert.That(t.Validate(), Has.Some.Contains("overlaps the pause"));
        }

        [Test]
        public void Validate_CatchesArcRunningIntoAPause()
        {
            LessonTimeline t = Load();
            var arc = Array.Find(t.events, e => e.type == "apply_gate");
            arc.duration = 500;
            Assert.That(t.Validate(), Has.Some.Contains("overlaps the pause"));
        }

        [Test]
        public void StartStateReplay_CatchesAWrongStartState()
        {
            LessonTimeline t = Load();
            t.Task("pause2").startState = "-";
            var states = t.StatesAtPauseOnsets();
            Assert.IsFalse(states[1].IsSameStateAs(Qubit.Minus));
        }

        [Test]
        public void ConditionTable_MatchesTheSpec()
        {
            var c1 = new ConditionConfig(Condition.Desktop2D);
            var c2 = new ConditionConfig(Condition.PassiveVR);
            var c3 = new ConditionConfig(Condition.EmbodiedVR);
            Assert.IsFalse(c1.UsesHeadset); Assert.IsTrue(c2.UsesHeadset); Assert.IsTrue(c3.UsesHeadset);
            foreach (var c in new[] { c1, c2 })
            {
                Assert.IsFalse(c.LearnerRotates);
                Assert.IsFalse(c.ShowsPredictionPrompt);
                Assert.IsFalse(c.ShowsFeedback);
                Assert.IsFalse(c.DimsAtPauses);
            }
            Assert.IsTrue(c3.LearnerRotates && c3.ShowsPredictionPrompt && c3.ShowsFeedback && c3.DimsAtPauses);
        }
    }
}
