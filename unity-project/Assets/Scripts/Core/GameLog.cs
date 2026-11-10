using UnityEngine;

namespace BattleHorizons.Core
{
    /// <summary>
    /// Compact tagged logging for gameplay systems and Android device tests.
    /// </summary>
    public static class GameLog
    {
        private const string Prefix = "[bh]";

        public static void Info(string tag, string message) =>
            Debug.Log(Format(tag, message));

        public static void Warn(string tag, string message) =>
            Debug.LogWarning(Format(tag, message));

        public static void Error(string tag, string message) =>
            Debug.LogError(Format(tag, message));

        public static void Assert(bool condition, string tag, string message)
        {
            if (condition)
            {
                return;
            }

            Error(tag, "ASSERTION FAILED: " + message);
        }

        private static string Format(string tag, string message) =>
            string.Concat(Prefix, "[", tag, "] ", message);

        /// <summary>One-line device summary, mirrors PerfManager.get_status_line().</summary>
        public static string DeviceSummary() =>
            string.Format(
                "Device: {0} | OS: {1} | GPU: {2} | GLES/API: {3} | shader level {4}",
                SystemInfo.deviceModel,
                SystemInfo.operatingSystem,
                SystemInfo.graphicsDeviceName,
                SystemInfo.graphicsDeviceVersion,
                SystemInfo.graphicsShaderLevel);

    }
}
