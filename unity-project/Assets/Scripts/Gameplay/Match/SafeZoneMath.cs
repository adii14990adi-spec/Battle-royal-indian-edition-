using UnityEngine;
using BattleHorizons.Core;

namespace BattleHorizons.Gameplay.Match
{
    /// <summary>One safe-zone circle in world XZ metres plus its radius.</summary>
    public struct ZoneCircle
    {
        public Vector2 Center;
        public float Radius;

        public ZoneCircle(Vector2 center, float radius)
        {
            Center = center;
            Radius = Mathf.Max(radius, 0f);
        }
    }

    /// <summary>
    /// Pure geometry for the shrinking blue zone: next-circle placement,
    /// interpolation while shrinking, and inside/outside queries. Kept free of
    /// MonoBehaviour state so EditMode tests and the future server-authoritative
    /// match code share one implementation.
    /// </summary>
    public static class SafeZoneMath
    {
        /// <summary>
        /// Places the next circle inside the current one: radius scales by
        /// <paramref name="radiusFactor"/> and the centre shifts uniformly at
        /// random, but never far enough for the new circle to escape the old.
        /// </summary>
        public static ZoneCircle NextCircle(RandomStream rng, ZoneCircle current, float radiusFactor)
        {
            var factor = Mathf.Clamp(radiusFactor, 0.05f, 1f);
            var newRadius = current.Radius * factor;
            var maxOffset = Mathf.Max(0f, current.Radius - newRadius);

            var offset = Vector2.zero;
            if (rng != null && maxOffset > 0f)
            {
                offset = rng.NextPointInUnitDisc() * maxOffset;
            }

            return new ZoneCircle(current.Center + offset, newRadius);
        }

        /// <summary>Linear blend used while the zone shrinks (t in [0,1]).</summary>
        public static ZoneCircle Lerp(ZoneCircle a, ZoneCircle b, float t)
        {
            var clamped = Mathf.Clamp01(t);
            return new ZoneCircle(
                Vector2.Lerp(a.Center, b.Center, clamped),
                Mathf.Lerp(a.Radius, b.Radius, clamped));
        }

        public static bool Contains(ZoneCircle circle, float x, float z) =>
            DistanceOutside(circle, x, z) <= 0f;

        /// <summary>Metres beyond the rim (0 when inside).</summary>
        public static float DistanceOutside(ZoneCircle circle, float x, float z) =>
            Mathf.Max(0f, Vector2.Distance(new Vector2(x, z), circle.Center) - circle.Radius);
    }
}
