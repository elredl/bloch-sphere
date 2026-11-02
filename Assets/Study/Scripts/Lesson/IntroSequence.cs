using System;
using System.Collections;
using Study.Visuals;
using UnityEngine;

namespace Study.Lesson
{
    // The intro segment, before the narrated lesson, the same in every condition. Drawings
    // are watched (they play to the end); every text box waits for NEXT, so the intro is
    // learner-paced. Texts come from intro_text.json (by id); the order is fixed here:
    //
    //   foundation  centered box: how a state is written, and why it is an arrow on a ball
    //   state       |ψ⟩ = (α, β) drawn in, |0⟩ and |1⟩ beside its rows; box beside it
    //   complex     α = a + b i, β = c + d i and i² = −1 drawn in; box: complex numbers
    //   why         box beside the whole formula: phase, which lets states interfere
    //   four        the formula fades; ℝ² with the vector to (a, b) drawn in; box: two
    //               numbers fit a page, four do not
    //   notfree     the plane fades; |α|² + |β|² = 1 drawn in; box: two of the four are fixed
    //   bloch       the Bloch sphere drawn in, then swapped for the real one; box: two
    //               angles, a point on a globe
    //   globe       the state vector draws out to a point on the sphere, labeled |ψ⟩ and
    //               1; box: height is latitude (the odds), direction is longitude (phase)
    //   turns       U|ψ⟩ = |ψ′⟩ and its matrix form written left of the sphere; then the
    //               vector turns to |ψ′⟩ (the old one stays as a dim ghost, |ψ⟩); box:
    //               every unitary is a turn. Then the labels fade and the lesson starts
    //
    // Logged as intro_start, intro_box (shown), intro_next (seconds on the box), intro_end.
    public class IntroSequence : MonoBehaviour
    {
        const float AnswerDelay = 3f; // a box's answer, if it has one, appears this long after its question
        const float FadeSeconds = 1.2f;
        const float TurnSeconds = 3.5f; // the example unitary's turn

        [Serializable]
        class Box
        {
            public string id;
            public string title;
            public string body;
            public string answer;
        }

        [Serializable]
        class IntroText
        {
            public string nextLabel;
            public Box[] boxes;

            public Box Find(string id) => Array.Find(boxes, b => b.id == id) ?? throw new ArgumentException("intro_text.json has no box " + id);
        }

        [SerializeField] GameObject root;
        [SerializeField] IntroBoardView board;
        [SerializeField] IntroSphereView drawnSphere;
        [SerializeField] IntroPanelView panel;
        [Tooltip("The lesson's Bloch sphere: shown when the drawn one is complete.")]
        [SerializeField] GameObject realSphere;
        [SerializeField] QubitState realState;
        [SerializeField] StateVectorView realVector;
        [Tooltip("The sphere's dim ghost vector: marks |ψ⟩ while the vector turns to |ψ′⟩.")]
        [SerializeField] StateVectorView ghost;
        [SerializeField] GateArcAnimator animator;
        [SerializeField] TextAsset textJson;

        public bool IsRunning { get; private set; }

        IntroText text;
        bool nextPressed;
        float started;

        void Awake()
        {
            text = JsonUtility.FromJson<IntroText>(textJson.text);
            root.SetActive(false);
        }

        void OnEnable() => panel.NextPressed += OnNext;
        void OnDisable() => panel.NextPressed -= OnNext;

        void OnNext() => nextPressed = true;

        public void Run(Action done)
        {
            IsRunning = true;
            StartCoroutine(Play(done));
        }

        // Withdrawal: stop where it is.
        public void Stop()
        {
            if (!IsRunning) return;
            StopAllCoroutines();
            IsRunning = false;
            panel.Hide();
            root.SetActive(false);
        }

        IEnumerator Play(Action done)
        {
            started = Time.realtimeSinceStartup;
            StudyEvents.Log("intro_start", "");
            realSphere.SetActive(false);
            root.SetActive(true);
            drawnSphere.gameObject.SetActive(false);

            yield return ShowBox("foundation", side: false);
            yield return board.DrawState();
            yield return ShowBox("state", side: true);
            yield return board.DrawComplex();
            yield return board.DrawImaginary();
            yield return ShowBox("complex", side: true);
            yield return ShowBox("why", side: true);

            yield return board.FadeFormula(FadeSeconds);
            yield return board.DrawPlane();
            yield return ShowBox("four", side: true);
            yield return board.FadePlane(FadeSeconds);
            yield return board.DrawNorm();
            yield return ShowBox("notfree", side: true);
            yield return board.FadeNorm(FadeSeconds);

            drawnSphere.gameObject.SetActive(true);
            yield return drawnSphere.Draw();
            realSphere.SetActive(true);
            realVector.SetVisible(false); // it comes in with its own step
            drawnSphere.HideSphere();
            yield return ShowBox("bloch", side: true);

            realState.Set(IntroSphereView.Example);
            realVector.SetLength(0);
            realVector.SetVisible(true);
            yield return GrowVector(2.0f);
            yield return drawnSphere.WriteVectorLabels();
            yield return ShowBox("globe", side: true);
            yield return board.DrawUnitary();
            ghost.ShowDirection(IntroSphereView.Example);
            ghost.SetVisible(true);
            yield return Wait(0.6f);
            animator.Play(IntroSphereView.Turn, TurnSeconds);
            while (animator.IsPlaying) yield return null;
            yield return drawnSphere.WriteMovedLabel();
            yield return ShowBox("turns", side: true);
            StartCoroutine(drawnSphere.FadeVectorLabels(FadeSeconds));
            yield return board.FadeUnitary(FadeSeconds);
            ghost.SetVisible(false);

            root.SetActive(false);
            IsRunning = false;
            StudyEvents.Log("intro_end", $"seconds={Time.realtimeSinceStartup - started:F1}");
            done();
        }

        static IEnumerator Wait(float seconds)
        {
            for (float t = 0; t < seconds; t += Time.deltaTime) yield return null;
        }

        // The real state vector drawn out from the center (ease in and out).
        IEnumerator GrowVector(float seconds)
        {
            for (float t = 0; t < seconds; t += Time.deltaTime)
            {
                realVector.SetLength(Mathf.SmoothStep(0, 1, t / seconds));
                yield return null;
            }
            realVector.SetLength(1);
        }

        // Shows a box and waits for NEXT, then for it to fade out.
        IEnumerator ShowBox(string id, bool side)
        {
            Box box = text.Find(id);
            nextPressed = false;
            panel.Show(box.title, box.body, string.IsNullOrEmpty(box.answer) ? null : box.answer,
                       side ? IntroPanelView.Pose.Side : IntroPanelView.Pose.Center, text.nextLabel);
            StudyEvents.Log("intro_box", $"box={id}");
            float shown = Time.realtimeSinceStartup;
            if (!string.IsNullOrEmpty(box.answer))
            {
                for (float t = 0; t < AnswerDelay; t += Time.deltaTime) yield return null;
                panel.RevealAnswer();
            }
            while (!nextPressed) yield return null;
            StudyEvents.Log("intro_next", $"box={id} seconds={Time.realtimeSinceStartup - shown:F1}");
            panel.Hide();
            while (!panel.IsFaded) yield return null;
        }
    }
}
