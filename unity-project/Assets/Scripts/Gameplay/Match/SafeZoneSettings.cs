using System;
using UnityEngine;

namespace BattleHorizons.Gameplay.Match
{
    /// <summary>
    /// One shrink step: hold the current circle, then shrink to
    /// radiusFactor × radius while dealing damagePerSecond outside the rim.
    /// </summary>
    [Serializable]
    public sealed class SafeZonePhase
    {
        [Min(0f)] public float waitSeconds = 60f;
        [Min(1f)] public float shrinkSeconds = 120f;
        [Range(0.05f, 1f)] public float radiusFactor = 0.5f;
        [Min(0f)] public float damagePerSecond = 1f;
    }

    /// <summary>
    /// Full zone schedule for a match. CreateDefault() encodes the shipping
    /// greybox schedule (~17 min from island-wide to final circle); tune it as
    /// an asset via Battle Horizons &gt; Gameplay &gt; Create Default Safe Zone
    /// Settings.
    /// </summary>
    [CreateAssetMenu(menuName = "Battle Horizons/Match/Safe Zone Settings", fileName = "SafeZoneSettings_New")]
    public class SafeZoneSettings : ScriptableObject
    {
        [Tooltip("Initial radius in metres; 950 covers the 2 km Siran Island (IslandRadiusM = 975).")]
        public float startRadiusM = 950f;
        public SafeZonePhase[] phases = Array.Empty<SafeZonePhase>();

        /// <summary>Damage of the final phase, used once the schedule is exhausted.</summary>
        public float finalDamagePerSecond = 12f;

        public int PhaseCount => phases?.Length ?? 0;

        public SafeZonePhase PhaseAt(int index)
        {
            if (phases == null || phases.Length == 0)
            {
                return null;
            }

            return phases[Mathf.Clamp(index, 0, phases.Length - 1)];
        }

        /// <summary>The greybox match schedule (6 phases).</summary>
        public static SafeZoneSettings CreateDefault()
        {
            var settings = CreateInstance<SafeZoneSettings>();
            settings.name = "SafeZoneSettings_Default";
            settings.startRadiusM = 950f;
            settings.finalDamagePerSecond = 12f;
            // ~16.8 min island-wide → ~37 m final circle (dps ramps 0.4 → 12).
            settings.phases = new[]
            {
                new SafeZonePhase { waitSeconds = 90f, shrinkSeconds = 180f, radiusFactor = 0.65f, damagePerSecond = 0.4f },
                new SafeZonePhase { waitSeconds = 75f, shrinkSeconds = 150f, radiusFactor = 0.60f, damagePerSecond = 0.8f },
                new SafeZonePhase { waitSeconds = 60f, shrinkSeconds = 120f, radiusFactor = 0.55f, damagePerSecond = 1.6f },
                new SafeZonePhase { waitSeconds = 45f, shrinkSeconds = 100f, radiusFactor = 0.50f, damagePerSecond = 3.0f },
                new SafeZonePhase { waitSeconds = 30f, shrinkSeconds = 80f, radiusFactor = 0.45f, damagePerSecond = 6.0f },
                new SafeZonePhase { waitSeconds = 20f, shrinkSeconds = 60f, radiusFactor = 0.40f, damagePerSecond = 12.0f },
            };
            return settings;
        }
    }
}
