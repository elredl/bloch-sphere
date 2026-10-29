using UnityEngine;

namespace Study.Visuals
{
    // Pause dimming (all conditions): the sphere drops to the palette's dim factor
    // (about 30 %) while the state vector, gate axis and trail stay at full
    // brightness, so attention goes to what the learner is about to move.
    // Fades slowly; never a flash.
    [ExecuteAlways]
    public class SphereDimmer : MonoBehaviour
    {
        BlochSphereRig rig;
        float current = 1f;
        float target = 1f;

        void OnEnable()
        {
            rig = GetComponent<BlochSphereRig>();
        }

        public bool IsDimmed => target < 1f;

        public void SetDimmed(bool dimmed)
        {
            target = dimmed ? rig.Palette.pauseDimFactor : 1f;
        }

        // Skips the fade. For Editor previews and screenshots only.
        public void SetDimmedImmediate(bool dimmed)
        {
            SetDimmed(dimmed);
            current = target;
            Apply();
        }

        void Update()
        {
            if (Mathf.Approximately(current, target)) return;
            float step = (1f - rig.Palette.pauseDimFactor) * Time.deltaTime / Mathf.Max(rig.Palette.dimFadeSeconds, 0.01f);
            current = Mathf.MoveTowards(current, target, step);
            Apply();
        }

        void Apply()
        {
            foreach (SphereView view in GetComponentsInChildren<SphereView>())
                if (view.IsDimmable) view.SetDim(current);
        }
    }
}
