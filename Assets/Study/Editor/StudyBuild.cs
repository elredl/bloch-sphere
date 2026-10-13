using System.Linq;
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
    // Player builds: the Launch scene (index 0; the experimenter enters the participant code
    // and the condition comes from allocation.csv) and the Lesson scene.
    //   BuildQuest()    Quest 3 APK, Conditions 2 and 3   Builds/Quest/lesson.apk
    //   BuildDesktop()  Condition 1 desktop               Builds/Desktop/lesson/lesson.x86_64
    // Post-processing must be off on every camera (no bloom, Checkpoint 3); the build checks.
    public static class StudyBuild
    {
        static readonly string[] Scenes = { "Assets/Study/Scenes/Launch.unity", "Assets/Study/Scenes/Lesson.unity" };
        const string OpenXRLoader = "UnityEngine.XR.OpenXR.OpenXRLoader";

        public static string BuildQuest(string arg = null) =>
            Build(BuildTarget.Android, BuildTargetGroup.Android, "Builds/Quest/lesson.apk");

        // Linux build (the only desktop player module installed). The same settings apply to a
        // Windows build once that module is added.
        public static string BuildDesktop(string arg = null)
        {
            ConfigureDesktopPlayer();
            return Build(BuildTarget.StandaloneLinux64, BuildTargetGroup.Standalone, "Builds/Desktop/lesson/lesson.x86_64");
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

        static string Build(BuildTarget target, BuildTargetGroup group, string path)
        {
            if (EditorUserBuildSettings.activeBuildTarget != target)
                EditorUserBuildSettings.SwitchActiveBuildTarget(group, target);

            foreach (string scenePath in Scenes)
            {
                EditorSceneManager.OpenScene(scenePath);
                foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include))
                    if (cam.GetComponent<UniversalAdditionalCameraData>().renderPostProcessing)
                        return $"Failed: post-processing is on for {cam.name} in {scenePath}";
            }
            EditorSceneManager.OpenScene(Scenes[1]);

            var options = new BuildPlayerOptions
            {
                scenes = Scenes,
                locationPathName = path,
                target = target,
                options = BuildOptions.None,
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);

            var s = report.summary;
            string errors = string.Join(" | ", report.steps.SelectMany(step => step.messages)
                .Where(m => m.type == LogType.Error || m.type == LogType.Exception).Select(m => m.content).Take(5));
            return $"{s.result} {path} {s.totalSize / 1048576f:F1} MB in {s.totalTime.TotalSeconds:F0}s errors={s.totalErrors} {errors}";
        }
    }
}
