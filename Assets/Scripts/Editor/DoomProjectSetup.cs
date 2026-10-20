using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Doom.EditorTools
{
    public static class DoomProjectSetup
    {
        const string ScenePath = "Assets/Scenes/Main.unity";
        const string BuildPath = "Builds/Windows/DOOM.exe";

        /// <summary>Creates the (empty) main scene, registers it for builds and applies Windows player settings.</summary>
        [MenuItem("Doom/Setup Project")]
        public static void Setup()
        {
            if (!File.Exists(ScenePath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ScenePath)!);
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                // The game bootstraps itself at runtime; this object just makes the scene self-explanatory.
                new GameObject("DOOM (spawned at runtime by Doom.Bootstrap)");
                EditorSceneManager.SaveScene(scene, ScenePath);
            }

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

            PlayerSettings.companyName = "DoomUnity";
            PlayerSettings.productName = "DOOM";
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.defaultIsNativeResolution = true;
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = false;
            PlayerSettings.visibleInBackground = true;
            PlayerSettings.allowFullscreenSwitch = true;

            AssetDatabase.SaveAssets();
            Debug.Log("[Doom] Project setup complete: scene registered, Windows player settings applied.");
        }

        [MenuItem("Doom/Build Windows (x64)")]
        public static void BuildWindows()
        {
            Setup();
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = BuildPath,
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None
            };
            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result == BuildResult.Succeeded)
                Debug.Log($"[Doom] Build succeeded: {Path.GetFullPath(BuildPath)} ({report.summary.totalSize / (1024 * 1024)} MB)");
            else
                Debug.LogError($"[Doom] Build failed: {report.summary.result}");

            if (Application.isBatchMode)
                EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
        }
    }
}
