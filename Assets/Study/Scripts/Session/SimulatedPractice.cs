#if UNITY_EDITOR
using System;
using Study.Quantum;
using Study.Visuals;
using UnityEngine;

namespace Study.Session
{
    // Editor dry runs only: does the orientation steps like a learner, through the same paths
    // as the controllers and mouse. Picks a wrong ring first, then the lit one, presses the
    // button, and turns the handle in two grabs (letting go halfway). Stops after `rounds`
    // rounds (0 = does nothing, to test orientation_incomplete).
    public class SimulatedPractice : MonoBehaviour
    {
        Orientation orientation;
        int rounds;
        float wait;
        int grabs;
        double turned;
        bool holding, releasing;
        int lastRound;

        public void Configure(Orientation o, int roundsToDo)
        {
            orientation = o;
            rounds = roundsToDo;
            wait = 0.8f;
        }

        void Update()
        {
            if (orientation == null || !orientation.Running || orientation.Round > rounds) return;
            if (orientation.Round != lastRound)
            {
                lastRound = orientation.Round;
                grabs = 0;
                turned = 0;
                holding = false;
                releasing = false;
            }
            wait -= Time.deltaTime;
            if (wait > 0) return;
            switch (orientation.Current)
            {
                case Orientation.Step.Select:
                    int lit = orientation.Targets.Lit;
                    orientation.Targets.Pick((lit + 1) % PracticeTargetsView.Count); // a miss does nothing
                    orientation.Targets.Pick(lit);
                    wait = 0.8f;
                    break;
                case Orientation.Step.Press:
                    orientation.Panel.Press(0);
                    wait = 0.6f;
                    break;
                case Orientation.Step.Rotate:
                    Turn();
                    break;
            }
        }

        // First grab turns 45 deg and lets go; the second finishes. Lets go one frame after the
        // hand reaches its stop, once the ratchet has seen the final hand position.
        void Turn()
        {
            if (releasing)
            {
                releasing = false;
                holding = false;
                grabs++;
                wait = 0.3f;
                orientation.DevRelease();
                return;
            }
            if (!holding)
            {
                holding = true;
                orientation.DevGrab(() => PracticeCubeView.HandleStart.RotatedAbout(PracticeCubeView.Axis, turned));
                return;
            }
            double stop = grabs == 0 ? Math.PI / 4 : Math.PI / 2;
            turned = Math.Min(stop, turned + Time.deltaTime * 1.5);
            if (turned >= stop) releasing = true;
        }
    }
}
#endif
