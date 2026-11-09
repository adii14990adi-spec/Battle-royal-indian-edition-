using UnityEngine;
using UnityEngine.SceneManagement;

namespace BattleHorizons.Core
{
    /// <summary>
    /// The single entry point of the game.
    ///
    /// Mirrors the role of the Godot project's `PerfManager` autoload + boot
    /// scene: pick a quality profile from the device, apply it, then hand over to
    /// the first real scene (main menu, or straight into the Siran Island test
    /// scene when launched with `-bhScene=gameplay`).
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
                // The Godot build locks landscape (SCREEN_SENSOR_LANDSCAPE).
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
                SceneManager.LoadSceneAsync(target, LoadSceneMode.Single);
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
