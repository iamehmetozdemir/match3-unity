using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Match3.EditorTools
{
    /// <summary>
    /// One-click builds, also usable from the command line:
    /// Unity -batchmode -quit -projectPath . -executeMethod Match3.EditorTools.BuildScript.BuildWebGL
    /// </summary>
    public static class BuildScript
    {
        private const string ScenePath = "Assets/Scenes/Main.unity";

        [MenuItem("Match3/Build WebGL")]
        public static void BuildWebGL()
        {
            PlayerSettings.WebGL.template = "PROJECT:Match3";
            // GitHub Pages does not send Content-Encoding headers for pre-compressed files.
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            Build(BuildTarget.WebGL, "Builds/WebGL");
        }

        [MenuItem("Match3/Build Windows")]
        public static void BuildWindows()
        {
            Build(BuildTarget.StandaloneWindows64, "Builds/Windows/Match3.exe");
        }

        private static void Build(BuildTarget target, string outputPath)
        {
            EnsureScene();
            PlayerSettings.productName = "Match3";

            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = outputPath,
                target = target,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;
            Debug.Log("Build " + target + ": " + summary.result + ", " + summary.totalSize / (1024 * 1024) + " MB -> " + outputPath);

            if (summary.result != BuildResult.Succeeded && Application.isBatchMode)
                EditorApplication.Exit(1);
        }

        // The game starts itself from code (see GameController.Bootstrap), so the scene can be empty.
        private static void EnsureScene()
        {
            if (File.Exists(ScenePath)) return;
            Directory.CreateDirectory("Assets/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, ScenePath);
        }
    }
}
