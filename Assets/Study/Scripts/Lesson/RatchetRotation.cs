using System;
using Study.Quantum;

namespace Study.Lesson
{
    // The Condition 3 grab, as plain math so it can be unit-tested.
    //
    // The learner's hand point (in Bloch coordinates, centered on the sphere) is
    // projected onto the plane perpendicular to the gate axis; its angle about the
    // axis (right-hand rule, the gate's own sense) drives the vector. The grab is a
    // ratchet: the vector follows the hand forward, stays put when the hand goes
    // back, and stops at the gate's angle, so it can only trace the scripted arc.
    // Grabbing never makes the vector jump: the hand's angle is measured from
    // where it was when the grab started.
    public sealed class RatchetRotation
    {
        // Hand points closer than this to the axis (fraction of the radius) give no
        // reliable angle; those frames are skipped.
        const double AxisDeadZone = 0.12;
        // Haptic tick after this much backward hand motion (radians), and again per step.
        public static readonly double WrongWayStep = 10 * Math.PI / 180;

        public readonly Gate Gate;
        public readonly double Tolerance; // radians below the target that still count on release

        readonly BlochVector axis;
        readonly BlochVector reference; // start state's component perpendicular to the axis, unit

        public double Angle { get; private set; } // radians turned so far, 0..Gate.Angle
        public double Fraction => Angle / Gate.Angle;
        public bool IsGrabbing { get; private set; }
        public bool WithinTolerance => Angle >= Gate.Angle - Tolerance - 1e-9;

        double handAngle;    // unwrapped hand angle about the axis
        double lastRaw;      // last raw hand angle, for unwrapping
        double grabOffset;   // vector angle minus hand angle at grab start
        double backward;     // hand travel the wrong way since the last tick

        public RatchetRotation(Qubit start, Gate gate, double toleranceRadians)
        {
            Gate = gate;
            Tolerance = toleranceRadians;
            axis = gate.Axis;
            BlochVector v = start.Bloch;
            BlochVector perpendicular = v - axis * BlochVector.Dot(v, axis);
            if (perpendicular.Length < 1e-6) throw new ArgumentException($"start state lies on the axis of {gate.Name}: nothing to rotate");
            reference = perpendicular.Normalized();
        }

        // Signed angle of a point about the axis, from the reference, in (-pi, pi]. NaN near the axis.
        public double RawAngle(BlochVector point)
        {
            BlochVector p = point - axis * BlochVector.Dot(point, axis);
            if (p.Length < AxisDeadZone) return double.NaN;
            return Math.Atan2(BlochVector.Dot(axis, BlochVector.Cross(reference, p)), BlochVector.Dot(reference, p));
        }

        public void Begin(BlochVector hand)
        {
            double raw = RawAngle(hand);
            if (double.IsNaN(raw)) raw = Angle; // grabbed right on the axis: treat as grabbing the tip
            lastRaw = raw;
            handAngle = raw;
            grabOffset = Angle - handAngle;
            backward = 0;
            IsGrabbing = true;
        }

        // Feeds one frame of hand motion. Returns true when a wrong-way haptic tick is due.
        public bool Move(BlochVector hand)
        {
            if (!IsGrabbing) return false;
            double raw = RawAngle(hand);
            if (double.IsNaN(raw)) return false;
            double delta = Wrap(raw - lastRaw);
            lastRaw = raw;
            handAngle += delta;

            double wanted = handAngle + grabOffset;
            if (wanted > Angle) Angle = Math.Min(wanted, Gate.Angle);

            // Only count backward motion that the ratchet is actually holding against.
            bool tick = false;
            if (delta < 0 && wanted < Angle)
            {
                backward -= delta;
                if (backward >= WrongWayStep - 1e-9)
                {
                    backward = 0;
                    tick = true;
                }
            }
            else if (delta > 0) backward = 0;
            return tick;
        }

        // Returns true if the release completes the gate (within tolerance of the target).
        public bool End()
        {
            IsGrabbing = false;
            return WithinTolerance;
        }

        static double Wrap(double a)
        {
            while (a > Math.PI) a -= 2 * Math.PI;
            while (a <= -Math.PI) a += 2 * Math.PI;
            return a;
        }
    }
}
