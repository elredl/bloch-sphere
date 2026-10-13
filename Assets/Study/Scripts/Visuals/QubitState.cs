using System;
using Study.Quantum;
using UnityEngine;

namespace Study.Visuals
{
    // Holds the qubit the sphere is showing. Every visual reads from here and
    // nothing else, so what is drawn is always a real, normalized state.
    public class QubitState : MonoBehaviour
    {
        public event Action<Qubit> Changed;

        public Qubit Current { get; private set; } = Qubit.Zero;

        // Read-only mirrors for the Inspector, so the live state can be checked in the Editor.
        [Header("Debug (read only)")]
        [SerializeField] Vector3 debugBloch;
        [SerializeField] float debugThetaDegrees;
        [SerializeField] float debugPhiDegrees;
        [SerializeField] float debugProbabilityZero;

        void Start()
        {
            Set(Current); // push the initial state to every listener once they have subscribed
        }

        public void Set(Qubit q)
        {
            Current = q;
            BlochVector b = q.Bloch;
            debugBloch = new Vector3((float)b.X, (float)b.Y, (float)b.Z);
            debugThetaDegrees = (float)(q.Theta * Mathf.Rad2Deg);
            debugPhiDegrees = (float)(q.Phi * Mathf.Rad2Deg);
            debugProbabilityZero = (float)q.ProbabilityZero;
            Changed?.Invoke(q);
        }
    }
}
