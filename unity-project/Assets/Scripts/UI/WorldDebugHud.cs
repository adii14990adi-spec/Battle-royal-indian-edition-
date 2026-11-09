using BattleHorizons.Core;
using BattleHorizons.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BattleHorizons.UI
{
    /// <summary>
    /// On-screen telemetry, port of `scripts/ui/world_debug_hud.gd`.
    ///
    /// The Godot HUD existed because a map that failed to bake looked identical to
    /// a map that worked (the "MISSING BAKE" incident in the README). The same
    /// information is exposed here for the same reason: chunk counts, zone under
    /// the camera, water surface and frame time, all from live state rather than
    /// from a claim.
    ///
    /// IMGUI (<see cref="OnGUI"/>) on purpose: it survives a missing font, needs no
    /// canvas, and is stripped out of release builds by the BH_DEBUG_HUD define.
    /// </summary>
    public sealed class WorldDebugHud : MonoBehaviour
    {
        [SerializeField] private bool visible = true;

        private GUIStyle _style;
        private PerformanceMonitor _perf;
        private SiranChunkStreamer _streamer;
        private SiranWorldBuilder _world;
        private Vector3 _lastCameraPosition;
        private float _cachedFps;
        private float _refreshTimer;

        private void Awake()
        {
            _perf = FindFirstObjectByType<PerformanceMonitor>();
            _world = FindFirstObjectByType<SiranWorldBuilder>();
            _streamer = FindFirstObjectByType<SiranChunkStreamer>();
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f3Key.wasPressedThisFrame)
            {
                visible = !visible;
            }

            var touchscreen = Touchscreen.current;
            if (touchscreen != null && touchscreen.touches.Count > 2)
            {
                // Three fingers toggles the HUD, the same gesture the Godot debug
                // HUD used so testers do not need a keyboard on the phone.
                var held = 0;
                for (var i = 0; i < touchscreen.touches.Count; i++)
                {
                    if (touchscreen.touches[i].isPressed)
                    {
                        held++;
                    }
                }

                if (held >= 3)
                {
                    visible = !visible;
                }
            }

            _refreshTimer += Time.unscaledDeltaTime;
            if (_refreshTimer < 0.25f)
            {
                return;
            }

            _refreshTimer = 0f;
            _cachedFps = 1f / Mathf.Max(Time.smoothDeltaTime, 0.0001f);
            var camera = Camera.main;
            if (camera != null)
            {
                _lastCameraPosition = camera.transform.position;
            }
        }

        private void OnGUI()
        {
#if BH_DEBUG_HUD || UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!visible)
            {
                return;
            }

            _style ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.Max(12, Screen.height / 42),
                normal = { textColor = new Color(0.92f, 1f, 0.92f) },
            };

            var data = _world != null ? _world.Data : null;
            var zone = SiranMapConfig.ZoneAt(_lastCameraPosition.x, _lastCameraPosition.z);
            var groundY = data != null ? data.SampleHeight(_lastCameraPosition.x, _lastCameraPosition.z) : 0f;
            var water = data != null
                ? IslandShapeMath.WaterSurfaceAt(_lastCameraPosition.x, _lastCameraPosition.z)
                : 0f;

            // Read the budget counters only when the HUD is drawn (twice a second):
            // these are the numbers Android profiles are judged on, and reading them
            // every frame would change the thing being measured.
            var budget = _perf != null
                ? string.Format("   {0:F1} ms avg / {1:F1} ms 1% low   {2} set-pass   {3:F0} MB reserved",
                    _perf.AverageFrameMs, _perf.OnePercentLowFrameMs, _perf.SetPassCalls, _perf.PeakReservedMb)
                : string.Empty;

            var text = string.Concat(
                "FPS ", _cachedFps.ToString("F0"),
                "   quality ", QualityProfiles.Active,
                budget, "\n",
                data != null
                    ? string.Format("island {0}x{0} @ {1:F0} m   chunks {2}/{3} resident\n",
                        data.Size, data.SpacingM, _streamer != null ? _streamer.ResidentChunkCount : -1,
                        SiranMapConfig.ChunkCount)
                    : "ISLAND DATA NOT LOADED\n",
                string.Format("pos {0:F0}, {1:F0}, {2:F0}   ground {3:F1} m   water {4:F1} m\n",
                    _lastCameraPosition.x, _lastCameraPosition.y, _lastCameraPosition.z, groundY, water),
                "zone ", zone != null ? zone.DisplayName : "(outside every zone)");

            var rect = new Rect(16f, 16f, Screen.width * 0.6f, Screen.height * 0.25f);
            GUI.color = new Color(0f, 0f, 0f, 0.45f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(rect.x + 10f, rect.y + 8f, rect.width, rect.height), text, _style);
#endif
        }
    }
}
