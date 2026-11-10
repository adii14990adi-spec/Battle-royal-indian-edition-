using System.Collections;
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
            public const string Boot = "Boot";
            public const string MainMenu = "MainMenu";
            public const string SiranIsland = "SiranIsland";
            public const string PlayerTest = "PlayerTest";
        }

        [Tooltip("Load this scene after boot. Empty = MainMenu.")]
        [SerializeField] private string nextScene = SceneNames.MainMenu;

        [Tooltip("Force a quality profile instead of detecting one. -1 = auto.")]
        [SerializeField] private int forcedQuality = QualityProfiles.AutoDetect;

        [Tooltip("Keep the performance monitor alive across scenes.")]
        [SerializeField] private bool persistentPerfMonitor = true;
        private const float MinimumLoadingScreenSeconds = 0.65f;

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
            var load = SceneManager.LoadSceneAsync(target, LoadSceneMode.Single);
            if (load == null)
            {
                GameLog.Error("boot", "could not start loading scene '" + target + "'");
                yield break;
            }

            // Keep the black Boot scene visible while assets load and briefly
            // afterward, so fast menu loads do not skip the startup screen.
            load.allowSceneActivation = false;
            var minimumDisplayTime = MinimumLoadingScreenSeconds;
            while (load.progress < 0.9f || minimumDisplayTime > 0f)
            {
                minimumDisplayTime -= Time.unscaledDeltaTime;
                yield return null;
            }

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
