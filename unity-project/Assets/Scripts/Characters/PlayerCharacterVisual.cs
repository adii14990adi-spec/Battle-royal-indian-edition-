using BattleHorizons.Core;
using BattleHorizons.Player;
using UnityEngine;

namespace BattleHorizons.Characters
{
    /// <summary>
    /// Spawns the configured character model (Male/Female/custom) under the
    /// player. Movement is owned entirely by
    /// <see cref="BattleHorizons.Player.ThirdPersonMotor"/>; when no character
    /// is configured the component stays dormant and the original capsule
    /// visual keeps behaving exactly as before — movement, input and gameplay
    /// systems are untouched.
    ///
    /// History: this component used to drive an Animator (Speed/IsCrouching/
    /// IsAiming parameters, UpperBody layer, shoot/reload/death triggers) from
    /// the Mixamo clip library. All animation clips were removed from the
    /// project on 2026-11-20 by owner decision, so the character renders in
    /// its static imported pose and the gameplay hooks below are honest
    /// no-ops kept for call-site stability.
    /// </summary>
    [DefaultExecutionOrder(20)]
    [DisallowMultipleComponent]
    public sealed class PlayerCharacterVisual : MonoBehaviour
    {
        [Header("Optional explicit references (auto-found when empty)")]
        [SerializeField] private ThirdPersonMotor motor;
        [SerializeField] private Transform visualCapsule;

        private CharacterAnimationConfig _config;
        private CharacterDefinition _active;
        private GameObject _instance;

        public bool IsCharacterSpawned => _instance != null;
        public CharacterDefinition ActiveDefinition => _active;

        private void Awake()
        {
            if (motor == null)
            {
                motor = GetComponent<ThirdPersonMotor>();
            }

            if (visualCapsule == null)
            {
                visualCapsule = transform.Find("PlayerCapsuleVisual");
            }
        }

        private void Start()
        {
            TrySpawn();
        }

        /// <summary>
        /// Spawn the selected character from the saved configuration.
        /// Returns false (silently, with one log line) when nothing usable is
        /// configured — the capsule visual remains the character in that case.
        /// </summary>
        public bool TrySpawn()
        {
            if (_instance != null)
            {
                return true;
            }

            _config = CharacterAnimationConfig.Load();
            _active = _config != null ? _config.SelectedCharacter() : null;
            if (_active == null || _active.model == null)
            {
                GameLog.Info("characters", "no character configured — capsule visual stays active");
                return false;
            }

            _instance = Instantiate(_active.model, transform);
            _instance.name = _active.displayName + " (CharacterVisual)";
            _instance.transform.localPosition = Vector3.zero;
            _instance.transform.localRotation = Quaternion.identity;

            // No Animator wiring: the project ships without animation clips,
            // so the motor moves the transform while the model holds a static
            // pose. The authored T-pose would read as broken, so a one-shot
            // procedural relaxed pose swings the arms down (no clips needed).
            CharacterRelaxedPose.Apply(_instance);
            SetCapsuleVisible(false);
            GameLog.Info("characters", "spawned character visual: " + _active.displayName);
            return true;
        }

        /// <summary>Gameplay hooks — honest no-ops since animations were removed.</summary>
        public void SetAiming(bool aiming)
        {
        }

        public void TriggerShoot()
        {
        }

        public void TriggerReload()
        {
        }

        public void TriggerDeath()
        {
        }

        /// <summary>Respawn / preview reset hook — no animation state remains.</summary>
        public void ResetToDefaultState()
        {
        }

        private void SetCapsuleVisible(bool visible)
        {
            if (visualCapsule == null)
            {
                return;
            }

            var renderer = visualCapsule.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.enabled = visible;
            }
        }
    }
}
