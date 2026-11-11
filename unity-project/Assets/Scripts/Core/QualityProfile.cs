using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

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
    /// The Low profile targets entry-level Android hardware; High is intended
    /// for mid/high devices. Selection uses reported device facts rather than
    /// guessing:
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
            // A 4 GB Unisoc T606-class device must land on Low.
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

        /// <summary>Applies the Unity quality level, URP runtime overrides, and frame-rate target.</summary>
        public static void Apply(QualityProfile profile)
        {
            var index = Mathf.Clamp(QualityIndex(profile), 0, QualitySettings.names.Length - 1);
            QualitySettings.SetQualityLevel(index, applyExpensiveChanges: true);
            ApplyPipelineOverrides(profile);
            _active = profile;
            _applied = true;

            ApplyFrameRate(profile);
        }

        /// <summary>
        /// The quality-level asset controls terrain, LOD and mip budgets; these
        /// URP settings live on the shared pipeline asset and therefore need an
        /// explicit runtime update when the player switches profiles.
        /// </summary>
        private static void ApplyPipelineOverrides(QualityProfile profile)
        {
            var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (pipeline == null)
            {
                GameLog.Warn("quality", "Active render pipeline is not URP; profile-specific render overrides were skipped.");
                return;
            }

            var high = profile == QualityProfile.High;
            pipeline.renderScale = RenderScale(profile);
            pipeline.shadowDistance = ShadowDistance(profile);
            pipeline.shadowCascadeCount = high ? 4 : 2;
            pipeline.mainLightShadowmapResolution = high ? 1024 : 512;
            pipeline.msaaSampleCount = high ? 2 : 1;
            pipeline.maxAdditionalLightsCount = high ? 4 : 2;
        }

        /// <summary>
        /// Apply the selected frame pacing target on mobile and desktop.
        /// Low targets 30 (the Unisoc floor), High targets 60 when the display can.
        /// Application.targetFrameRate caps the *rendered* frames, which is what
        /// keeps a 1-core Mali from thermally throttling within minutes.
        /// </summary>
        public static void ApplyFrameRate(QualityProfile profile)
        {
            var refresh = (int)Mathf.Max((float)Screen.currentResolution.refreshRateRatio.value, 60f);
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
        /// <see cref="ApplyPipelineOverrides"/> writes this value to the active URP
        /// asset whenever a profile is applied or changed.
        /// </summary>
        public static float RenderScale(QualityProfile profile) =>
            profile == QualityProfile.Low ? 0.85f : 1.0f;

        /// <summary>Camera/fog distance per profile; independent of the 50 m streaming disk.</summary>
        public static float ViewDistance(QualityProfile profile) =>
            profile == QualityProfile.Low ? 900f : 1600f;

        /// <summary>Fixed world-space visual streaming radius, independent of quality.</summary>
        public static float StreamRadius(QualityProfile profile) => 50f;

        /// <summary>Shadow distance per profile, kept in sync with QualitySettings.asset.</summary>
        public static float ShadowDistance(QualityProfile profile) =>
            profile == QualityProfile.Low ? 60f : 120f;
    }
}
