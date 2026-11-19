namespace BattleHorizons.Gameplay.Match
{
    /// <summary>
    /// Minimal health contract. The safe zone, fall damage and (later) weapons
    /// all push damage through this so gameplay code never depends on a
    /// concrete player class.
    /// </summary>
    public interface IDamageable
    {
        bool IsAlive { get; }

        /// <param name="amount">Hit points, positive.</param>
        /// <param name="source">Stable id for the kill feed: "safe_zone", "fall", "wpn_sniper"…</param>
        void TakeDamage(float amount, string source);
    }
}
