using System.Globalization;
using Study.Lesson;
using Study.Session;
using Study.Visuals;
using UnityEditor.SceneManagement;
using UnityEditor;
using UnityEngine;

namespace Study.EditorTools
{
    // Editor helpers for lesson dry runs, called through the live command runner.
    public static class LessonDevTools
    {
        // Edit Mode, before entering Play Mode. arg = "condition,speed[,full]", e.g. "2,3".
        // full: run calibration and orientation first (otherwise straight to the lesson).
        // The speed also applies when the Launch scene leads into the lesson.
        public static string SetDevRun(string arg)
        {
            string[] parts = arg.Split(',');
            if (EditorSceneManager.GetActiveScene().path != LessonScenePath) EditorSceneManager.OpenScene(LessonScenePath);
            var lesson = Object.FindAnyObjectByType<LessonController>() ?? throw new System.Exception("no LessonController in scene");
            var so = new SerializedObject(lesson);
            so.FindProperty("devCondition").enumValueIndex = int.Parse(parts[0]) - 1;
            so.FindProperty("devPlaybackSpeed").floatValue = float.Parse(parts[1], CultureInfo.InvariantCulture);
            so.ApplyModifiedProperties();
            bool full = parts.Length > 2 && parts[2] == "full";
            var flow = new SerializedObject(Object.FindAnyObjectByType<SessionFlow>());
            flow.FindProperty("devSkipToLesson").boolValue = !full;
            flow.ApplyModifiedProperties();
            EditorSceneManager.SaveOpenScenes();
            return $"dev run: condition {parts[0]}, speed {parts[1]}x, {(full ? "full session" : "lesson only")}";
        }

        const string LessonScenePath = "Assets/Study/Scenes/Lesson.unity";

        // Edit Mode. arg = scene path, e.g. Assets/Study/Scenes/Launch.unity
        public static string OpenScene(string arg)
        {
            EditorSceneManager.OpenScene(arg);
            return "opened " + arg;
        }

        // Writes DevData/allocation.csv (the Editor's data folder). arg = rows after the header,
        // separated by ';', e.g. "P01,3,novice;P02,1,math_literate". A previous file is kept as
        // allocation.csv.bak.
        public static string WriteDevAllocation(string arg)
        {
            string dir = StudySession.DataDirectory;
            System.IO.Directory.CreateDirectory(dir);
            string path = System.IO.Path.Combine(dir, StudySession.AllocationFile);
            if (System.IO.File.Exists(path)) System.IO.File.Copy(path, path + ".bak", true);
            System.IO.File.WriteAllText(path, Allocation.Header + "\n" + string.Join("\n", arg.Split(';')) + "\n");
            return "wrote " + path;
        }

        // Play Mode, Launch scene: types the participant number on the keypad.
        public static string LaunchType(string arg) => "code " + Object.FindAnyObjectByType<LaunchScreen>().DevType(arg);

        // Play Mode. arg = "panel object name,button index", e.g. "Launch Panel,12".
        public static string PanelPress(string arg)
        {
            string[] parts = arg.Split(',');
            StudyPanelView panel = FindPanel(parts[0]);
            panel.Press(int.Parse(parts[1]));
            return "pressed " + arg + " -> " + panel.Describe();
        }

        // Play Mode. arg = panel object name.
        public static string PanelText(string arg) => FindPanel(arg).Describe();

        static StudyPanelView FindPanel(string name)
        {
            foreach (var panel in Object.FindObjectsByType<StudyPanelView>(FindObjectsInactive.Include))
                if (panel.name == name) return panel;
            throw new System.Exception("no panel " + name);
        }

        // Play Mode, Lesson scene. arg = begin | break | abort | calibrate (the experimenter's and
        // participant's buttons).
        public static string Session(string arg)
        {
            var flow = Object.FindAnyObjectByType<SessionFlow>();
            switch (arg)
            {
                case "begin": flow.DevBegin(); break;
                case "break": flow.DevBreak(); break;
                case "abort": flow.DevAbort(); break;
                case "calibrate": flow.DevCalibrate(); break;
                default: return "unknown " + arg;
            }
            return SessionStatus();
        }

        // Play Mode, Lesson scene. arg = rounds of practice to do (0: none).
        public static string SimulatePractice(string arg)
        {
            var flow = Object.FindAnyObjectByType<SessionFlow>();
            var sim = flow.gameObject.GetComponent<SimulatedPractice>() ?? flow.gameObject.AddComponent<SimulatedPractice>();
            sim.Configure(flow.Orientation, int.Parse(arg));
            return "simulated practice: " + arg + " rounds";
        }

        public static string SessionStatus()
        {
            var s = StudySession.Current;
            var flow = Object.FindAnyObjectByType<SessionFlow>();
            var lesson = Object.FindAnyObjectByType<LessonController>();
            string file = s == null ? "no session" : $"{s.Code} {s.Log.FileName} events={s.Log.EventCount} finished={s.Finished}";
            string where = flow == null ? "launch scene" : $"step={flow.Current} break={flow.OnBreak} orientation_t={flow.Orientation.Elapsed:F1}";
            string lessonText = lesson == null || lesson.Config == null ? "" : $" lessonTime={lesson.Time:F2} running={lesson.IsRunning}";
            return $"{file} | {where}{lessonText} timeScale={Time.timeScale}";
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

        // Play Mode. arg = "camera,path": a capture now, for moments with no lesson time (session screens).
        public static string CaptureNow(string arg)
        {
            string[] parts = arg.Split(',');
            Render(GameObject.Find(parts[0]).GetComponent<Camera>(), parts[1]);
            return "captured " + parts[1];
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
