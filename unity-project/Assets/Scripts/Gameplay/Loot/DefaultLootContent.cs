using System.Collections.Generic;
using UnityEngine;

namespace BattleHorizons.Gameplay.Loot
{
    /// <summary>
    /// The starter loot content, authored in code so a fresh clone has a
    /// playable loot layer without any .asset files, and so EditMode tests can
    /// assert the tier rules (Tier 3 = Military Base only) without loading
    /// assets. The Battle Horizons &gt; Gameplay &gt; Create Default Loot Assets
    /// menu persists the same content as editable ScriptableObject assets.
    ///
    /// POI mapping onto Siran Island (docs/gameplay/MAP_POI_LAYOUT.md):
    ///   central_state      → Military Base (centre)        — Tier 3 weapons
    ///   factory_island     → Abandoned Village (north-west)— Tier 1/2
    ///   konkan_docks       → Docks (west coast)            — Tier 1/2 + resources
    ///   graveyard_center   → Forest &amp; Quarry (east)         — resources only
    ///   aircraft_industry  → Airfield Hangars (south)      — Tier 2
    /// </summary>
    public static class DefaultLootContent
    {
        public const string ZoneMilitaryBase = "central_state";
        public const string ZoneVillage = "factory_island";
        public const string ZoneDocks = "konkan_docks";
        public const string ZoneForestQuarry = "graveyard_center";
        public const string ZoneAirfield = "aircraft_industry";

        public static ItemDefinition Item(
            string id, string displayName, ItemCategory category, LootTier tier, int maxStack = 1)
        {
            var item = ScriptableObject.CreateInstance<ItemDefinition>();
            item.name = "Item_" + id;
            item.itemId = id;
            item.displayName = displayName;
            item.category = category;
            item.tier = tier;
            item.maxStack = maxStack;
            item.icon = null;
            return item;
        }

        /// <summary>Every starter item, keyed by itemId.</summary>
        public static Dictionary<string, ItemDefinition> CreateItems()
        {
            var items = new Dictionary<string, ItemDefinition>();

            void Add(ItemDefinition item) => items[item.itemId] = item;

            // Weapons — Tier 1/2 anywhere, Tier 3 only in the Military Base.
            Add(Item("wpn_pistol", "Viper M9 Pistol", ItemCategory.Weapon, LootTier.Tier1, 1));
            Add(Item("wpn_smg", "Kestrel SMG", ItemCategory.Weapon, LootTier.Tier2, 1));
            Add(Item("wpn_assault_rifle", "Bhairav Assault Rifle", ItemCategory.Weapon, LootTier.Tier3, 1));
            Add(Item("wpn_sniper", "Horizon M82 Sniper", ItemCategory.Weapon, LootTier.Tier3, 1));

            // Ammo.
            Add(Item("ammo_light", "9mm Light Ammo", ItemCategory.Ammo, LootTier.Tier1, 120));
            Add(Item("ammo_heavy", "7.62mm Heavy Ammo", ItemCategory.Ammo, LootTier.Tier2, 90));

            // Armor.
            Add(Item("armor_vest_t1", "Field Vest", ItemCategory.Armor, LootTier.Tier1, 1));
            Add(Item("armor_vest_t2", "Ballistic Vest", ItemCategory.Armor, LootTier.Tier2, 1));
            Add(Item("armor_helmet_t2", "Steel Helmet", ItemCategory.Armor, LootTier.Tier2, 1));
            Add(Item("armor_helmet_t3", "Riot Helmet", ItemCategory.Armor, LootTier.Tier3, 1));

            // Healing.
            Add(Item("heal_bandage", "Bandage", ItemCategory.Healing, LootTier.Tier1, 10));
            Add(Item("heal_medkit", "Medkit", ItemCategory.Healing, LootTier.Tier2, 3));

            // Resources — Forest & Quarry nodes.
            Add(Item("res_wood", "Wood", ItemCategory.Resource, LootTier.Tier1, 50));
            Add(Item("res_stone", "Stone", ItemCategory.Resource, LootTier.Tier1, 50));
            Add(Item("res_scrap_metal", "Scrap Metal", ItemCategory.Resource, LootTier.Tier2, 30));

            return items;
        }

        public static LootTable Table(string id, params LootTable.Entry[] entries)
        {
            var table = ScriptableObject.CreateInstance<LootTable>();
            table.name = "LootTable_" + id;
            table.tableId = id;
            table.entries = entries;
            return table;
        }

        private static LootTable.Entry Entry(ItemDefinition item, float weight, int min = 1, int max = 1) =>
            new LootTable.Entry { item = item, weight = weight, minCount = min, maxCount = max };

        /// <summary>The four starter tables, built from <see cref="CreateItems"/>.</summary>
        public static Dictionary<string, LootTable> CreateTables(Dictionary<string, ItemDefinition> items)
        {
            ItemDefinition I(string id) => items[id];

            var military = Table(
                "military",
                Entry(I("wpn_assault_rifle"), 3f),
                Entry(I("wpn_sniper"), 1.5f),
                Entry(I("armor_helmet_t3"), 1.5f),
                Entry(I("armor_vest_t2"), 2f),
                Entry(I("wpn_smg"), 2f),
                Entry(I("ammo_heavy"), 4f, 20, 60),
                Entry(I("heal_medkit"), 3f, 1, 2));

            var village = Table(
                "village",
                Entry(I("wpn_pistol"), 4f),
                Entry(I("wpn_smg"), 2f),
                Entry(I("armor_vest_t1"), 3f),
                Entry(I("armor_helmet_t2"), 1.5f),
                Entry(I("ammo_light"), 5f, 15, 45),
                Entry(I("heal_bandage"), 5f, 2, 6),
                Entry(I("heal_medkit"), 1f));

            var docks = Table(
                "docks",
                Entry(I("wpn_pistol"), 3f),
                Entry(I("ammo_light"), 4f, 15, 45),
                Entry(I("armor_vest_t1"), 2f),
                Entry(I("heal_bandage"), 4f, 2, 6),
                Entry(I("res_wood"), 3f, 5, 20),
                Entry(I("res_scrap_metal"), 2f, 3, 12));

            var resources = Table(
                "resources",
                Entry(I("res_wood"), 5f, 5, 25),
                Entry(I("res_stone"), 5f, 5, 25),
                Entry(I("res_scrap_metal"), 2f, 2, 10),
                Entry(I("heal_bandage"), 1f, 1, 3));

            return new Dictionary<string, LootTable>
            {
                ["military"] = military,
                ["village"] = village,
                ["docks"] = docks,
                ["resources"] = resources,
            };
        }

        /// <summary>
        /// The full default configuration. This is the tier contract the game
        /// ships with: Tier 3 exists only in the Military Base rule; the
        /// village and docks cap at Tier 2; the Forest &amp; Quarry is resources
        /// only (Tier 1 cap).
        /// </summary>
        public static LootSpawnConfig CreateConfig()
        {
            var items = CreateItems();
            return BuildConfig(items, CreateTables(items));
        }

        /// <summary>
        /// Assembles the config from already-built items/tables. The editor
        /// asset builder persists the pieces first and calls this with the
        /// saved assets, so the runtime default and the authored assets can
        /// never drift apart.
        /// </summary>
        public static LootSpawnConfig BuildConfig(
            Dictionary<string, ItemDefinition> items,
            Dictionary<string, LootTable> tables)
        {
            tables ??= CreateTables(items);

            var config = ScriptableObject.CreateInstance<LootSpawnConfig>();
            config.name = "LootSpawnConfig_Default";
            config.seed = 20261119;
            config.defaultMaxTier = LootTier.Tier1;
            config.defaultTable = tables["village"];
            config.zoneRules = new[]
            {
                new LootZoneRule
                {
                    zoneId = ZoneMilitaryBase,
                    poiName = "Military Base",
                    maxTier = LootTier.Tier3,
                    table = tables["military"],
                    minDropsPerContainer = 2,
                    maxDropsPerContainer = 4,
                },
                new LootZoneRule
                {
                    zoneId = ZoneVillage,
                    poiName = "Abandoned Village",
                    maxTier = LootTier.Tier2,
                    table = tables["village"],
                    minDropsPerContainer = 1,
                    maxDropsPerContainer = 3,
                },
                new LootZoneRule
                {
                    zoneId = ZoneDocks,
                    poiName = "Konkan Docks",
                    maxTier = LootTier.Tier2,
                    table = tables["docks"],
                    minDropsPerContainer = 1,
                    maxDropsPerContainer = 3,
                },
                new LootZoneRule
                {
                    zoneId = ZoneForestQuarry,
                    poiName = "Forest & Quarry",
                    maxTier = LootTier.Tier1,
                    allowedCategories = new[] { ItemCategory.Resource, ItemCategory.Healing },
                    table = tables["resources"],
                    minDropsPerContainer = 1,
                    maxDropsPerContainer = 2,
                },
                new LootZoneRule
                {
                    zoneId = ZoneAirfield,
                    poiName = "Airfield Hangars",
                    maxTier = LootTier.Tier2,
                    table = tables["docks"],
                    minDropsPerContainer = 1,
                    maxDropsPerContainer = 3,
                },
            };
            return config;
        }
    }
}
