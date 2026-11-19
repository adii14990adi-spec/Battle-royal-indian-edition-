using System.Collections.Generic;
using BattleHorizons.Core;
using BattleHorizons.Gameplay.Loot;
using BattleHorizons.Gameplay.Match;
using BattleHorizons.World;
using NUnit.Framework;
using UnityEngine;

namespace BattleHorizons.Tests.EditMode
{
    /// <summary>
    /// Static-contract tests for the gameplay foundations: deterministic RNG,
    /// the loot tier rules (Tier 3 = Military Base only), the 50-point edge
    /// spawn ring and the safe-zone geometry. No scene, no play mode required.
    /// </summary>
    public class GameplaySystemsTests
    {
        // --- RandomStream -------------------------------------------------

        [Test]
        public void RandomStreamIsDeterministicForTheSameSeed()
        {
            var a = new RandomStream(20261119);
            var b = new RandomStream(20261119);
            for (var i = 0; i < 256; i++)
            {
                Assert.AreEqual(a.NextUInt(), b.NextUInt(), $"diverged at draw {i}");
            }
        }

        [Test]
        public void RandomStreamDifferentSeedsDiverge()
        {
            var a = new RandomStream(1);
            var b = new RandomStream(2);
            var same = true;
            for (var i = 0; i < 16; i++)
            {
                same &= a.NextUInt() == b.NextUInt();
            }

            Assert.IsFalse(same, "different seeds produced identical 16-draw prefixes");
        }

        [Test]
        public void RandomStreamRangesStayInBounds()
        {
            var rng = new RandomStream(7);
            for (var i = 0; i < 1000; i++)
            {
                var f = rng.NextFloat();
                Assert.GreaterOrEqual(f, 0f);
                Assert.Less(f, 1f);

                var n = rng.Range(3, 9);
                Assert.GreaterOrEqual(n, 3);
                Assert.LessOrEqual(n, 9);

                Assert.AreEqual(5, rng.Range(5, 2), "inverted int range must clamp to min");
            }

            Assert.IsFalse(rng.Chance(0f));
            Assert.IsTrue(rng.Chance(1f));
        }

        [Test]
        public void WeightedIndexHonoursWeights()
        {
            var rng = new RandomStream(11);
            var weights = new List<float> { 0f, 0f, 1f };
            for (var i = 0; i < 100; i++)
            {
                Assert.AreEqual(2, rng.WeightedIndex(weights), "zero-weight entries must never be picked");
            }

            Assert.AreEqual(0, rng.WeightedIndex(new List<float> { 0f, 0f }), "all-zero weights fall back to 0");
            Assert.AreEqual(-1, rng.WeightedIndex(new List<float>()), "empty weights return -1");
        }

        // --- Loot tier contract -------------------------------------------

        [Test]
        public void MilitaryBaseIsTheOnlyTier3Zone()
        {
            var config = DefaultLootContent.CreateConfig();
            foreach (var rule in config.zoneRules)
            {
                if (rule.zoneId == DefaultLootContent.ZoneMilitaryBase)
                {
                    Assert.AreEqual(LootTier.Tier3, rule.maxTier);
                }
                else
                {
                    Assert.Less(rule.maxTier, LootTier.Tier3,
                        $"zone '{rule.zoneId}' ({rule.poiName}) must never roll Tier 3");
                }
            }
        }

        [Test]
        public void VillageCapsAtTier2AndForestQuarryIsResourcesOnly()
        {
            var config = DefaultLootContent.CreateConfig();

            var village = config.RuleFor(DefaultLootContent.ZoneVillage);
            Assert.IsNotNull(village);
            Assert.AreEqual(LootTier.Tier2, village.maxTier);

            var forest = config.RuleFor(DefaultLootContent.ZoneForestQuarry);
            Assert.IsNotNull(forest);
            Assert.AreEqual(LootTier.Tier1, forest.maxTier);
            CollectionAssert.Contains(forest.allowedCategories, ItemCategory.Resource);
            CollectionAssert.DoesNotContain(forest.allowedCategories, ItemCategory.Weapon);
        }

        [Test]
        public void DefaultItemsCoverEveryCategoryWithStableIds()
        {
            var items = DefaultLootContent.CreateItems();
            Assert.GreaterOrEqual(items.Count, 12);

            var categories = new HashSet<ItemCategory>();
            foreach (var pair in items)
            {
                Assert.IsFalse(string.IsNullOrEmpty(pair.Value.itemId));
                Assert.AreEqual(pair.Key, pair.Value.itemId, "dictionary key must match itemId");
                Assert.GreaterOrEqual(pair.Value.maxStack, 1);
                categories.Add(pair.Value.category);
            }

            CollectionAssert.Contains(categories, ItemCategory.Weapon);
            CollectionAssert.Contains(categories, ItemCategory.Resource);
            CollectionAssert.Contains(categories, ItemCategory.Healing);
        }

        [Test]
        public void LootTableRollsNeverExceedTheTierCapOrCategoryFilter()
        {
            var items = DefaultLootContent.CreateItems();
            var tables = DefaultLootContent.CreateTables(items);
            var rng = new RandomStream(99);

            for (var i = 0; i < 500; i++)
            {
                var militaryAtTier1 = tables["military"].RollOne(rng, LootTier.Tier1);
                Assert.IsNotNull(militaryAtTier1.Item, "military table must still roll Tier 1 support items");
                Assert.LessOrEqual(militaryAtTier1.Item.tier, LootTier.Tier1);

                var resourcesOnly = tables["resources"].RollOne(
                    rng, LootTier.Tier1, new HashSet<ItemCategory> { ItemCategory.Resource });
                Assert.IsNotNull(resourcesOnly.Item);
                Assert.AreEqual(ItemCategory.Resource, resourcesOnly.Item.category);
            }
        }

        [Test]
        public void LootTableReturnsEmptyRollWhenNothingIsEligible()
        {
            var table = DefaultLootContent.Table("empty_test");
            var roll = table.RollOne(new RandomStream(1), LootTier.Tier3);
            Assert.IsNull(roll.Item);
        }

        // --- 50-point edge spawn ring ---------------------------------------

        [Test]
        public void SpawnRingHasFiftyDeterministicPoints()
        {
            var a = PlayerSpawnService.GenerateEdgeSpawnPoints(20261119);
            var b = PlayerSpawnService.GenerateEdgeSpawnPoints(20261119);

            Assert.AreEqual(PlayerSpawnService.DefaultSpawnPointCount, a.Length);
            Assert.AreEqual(50, a.Length);
            for (var i = 0; i < a.Length; i++)
            {
                Assert.AreEqual(a[i], b[i], $"spawn point {i} is not reproducible");
            }

            var c = PlayerSpawnService.GenerateEdgeSpawnPoints(4242);
            var identical = true;
            for (var i = 0; i < a.Length; i++)
            {
                identical &= a[i] == c[i];
            }

            Assert.IsFalse(identical, "different seeds must produce different rings");
        }

        [Test]
        public void SpawnRingPointsAreOnLandNearTheCoast()
        {
            var points = PlayerSpawnService.GenerateEdgeSpawnPoints(20261119);
            var minDistanceFromCentre = float.MaxValue;
            var maxDistanceFromCentre = 0f;

            foreach (var point in points)
            {
                Assert.IsTrue(PlayerSpawnService.IsValidSpawnPoint(point),
                    $"spawn point {point} is underwater or outside the map");

                var distance = new Vector2(point.x, point.z).magnitude;
                minDistanceFromCentre = Mathf.Min(minDistanceFromCentre, distance);
                maxDistanceFromCentre = Mathf.Max(maxDistanceFromCentre, distance);
                Assert.LessOrEqual(distance, SiranMapConfig.IslandRadiusM,
                    "spawn points must stay inside the coastline");
            }

            Assert.Greater(minDistanceFromCentre, SiranMapConfig.IslandRadiusM * 0.35f,
                "spawn points must hug the map edge, not cluster at the centre");
            Assert.Greater(maxDistanceFromCentre, SiranMapConfig.IslandRadiusM * 0.6f);
        }

        [Test]
        public void SpawnRingPointsAreSpreadApart()
        {
            var points = PlayerSpawnService.GenerateEdgeSpawnPoints(20261119);
            for (var i = 0; i < points.Length; i++)
            {
                for (var j = i + 1; j < points.Length; j++)
                {
                    var distance = Vector3.Distance(points[i], points[j]);
                    Assert.Greater(distance, 15f,
                        $"spawn points {i} and {j} nearly overlap ({distance:F1} m apart)");
                }
            }
        }

        // --- Safe zone ------------------------------------------------------

        [Test]
        public void NextCircleAlwaysFitsInsideThePreviousOne()
        {
            var rng = new RandomStream(5);
            var circle = new ZoneCircle(Vector2.zero, 1900f);
            for (var phase = 0; phase < 8; phase++)
            {
                var next = SafeZoneMath.NextCircle(rng, circle, 0.55f);
                Assert.LessOrEqual(next.Radius + Vector2.Distance(next.Center, circle.Center),
                    circle.Radius + 0.001f, "the new circle escaped the old one");
                circle = next;
            }
        }

        [Test]
        public void SafeZoneQueriesMatchTheGeometry()
        {
            var circle = new ZoneCircle(new Vector2(10f, -20f), 100f);
            Assert.IsTrue(SafeZoneMath.Contains(circle, 10f, -20f));
            Assert.IsTrue(SafeZoneMath.Contains(circle, 60f, -20f));
            Assert.IsFalse(SafeZoneMath.Contains(circle, 200f, -20f));
            Assert.AreEqual(100f, SafeZoneMath.DistanceOutside(circle, 210f, -20f), 0.01f);
            Assert.AreEqual(0f, SafeZoneMath.DistanceOutside(circle, 10f, -20f));
        }

        [Test]
        public void ZoneLerpBlendsCentreAndRadius()
        {
            var a = new ZoneCircle(Vector2.zero, 100f);
            var b = new ZoneCircle(new Vector2(10f, 0f), 50f);
            var mid = SafeZoneMath.Lerp(a, b, 0.5f);
            Assert.AreEqual(75f, mid.Radius, 0.001f);
            Assert.AreEqual(5f, mid.Center.x, 0.001f);
            Assert.AreEqual(a.Radius, SafeZoneMath.Lerp(a, b, 0f).Radius, 0.001f);
            Assert.AreEqual(b.Radius, SafeZoneMath.Lerp(a, b, 1f).Radius, 0.001f);
            Assert.AreEqual(b.Radius, SafeZoneMath.Lerp(a, b, 5f).Radius, 0.001f, "t must clamp");
        }

        [Test]
        public void DefaultScheduleShrinksToAFinalCircleAndRampsDamage()
        {
            var settings = SafeZoneSettings.CreateDefault();
            Assert.AreEqual(6, settings.PhaseCount);

            var radius = settings.startRadiusM;
            var previousDamage = 0f;
            for (var i = 0; i < settings.PhaseCount; i++)
            {
                var phase = settings.PhaseAt(i);
                Assert.Greater(phase.shrinkSeconds, 0f);
                Assert.GreaterOrEqual(phase.damagePerSecond, previousDamage,
                    "zone damage must ramp, never drop");
                previousDamage = phase.damagePerSecond;
                radius *= phase.radiusFactor;
            }

            Assert.Greater(radius, 20f, "final circle must stay playable (>40 m across)");
            Assert.Less(radius, settings.startRadiusM * 0.1f);
            Assert.Greater(settings.finalDamagePerSecond, previousDamage * 0.5f);
        }
    }
}
