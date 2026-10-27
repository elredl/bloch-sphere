using System;
using System.IO;
using NUnit.Framework;
using Study.Lesson;
using Study.Quantum;

namespace Study.Tests
{
    public class EmbodiedPauseTests
    {
        const string TimelinePath = "Assets/Study/Data/lesson_timeline.json";
        const string FeedbackPath = "Assets/Study/Data/pause_feedback.json";
        static readonly double Deg = Math.PI / 180;

        static LessonTimeline Timeline() => LessonTimeline.FromJson(File.ReadAllText(TimelinePath));
        static PauseFeedback Feedback() => PauseFeedback.FromJson(File.ReadAllText(FeedbackPath));

        // The hand on the tip of the start state, turned by the given angle about the gate axis.
        static BlochVector Hand(Qubit start, Gate gate, double degrees) => start.Bloch.RotatedAbout(gate.Axis, degrees * Deg);

        static RatchetRotation Ratchet(Qubit start, Gate gate, double handDegreesAtGrab = 0)
        {
            var r = new RatchetRotation(start, gate, 10 * Deg);
            r.Begin(Hand(start, gate, handDegreesAtGrab));
            return r;
        }

        // Moves the hand in small steps, as a frame loop would. Returns the number of haptic ticks.
        static int Sweep(RatchetRotation r, Qubit start, Gate gate, double fromDegrees, double toDegrees)
        {
            int ticks = 0;
            int steps = (int)Math.Ceiling(Math.Abs(toDegrees - fromDegrees));
            for (int i = 1; i <= steps; i++)
                if (r.Move(Hand(start, gate, fromDegrees + (toDegrees - fromDegrees) * i / steps))) ticks++;
            return ticks;
        }

        [Test]
        public void Ratchet_FollowsTheHandForward_AlongTheGatesOwnArc()
        {
            foreach (var task in Timeline().pauseTasks)
            {
                Qubit start = Qubit.FromName(task.startState);
                Gate gate = Gate.FromName(task.gate);
                var r = Ratchet(start, gate);
                double half = gate.Angle / Deg / 2;
                Sweep(r, start, gate, 0, half);
                Assert.AreEqual(0.5, r.Fraction, 1e-6, task.id);
                // The state the ratchet asks for is the scripted arc's state at the same fraction.
                Qubit viaRatchet = GateArc.StateAt(start, gate, r.Fraction);
                Assert.AreEqual(0, BlochVector.AngleBetween(viaRatchet.Bloch, Hand(start, gate, half)), 1e-6, task.id);
            }
        }

        [Test]
        public void Ratchet_NeverOvershoots_AndCompletesOnRelease()
        {
            Qubit start = Qubit.Zero;
            Gate gate = Gate.FromName("Ry(90)");
            var r = Ratchet(start, gate);
            Sweep(r, start, gate, 0, 140);
            Assert.AreEqual(1.0, r.Fraction, 1e-9);
            Assert.IsTrue(r.End());
        }

        [Test]
        public void Ratchet_HoldsAgainstBackwardMotion_WithHapticTicks()
        {
            Qubit start = Qubit.Plus;
            Gate gate = Gate.FromName("Rz(90)");
            var r = Ratchet(start, gate);
            Sweep(r, start, gate, 0, 50);
            int ticks = Sweep(r, start, gate, 50, 20);
            Assert.AreEqual(50 * Deg, r.Angle, 1e-6, "vector stays at the furthest point");
            Assert.AreEqual(3, ticks, "one tick per 10 deg of backward pull");
            Sweep(r, start, gate, 20, 45);
            Assert.AreEqual(50 * Deg, r.Angle, 1e-6, "forward again does nothing until the hand passes the vector");
            Sweep(r, start, gate, 45, 70);
            Assert.AreEqual(70 * Deg, r.Angle, 1e-6);
        }

        [Test]
        public void Ratchet_ReleaseOutsideTolerance_DoesNotComplete_AndRegrabDoesNotJump()
        {
            Qubit start = Qubit.Zero;
            Gate gate = Gate.FromName("Ry(60)");
            var r = Ratchet(start, gate);
            Sweep(r, start, gate, 0, 45);
            Assert.IsFalse(r.End(), "15 deg short is outside the 10 deg tolerance");
            // Regrab with the hand somewhere else entirely: the vector must not move until the hand does.
            r.Begin(Hand(start, gate, -80));
            Assert.AreEqual(45 * Deg, r.Angle, 1e-9);
            Sweep(r, start, gate, -80, -72);
            Assert.AreEqual(53 * Deg, r.Angle, 1e-6);
            Assert.IsTrue(r.End(), "7 deg short is inside the tolerance");
        }

        [Test]
        public void Ratchet_WorksForH_WhoseAxisIsTilted()
        {
            Qubit start = Qubit.Plus;
            Gate gate = Gate.H;
            var r = Ratchet(start, gate);
            Sweep(r, start, gate, 0, 180);
            Assert.AreEqual(1.0, r.Fraction, 1e-6);
            Assert.IsTrue(GateArc.StateAt(start, gate, r.Fraction).IsSameStateAs(Qubit.Zero, 1e-9));
        }

        [Test]
        public void RealFeedback_IsValidAgainstTheTimeline()
        {
            var errors = Feedback().Validate(Timeline());
            Assert.IsEmpty(errors, string.Join("\n", errors));
        }

        [Test]
        public void RealFeedback_EveryAnswerGetsText_WithTheRightType()
        {
            PauseFeedback f = Feedback();
            foreach (var task in Timeline().pauseTasks)
            {
                Assert.AreEqual("no_prediction", f.For(task, null).Type, task.id);
                foreach (string choice in task.choices)
                {
                    PauseFeedback.Choice c = f.For(task, choice);
                    Assert.IsFalse(string.IsNullOrWhiteSpace(c.Text), $"{task.id} {choice}");
                    if (choice == task.correct) Assert.AreEqual(PauseFeedback.Tone.Correct, c.Tone);
                    else Assert.AreEqual(PauseFeedback.Tone.Incorrect, c.Tone, $"{task.id} {choice}");
                }
            }
        }

        [Test]
        public void RealFeedback_MapsTheSpecMisconceptions()
        {
            PauseFeedback f = Feedback();
            LessonTimeline t = Timeline();
            Assert.AreEqual("rotation_does_not_change_outcome", f.For(t.Task("pause1"), "100").Id);
            Assert.AreEqual("quarter_turn_reaches_one", f.For(t.Task("pause1"), "0").Id);
            Assert.AreEqual("generic_incorrect", f.For(t.Task("pause1"), "75").Id);
            Assert.AreEqual("polar_azimuthal_confusion", f.For(t.Task("pause2"), "100").Id);
            Assert.AreEqual("polar_azimuthal_confusion", f.For(t.Task("pause2"), "0").Id);
            Assert.AreEqual("generic_incorrect", f.For(t.Task("pause2"), "75").Id);
            Assert.AreEqual("h_always_undoes_h", f.For(t.Task("pause3"), "100").Id);
            Assert.AreEqual("h_always_creates_superposition", f.For(t.Task("pause3"), "50").Id);
            Assert.AreEqual("generic_incorrect", f.For(t.Task("pause3"), "25").Id);
            Assert.AreEqual("any_superposition_is_50_50", f.For(t.Task("pause4"), "50").Id);
            Assert.AreEqual("probability_linear_in_angle", f.For(t.Task("pause4"), "67").Id);
            Assert.AreEqual("only_equator_is_superposition", f.For(t.Task("pause4"), "100").Id);
        }

        [Test]
        public void Placement_PointTasksNeedTheEndState_HeightTasksOnlyTheTilt()
        {
            LessonTimeline t = Timeline();
            // Pause 2 (point): S takes |+i> to |->; |+> is the opposite point.
            Assert.AreEqual(0, PauseFeedback.PlacementErrorDegrees(t.Task("pause2"), Qubit.Minus.Bloch), 1e-6);
            Assert.AreEqual(180, PauseFeedback.PlacementErrorDegrees(t.Task("pause2"), Qubit.Plus.Bloch), 1e-6);
            // Pause 1 (height): any equator point is right; the side is not asked.
            Assert.AreEqual(0, PauseFeedback.PlacementErrorDegrees(t.Task("pause1"), Qubit.MinusI.Bloch), 1e-6);
            Assert.AreEqual(90, PauseFeedback.PlacementErrorDegrees(t.Task("pause1"), Qubit.Zero.Bloch), 1e-6);
            // Pause 4 (height): 60 deg from |0>; the equator is 30 deg off.
            Assert.AreEqual(30, PauseFeedback.PlacementErrorDegrees(t.Task("pause4"), Qubit.Plus.Bloch), 1e-6);
        }

        [Test]
        public void Placement_NearestNamedState_OnlyWithinTheSnapAngle()
        {
            Assert.AreEqual("+i", PauseFeedback.NearestNamedState(Qubit.FromBlochAngles(90 * Deg, 80 * Deg).Bloch, 30));
            Assert.IsNull(PauseFeedback.NearestNamedState(Qubit.FromBlochAngles(120 * Deg, 45 * Deg).Bloch, 30));
        }

        [Test]
        public void RealFeedback_MapsThePlacementMisconceptions()
        {
            PauseFeedback f = Feedback();
            LessonTimeline t = Timeline();
            Assert.AreEqual("wrong_rotation_direction", f.ForPlacement(t.Task("pause2"), true, false, "+").Id);
            Assert.AreEqual("s_taken_as_half_turn", f.ForPlacement(t.Task("pause2"), true, false, "-i").Id);
            Assert.AreEqual("h_always_undoes_h", f.ForPlacement(t.Task("pause3"), true, false, "0").Id);
            Assert.AreEqual("sixty_reaches_equator", f.ForPlacement(t.Task("pause4"), true, false, "+i").Id);
            Assert.AreEqual("generic_incorrect", f.ForPlacement(t.Task("pause3"), true, false, null).Id);
            Assert.AreEqual("correct", f.ForPlacement(t.Task("pause1"), true, true, "+").Id);
            Assert.AreEqual("no_prediction", f.ForPlacement(t.Task("pause1"), false, false, null).Type);
        }

        [Test]
        public void TimeoutArc_AlwaysFinishesBeforeTheMeasurementDemo()
        {
            LessonTimeline.PauseSchedule p = Timeline().pauseSchedule;
            Assert.LessOrEqual(p.rotationDeadline + p.timeoutArcMaxDuration, p.measurementDemoStart);
            Assert.Less(p.placementDeadline, p.predictionDeadline);
            Assert.Less(p.predictionDeadline, p.rotationDeadline);
        }
    }
}
