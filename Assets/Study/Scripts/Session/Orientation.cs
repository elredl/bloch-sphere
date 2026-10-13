using System;
using System.Collections.Generic;
using System.Linq;
using Study.Lesson;
using Study.Quantum;
using Study.Visuals;
using UnityEngine;

namespace Study.Session
{
    // Orientation (spec, Session flow): a fixed 2:00 in every condition, in the lesson's
    // environment, practising the three moves of a Condition 3 pause in the order the lesson
    // uses them:
    //   select   point at the lit ring and pull the trigger (Cond 1: click it)
    //   press    press the panel button
    //   rotate   grab the cube's handle and ratchet-turn it 90 deg about its axis
    //            (Cond 1: drag it with the mouse)
    // Conditions 2 and 3 do exactly the same (matched exposure). Each completed step is
    // logged. Learners who finish practise again until 2:00; if the first round is not
    // done by then it is logged as orientation_incomplete. Never extended. The clock uses
    // scaled time, so a break (time scale 0) holds it.
    public class Orientation : MonoBehaviour
    {
        public enum Step { Select, Press, Rotate, RoundDone }

        [SerializeField] GameObject practiceRoot;
        [SerializeField] StudyPanelView panel;
        [SerializeField] PracticeTargetsView targets;
        [SerializeField] PracticeCubeView cube;
        [SerializeField] GateDirectionView arrow;
        [SerializeField] GateAxisView axis;
        [SerializeField] PracticeGrab grab;
        [SerializeField] float durationSeconds = 120f;
        [SerializeField] float turnDegrees = 90f;
        [SerializeField] float toleranceDegrees = 10f;
        [SerializeField] float roundDoneSeconds = 2.5f;

        static readonly int[] LitSequence = { 2, 0, 4, 1, 3 };

        public event Action Finished;
        public bool Running { get; private set; }
        public Step Current => step;
        public double Elapsed => elapsed;
        public int Round => round + 1;
        public PracticeTargetsView Targets => targets;
        public StudyPanelView Panel => panel;
        public PracticeGrab Grab => grab;

        SessionText text;
        bool desktop, paused;
        double elapsed, roundDoneAt;
        int round;
        Step step;
        Gate turn;
        RatchetRotation ratchet;
        Func<BlochVector> hand;
        readonly List<string> firstRound = new List<string>();
        int stepsDone;

        void Awake()
        {
            practiceRoot.SetActive(false);
        }

        void OnEnable()
        {
            targets.Picked += OnPicked;
            panel.Pressed += OnPressed;
            grab.Started += OnGrabStarted;
            grab.Ended += OnGrabEnded;
        }

        void OnDisable()
        {
            targets.Picked -= OnPicked;
            panel.Pressed -= OnPressed;
            grab.Started -= OnGrabStarted;
            grab.Ended -= OnGrabEnded;
        }

        // desktop: Condition 1 (mouse instead of controllers).
        public void Begin(SessionText sessionText, bool isDesktop)
        {
            text = sessionText;
            desktop = isDesktop;
            turn = Gate.Rotation("practice", PracticeCubeView.Axis, turnDegrees * Math.PI / 180);
            elapsed = 0;
            round = 0;
            stepsDone = 0;
            firstRound.Clear();
            paused = false;
            Running = true;
            practiceRoot.SetActive(true);
            StudyEvents.Log("orientation_start", $"duration={durationSeconds:F0} input={(desktop ? "mouse" : "controllers")}");
            StartRound();
        }

        // Abort: everything goes away, nothing more is logged.
        public void Stop()
        {
            if (!Running) return;
            Running = false;
            Teardown();
        }

        // A break: no input counts until it ends.
        public void SetPaused(bool on)
        {
            paused = on;
            grab.SetGrabbable(!on && Running && step == Step.Rotate, desktop);
        }

        void StartRound()
        {
            step = Step.Select;
            ratchet = null;
            cube.SetAngle(0);
            arrow.Hide();
            axis.Hide();
            grab.SetGrabbable(false, desktop);
            targets.Show(LitSequence[round % LitSequence.Length]);
            panel.Show(text.practiceTitle, desktop ? text.selectDesktop : text.selectVr);
            panel.HideButtons();
        }

        void Update()
        {
            if (!Running) return;
            elapsed += Time.deltaTime;

            if (step == Step.Rotate && ratchet != null && ratchet.IsGrabbing && !paused)
            {
                if (ratchet.Move(hand())) grab.WrongWayTick();
                cube.SetAngle(ratchet.Angle);
                arrow.SetAngle(ratchet.Angle);
            }
            if (step == Step.RoundDone && elapsed >= roundDoneAt)
            {
                round++;
                StartRound();
            }
            if (elapsed >= durationSeconds) End();
        }

        void OnPicked(int index)
        {
            if (!Running || paused || step != Step.Select || index != targets.Lit) return;
            Completed("select");
            targets.Hide();
            step = Step.Press;
            panel.SetBody(desktop ? text.pressDesktop : text.pressVr);
            panel.SetButton(0, text.pressLabel);
        }

        void OnPressed(int index)
        {
            if (!Running || paused || step != Step.Press) return;
            Completed("press");
            panel.HideButtons();
            step = Step.Rotate;
            ratchet = new RatchetRotation(Qubit.Plus, turn, toleranceDegrees * Math.PI / 180);
            axis.Show(turn);
            arrow.Show(turn, Qubit.Plus, 0);
            panel.SetBody(desktop ? text.rotateDesktop : text.rotateVr);
            grab.SetGrabbable(true, desktop);
        }

        void OnGrabStarted(string mode, Func<BlochVector> handPoint)
        {
            if (!Running || paused || step != Step.Rotate || ratchet.IsGrabbing) return;
            hand = handPoint;
            ratchet.Begin(hand());
            StudyEvents.Log("orientation_grab", $"round={Round} mode={mode} t={elapsed:F2}");
        }

        void OnGrabEnded()
        {
            if (step != Step.Rotate || ratchet == null || !ratchet.IsGrabbing) return;
            if (!ratchet.End()) return; // let go short of the end: grab again
            cube.SetAngle(turn.Angle);
            arrow.Hide();
            axis.Hide();
            grab.SetGrabbable(false, desktop);
            Completed("rotate");
            step = Step.RoundDone;
            roundDoneAt = elapsed + roundDoneSeconds;
            panel.SetBody(text.roundDone);
        }

        void Completed(string stepName)
        {
            stepsDone++;
            if (round == 0) firstRound.Add(stepName);
            StudyEvents.Log("orientation_step", $"step={stepName} round={Round} t={elapsed:F2}");
        }

        void End()
        {
            Running = false;
            int roundsDone = step == Step.RoundDone ? round + 1 : round;
            if (roundsDone == 0)
            {
                string missing = string.Join(";", new[] { "select", "press", "rotate" }.Where(s => !firstRound.Contains(s)));
                StudyEvents.Log("orientation_incomplete", $"done={(firstRound.Count == 0 ? "none" : string.Join(";", firstRound))} missing={missing}");
            }
            StudyEvents.Log("orientation_end", $"rounds={roundsDone} steps={stepsDone} t={elapsed:F2}");
            Teardown();
            Finished?.Invoke();
        }

#if UNITY_EDITOR
        // Dev runs (SimulatedPractice): the same paths as a grab on the handle.
        public void DevGrab(Func<BlochVector> handPoint) => OnGrabStarted("dev", handPoint);
        public void DevRelease() => OnGrabEnded();
#endif

        void Teardown()
        {
            grab.SetGrabbable(false, desktop);
            targets.Hide();
            arrow.Hide();
            axis.Hide();
            panel.Hide();
            practiceRoot.SetActive(false);
        }
    }
}
