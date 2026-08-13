using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace HyperPuzzle2D.Editor
{
    /// <summary>
    /// Batch-mode entry points for CI / local packaging.
    /// Usage:
    ///   Unity -batchmode -projectPath ... -executeMethod HyperPuzzle2D.Editor.BuildPlayer.BuildAndroid
    ///   Unity -batchmode -projectPath ... -executeMethod HyperPuzzle2D.Editor.BuildPlayer.BuildIOS
    ///   Unity -batchmode -projectPath ... -executeMethod HyperPuzzle2D.Editor.BuildPlayer.BuildMac
    /// </summary>
    public static class BuildPlayer
    {
        const string AndroidOut = "Builds/Android/HyperSmash.apk";
        const string IosOut = "Builds/iOS";
        const string MacOut = "Builds/Mac/HyperSmash.app";

        public static void BuildAndroid()
        {
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.hyperpuzzle.hypersmash");
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.forceSDCardPermission = false;
            EditorUserBuildSettings.buildAppBundle = false;
            EditorUserBuildSettings.androidBuildSystem = AndroidBuildSystem.Gradle;

            Run(BuildTarget.Android, AndroidOut, BuildOptions.None);
        }

        public static void BuildIOS()
        {
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.iOS, "com.hyperpuzzle.hypersmash");
            PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneAndiPad;
            PlayerSettings.iOS.sdkVersion = iOSSdkVersion.DeviceSDK;
            Run(BuildTarget.iOS, IosOut, BuildOptions.None);
        }

        public static void BuildMac()
        {
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Standalone, "com.hyperpuzzle.hypersmash");
            Run(BuildTarget.StandaloneOSX, MacOut, BuildOptions.None);
        }

        static void Run(BuildTarget target, string relativeOut, BuildOptions options)
        {
            var scenes = EditorBuildSettingsScene.GetActiveSceneList(EditorBuildSettings.scenes);
            if (scenes == null || scenes.Length == 0)
            {
                throw new InvalidOperationException("No scenes in Build Settings. Expected Assets/Scenes/Boot.unity.");
            }

            var outPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), relativeOut));
            var dir = Path.GetDirectoryName(outPath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            Debug.Log($"[BuildPlayer] Building {target} → {outPath}");
            var report = BuildPipeline.BuildPlayer(scenes, outPath, target, options);
            var summary = report.summary;
            Debug.Log($"[BuildPlayer] Result={summary.result} size={summary.totalSize} errors={summary.totalErrors}");
            if (summary.result != BuildResult.Succeeded)
            {
                EditorApplication.Exit(1);
                return;
            }

            EditorApplication.Exit(0);
        }
    }
}
