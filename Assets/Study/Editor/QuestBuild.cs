using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Study.EditorTools
{
    // Quest 3 APK of the Lesson scene. arg = "bloom" or "nobloom": post-processing on the
    // headset camera, for the frame-rate comparison. The scene on disk is not changed.
    public static class QuestBuild
    {
        const string ScenePath = "Assets/Study/Scenes/Lesson.unity";

        public static string BuildApk(string arg)
        {
            bool bloom = arg == "bloom";
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);

            var scene = EditorSceneManager.OpenScene(ScenePath);
            foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                cam.GetComponent<UniversalAdditionalCameraData>().renderPostProcessing = bloom;
            // A copy, so the bloom setting never reaches Lesson.unity. Overwrites one left by a crashed build.
            EditorSceneManager.SaveScene(scene, "Assets/Study/Scenes/Lesson_QuestBuild.unity", true);

            string path = $"Builds/Quest/lesson_{(bloom ? "bloom" : "nobloom")}.apk";
            var options = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Study/Scenes/Lesson_QuestBuild.unity" },
                locationPathName = path,
                target = BuildTarget.Android,
                options = BuildOptions.None,
            };
            BuildReport report;
            try
            {
                report = BuildPipeline.BuildPlayer(options);
            }
            finally
            {
                AssetDatabase.DeleteAsset("Assets/Study/Scenes/Lesson_QuestBuild.unity");
                EditorSceneManager.OpenScene(ScenePath);
            }

            var s = report.summary;
            string errors = string.Join(" | ", report.steps.SelectMany(step => step.messages)
                .Where(m => m.type == LogType.Error || m.type == LogType.Exception).Select(m => m.content).Take(5));
            return $"{s.result} {path} {s.totalSize / 1048576f:F1} MB in {s.totalTime.TotalSeconds:F0}s errors={s.totalErrors} {errors}";
        }
    }
}
