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

        // App identity. Participants see the product name in the Quest library, so it is neutral
        // (nothing about conditions or hypotheses). The package / company name set the data
        // folder: Quest /sdcard/Android/data/<package>/files, Linux ~/.config/unity3d/<company>/<product>.
        // Changing any of these later installs a separate app with its own data folder.
        public const string ProductName = "Qubit Lesson";
        public const string CompanyName = "Qupacabras";
        public const string PackageId = "edu.colostate.qupacabras.qubitlesson";
        // Bump for every build that runs participants; the session files record it (app_version).
        public const string Version = "1.0.0";

        public static string ConfigureIdentity(string arg = null)
        {
            PlayerSettings.productName = ProductName;
            PlayerSettings.companyName = CompanyName;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, PackageId);
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, PackageId);
            PlayerSettings.bundleVersion = Version;
            PlayerSettings.Android.bundleVersionCode = 1;
            AssetDatabase.SaveAssets();
            return $"{ProductName} / {CompanyName} / {PackageId} / {Version}";
        }

        // The commit the build came from, for BUILD_STAMP.txt. "-dirty": the study's own code,
        // data, scenes or packages had uncommitted changes (builds and Play Mode rewrite some Unity
        // settings and the dynamic font atlases by themselves, so those don't count).
        public static string BuildCommit(string arg = null)
        {
            string commit = Git("rev-parse --short HEAD");
            bool dirty = Git("status --porcelain --untracked-files=no -- Assets/Study :!Assets/Study/Fonts Packages").Length > 0;
            return commit.Length == 0 ? "unknown" : commit + (dirty ? "-dirty" : "");
        }

        static string Git(string args)
        {
            try
            {
                var info = new System.Diagnostics.ProcessStartInfo("git", args)
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = System.IO.Path.GetDirectoryName(Application.dataPath),
                };
                using var process = System.Diagnostics.Process.Start(info);
                string output = process.StandardOutput.ReadToEnd().Trim();
                process.WaitForExit();
                return process.ExitCode == 0 ? output : "";
            }
            catch (System.Exception)
            {
                return "";
            }
        }

        // BUILD_STAMP.txt next to the build: what was built, from which commit, when.
        static void WriteStamp(BuildReport report, string path)
        {
            string folder = System.IO.Path.GetDirectoryName(path);
            System.IO.File.WriteAllText(System.IO.Path.Combine(folder, "BUILD_STAMP.txt"),
                $"file      {System.IO.Path.GetFileName(path)}\n" +
                $"result    {report.summary.result}\n" +
                $"version   {Version}\n" +
                $"commit    {BuildCommit()} ({Git("rev-parse HEAD")})\n" +
                $"built     {System.DateTime.Now:yyyy-MM-dd HH:mm}\n" +
                $"unity     {Application.unityVersion}\n" +
                $"target    {report.summary.platform}\n" +
                $"product   {ProductName} ({CompanyName})\n" +
                $"package   {PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.FromBuildTargetGroup(BuildPipeline.GetBuildTargetGroup(report.summary.platform)))}\n");
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
            ConfigureIdentity();
            BuildReport report = BuildPipeline.BuildPlayer(options);
            WriteStamp(report, path);

            var s = report.summary;
            string errors = string.Join(" | ", report.steps.SelectMany(step => step.messages)
                .Where(m => m.type == LogType.Error || m.type == LogType.Exception).Select(m => m.content).Take(5));
            return $"{s.result} {path} {s.totalSize / 1048576f:F1} MB in {s.totalTime.TotalSeconds:F0}s errors={s.totalErrors} {errors}";
        }
    }
}
