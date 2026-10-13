using System.Linq;
using Study.Lesson;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Study.EditorTools
{
    // Player builds of the Lesson scene, one per condition, until Checkpoint 6's launch
    // screen picks the condition per participant:
    //   BuildApk("2" | "3")  Quest 3 APK          Builds/Quest/lesson_c{n}.apk
    //   BuildDesktop("1")    Condition 1 desktop  Builds/Desktop/lesson_c1/lesson_c1.x86_64
    // Each build bakes the condition into a temporary copy of the scene, so Lesson.unity on
    // disk never changes. Post-processing is off on every camera (no bloom, Checkpoint 3).
    public static class StudyBuild
    {
        const string ScenePath = "Assets/Study/Scenes/Lesson.unity";
        const string TempScenePath = "Assets/Study/Scenes/Lesson_Build.unity";
        const string OpenXRLoader = "UnityEngine.XR.OpenXR.OpenXRLoader";

        public static string BuildApk(string arg)
        {
            if (arg != "2" && arg != "3") throw new System.ArgumentException("arg must be the condition: 2 or 3");
            return Build(int.Parse(arg), BuildTarget.Android, BuildTargetGroup.Android, $"Builds/Quest/lesson_c{arg}.apk");
        }

        // Linux build (the only desktop player module installed). The same settings apply to a
        // Windows build once that module is added.
        public static string BuildDesktop(string arg)
        {
            if (arg != "1") throw new System.ArgumentException("the desktop build is Condition 1");
            ConfigureDesktopPlayer();
            return Build(1, BuildTarget.StandaloneLinux64, BuildTargetGroup.Standalone, "Builds/Desktop/lesson_c1/lesson_c1.x86_64");
        }

        // Condition 1 never starts XR: no OpenXR loader on desktop and no XR start-up. This also
        // stops the Editor (which uses the desktop settings) trying to load OpenXR on Play.
        // Quest (Android) keeps its OpenXR loader.
        public static string DisableDesktopXr()
        {
            var settings = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Standalone);
            if (settings == null) return "no desktop XR settings: XR is off on desktop";
            settings.InitManagerOnStart = false;
            bool removed = settings.AssignedSettings != null &&
                           XRPackageMetadataStore.RemoveLoader(settings.AssignedSettings, OpenXRLoader, BuildTargetGroup.Standalone);
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            int left = settings.AssignedSettings == null ? 0 : settings.AssignedSettings.activeLoaders.Count;
            return $"desktop XR: start-up off, OpenXR loader removed={removed}, loaders left={left}";
        }

        // 1920 x 1080 borderless fullscreen, not resizable (DesktopDisplay enforces it at run time too).
        static void ConfigureDesktopPlayer()
        {
            DisableDesktopXr();
            PlayerSettings.defaultIsNativeResolution = false;
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.resizableWindow = false;
            PlayerSettings.runInBackground = true; // a focus change must not stall the lesson clock's frames
            // Mono: the Linux IL2CPP module isn't installed, and desktop has no need for it (Quest stays IL2CPP).
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
        }

        static string Build(int condition, BuildTarget target, BuildTargetGroup group, string path)
        {
            if (EditorUserBuildSettings.activeBuildTarget != target)
                EditorUserBuildSettings.SwitchActiveBuildTarget(group, target);

            var scene = EditorSceneManager.OpenScene(ScenePath);
            foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include))
                cam.GetComponent<UniversalAdditionalCameraData>().renderPostProcessing = false;
            var lesson = new SerializedObject(Object.FindAnyObjectByType<LessonController>());
            lesson.FindProperty("devCondition").enumValueIndex = condition - 1;
            lesson.ApplyModifiedPropertiesWithoutUndo();
            // A copy, so the build settings never reach Lesson.unity. Overwrites one left by a crashed build.
            EditorSceneManager.SaveScene(scene, TempScenePath, true);

            var options = new BuildPlayerOptions
            {
                scenes = new[] { TempScenePath },
                locationPathName = path,
                target = target,
                options = BuildOptions.None,
            };
            BuildReport report;
            try
            {
                report = BuildPipeline.BuildPlayer(options);
            }
            finally
            {
                AssetDatabase.DeleteAsset(TempScenePath);
                EditorSceneManager.OpenScene(ScenePath);
            }

            var s = report.summary;
            string errors = string.Join(" | ", report.steps.SelectMany(step => step.messages)
                .Where(m => m.type == LogType.Error || m.type == LogType.Exception).Select(m => m.content).Take(5));
            return $"{s.result} {path} {s.totalSize / 1048576f:F1} MB in {s.totalTime.TotalSeconds:F0}s errors={s.totalErrors} {errors}";
        }
    }
}
