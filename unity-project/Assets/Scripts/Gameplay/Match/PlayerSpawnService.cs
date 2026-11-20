using UnityEngine;
using BattleHorizons.Core;
using BattleHorizons.World;

namespace BattleHorizons.Gameplay.Match
{
    /// <summary>
    /// Generates the 50 drop points around the edge of Siran Island.
    ///
    /// The ring sits at <c>IslandRadiusM * RingRadiusFactor</c> so every player
    /// lands near the coast and rotates inward — classic battle-royale spread.
    /// Angles are evenly divided with a seeded jitter, and any candidate that
    /// lands in water (sea, lake or river, via IslandShapeMath.WaterSurfaceAt)
    /// is nudged inward deterministically until it is dry land.
    ///
    /// Pure static math: no scene access, fully testable in EditMode, and
    /// identical on every device for a given seed (validate_csharp.py rule 8).
    /// </summary>
    public static class PlayerSpawnService
    {
        public const int DefaultSpawnPointCount = 50;

        /// <summary>Ring radius as a fraction of SiranMapConfig.IslandRadiusM (1950 m).</summary>
        public const float RingRadiusFactor = 0.78f;

        /// <summary>Spawn capsule clearance above the ground, metres.</summary>
        public const float GroundClearanceM = 1.2f;

        private const int MaxWaterRetries = 6;

        /// <summary>
        /// Builds the spawn ring. <paramref name="island"/> supplies real
        /// terrain heights; when null (EditMode tests, boot scene) the zone
        /// flat heights from SiranMapConfig approximate the ground.
        /// </summary>
        public static Vector3[] GenerateEdgeSpawnPoints(
            int seed,
            int count = DefaultSpawnPointCount,
            SiranIslandData island = null)
        {
            count = Mathf.Clamp(count, 1, 256);
            var rng = new RandomStream(seed);
            var points = new Vector3[count];
            var baseRadius = SiranMapConfig.IslandRadiusM * RingRadiusFactor;

            for (var i = 0; i < count; i++)
            {
                var angle = (360f * i / count) + rng.Range(-0.5f, 0.5f) * (360f / count);

                var x = 0f;
                var z = 0f;
                var ground = 0f;
                var dry = false;
                for (var attempt = 0; attempt < MaxWaterRetries && !dry; attempt++)
                {
                    // Each retry jitters the angle off the flooded bearing and
                    // pulls the candidate inland, so lake/river crossings on the
                    // ring (Siran Lake south, Siran River east) resolve fast.
                    var jittered = angle + rng.Range(-1f, 1f) * 7f * attempt;
                    var radians = jittered * Mathf.Deg2Rad;
                    var radius = baseRadius * rng.Range(0.94f, 1.06f) * (1f - attempt * 0.04f);
                    x = Mathf.Cos(radians) * radius;
                    z = Mathf.Sin(radians) * radius;

                    ground = GroundHeight(x, z, island);
                    dry = ground > IslandShapeMath.WaterSurfaceAt(x, z);

                    // Keep thumbs from stacking: a candidate that lands on top
                    // of an already placed point walks further around the ring.
                    if (dry)
                    {
                        var crowded = false;
                        for (var k = 0; k < i; k++)
                        {
                            var dx = points[k].x - x;
                            var dz = points[k].z - z;
                            if (dx * dx + dz * dz < 25f * 25f)
                            {
                                crowded = true;
                                break;
                            }
                        }

                        if (crowded)
                        {
                            dry = false;
                            angle += 2.5f;
                        }
                    }
                }

                if (!dry)
                {
                    // Deterministic last resort: walk the final bearing inland in
                    // 5 % steps until the ground clears the water line.
                    var radians = angle * Mathf.Deg2Rad;
                    for (var factor = 0.9f; factor >= 0.2f && !dry; factor -= 0.05f)
                    {
                        x = Mathf.Cos(radians) * baseRadius * factor;
                        z = Mathf.Sin(radians) * baseRadius * factor;
                        ground = GroundHeight(x, z, island);
                        dry = ground > IslandShapeMath.WaterSurfaceAt(x, z);
                    }
                }

                points[i] = new Vector3(x, ground + GroundClearanceM, z);
            }

            return points;
        }

        /// <summary>True when the point is inside the map, on land and above the water line.</summary>
        public static bool IsValidSpawnPoint(Vector3 point, SiranIslandData island = null)
        {
            var x = point.x;
            var z = point.z;
            if (Mathf.Abs(x) > SiranMapConfig.HalfExtent || Mathf.Abs(z) > SiranMapConfig.HalfExtent)
            {
                return false;
            }

            var ground = GroundHeight(x, z, island);
            return ground > IslandShapeMath.WaterSurfaceAt(x, z) && point.y >= ground;
        }

        private static float GroundHeight(float x, float z, SiranIslandData island)
        {
            if (island != null && island.IsLoaded)
            {
                return island.SampleHeight(x, z);
            }

            // Greybox estimate: the flattening height of the zone under the point,
            // or a small coastal shelf outside every zone.
            var zone = SiranMapConfig.ZoneAt(x, z);
            return zone != null ? zone.FlatHeight : SiranMapConfig.SeaLevel + 3f;
        }
    }
}
