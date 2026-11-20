using UnityEngine;
using BattleHorizons.Gameplay.Loot;

namespace BattleHorizons.Gameplay.Combat
{
    /// <summary>How the trigger behaves: semi needs a release between shots.</summary>
    public enum FireMode
    {
        Semi = 0,
        Auto = 1,
    }

    /// <summary>
    /// Static weapon stats. One asset per gun; <see cref="DefaultWeaponContent"/>
    /// authors the four loot weapons in code so the tier contract (Tier 3 =
    /// Military Base) carries straight into combat balance.
    /// </summary>
    [CreateAssetMenu(menuName = "Battle Horizons/Combat/Weapon Data", fileName = "Weapon_New")]
    public class WeaponData : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Must equal the ItemDefinition.itemId this gun drops as (e.g. 'wpn_smg').")]
        public string weaponId;
        public string displayName;
        public LootTier tier = LootTier.Tier1;
        public FireMode fireMode = FireMode.Semi;

        [Header("Ballistics")]
        [Min(0f)] public float damage = 20f;
        [Tooltip("Shots per minute.")]
        [Min(1f)] public float roundsPerMinute = 300f;
        [Min(1f)] public float rangeM = 120f;
        [Min(0f)] public float hipSpreadDeg = 1.5f;
        [Min(0f)] public float adsSpreadDeg = 0.25f;

        [Header("Ammo")]
        [Min(1)] public int magazineSize = 12;
        [Min(0)] public int startingReserve = 48;
        [Min(0)] public int maxReserve = 120;
        [Min(0.1f)] public float reloadSeconds = 1.8f;

        [Header("Aim down sights")]
        [Tooltip("Camera FOV while aiming; hip FOV lives on the WeaponController (60 default).")]
        [Range(20f, 70f)] public float adsFovDeg = 45f;
        [Min(0.05f)] public float adsEnterSeconds = 0.18f;
        [Min(0.05f)] public float adsExitSeconds = 0.22f;

        [Header("Recoil (degrees of camera kick per shot)")]
        [Min(0f)] public float recoilPitchDeg = 1.2f;
        [Min(0f)] public float recoilYawJitterDeg = 0.5f;
        [Tooltip("Recoil multiplier while aiming (ADS sprays tighter).")]
        [Range(0.1f, 1f)] public float adsRecoilScale = 0.55f;
        [Min(0f)] public float recoveryDegPerSec = 6f;
        [Tooltip("Fraction of accumulated kick that returns to the camera as recovery.")]
        [Range(0f, 1f)] public float recoveryReturnRatio = 0.7f;

        public float SecondsPerShot => 60f / Mathf.Max(1f, roundsPerMinute);
    }
}
