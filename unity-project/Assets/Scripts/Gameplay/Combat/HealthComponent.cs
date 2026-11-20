using System;
using UnityEngine;
using BattleHorizons.Core;
using BattleHorizons.Gameplay.Match;

namespace BattleHorizons.Gameplay.Combat
{
    /// <summary>
    /// The reference <see cref="IDamageable"/> implementation: bullets
    /// (WeaponController) and the blue zone (SafeZoneController) both push
    /// damage through it, and the future HUD subscribes to the events.
    /// Put it on the player root and on any target dummy in the test scene.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HealthComponent : MonoBehaviour, IDamageable
    {
        private const string Tag = "health";

        [Header("Health")]
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private bool logDamage;

        public float MaxHealth => maxHealth;
        public float CurrentHealth { get; private set; }
        public bool IsAlive => CurrentHealth > 0f;

        /// <summary>(amount, sourceId) after every applied hit.</summary>
        public event Action<float, string> Damaged;

        /// <summary>(sourceId) once, on the lethal hit.</summary>
        public event Action<string> Died;

        private void Awake()
        {
            CurrentHealth = maxHealth;
        }

        public void TakeDamage(float amount, string source)
        {
            if (!IsAlive || amount <= 0f)
            {
                return;
            }

            CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
            Damaged?.Invoke(amount, source);

            if (logDamage)
            {
                GameLog.Info(Tag, $"{name} took {amount:F1} from '{source}' → {CurrentHealth:F1}/{maxHealth:F0}");
            }

            if (CurrentHealth <= 0f)
            {
                Died?.Invoke(source);
                GameLog.Info(Tag, $"{name} eliminated by '{source}'");
            }
        }

        public void Heal(float amount)
        {
            if (!IsAlive || amount <= 0f)
            {
                return;
            }

            CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + amount);
        }

        public void ResetHealth()
        {
            CurrentHealth = maxHealth;
        }
    }
}
