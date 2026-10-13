using System.Globalization;
using Study.Lesson;
using UnityEditor;
using UnityEngine;

namespace Study.EditorTools
{
    // Editor helpers for lesson dry runs, called through the live command runner.
    public static class LessonDevTools
    {
        // Edit Mode, before entering Play Mode. arg = "condition,speed", e.g. "2,3".
        public static string SetDevRun(string arg)
        {
            string[] parts = arg.Split(',');
            var lesson = Object.FindAnyObjectByType<LessonController>() ?? throw new System.Exception("no LessonController in scene");
            var so = new SerializedObject(lesson);
            so.FindProperty("devCondition").enumValueIndex = int.Parse(parts[0]) - 1;
            so.FindProperty("devPlaybackSpeed").floatValue = float.Parse(parts[1], CultureInfo.InvariantCulture);
            so.ApplyModifiedProperties();
            return $"dev run: condition {parts[0]}, speed {parts[1]}x";
        }

        // Lists a GameObject's subtree: path, active state and component types, one per line.
        public static string DumpHierarchy(string rootName)
        {
            var root = GameObject.Find(rootName);
            if (root == null)
                foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
                    if (go.name == rootName && go.scene.IsValid()) root = go;
            if (root == null) return "not found: " + rootName;
            var sb = new System.Text.StringBuilder();
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                string path = t.name;
                for (Transform p = t.parent; p != null && p != root.transform.parent; p = p.parent) path = p.name + "/" + path;
                var types = new System.Collections.Generic.List<string>();
                foreach (Component c in t.GetComponents<Component>()) if (c != null && !(c is Transform)) types.Add(c.GetType().Name);
                sb.AppendLine($"{(t.gameObject.activeSelf ? "+" : "-")} {path}  [{string.Join(", ", types)}]");
            }
            System.IO.File.WriteAllText("LiveCommands/hierarchy.txt", sb.ToString());
            return "wrote LiveCommands/hierarchy.txt";
        }

        // Play Mode. arg = seconds.
        public static string Seek(string arg)
        {
            var lesson = Object.FindAnyObjectByType<LessonController>();
            lesson.DevSeek(double.Parse(arg, CultureInfo.InvariantCulture));
            return "seek " + arg;
        }

        // Play Mode. arg = "lessonTime,camera,path;lessonTime,camera,path;..." Each capture is
        // taken on the first frame at or after its lesson time, so short moments (a 150 ms
        // flash) can be caught exactly.
        public static string CaptureAt(string arg)
        {
            foreach (string item in arg.Split(';'))
            {
                string[] parts = item.Split(',');
                pendingCaptures.Add((double.Parse(parts[0], CultureInfo.InvariantCulture), parts[1], parts[2]));
            }
            pendingCaptures.Sort((a, b) => a.time.CompareTo(b.time));
            EditorApplication.update -= CaptureTick;
            EditorApplication.update += CaptureTick;
            return $"{pendingCaptures.Count} captures pending";
        }

        static readonly System.Collections.Generic.List<(double time, string camera, string path)> pendingCaptures =
            new System.Collections.Generic.List<(double, string, string)>();

        static void CaptureTick()
        {
            var lesson = Object.FindAnyObjectByType<LessonController>();
            if (!EditorApplication.isPlaying || lesson == null) return;
            while (pendingCaptures.Count > 0 && lesson.Time >= pendingCaptures[0].time)
            {
                var (time, cameraName, path) = pendingCaptures[0];
                pendingCaptures.RemoveAt(0);
                Render(GameObject.Find(cameraName).GetComponent<Camera>(), path);
                Debug.Log($"[Capture] {path} at lesson {lesson.Time:F3} (asked {time:F3})");
            }
        }

        static void Render(Camera cam, string path)
        {
            var rt = new RenderTexture(1920, 1080, 24) { antiAliasing = 4 };
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = null;
            RenderTexture.active = rt;
            var tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
            RenderTexture.active = null;
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            rt.Release();
            Object.DestroyImmediate(rt);
        }

        // Play Mode, Condition 3. arg = one learner profile per pause, e.g. "fast,wrong,slow,never"
        // (fewer than four: the last repeats). Profiles are in SimulatedLearner.Named.
        public static string SimulateLearner(string arg)
        {
            var embodied = Object.FindAnyObjectByType<EmbodiedPause>() ?? throw new System.Exception("no EmbodiedPause in scene");
            var learner = embodied.gameObject.GetComponent<SimulatedLearner>() ?? embodied.gameObject.AddComponent<SimulatedLearner>();
            var profiles = System.Array.ConvertAll(arg.Split(','), name => SimulatedLearner.Named(name.Trim()));
            learner.Configure(profiles, embodied, Object.FindAnyObjectByType<Study.Visuals.PredictionPanelView>(),
                Object.FindAnyObjectByType<Study.Visuals.PositionMarkersView>());
            return "simulated learner: " + arg;
        }

        // Moves assets (with their .meta files) to the OS trash, so a deletion can be undone.
        // arg = paths separated by ';'.
        public static string TrashAssets(string arg)
        {
            var failed = new System.Collections.Generic.List<string>();
            AssetDatabase.MoveAssetsToTrash(arg.Split(';'), failed);
            AssetDatabase.Refresh();
            return failed.Count == 0 ? "trashed " + arg : "could not trash: " + string.Join(", ", failed);
        }

        // Play Mode. Reports lesson time, audio position and running state.
        public static string Status()
        {
            var lesson = Object.FindAnyObjectByType<LessonController>();
            if (lesson == null || lesson.Config == null) return "no lesson running";
            return $"{lesson.Config} running={lesson.IsRunning} lessonTime={lesson.Time:F2}";
        }
    }
}
