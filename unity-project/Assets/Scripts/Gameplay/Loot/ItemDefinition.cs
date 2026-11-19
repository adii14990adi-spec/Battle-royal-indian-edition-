using UnityEngine;

namespace BattleHorizons.Gameplay.Loot
{
    /// <summary>What kind of thing an item is (drives UI grouping and loot rules).</summary>
    public enum ItemCategory
    {
        Weapon = 0,
        Ammo = 1,
        Armor = 2,
        Healing = 3,
        Resource = 4,
        Attachment = 5,
    }

    /// <summary>
    /// Loot tier gates where an item may spawn:
    ///   Tier 1 — village/dock basics (pistols, bandages, wood),
    ///   Tier 2 — mid-tier (SMGs, vests, helmets),
    ///   Tier 3 — high-tier, Military Base only (assault rifles, snipers).
    /// </summary>
    public enum LootTier
    {
        Tier1 = 1,
        Tier2 = 2,
        Tier3 = 3,
    }

    /// <summary>
    /// Static description of one item type. Author these as assets via
    /// Battle Horizons &gt; Gameplay &gt; Create Default Loot Assets, or let
    /// <see cref="DefaultLootContent"/> build the starter set in code.
    /// </summary>
    [CreateAssetMenu(menuName = "Battle Horizons/Loot/Item Definition", fileName = "Item_New")]
    public class ItemDefinition : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable snake-case id used by save data and analytics. Never rename after ship.")]
        public string itemId;
        public string displayName;
        public ItemCategory category = ItemCategory.Weapon;
        [Tooltip("Highest-tier POIs may still roll lower-tier items; a village never rolls above its cap.")]
        public LootTier tier = LootTier.Tier1;

        [Header("Stacking")]
        [Min(1)] public int maxStack = 1;

        [Header("World representation")]
        [Tooltip("Prefab dropped in the world. When null, LootSpawner builds a greybox cube.")]
        public GameObject worldPrefab;
        public Sprite icon;

        /// <summary>Tier accent colours shared by the greybox fallback prefabs and the HUD.</summary>
        public static Color TierColor(LootTier tier)
        {
            switch (tier)
            {
                case LootTier.Tier3: return new Color(1.00f, 0.78f, 0.25f); // gold
                case LootTier.Tier2: return new Color(0.35f, 0.62f, 1.00f); // blue
                default: return new Color(0.75f, 0.75f, 0.75f);             // grey
            }
        }
    }
}
