namespace Study.Quantum
{
    // The path a state takes while a gate is applied: a partial rotation about
    // the gate's own axis. Interpolating the rotation angle (never the end
    // points) keeps the vector on the sphere and traces the true arc.
    public static class GateArc
    {
        // fraction 0 = start state, 1 = gate fully applied.
        public static Qubit StateAt(Qubit start, Gate gate, double fraction)
        {
            if (fraction <= 0) return start;
            // Use the gate's exact matrix at the end, so the final amplitudes are
            // the textbook ones rather than the rotation form's (same state, different global phase).
            if (fraction >= 1) return gate.Apply(start);
            return Gate.Rotation(gate.Name, gate.Axis, gate.Angle * fraction).Apply(start);
        }
    }
}
