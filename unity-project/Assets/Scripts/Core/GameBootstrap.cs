using System.Collections;
using BattleHorizons.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BattleHorizons.Core
{
    /// <summary>
    /// Single entry point: choose a quality profile, apply it, then load the
    /// first scene (normally the main menu, or a test scene via `-bhScene`).
    ///
    /// Scenes are referenced by *name* through <see cref="SceneNames"/> rather than
    /// by index so a missing scene fails with a readable message instead of
    /// loading the wrong one. Both scenes are added to EditorBuildSettings by
    /// Assets/Editor/ProjectBootstrap.cs.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameBootstrap : MonoBehaviour
    {
        public static class SceneNames
        {
            public const string SiranIslandGreybox = "SiranIsland_Greybox";
            public const string Boot = "Boot";
            public const string MainMenu = "MainMenu";
            public const string SiranIsland = "SiranIsland";
            public const string PlayerTest = "PlayerTest";
        }

        [Tooltip("Load this scene after boot. Empty = SiranIsland_Greybox.")]
        [SerializeField] private string nextScene = SceneNames.SiranIslandGreybox;

        [Tooltip("Force a quality profile instead of detecting one. -1 = auto.")]
        [SerializeField] private int forcedQuality = QualityProfiles.AutoDetect;

        [Tooltip("Keep the performance monitor alive across scenes.")]
        [SerializeField] private bool persistentPerfMonitor = true;
        private const float MinimumLoadingScreenSeconds = 0.65f;
        private const float ShowcaseLoadingScreenSeconds = 2.2f;

        public static GameBootstrap Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            var profile = ResolveQuality();
            QualityProfiles.Apply(profile);
            GameLog.Info("boot", string.Format(
                "quality profile {0} (override {1}, device detect {2})",
                profile, QualityProfiles.ReadOverride(), QualityProfiles.DetectFromDevice()));

            if (Application.isMobilePlatform)
            {
                // Keep mobile gameplay in landscape while allowing either orientation.
                Screen.orientation = ScreenOrientation.AutoRotation;
                Screen.autorotateToLandscapeLeft = true;
                Screen.autorotateToLandscapeRight = true;
                Screen.autorotateToPortrait = false;
                Screen.autorotateToPortraitUpsideDown = false;
            }

            if (persistentPerfMonitor && FindFirstObjectByType<PerformanceMonitor>() == null)
            {
                var monitor = new GameObject("PerformanceMonitor").AddComponent<PerformanceMonitor>();
                DontDestroyOnLoad(monitor.gameObject);
            }

            var target = ResolveNextScene();
            if (string.IsNullOrEmpty(target) || target == SceneNames.Boot)
            {
                return;
            }

            if (Application.CanStreamedLevelBeLoaded(target))
            {
                StartCoroutine(LoadSceneWithLoadingScreen(target));
            }
            else
            {
                GameLog.Error("boot", string.Format(
                    "scene '{0}' is not in EditorBuildSettings; run " +
                    "Battle Horizons > Project Setup > Configure (or add it by hand).",
                    target));
            }
        }

        private QualityProfile ResolveQuality()
        {
            if (forcedQuality != QualityProfiles.AutoDetect)
            {
                return forcedQuality == 0 ? QualityProfile.Low : QualityProfile.High;
            }

            var stored = QualityProfiles.ReadOverride();
            if (stored != QualityProfiles.AutoDetect)
            {
                return stored == 0 ? QualityProfile.Low : QualityProfile.High;
            }

            return QualityProfiles.DetectFromDevice();
        }

        private IEnumerator LoadSceneWithLoadingScreen(string target)
        {
            var bootCanvasObj = GameObject.Find("BootLoadingCanvas");
            var bootCanvas = bootCanvasObj != null ? bootCanvasObj.GetComponent<Canvas>() : null;
            var loadingScreen = LoadingScreenView.AttachOrBuild(bootCanvas);
            loadingScreen.SetProgress(0f);

            var load = SceneManager.LoadSceneAsync(target, LoadSceneMode.Single);
            if (load == null)
            {
                GameLog.Error("boot", "could not start loading scene '" + target + "'");
                yield break;
            }

            // Keep the Boot loading screen visible while assets load and animate
            // progress smoothly from 0% to 100% before activating the target scene.
            load.allowSceneActivation = false;
            var totalDuration = Application.isBatchMode
                ? MinimumLoadingScreenSeconds
                : Mathf.Max(MinimumLoadingScreenSeconds, ShowcaseLoadingScreenSeconds);
            var minimumDisplayTime = totalDuration;
            var elapsed = 0f;

            while (load.progress < 0.9f || minimumDisplayTime > 0f)
            {
                var dt = Time.unscaledDeltaTime;
                minimumDisplayTime -= dt;
                elapsed += dt;

                var timeFraction = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, totalDuration));
                var asyncFraction = Mathf.Clamp01(load.progress / 0.9f);
                var displayProgress = Mathf.Min(timeFraction, Mathf.Max(asyncFraction, timeFraction * 0.96f));
                loadingScreen.SetProgress(displayProgress);
                yield return null;
            }

            loadingScreen.SetProgress(1f);
            yield return null;
            load.allowSceneActivation = true;
        }

        private string ResolveNextScene()
        {
            // Command-line override, usable from a device via `adb shell am start ... --es bhScene gameplay`.
            var args = System.Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "-bhScene")
                {
                    return args[i + 1];
                }
            }

            return nextScene;
        }
    }
}
