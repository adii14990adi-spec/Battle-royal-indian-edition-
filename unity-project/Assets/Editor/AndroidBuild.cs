using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace BattleHorizons.EditorTools
{
    /// <summary>
    /// Android build entry points, so the APK can be produced without the GUI:
    ///
    /// <code>
    /// Unity -batchmode -quit -projectPath unity-project -buildTarget Android \
    ///       -executeMethod BattleHorizons.EditorTools.AndroidBuild.BuildApk
    /// </code>
    ///
    /// It refuses to build when the project has not been configured (no URP asset
    /// or no build scenes), and it reports the *real* result: the APK path and
    /// size on success, an error and a non-zero exit on failure. It never prints a
    /// success line for a build that did not happen.
    ///
    /// Signing: a release keystore is taken from the environment when present
    /// (<c>BH_KEYSTORE_PATH</c>, <c>BH_KEYSTORE_PASS</c>, <c>BH_KEYALIAS_NAME</c>,
    /// <c>BH_KEYALIAS_PASS</c>) — that is how CI injects secrets — otherwise the
    /// build is unsigned/debug and says so.
    /// </summary>
    public static class AndroidBuild
    {
        public const string OutputDirectory = "Builds";
        public const string ApkName = "battle-horizons.apk";
        public const string AabName = "battle-horizons.aab";

        [MenuItem("Battle Horizons/Build/Android APK", priority = 20)]
        public static void BuildApkFromMenu() => BuildApk();

        [MenuItem("Battle Horizons/Build/Android AAB (Play Store)", priority = 21)]
        public static void BuildAabFromMenu() => BuildAab();

        public static void BuildApk() => Build(androidAppBundle: false);

        public static void BuildAab() => Build(androidAppBundle: true);

        private static void Build(bool androidAppBundle)
        {
            try
            {
                if (!Prepare(out var scenes))
                {
                    EditorApplication.Exit(1);
                    return;
                }

                var outputPath = Path.Combine(OutputDirectory,
                    androidAppBundle ? AabName : ApkName);
                Directory.CreateDirectory(OutputDirectory);

                var options = new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = outputPath,
                    target = BuildTarget.Android,
                    targetGroup = BuildTargetGroup.Android,
                    options = BuildOptions.None,
                };

                Debug.Log(string.Format("[bh][build] starting Android {0} build: {1}",
                    androidAppBundle ? "AAB" : "APK", outputPath));

                var report = BuildPipeline.BuildPlayer(options);
                var summary = report.summary;

                if (summary.result != BuildResult.Succeeded)
                {
                    Debug.LogError(string.Format(
                        "[bh][build] FAILED: {0} error(s), {1} warning(s). See the log above for the first error.",
                        summary.totalErrors, summary.totalWarnings));
                    EditorApplication.Exit(1);
                    return;
                }

                if (!File.Exists(outputPath) || new FileInfo(outputPath).Length < 1024 * 1024)
                {
                    Debug.LogError("[bh][build] BuildPipeline reported success but " + outputPath +
                                   " does not exist or is implausibly small");
                    EditorApplication.Exit(1);
                    return;
                }

                var size = new FileInfo(outputPath).Length;
                Debug.Log(string.Format(
                    "[bh][build] SUCCESS: {0} ({1:F1} MB, {2} scenes, {3} s). " +
                    "Install with: adb install -r {0}",
                    outputPath, size / (1024.0 * 1024.0), scenes.Length, summary.totalTime.TotalSeconds));
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogError("[bh][build] exception: " + exception);
                EditorApplication.Exit(1);
            }
        }

        private static bool Prepare(out string[] scenes)
        {
            scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();

            if (scenes.Length == 0)
            {
                Debug.LogError("[bh][build] no enabled scenes in EditorBuildSettings. " +
                               "Run: -executeMethod BattleHorizons.EditorTools.ProjectBootstrap.ConfigureFromCommandLine");
                return false;
            }

            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
            {
                Debug.LogError("[bh][build] Android build support is not installed for this editor. " +
                               "Install it from Unity Hub > Installs > Add Modules.");
                return false;
            }

            if (UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline == null)
            {
                Debug.LogError("[bh][build] no render pipeline asset is assigned. Run Project Setup > Configure first.");
                return false;
            }

            ApplySigning();

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                Debug.Log("[bh][build] switching the active build target to Android (this re-imports assets)");
                if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android))
                {
                    Debug.LogError("[bh][build] could not switch to the Android build target");
                    return false;
                }
            }

            return true;
        }

        /// <summary>Keystore from the environment only — never from a committed file.</summary>
        private static void ApplySigning()
        {
            var keystore = Environment.GetEnvironmentVariable("BH_KEYSTORE_PATH");
            if (string.IsNullOrEmpty(keystore))
            {
                PlayerSettings.Android.useCustomKeystore = false;
                Debug.LogWarning("[bh][build] no BH_KEYSTORE_PATH set: the APK will be debug-signed. " +
                                 "Do not publish it; configure the four BH_* secrets in CI.");
                return;
            }

            if (!File.Exists(keystore))
            {
                Debug.LogError("[bh][build] BH_KEYSTORE_PATH points at a file that does not exist: " + keystore);
                return;
            }

            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName = keystore;
            PlayerSettings.Android.keystorePass = Environment.GetEnvironmentVariable("BH_KEYSTORE_PASS") ?? string.Empty;
            PlayerSettings.Android.keyaliasName = Environment.GetEnvironmentVariable("BH_KEYALIAS_NAME") ?? string.Empty;
            PlayerSettings.Android.keyaliasPass = Environment.GetEnvironmentVariable("BH_KEYALIAS_PASS") ?? string.Empty;
            Debug.Log("[bh][build] using the release keystore from BH_KEYSTORE_PATH (values are not logged)");
        }
    }
}
