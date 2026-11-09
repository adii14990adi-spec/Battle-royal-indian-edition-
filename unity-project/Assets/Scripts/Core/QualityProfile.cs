using UnityEngine;
using UnityEngine.Rendering;

namespace BattleHorizons.Core
{
    /// <summary>Which graphics profile the device is running.</summary>
    public enum QualityProfile
    {
        Low = 0,
        High = 1,
    }

    /// <summary>
    /// Runtime quality selection and application.
    ///
    /// The Godot project pinned one conservative profile for a Unisoc T606
    /// (docs/UNISOC_BUDGET.md). The Unity port keeps that as <see cref="Low"/>
    /// and adds <see cref="High"/> for the mid/high Android range, then picks
    /// between them from measured device facts instead of guessing:
    ///
    ///   * <see cref="SystemInfo.systemMemorySize"/> (MB)  — 8 GB baseline = 7000+
    ///   * <see cref="SystemInfo.processorCount"/>
    ///   * <see cref="SystemInfo.graphicsShaderLevel"/> / max texture size
    ///
    /// A player override is persisted in PlayerPrefs ("bh.quality") and always
    /// wins, which is what the settings screen binds to.
    ///
    /// Nothing here claims a frame rate. It changes budgets; whether the budget
    /// is met is measured on hardware (see docs/performance/ANDROID_PROFILES.md).
    /// </summary>
    public static class QualityProfiles
    {
        public const string OverridePrefsKey = "bh.quality";
        public const int AutoDetect = -1;

        private static bool _applied;
        private static QualityProfile _active = QualityProfile.Low;

        public static QualityProfile Active => _active;
        public static bool HasApplied => _applied;

        /// <summary>Quality level index in QualitySettings.asset created by the bootstrap.</summary>
        public static int QualityIndex(QualityProfile profile) =>
            profile == QualityProfile.High ? 1 : 0;

        /// <summary>Chooses a profile from device facts. Pure function of its inputs so it is testable.</summary>
        public static QualityProfile Detect(int systemMemoryMb, int processorCount, int graphicsMemoryMb)
        {
            // The Godot baseline (Unisoc T606, 4 GB) must land on Low.
            var memoryOk = systemMemoryMb >= 5500;
            var coresOk = processorCount >= 8;
            // graphicsMemoryMb is 0 on most Android GLES drivers; treat 0 as "unknown, do not veto".
            var graphicsOk = graphicsMemoryMb <= 0 || graphicsMemoryMb >= 1500;
            return memoryOk && coresOk && graphicsOk ? QualityProfile.High : QualityProfile.Low;
        }

        public static QualityProfile DetectFromDevice() =>
            Detect(SystemInfo.systemMemorySize, SystemInfo.processorCount, SystemInfo.graphicsMemorySize);

        /// <summary>Reads the persisted override; returns <see cref="AutoDetect"/> when unset.</summary>
        public static int ReadOverride()
        {
            return PlayerPrefs.HasKey(OverridePrefsKey) ? PlayerPrefs.GetInt(OverridePrefsKey, AutoDetect) : AutoDetect;
        }

        public static void SetOverride(int overrideValue)
        {
            if (overrideValue == AutoDetect)
            {
                PlayerPrefs.DeleteKey(OverridePrefsKey);
            }
            else
            {
                PlayerPrefs.SetInt(OverridePrefsKey, Mathf.Clamp(overrideValue, 0, 1));
            }
            PlayerPrefs.Save();
        }

        /// <summary>Applies a profile: QualitySettings level + frame-rate target.</summary>
        public static void Apply(QualityProfile profile)
        {
            var index = Mathf.Clamp(QualityIndex(profile), 0, QualitySettings.names.Length - 1);
            QualitySettings.SetQualityLevel(index, applyExpensiveChanges: true);
            _active = profile;
            _applied = true;

            ApplyFrameRate(profile);
        }

        /// <summary>
        /// Frame pacing, ported from `application/run/max_fps=30` in project.godot.
        /// Low targets 30 (the Unisoc floor), High targets 60 when the display can.
        /// Application.targetFrameRate caps the *rendered* frames, which is what
        /// keeps a 1-core Mali from thermally throttling within minutes.
        /// </summary>
        public static void ApplyFrameRate(QualityProfile profile)
        {
            var refresh = (int)Mathf.Max(Screen.currentResolution.refreshRateRatio.value, 60f);
            if (Application.isMobilePlatform)
            {
                Application.targetFrameRate = profile == QualityProfile.Low ? 30 : Mathf.Min(60, refresh);
            }
            else
            {
                Application.targetFrameRate = profile == QualityProfile.Low ? 30 : 60;
            }
            QualitySettings.vSyncCount = 0; // targetFrameRate is the pacing control on Android.
        }

        /// <summary>
        /// Render scale, ported from the "reduced render resolution" requirement.
        /// URP's own render-scale knob lives on the pipeline asset; this returns the
        /// value the bootstrap writes into the URP asset for each profile so a
        /// runtime switch is one assignment.
        /// </summary>
        public static float RenderScale(QualityProfile profile) =>
            profile == QualityProfile.Low ? 0.85f : 1.0f;

        /// <summary>Distance at which world chunks stop being drawn, per profile.</summary>
        public static float ViewDistance(QualityProfile profile) =>
            profile == QualityProfile.Low ? 900f : 1600f;

        /// <summary>Chunk streaming radius per profile (the map is 500 m chunks).</summary>
        public static float StreamRadius(QualityProfile profile) =>
            profile == QualityProfile.Low ? 1100f : 1600f;

        /// <summary>Shadow distance per profile, kept in sync with QualitySettings.asset.</summary>
        public static float ShadowDistance(QualityProfile profile) =>
            profile == QualityProfile.Low ? 60f : 120f;
    }
}
