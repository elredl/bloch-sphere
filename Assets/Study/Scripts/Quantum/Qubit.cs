using System;
using System.Numerics;

namespace Study.Quantum
{
    // A single-qubit pure state a|0> + b|1>. Always normalized on construction,
    // so nothing downstream can show a vector whose length isn't 1.
    public readonly struct Qubit
    {
        public readonly Complex Alpha; // amplitude of |0>
        public readonly Complex Beta;  // amplitude of |1>

        public Qubit(Complex alpha, Complex beta)
        {
            double norm = Math.Sqrt(alpha.Magnitude * alpha.Magnitude + beta.Magnitude * beta.Magnitude);
            if (norm < 1e-12) throw new ArgumentException("A qubit state cannot have zero norm.");
            Alpha = alpha / norm;
            Beta = beta / norm;
        }

        static readonly double InvSqrt2 = 1.0 / Math.Sqrt(2.0);

        // The six cardinal states, named the way the lesson names them.
        public static readonly Qubit Zero = new Qubit(1, 0);                                  // |0>,  +z
        public static readonly Qubit One = new Qubit(0, 1);                                   // |1>,  -z
        public static readonly Qubit Plus = new Qubit(InvSqrt2, InvSqrt2);                     // |+>,  +x
        public static readonly Qubit Minus = new Qubit(InvSqrt2, -InvSqrt2);                   // |->,  -x
        public static readonly Qubit PlusI = new Qubit(InvSqrt2, new Complex(0, InvSqrt2));    // |+i>, +y
        public static readonly Qubit MinusI = new Qubit(InvSqrt2, new Complex(0, -InvSqrt2));  // |-i>, -y

        // Looks up a cardinal state by the short names used in data files: 0, 1, +, -, +i, -i.
        public static Qubit FromName(string name)
        {
            switch (name)
            {
                case "0": return Zero;
                case "1": return One;
                case "+": return Plus;
                case "-": return Minus;
                case "+i": return PlusI;
                case "-i": return MinusI;
                default: throw new ArgumentException($"Unknown state name '{name}'. Use 0, 1, +, -, +i or -i.");
            }
        }

        // theta = polar angle from +z, phi = azimuthal angle from +x toward +y.
        public static Qubit FromBlochAngles(double theta, double phi)
        {
            return new Qubit(Math.Cos(theta / 2), Complex.FromPolarCoordinates(Math.Sin(theta / 2), phi));
        }

        public double ProbabilityZero => Alpha.Magnitude * Alpha.Magnitude;
        public double ProbabilityOne => Beta.Magnitude * Beta.Magnitude;

        public BlochVector Bloch
        {
            get
            {
                Complex cross = Complex.Conjugate(Alpha) * Beta;
                return new BlochVector(2 * cross.Real, 2 * cross.Imaginary, ProbabilityZero - ProbabilityOne);
            }
        }

        public double Theta => Math.Acos(Math.Max(-1.0, Math.Min(1.0, Bloch.Z)));

        // In [0, 2pi). Undefined at the poles; returns 0 there by convention.
        public double Phi
        {
            get
            {
                BlochVector b = Bloch;
                if (Math.Abs(b.X) < 1e-12 && Math.Abs(b.Y) < 1e-12) return 0;
                double phi = Math.Atan2(b.Y, b.X);
                return phi < 0 ? phi + 2 * Math.PI : phi;
            }
        }

        // |<this|other>|^2. 1 means the same physical state (global phase ignored).
        public double FidelityWith(Qubit other)
        {
            Complex overlap = Complex.Conjugate(Alpha) * other.Alpha + Complex.Conjugate(Beta) * other.Beta;
            return overlap.Magnitude * overlap.Magnitude;
        }

        public bool IsSameStateAs(Qubit other, double tolerance = 1e-9) => FidelityWith(other) > 1 - tolerance;

        public override string ToString() => $"{Alpha}|0> + {Beta}|1>  bloch={Bloch}";
    }
}
