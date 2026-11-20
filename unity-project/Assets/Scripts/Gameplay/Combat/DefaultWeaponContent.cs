using System.Collections.Generic;
using UnityEngine;
using BattleHorizons.Gameplay.Loot;

namespace BattleHorizons.Gameplay.Combat
{
    /// <summary>
    /// Combat stats for the four weapons that <see cref="DefaultLootContent"/>
    /// drops, keyed by itemId so the LootPickup.Collected event hands the
    /// WeaponController its stats with no lookup tables in the scene.
    ///
    /// Balance intent (greybox): pistol = dependable semi sidearm, SMG = fast
    /// close-range auto, assault rifle = the Tier 3 all-rounder, sniper = one
    /// shot one dream with a heavy ADS pull-in. Tiers mirror the loot contract:
    /// the two Tier 3 guns only ever spawn in the Military Base.
    /// </summary>
    public static class DefaultWeaponContent
    {
        public static WeaponData Create(
            string weaponId, string displayName, LootTier tier, FireMode fireMode,
            float damage, float roundsPerMinute, float rangeM,
            int magazineSize, int startingReserve, int maxReserve, float reloadSeconds,
            float hipSpreadDeg, float adsSpreadDeg, float adsFovDeg,
            float recoilPitchDeg, float recoilYawJitterDeg, float recoveryDegPerSec)
        {
            var data = ScriptableObject.CreateInstance<WeaponData>();
            data.name = "Weapon_" + weaponId;
            data.weaponId = weaponId;
            data.displayName = displayName;
            data.tier = tier;
            data.fireMode = fireMode;
            data.damage = damage;
            data.roundsPerMinute = roundsPerMinute;
            data.rangeM = rangeM;
            data.magazineSize = magazineSize;
            data.startingReserve = startingReserve;
            data.maxReserve = maxReserve;
            data.reloadSeconds = reloadSeconds;
            data.hipSpreadDeg = hipSpreadDeg;
            data.adsSpreadDeg = adsSpreadDeg;
            data.adsFovDeg = adsFovDeg;
            data.recoilPitchDeg = recoilPitchDeg;
            data.recoilYawJitterDeg = recoilYawJitterDeg;
            data.recoveryDegPerSec = recoveryDegPerSec;
            return data;
        }

        /// <summary>All four starter guns, keyed by loot itemId.</summary>
        public static Dictionary<string, WeaponData> CreateAll()
        {
            var all = new Dictionary<string, WeaponData>();

            void Add(WeaponData data) => all[data.weaponId] = data;

            Add(Create(
                "wpn_pistol", "Viper M9 Pistol", LootTier.Tier1, FireMode.Semi,
                damage: 22f, roundsPerMinute: 300f, rangeM: 90f,
                magazineSize: 12, startingReserve: 48, maxReserve: 96, reloadSeconds: 1.5f,
                hipSpreadDeg: 2.0f, adsSpreadDeg: 0.4f, adsFovDeg: 50f,
                recoilPitchDeg: 1.0f, recoilYawJitterDeg: 0.35f, recoveryDegPerSec: 8f));

            Add(Create(
                "wpn_smg", "Kestrel SMG", LootTier.Tier2, FireMode.Auto,
                damage: 17f, roundsPerMinute: 750f, rangeM: 110f,
                magazineSize: 30, startingReserve: 90, maxReserve: 180, reloadSeconds: 1.9f,
                hipSpreadDeg: 2.6f, adsSpreadDeg: 0.7f, adsFovDeg: 48f,
                recoilPitchDeg: 0.9f, recoilYawJitterDeg: 0.55f, recoveryDegPerSec: 7f));

            Add(Create(
                "wpn_assault_rifle", "Bhairav Assault Rifle", LootTier.Tier3, FireMode.Auto,
                damage: 28f, roundsPerMinute: 600f, rangeM: 220f,
                magazineSize: 30, startingReserve: 120, maxReserve: 240, reloadSeconds: 2.3f,
                hipSpreadDeg: 2.2f, adsSpreadDeg: 0.35f, adsFovDeg: 45f,
                recoilPitchDeg: 1.4f, recoilYawJitterDeg: 0.5f, recoveryDegPerSec: 6f));

            Add(Create(
                "wpn_sniper", "Horizon M82 Sniper", LootTier.Tier3, FireMode.Semi,
                damage: 85f, roundsPerMinute: 45f, rangeM: 600f,
                magazineSize: 5, startingReserve: 20, maxReserve: 40, reloadSeconds: 3.2f,
                hipSpreadDeg: 5.0f, adsSpreadDeg: 0.02f, adsFovDeg: 30f,
                recoilPitchDeg: 4.5f, recoilYawJitterDeg: 0.8f, recoveryDegPerSec: 5f));

            return all;
        }

        /// <summary>The weapon the player spawns with so a fresh scene is testable.</summary>
        public const string StarterWeaponId = "wpn_pistol";
    }
}
