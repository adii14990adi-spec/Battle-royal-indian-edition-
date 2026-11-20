using System;
using System.Collections.Generic;
using UnityEngine;
using BattleHorizons.Core;

namespace BattleHorizons.Gameplay.Loot
{
    /// <summary>
    /// Weighted item bag. Roll it with a <see cref="RandomStream"/> so every
    /// match is reproducible from its seed.
    /// </summary>
    [CreateAssetMenu(menuName = "Battle Horizons/Loot/Loot Table", fileName = "LootTable_New")]
    public class LootTable : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public ItemDefinition item;
            [Min(0f)] public float weight = 1f;
            [Min(1)] public int minCount = 1;
            [Min(1)] public int maxCount = 1;
        }

        /// <summary>One rolled result: which item and how many of it.</summary>
        public readonly struct Roll
        {
            public readonly ItemDefinition Item;
            public readonly int Count;

            public Roll(ItemDefinition item, int count)
            {
                Item = item;
                Count = count;
            }
        }

        public string tableId;
        public Entry[] entries = Array.Empty<Entry>();

        private readonly List<float> _weightScratch = new List<float>(16);
        private readonly List<Entry> _eligibleScratch = new List<Entry>(16);

        /// <summary>
        /// Rolls one item that satisfies the tier cap and category filter.
        /// Returns an empty roll (Item == null) when nothing is eligible.
        /// </summary>
        public Roll RollOne(RandomStream rng, LootTier maxTier, ICollection<ItemCategory> allowedCategories = null)
        {
            if (rng == null || entries == null || entries.Length == 0)
            {
                return default;
            }

            _eligibleScratch.Clear();
            _weightScratch.Clear();
            for (var i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                if (entry == null || entry.item == null || entry.weight <= 0f)
                {
                    continue;
                }

                if (entry.item.tier > maxTier)
                {
                    continue;
                }

                if (allowedCategories != null && allowedCategories.Count > 0 &&
                    !allowedCategories.Contains(entry.item.category))
                {
                    continue;
                }

                _eligibleScratch.Add(entry);
                _weightScratch.Add(entry.weight);
            }

            if (_eligibleScratch.Count == 0)
            {
                return default;
            }

            var chosen = _eligibleScratch[rng.WeightedIndex(_weightScratch)];
            var count = Mathf.Clamp(rng.Range(chosen.minCount, chosen.maxCount), 1, Mathf.Max(1, chosen.item.maxStack));
            return new Roll(chosen.item, count);
        }

        /// <summary>Rolls up to <paramref name="rolls"/> items; results land in <paramref name="results"/>.</summary>
        public int RollMany(
            RandomStream rng,
            int rolls,
            LootTier maxTier,
            List<Roll> results,
            ICollection<ItemCategory> allowedCategories = null)
        {
            if (results == null)
            {
                return 0;
            }

            var added = 0;
            for (var i = 0; i < rolls; i++)
            {
                var roll = RollOne(rng, maxTier, allowedCategories);
                if (roll.Item == null)
                {
                    continue;
                }

                results.Add(roll);
                added++;
            }

            return added;
        }
    }
}
