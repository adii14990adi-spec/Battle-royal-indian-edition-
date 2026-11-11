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
        public const string ApkName = "BattleHorizons.apk";
        public const string AabName = "BattleHorizons.aab";

        [MenuItem("Battle Horizons/Build/Android APK", priority = 20)]
        public static void BuildApkFromMenu() => BuildApk();

        [MenuItem("Battle Horizons/Build/Android AAB (Play Store)", priority = 21)]
        public static void BuildAabFromMenu() => BuildAab();

        public static void BuildApk() => Build(androidAppBundle: false);

        public static void BuildAab() => Build(androidAppBundle: true);

        /// <summary>
        /// CI entry point: configure the project, validate it, then build. One
        /// command, so a fresh checkout needs no manual setup step and a
        /// validation failure stops before an APK is produced.
        /// </summary>
        public static void ConfigureValidateAndBuildApk()
        {
            try
            {
                ProjectBootstrap.Configure();
            }
            catch (Exception exception)
            {
                Debug.LogError("[bh][build] configuration threw: " + exception);
                EditorApplication.Exit(1);
                return;
            }

            var failures = ProjectValidator.ValidateAll(out var report);
            Debug.Log("[bh][validate]\n" + report);
            if (failures > 0)
            {
                Debug.LogError(string.Format(
                    "[bh][build] refusing to build: {0} project validation failure(s)", failures));
                EditorApplication.Exit(1);
                return;
            }

            Build(androidAppBundle: false);
        }

        private static void Build(bool androidAppBundle)
        {
            try
            {
                if (!Prepare(out var scenes))
                {
                    EditorApplication.Exit(1);
                    return;
                }

                var outputPath = ResolveOutputPath(androidAppBundle);
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? OutputDirectory);
                EditorUserBuildSettings.buildAppBundle = androidAppBundle;

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

                if (!androidAppBundle)
                {
                    var projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Directory.GetCurrentDirectory();
                    var canonicalApkPath = Path.GetFullPath(Path.Combine(projectRoot, OutputDirectory, ApkName));
                    if (!string.Equals(Path.GetFullPath(outputPath), canonicalApkPath, StringComparison.Ordinal))
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(canonicalApkPath) ?? OutputDirectory);
                        File.Copy(outputPath, canonicalApkPath, overwrite: true);
                    }
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

        public static string ResolveSetting(string envVar, string cliFlag, string fallbackEnvVar = null)
        {
            var value = Environment.GetEnvironmentVariable(envVar);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }

            if (!string.IsNullOrEmpty(fallbackEnvVar))
            {
                value = Environment.GetEnvironmentVariable(fallbackEnvVar);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }
            }

            if (!string.IsNullOrEmpty(cliFlag))
            {
                var args = Environment.GetCommandLineArgs();
                for (var i = 0; i < args.Length - 1; i++)
                {
                    if (string.Equals(args[i], cliFlag, StringComparison.Ordinal))
                    {
                        var candidate = args[i + 1];
                        if (!string.IsNullOrWhiteSpace(candidate) && !candidate.StartsWith("-", StringComparison.Ordinal))
                        {
                            return candidate.Trim();
                        }
                    }
                }
            }

            return string.Empty;
        }

        private static string ResolveOutputPath(bool androidAppBundle)
        {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Directory.GetCurrentDirectory();
            var configured = ResolveSetting("BH_BUILD_OUTPUT", "-customBuildPath", "CUSTOM_BUILD_PATH");
            if (!androidAppBundle && !string.IsNullOrWhiteSpace(configured))
            {
                return Path.GetFullPath(Path.IsPathRooted(configured)
                    ? configured
                    : Path.Combine(projectRoot, configured));
            }

            return Path.GetFullPath(Path.Combine(projectRoot, OutputDirectory,
                androidAppBundle ? AabName : ApkName));
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

            if (!ApplySigning())
            {
                return false;
            }

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

        /// <summary>Keystore from the environment or GameCI command line — never from a committed file.</summary>
        private static bool ApplySigning()
        {
            var keystore = ResolveSetting("BH_KEYSTORE_PATH", "-androidKeystoreName", "ANDROID_KEYSTORE_NAME");
            if (string.IsNullOrWhiteSpace(keystore))
            {
                PlayerSettings.Android.useCustomKeystore = false;
                Debug.LogWarning("[bh][build] no release keystore configured: producing a debug-signed test APK; do not publish it");
                return true;
            }

            // Relative paths are resolved against the project root, because batch
            // mode's working directory is the editor install, not the project.
            if (!Path.IsPathRooted(keystore))
            {
                var projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Directory.GetCurrentDirectory();
                keystore = Path.GetFullPath(Path.Combine(projectRoot, keystore));
            }

            var keystorePass = ResolveSetting("BH_KEYSTORE_PASS", "-androidKeystorePass", "ANDROID_KEYSTORE_PASS");
            var aliasName = ResolveSetting("BH_KEYALIAS_NAME", "-androidKeyaliasName", "ANDROID_KEYALIAS_NAME");
            var aliasPass = ResolveSetting("BH_KEYALIAS_PASS", "-androidKeyaliasPass", "ANDROID_KEYALIAS_PASS");
            if (!File.Exists(keystore) || string.IsNullOrEmpty(keystorePass) ||
                string.IsNullOrEmpty(aliasName) || string.IsNullOrEmpty(aliasPass))
            {
                Debug.LogError("[bh][build] release signing was requested but the keystore or one of the BH_* signing values is missing");
                return false;
            }

            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName = keystore;
            PlayerSettings.Android.keystorePass = keystorePass;
            PlayerSettings.Android.keyaliasName = aliasName;
            PlayerSettings.Android.keyaliasPass = aliasPass;
            Debug.Log("[bh][build] using the configured release keystore (values are not logged)");
            return true;
        }
    }
}
