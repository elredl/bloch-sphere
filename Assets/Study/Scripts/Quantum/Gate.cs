using System;
using System.Numerics;

namespace Study.Quantum
{
    // A single-qubit gate: its 2x2 unitary plus the Bloch-sphere rotation it
    // performs (unit axis and angle, right-hand rule). The matrix is what the
    // physics does; the axis and angle are what the animation draws. Tests check
    // that the two agree up to global phase for every gate.
    public sealed class Gate
    {
        public readonly string Name;
        public readonly Complex M00, M01, M10, M11;
        public readonly BlochVector Axis;
        public readonly double Angle;

        Gate(string name, Complex m00, Complex m01, Complex m10, Complex m11, BlochVector axis, double angle)
        {
            Name = name;
            M00 = m00; M01 = m01; M10 = m10; M11 = m11;
            Axis = axis.Normalized();
            Angle = angle;
        }

        public Qubit Apply(Qubit q) => new Qubit(
            M00 * q.Alpha + M01 * q.Beta,
            M10 * q.Alpha + M11 * q.Beta);

        static readonly double InvSqrt2 = 1.0 / Math.Sqrt(2.0);

        // Named gates use their textbook matrices (not the rotation form), so
        // amplitudes match what a learner would compute by hand.
        public static readonly Gate X = new Gate("X", 0, 1, 1, 0, BlochVector.UnitX, Math.PI);
        public static readonly Gate Y = new Gate("Y", 0, -Complex.ImaginaryOne, Complex.ImaginaryOne, 0, BlochVector.UnitY, Math.PI);
        public static readonly Gate Z = new Gate("Z", 1, 0, 0, -1, BlochVector.UnitZ, Math.PI);
        public static readonly Gate H = new Gate("H", InvSqrt2, InvSqrt2, InvSqrt2, -InvSqrt2, new BlochVector(1, 0, 1), Math.PI);
        public static readonly Gate S = new Gate("S", 1, 0, 0, Complex.ImaginaryOne, BlochVector.UnitZ, Math.PI / 2);
        public static readonly Gate T = new Gate("T", 1, 0, 0, Complex.FromPolarCoordinates(1, Math.PI / 4), BlochVector.UnitZ, Math.PI / 4);

        public static Gate Rx(double angle) => Rotation("Rx", BlochVector.UnitX, angle);
        public static Gate Ry(double angle) => Rotation("Ry", BlochVector.UnitY, angle);
        public static Gate Rz(double angle) => Rotation("Rz", BlochVector.UnitZ, angle);

        // U = cos(a/2) I - i sin(a/2) (n . sigma): rotates the Bloch vector by a about n.
        public static Gate Rotation(string name, BlochVector axis, double angle)
        {
            BlochVector n = axis.Normalized();
            double c = Math.Cos(angle / 2);
            double s = Math.Sin(angle / 2);
            return new Gate(name,
                new Complex(c, -s * n.Z), new Complex(-s * n.Y, -s * n.X),
                new Complex(s * n.Y, -s * n.X), new Complex(c, s * n.Z),
                n, angle);
        }

        // Looks up a gate by the names used in data files: X, Y, Z, H, S, T,
        // or Rx/Ry/Rz with an angle in degrees, e.g. "Ry(90)".
        public static Gate FromName(string name)
        {
            switch (name)
            {
                case "X": return X;
                case "Y": return Y;
                case "Z": return Z;
                case "H": return H;
                case "S": return S;
                case "T": return T;
            }
            if (name.Length > 4 && name[0] == 'R' && name[2] == '(' && name[name.Length - 1] == ')')
            {
                string degreesText = name.Substring(3, name.Length - 4);
                if (double.TryParse(degreesText, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out double degrees))
                {
                    double radians = degrees * Math.PI / 180.0;
                    switch (name[1])
                    {
                        case 'x': return Rx(radians);
                        case 'y': return Ry(radians);
                        case 'z': return Rz(radians);
                    }
                }
            }
            throw new ArgumentException($"Unknown gate '{name}'. Use X, Y, Z, H, S, T, Rx(deg), Ry(deg) or Rz(deg).");
        }

        public override string ToString() => $"{Name} (axis {Axis}, {Angle * 180 / Math.PI:F1} deg)";
    }
}
