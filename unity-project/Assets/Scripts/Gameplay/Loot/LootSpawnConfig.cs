using System;
using UnityEngine;

namespace BattleHorizons.Gameplay.Loot
{
    /// <summary>
    /// Per-POI loot rule: which Siran Island zone id it applies to, the highest
    /// tier that may drop there, and how many rolls each container gets.
    /// Zone ids match <c>SiranMapConfig.Zones[i].Id</c>.
    /// </summary>
    [Serializable]
    public sealed class LootZoneRule
    {
        [Tooltip("SiranMapConfig zone id, e.g. 'central_state'.")]
        public string zoneId;
        [Tooltip("Human name shown in the minimap/debug HUD, e.g. 'Military Base'.")]
        public string poiName;
        [Tooltip("Hard tier cap. Tier 3 is reserved for the Military Base rule.")]
        public LootTier maxTier = LootTier.Tier1;
        [Tooltip("Empty = every category may drop here.")]
        public ItemCategory[] allowedCategories = Array.Empty<ItemCategory>();
        [Tooltip("Table used inside this zone; falls back to LootSpawnConfig.defaultTable.")]
        public LootTable table;
        [Min(0)] public int minDropsPerContainer = 1;
        [Min(1)] public int maxDropsPerContainer = 3;
    }

    /// <summary>
    /// Match-level loot configuration: one seed, a default table and one rule
    /// per POI zone. Assign a saved asset in the inspector or leave the field
    /// empty to use <see cref="DefaultLootContent.CreateConfig"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "Battle Horizons/Loot/Spawn Config", fileName = "LootSpawnConfig_New")]
    public class LootSpawnConfig : ScriptableObject
    {
        [Tooltip("Match loot seed. Same seed + same containers = identical loot.")]
        public int seed = 20261119;
        [Tooltip("Tier cap for containers that sit outside every mapped zone.")]
        public LootTier defaultMaxTier = LootTier.Tier1;
        public LootTable defaultTable;
        public LootZoneRule[] zoneRules = Array.Empty<LootZoneRule>();

        public LootZoneRule RuleFor(string zoneId)
        {
            if (string.IsNullOrEmpty(zoneId) || zoneRules == null)
            {
                return null;
            }

            for (var i = 0; i < zoneRules.Length; i++)
            {
                if (zoneRules[i] != null && zoneRules[i].zoneId == zoneId)
                {
                    return zoneRules[i];
                }
            }

            return null;
        }

        /// <summary>Effective tier cap for a zone (rule cap, or defaultMaxTier).</summary>
        public LootTier MaxTierFor(string zoneId)
        {
            var rule = RuleFor(zoneId);
            return rule != null ? rule.maxTier : defaultMaxTier;
        }
    }
}
