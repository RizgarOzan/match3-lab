using System;
using System.IO;
using Match3Lab.Unity;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Match3Lab.UnityEditor
{
    /// <summary>
    /// The demo scene is generated, not hand-made: one camera and one <see cref="GameController"/>,
    /// everything else is constructed at runtime. That keeps the repository free of scene YAML that
    /// only Unity can merge, and lets a batch-mode build start from a clean checkout.
    /// </summary>
    public static class Builds
    {
        public const string ScenePath = "Assets/Match3Lab/Scenes/Demo.unity";
        public const string WebGLOutput = "../Builds/WebGL";

        [MenuItem("Match3 Lab/Create Demo Scene")]
        public static void CreateDemoScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            camGo.tag = "MainCamera";
            var cam = camGo.GetComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 7;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.10f, 0.11f, 0.15f);
            camGo.transform.position = new Vector3(0, 0, -10);

            new GameObject("Match3Lab", typeof(GameController));

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            Debug.Log("Created " + ScenePath);
        }

        /// <summary>Batch entry point: <c>-executeMethod Match3Lab.UnityEditor.Builds.WebGL</c>.</summary>
        public static void WebGL()
        {
            LevelSync.Sync();
            if (!File.Exists(ScenePath)) CreateDemoScene();

            PlayerSettings.companyName = "Rızgar Ozan";
            PlayerSettings.productName = "Match3 Lab";
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.template = "PROJECT:Match3Lab";
            PlayerSettings.runInBackground = true;

            string output = Path.GetFullPath(Path.Combine(Application.dataPath, "..", WebGLOutput));
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = output,
                target = BuildTarget.WebGL,
                options = BuildOptions.None,
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            Debug.Log("WebGL build: " + summary.result + ", " + summary.totalSize / (1024 * 1024) + " MB, " + summary.totalTime + " → " + output);
            if (summary.result != BuildResult.Succeeded && Application.isBatchMode) EditorApplication.Exit(1);
        }

        /// <summary>Batch entry point: a Windows player, used by tools/capture to record screenshots and GIF frames.</summary>
        public static void Windows()
        {
            LevelSync.Sync();
            if (!File.Exists(ScenePath)) CreateDemoScene();
            PlayerSettings.companyName = "Rızgar Ozan";
            PlayerSettings.productName = "Match3 Lab";
            PlayerSettings.defaultScreenWidth = 720;
            PlayerSettings.defaultScreenHeight = 1280;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = false;

            string output = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "../Builds/Windows/Match3Lab.exe"));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = output,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            });
            Debug.Log("Windows build: " + report.summary.result + " → " + output);
            if (report.summary.result != BuildResult.Succeeded && Application.isBatchMode) EditorApplication.Exit(1);
        }

        /// <summary>Batch entry point that only proves the scripts compile and the scene can be made.</summary>
        public static void CheckCompile()
        {
            LevelSync.Sync();
            if (!File.Exists(ScenePath)) CreateDemoScene();
            Debug.Log("CheckCompile: OK");
        }
    }
}
