using BattleHorizons.Core;
using UnityEngine;

namespace BattleHorizons.UI
{
    /// <summary>
    /// RETIRED debug overlay. The on-device build shipped with a text HUD
    /// (position/FPS/zone diagnostics) drawn over the gameplay view; the
    /// production home screen and match flow no longer want any debug chrome,
    /// so this component now intentionally builds nothing. The type stays so
    /// older scenes/prefabs referencing it keep loading; ProjectBootstrap no
    /// longer adds it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WorldDebugHud : MonoBehaviour
    {
        private void Awake()
        {
            // Nothing to draw: the debug HUD was removed by design.
            GameLog.Info("hud", "WorldDebugHud is retired; no overlay created");
        }
    }
}
