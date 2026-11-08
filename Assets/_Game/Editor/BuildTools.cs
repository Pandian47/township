using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace TownBuilder.EditorTools
{
    /// <summary>
    /// Android builds:
    ///   * APK  (debug-signed)      - install directly on a phone for testing.
    ///   * AAB  (upload-key signed) - the format Google Play requires.
    ///
    /// Release signing never stores secrets in the project. The keystore location and passwords come from
    /// environment variables (set them in your own terminal, never commit them):
    ///   TOWNBUILDER_KEYSTORE        full path to the .keystore / .jks file (keep it OUTSIDE the repo, back it up)
    ///   TOWNBUILDER_KEYSTORE_PASS   keystore password
    ///   TOWNBUILDER_KEY_ALIAS       key alias (default: upload)
    ///   TOWNBUILDER_KEY_PASS        key password (defaults to the keystore password)
    ///
    /// Command line:
    ///   Unity.exe -batchmode -quit -projectPath . -executeMethod TownBuilder.EditorTools.BuildTools.BuildApkFromCommandLine
    ///   Unity.exe -batchmode -quit -projectPath . -executeMethod TownBuilder.EditorTools.BuildTools.BuildAabFromCommandLine
    /// Output: Builds/Android/
    /// </summary>
    public static class BuildTools
    {
        public const string OutputFolder = "Builds/Android";

        [MenuItem("TownBuilder/Build/Android APK (test on device)", priority = 60)]
        public static void BuildApkMenu() => Report(BuildApk());

        [MenuItem("TownBuilder/Build/Android AAB (Google Play)", priority = 61)]
        public static void BuildAabMenu() => Report(BuildAab());

        public static void BuildApkFromCommandLine() => ExitWith(BuildApk());

        public static void BuildAabFromCommandLine() => ExitWith(BuildAab());

        public static BuildResult BuildApk()
        {
            ConfigureCommon();
            EditorUserBuildSettings.buildAppBundle = false;
            PlayerSettings.Android.useCustomKeystore = false; // debug signing, fine for side-loading
            // Test APKs include developer mode (DEV button); the Play Store bundle (AAB) never does.
            return Build($"{OutputFolder}/TownBuilder-{PlayerSettings.bundleVersion}-{PlayerSettings.Android.bundleVersionCode}.apk",
                BuildOptions.None, "TOWNBUILDER_DEVTOOLS");
        }

        public static BuildResult BuildAab()
        {
            ConfigureCommon();
            if (!ConfigureReleaseSigning(out var error))
            {
                Debug.LogError($"[Build] AAB not built: {error}");
                return BuildResult.Failed;
            }

            EditorUserBuildSettings.buildAppBundle = true;
            try
            {
                return Build($"{OutputFolder}/TownBuilder-{PlayerSettings.bundleVersion}-{PlayerSettings.Android.bundleVersionCode}.aab",
                    BuildOptions.None);
            }
            finally
            {
                // Don't leave passwords in the editor session after the build.
                PlayerSettings.Android.keystorePass = string.Empty;
                PlayerSettings.Android.keyaliasPass = string.Empty;
                EditorUserBuildSettings.buildAppBundle = false;
            }
        }

        private static void ConfigureCommon()
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
                throw new InvalidOperationException(
                    "Android Build Support is not installed for this Unity version. Unity Hub > Installs > 6000.6.8f1 > Add modules > Android Build Support (+ SDK & NDK, OpenJDK).");

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);

            EditorUserBuildSettings.development = false;
        }

        private static bool ConfigureReleaseSigning(out string error)
        {
            error = null;
            string keystore = Environment.GetEnvironmentVariable("TOWNBUILDER_KEYSTORE");
            string storePass = Environment.GetEnvironmentVariable("TOWNBUILDER_KEYSTORE_PASS");
            string alias = Environment.GetEnvironmentVariable("TOWNBUILDER_KEY_ALIAS");
            string keyPass = Environment.GetEnvironmentVariable("TOWNBUILDER_KEY_PASS");

            if (string.IsNullOrEmpty(keystore) || !File.Exists(keystore))
            {
                error = "Upload keystore not found. Set TOWNBUILDER_KEYSTORE to your .keystore path (see Documentation/AndroidRelease.md).";
                return false;
            }

            if (string.IsNullOrEmpty(storePass))
            {
                error = "TOWNBUILDER_KEYSTORE_PASS is not set.";
                return false;
            }

            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName = keystore;
            PlayerSettings.Android.keystorePass = storePass;
            PlayerSettings.Android.keyaliasName = string.IsNullOrEmpty(alias) ? "upload" : alias;
            PlayerSettings.Android.keyaliasPass = string.IsNullOrEmpty(keyPass) ? storePass : keyPass;
            return true;
        }

        private static BuildResult Build(string outputPath, BuildOptions options, params string[] extraDefines)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0) throw new InvalidOperationException("No scenes in Build Settings. Run TownBuilder > Setup > Run Full Setup.");

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = options,
                extraScriptingDefines = extraDefines,
            });

            var summary = report.summary;
            Debug.Log($"[Build] {summary.result}: {outputPath} ({summary.totalSize / (1024f * 1024f):F1} MB, " +
                      $"{summary.totalErrors} errors, {summary.totalTime.TotalSeconds:F0}s)");
            return summary.result;
        }

        private static void Report(BuildResult result)
        {
            if (result == BuildResult.Succeeded) EditorUtility.RevealInFinder(OutputFolder);
            else EditorUtility.DisplayDialog("Build failed", "See the Console for details.", "OK");
        }

        private static void ExitWith(BuildResult result)
        {
            EditorApplication.Exit(result == BuildResult.Succeeded ? 0 : 1);
        }
    }
}
