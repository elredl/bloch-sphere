using System;

namespace Study.Quantum
{
    // A point on (or direction in) the Bloch ball, in physics coordinates:
    // +z is |0>, -z is |1>, +x is |+>, +y is |+i>. Doubles, so the math
    // stays exact enough for tests; the Unity side converts to Vector3.
    public readonly struct BlochVector
    {
        public readonly double X;
        public readonly double Y;
        public readonly double Z;

        public BlochVector(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public static readonly BlochVector UnitX = new BlochVector(1, 0, 0);
        public static readonly BlochVector UnitY = new BlochVector(0, 1, 0);
        public static readonly BlochVector UnitZ = new BlochVector(0, 0, 1);

        public double Length => Math.Sqrt(X * X + Y * Y + Z * Z);

        public BlochVector Normalized()
        {
            double length = Length;
            if (length < 1e-12) throw new InvalidOperationException("Cannot normalize a zero vector.");
            return new BlochVector(X / length, Y / length, Z / length);
        }

        public static double Dot(BlochVector a, BlochVector b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

        public static BlochVector Cross(BlochVector a, BlochVector b) => new BlochVector(
            a.Y * b.Z - a.Z * b.Y,
            a.Z * b.X - a.X * b.Z,
            a.X * b.Y - a.Y * b.X);

        // Angle in radians between two directions, safe against rounding just past +/-1.
        public static double AngleBetween(BlochVector a, BlochVector b)
        {
            double cos = Dot(a, b) / (a.Length * b.Length);
            return Math.Acos(Math.Max(-1.0, Math.Min(1.0, cos)));
        }

        // Rodrigues' rotation (right-hand rule about a unit axis). Used by tests to
        // cross-check the state model and by visuals that need the arc geometry.
        public BlochVector RotatedAbout(BlochVector unitAxis, double angle)
        {
            double cos = Math.Cos(angle);
            double sin = Math.Sin(angle);
            BlochVector cross = Cross(unitAxis, this);
            double dot = Dot(unitAxis, this);
            return new BlochVector(
                X * cos + cross.X * sin + unitAxis.X * dot * (1 - cos),
                Y * cos + cross.Y * sin + unitAxis.Y * dot * (1 - cos),
                Z * cos + cross.Z * sin + unitAxis.Z * dot * (1 - cos));
        }

        public static BlochVector operator +(BlochVector a, BlochVector b) => new BlochVector(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static BlochVector operator -(BlochVector a, BlochVector b) => new BlochVector(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static BlochVector operator *(BlochVector a, double s) => new BlochVector(a.X * s, a.Y * s, a.Z * s);

        public override string ToString() => $"({X:F4}, {Y:F4}, {Z:F4})";
    }
}
