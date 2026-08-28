using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Batch-mode build entry points for CI.
    ///
    /// Invoked as:
    ///   Unity -quit -batchmode -projectPath unity \
    ///         -executeMethod Game.EditorTools.BuildScript.BuildWindows \
    ///         -logFile -
    ///
    /// Every method exits with a non-zero code on failure. Without the explicit
    /// EditorApplication.Exit, a failed build still returns 0 and CI goes green
    /// on a broken artefact.
    /// </summary>
    public static class BuildScript
    {
        private const string DefaultOutputRoot = "../build";

        [MenuItem("Game/Build/Windows x64")]
        public static void BuildWindows() =>
            Build(BuildTarget.StandaloneWindows64, "Windows", "Game.exe");

        [MenuItem("Game/Build/Linux x64")]
        public static void BuildLinux() =>
            Build(BuildTarget.StandaloneLinux64, "Linux", "Game.x86_64");

        [MenuItem("Game/Build/macOS")]
        public static void BuildMac() =>
            Build(BuildTarget.StandaloneOSX, "macOS", "Game.app");

        [MenuItem("Game/Build/WebGL")]
        public static void BuildWebGL()
        {
            // Brotli plus no decompression fallback is the right pairing when the
            // host can set Content-Encoding. web/_headers does exactly that; if
            // you move off Cloudflare Pages, check the new host before shipping.
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
            PlayerSettings.WebGL.decompressionFallback = false;

            Build(BuildTarget.WebGL, "WebGL", string.Empty);
        }

        private static void Build(BuildTarget target, string folderName, string executableName)
        {
            string[] scenes = EnabledScenes();
            if (scenes.Length == 0)
            {
                Fail("No enabled scenes in Build Settings. Add at least one before building.");
                return;
            }

            string outputRoot = ArgumentValue("-buildOutput") ?? DefaultOutputRoot;
            string path = string.IsNullOrEmpty(executableName)
                ? $"{outputRoot}/{folderName}"
                : $"{outputRoot}/{folderName}/{executableName}";

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                target = target,
                locationPathName = path,
                options = BuildOptions.None,
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log(
                    $"Build succeeded: {path} " +
                    $"({summary.totalSize / (1024 * 1024)} MB in {summary.totalTime.TotalSeconds:0}s)");
                return;
            }

            Fail($"Build failed with result {summary.result} and {summary.totalErrors} error(s).");
        }

        private static string[] EnabledScenes() =>
            EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();

        private static string ArgumentValue(string flag)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == flag) return args[i + 1];
            }
            return null;
        }

        private static void Fail(string message)
        {
            Debug.LogError(message);
            if (Application.isBatchMode) EditorApplication.Exit(1);
        }
    }
}
