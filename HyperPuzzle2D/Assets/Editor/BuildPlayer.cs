using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
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
        const string AndroidAabOut = "Builds/Android/HyperSmash.aab";
        const string IosOut = "Builds/iOS";
        const string IosSimulatorOut = "Builds/iOSSimulator";
        const string MacOut = "Builds/Mac/HyperSmash.app";

        public static void BuildAndroid()
        {
            ConfigureAndroid();
            EditorUserBuildSettings.buildAppBundle = false;
            Run(BuildTarget.Android, AndroidOut, BuildOptions.None);
        }

        public static void BuildAndroidAab()
        {
            ConfigureAndroid();
            EditorUserBuildSettings.buildAppBundle = true;
            Run(BuildTarget.Android, AndroidAabOut, BuildOptions.None);
        }

        public static void BuildIOS()
        {
            ConfigureIOS();
            PlayerSettings.iOS.sdkVersion = iOSSdkVersion.DeviceSDK;
            Run(BuildTarget.iOS, IosOut, BuildOptions.None);
        }

        public static void BuildIOSSimulator()
        {
            ConfigureIOS();
            PlayerSettings.iOS.sdkVersion = iOSSdkVersion.SimulatorSDK;
            PlayerSettings.iOS.simulatorSdkArchitecture = AppleMobileArchitectureSimulator.ARM64;
            Run(BuildTarget.iOS, IosSimulatorOut, BuildOptions.None);
        }

        public static void BuildMac()
        {
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, "com.hyperpuzzle.hypersmash");
            Run(BuildTarget.StandaloneOSX, MacOut, BuildOptions.None);
        }

        static void ConfigureAndroid()
        {
            BrandAssets.EnsureAndApply();
            PlayerSettings.productName = "Hyper Smash";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.hyperpuzzle.hypersmash");
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetIl2CppCompilerConfiguration(NamedBuildTarget.Android, Il2CppCompilerConfiguration.Release);
            PlayerSettings.stripEngineCode = false;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.forceSDCardPermission = false;
            EditorUserBuildSettings.androidBuildSystem = AndroidBuildSystem.Gradle;
        }

        static void ConfigureIOS()
        {
            BrandAssets.EnsureAndApply();
            PlayerSettings.productName = "Hyper Smash";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, "com.hyperpuzzle.hypersmash");
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.iOS, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetIl2CppCompilerConfiguration(NamedBuildTarget.iOS, Il2CppCompilerConfiguration.Release);
            PlayerSettings.stripEngineCode = false;
            PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneAndiPad;
            PlayerSettings.iOS.targetOSVersionString = "15.0";
            PlayerSettings.statusBarHidden = true;
            PlayerSettings.iOS.requiresPersistentWiFi = false;
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

            AssetDatabase.SaveAssets();
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
