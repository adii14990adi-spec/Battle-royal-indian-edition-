using System.Text;
using UnityEngine;
using UnityEngine.Profiling;

namespace BattleHorizons.Core
{
    /// <summary>
    /// Port of `scripts/autoload/perf_manager.gd`.
    ///
    /// Every second it logs the same numbers the Godot build logged, under the
    /// same `[perf]` tag, so `adb logcat -s Unity | grep [perf]` and the existing
    /// docs/UNISOC_BUDGET.md budgets still apply:
    ///
    ///     [perf] fps=.. draws=.. tris=.. static_mb=.. gfx_mb=.. quality=..
    ///
    /// This component allocates nothing per frame: the StringBuilder is reused
    /// and formatting happens once per interval.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PerformanceMonitor : MonoBehaviour
    {
        public const float LogIntervalSeconds = 1f;

        [Tooltip("Log device + quality facts once at startup.")]
        [SerializeField] private bool logStartupBanner = true;

        [Tooltip("Seconds between [perf] log lines. 0 disables periodic logging.")]
        [SerializeField] private float interval = LogIntervalSeconds;

        // 240 frames is 4 s at 60 Hz / 8 s at 30 Hz: enough to see a hitch without
        // holding a stale number for a whole match.
        private const int FrameHistorySize = 240;

        private readonly StringBuilder _builder = new StringBuilder(192);
        private readonly float[] _frameHistory = new float[FrameHistorySize];
        private readonly float[] _sortScratch = new float[FrameHistorySize];
        private int _frameHistoryCount;
        private int _frameHistoryCursor;
        private ProfilerRecorder _drawCalls;
        private ProfilerRecorder _batches;
        private ProfilerRecorder _triangles;
        private float _accumulator;
        private int _frames;
        private float _frameTimeSum;
        private long _peakReservedBytes;

        /// <summary>Smoothed frames per second over the last interval (0 until the first sample).</summary>
        public float FramesPerSecond { get; private set; }

        /// <summary>Smoothed frame time in milliseconds over the last interval.</summary>
        public float FrameTimeMs { get; private set; }

        /// <summary>Peak total memory Unity has reserved, in MB (a proxy for PSS growth).</summary>
        public float PeakReservedMb => _peakReservedBytes / (1024f * 1024f);

        /// <summary>
        /// Mean frame time over the recent window. This is the number the profile
        /// budget is written against (33.3 ms → 30 fps).
        /// </summary>
        public float AverageFrameMs
        {
            get
            {
                if (_frameHistoryCount == 0)
                {
                    return 0f;
                }

                var sum = 0f;
                for (var i = 0; i < _frameHistoryCount; i++)
                {
                    sum += _frameHistory[i];
                }

                return sum / _frameHistoryCount;
            }
        }

        /// <summary>
        /// 99th-percentile frame time: the slow 1 % of frames, which is what a
        /// player actually notices. Sorted into a scratch buffer, so no allocation.
        /// </summary>
        public float OnePercentLowFrameMs
        {
            get
            {
                if (_frameHistoryCount == 0)
                {
                    return 0f;
                }

                System.Array.Copy(_frameHistory, _sortScratch, _frameHistoryCount);
                System.Array.Sort(_sortScratch, 0, _frameHistoryCount);
                var index = Mathf.Clamp(Mathf.FloorToInt(_frameHistoryCount * 0.99f), 0, _frameHistoryCount - 1);
                return _sortScratch[index];
            }
        }

        /// <summary>Latest draw-call substitute: URP set-pass calls. -1 when the counter is unavailable.</summary>
        public int SetPassCalls => _drawCalls.Valid ? (int)_drawCalls.LastValue : -1;

        private void Awake()
        {
            _drawCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
            _batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
            _triangles = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");

            if (logStartupBanner)
            {
                LogBanner();
            }
        }

        private void Update()
        {
            _frames++;
            _frameTimeSum += Time.unscaledDeltaTime;

            _frameHistory[_frameHistoryCursor] = Time.unscaledDeltaTime * 1000f;
            _frameHistoryCursor = (_frameHistoryCursor + 1) % FrameHistorySize;
            if (_frameHistoryCount < FrameHistorySize)
            {
                _frameHistoryCount++;
            }

            var reserved = Profiler.GetTotalReservedMemoryLong();
            if (reserved > _peakReservedBytes)
            {
                _peakReservedBytes = reserved;
            }

            if (interval <= 0f)
            {
                return;
            }

            _accumulator += Time.unscaledDeltaTime;
            if (_accumulator < interval)
            {
                return;
            }

            FramesPerSecond = _frames / _accumulator;
            FrameTimeMs = _frameTimeSum * 1000f / Mathf.Max(_frames, 1);
            _accumulator = 0f;
            _frames = 0;
            _frameTimeSum = 0f;

            GameLog.Info("perf", BuildLine());

            var budgetMs = QualityProfiles.Active == QualityProfile.High ? 16.6f : 33.3f;
            if (FrameTimeMs > budgetMs * 1.1f)
            {
                GameLog.Warn("perf", string.Format(
                    "frame time {0:F1} ms exceeds the {1} budget of {2:F1} ms - reduce draw distance or resolution",
                    FrameTimeMs, QualityProfiles.Active, budgetMs));
            }
        }

        private string BuildLine()
        {
            _builder.Clear();
            _builder.Append("fps=").Append(FramesPerSecond.ToString("F1"));
            _builder.Append(" frame_ms=").Append(FrameTimeMs.ToString("F2"));
            _builder.Append(" draws=").Append(_drawCalls.Valid ? _drawCalls.LastValue : -1);
            _builder.Append(" batches=").Append(_batches.Valid ? _batches.LastValue : -1);
            _builder.Append(" tris=").Append(_triangles.Valid ? _triangles.LastValue : -1);
            _builder.Append(" static_mb=").Append(BytesToMb(Profiler.GetMonoUsedSizeLong()).ToString("F1"));
            _builder.Append(" reserved_mb=").Append(PeakReservedMb.ToString("F1"));
            _builder.Append(" quality=").Append(QualityProfiles.Active);
            _builder.Append(" resolved_scale=").Append(QualityProfiles.RenderScale(QualityProfiles.Active).ToString("F2"));
            return _builder.ToString();
        }

        private void LogBanner()
        {
            GameLog.Info("perf", "---- Battle Horizons (Unity 6 URP) boot ----");
            GameLog.Info("perf", GameLog.DeviceSummary());
            GameLog.Info("perf", string.Format(
                "cores={0} system_mem_mb={1} gfx_mem_mb={2} max_texture={3} supports_instancing={4} supports_astc={5}",
                SystemInfo.processorCount,
                SystemInfo.systemMemorySize,
                SystemInfo.graphicsMemorySize,
                SystemInfo.maxTextureSize,
                SystemInfo.supportsInstancing,
                SystemInfo.SupportsTextureFormat(TextureFormat.ASTC_8x8)));
            GameLog.Info("perf", string.Format(
                "quality={0} target_fps={1} shadow_distance={2} stream_radius={3} view_distance={4}",
                QualityProfiles.Active,
                Application.targetFrameRate,
                QualityProfiles.ShadowDistance(QualityProfiles.Active),
                QualityProfiles.StreamRadius(QualityProfiles.Active),
                QualityProfiles.ViewDistance(QualityProfiles.Active)));
        }

        private void OnDisable()
        {
            _drawCalls.Dispose();
            _batches.Dispose();
            _triangles.Dispose();
        }

        private static float BytesToMb(long bytes) => bytes / (1024f * 1024f);
    }
}
