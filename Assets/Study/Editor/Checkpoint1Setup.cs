using System.Globalization;
using Study.Quantum;
using Study.Visuals;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Study.EditorTools
{
    // Editor helpers for Checkpoint 1, called through the live command runner
    // (action "call_static"). Wires the existing BasicScene primitives to the
    // qubit model. Checkpoint 1.5 replaces these primitives entirely.
    public static class Checkpoint1Setup
    {
        public static string WireExistingSphere()
        {
            var sphere = Find("Sphere");
            var vector = Find("Bloch State Vector");

            var state = Ensure<QubitState>(sphere);
            var animator = Ensure<GateArcAnimator>(sphere);
            SetRef(animator, "state", state);

            var view = Ensure<StateVectorView>(vector);
            SetRef(view, "state", state);
            SetRef(view, "sphereCenter", sphere.transform);
            var so = new SerializedObject(view);
            so.FindProperty("sphereRadius").floatValue = sphere.transform.lossyScale.x * 0.5f;
            so.ApplyModifiedProperties();

            // The old rods were labeled with Unity axes (Y up). Re-aim them to the
            // agreed Bloch convention: z up, x toward the participant, y to the right.
            SetRotation("Bloch Axis Z", Vector3.zero);
            SetRotation("Bloch Axis X", new Vector3(90, 0, 0));
            SetRotation("Bloch Axis Y", new Vector3(0, 0, 90));

            // Offscreen review camera at the participant's eye point (0.25 m above the
            // sphere center, 0.8 m in front, 60 deg vertical FOV). Disabled, so it never
            // renders during play; the live runner renders it on demand for screenshots.
            var cameraObject = GameObject.Find("Review Camera") ?? new GameObject("Review Camera");
            var cam = Ensure<Camera>(cameraObject);
            cam.enabled = false;
            cam.fieldOfView = 60f;
            cam.nearClipPlane = 0.05f;
            Vector3 center = sphere.transform.position;
            cameraObject.transform.position = center + new Vector3(0f, 0.25f, -0.8f);
            cameraObject.transform.LookAt(center);

            EditorSceneManager.MarkSceneDirty(sphere.scene);
            EditorSceneManager.SaveScene(sphere.scene);
            return "wired Sphere/Bloch State Vector to QubitState and saved scene";
        }

        // Play Mode only. arg = "gate,startState,seconds", e.g. "H,0,3".
        public static string PlayGate(string arg)
        {
            if (!EditorApplication.isPlaying) return "not in play mode";
            string[] parts = arg.Split(',');
            var animator = Object.FindAnyObjectByType<GateArcAnimator>();
            var state = Object.FindAnyObjectByType<QubitState>();
            state.Set(Qubit.FromName(parts[1]));
            animator.Play(Gate.FromName(parts[0]), double.Parse(parts[2], CultureInfo.InvariantCulture));
            return $"playing {parts[0]} from |{parts[1]}> over {parts[2]} s";
        }

        // Play Mode only. Reports what the scene objects show right now.
        public static string ReportState()
        {
            var state = Object.FindAnyObjectByType<QubitState>();
            var view = Object.FindAnyObjectByType<StateVectorView>();
            var sphere = GameObject.Find("Sphere").transform;
            Vector3 tip = view.transform.position + view.transform.up * view.transform.localScale.y;
            Vector3 fromCenter = tip - sphere.position;
            return $"model bloch={state.Current.Bloch} theta={state.Current.Theta * Mathf.Rad2Deg:F1} " +
                   $"phi={state.Current.Phi * Mathf.Rad2Deg:F1} | rod tip (local)={fromCenter:F3} |tip|={fromCenter.magnitude:F4} " +
                   $"-> as bloch {BlochSpace.FromLocal(fromCenter / fromCenter.magnitude)}";
        }

        static GameObject Find(string name) =>
            GameObject.Find(name) ?? throw new System.Exception($"GameObject '{name}' not found");

        // TryGetComponent, not "??": Unity's fake-null components break the ?? operator.
        static T Ensure<T>(GameObject go) where T : Component =>
            go.TryGetComponent(out T existing) ? existing : Undo.AddComponent<T>(go);

        static void SetRef(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).objectReferenceValue = value;
            so.ApplyModifiedProperties();
        }

        static void SetRotation(string name, Vector3 euler)
        {
            var t = Find(name).transform;
            Undo.RecordObject(t, "Re-aim axis");
            t.eulerAngles = euler;
        }
    }
}
