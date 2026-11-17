using BattleHorizons.Core;
using BattleHorizons.Player;
using UnityEngine;

namespace BattleHorizons.Characters
{
    /// <summary>
    /// Spawns the configured character model (Male/Female/custom) under the
    /// player and drives its Animator from the existing
    /// <see cref="BattleHorizons.Player.ThirdPersonMotor"/>. When no character
    /// is configured the component stays dormant and the original capsule
    /// visual keeps behaving exactly as before — movement, input and gameplay
    /// systems are untouched.
    ///
    /// Parameter mapping (names shared with the controller builder via
    /// <see cref="CharacterAnimParameters"/>):
    ///   Speed          ← motor.HorizontalSpeed (damped)
    ///   IsAirborne     ← !motor.IsGrounded
    ///   VerticalSpeed  ← motor.Velocity.y (clamped)
    ///   IsCrouching    ← motor.IsCrouching
    ///   UpperBody layer weight ← current aiming state (damped)
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
        private Animator _animator;
        private bool _aiming;
        private float _upperBodyWeight;

        public bool IsCharacterSpawned => _instance != null;
        public CharacterDefinition ActiveDefinition => _active;
        public Animator CharacterAnimator => _animator;

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

            _animator = _instance.GetComponentInChildren<Animator>();
            if (_animator == null)
            {
                GameLog.Error("characters", _active.displayName + " has no Animator — capsule visual stays active");
                Destroy(_instance);
                _instance = null;
                return false;
            }

            _animator.applyRootMotion = false;
            _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            if (_active.controller != null)
            {
                _animator.runtimeAnimatorController = _active.controller;
            }
            else
            {
                GameLog.Error("characters", _active.displayName + " has no generated Animator controller");
            }

            SetCapsuleVisible(false);
            GameLog.Info("characters", "spawned character visual: " + _active.displayName);
            return true;
        }

        /// <summary>Gameplay hook: aiming changes the UpperBody layer weight.</summary>
        public void SetAiming(bool aiming)
        {
            _aiming = aiming;
        }

        /// <summary>Gameplay hooks for the weapon one-shots (no-op when dormant).</summary>
        public void TriggerShoot()
        {
            if (_animator != null)
            {
                _animator.SetTrigger(CharacterAnimParameters.Shoot);
            }
        }

        public void TriggerReload()
        {
            if (_animator != null)
            {
                _animator.SetTrigger(CharacterAnimParameters.Reload);
            }
        }

        public void TriggerDeath()
        {
            if (_animator != null)
            {
                _animator.SetTrigger(CharacterAnimParameters.Death);
            }
        }

        /// <summary>Respawn / preview reset: back to the default state.</summary>
        public void ResetToDefaultState()
        {
            if (_animator != null)
            {
                _animator.Rebind();
                _animator.Update(0f);
            }
        }

        private void Update()
        {
            if (_animator == null || motor == null)
            {
                return;
            }

            var dt = Time.deltaTime;
            _animator.SetFloat(CharacterAnimParameters.Speed, motor.HorizontalSpeed, 0.12f, dt);
            _animator.SetBool(CharacterAnimParameters.IsAirborne, !motor.IsGrounded);
            _animator.SetFloat(CharacterAnimParameters.VerticalSpeed,
                Mathf.Clamp(motor.Velocity.y, -30f, 30f));
            _animator.SetBool(CharacterAnimParameters.IsCrouching, motor.IsCrouching);

            if (_animator.layerCount > CharacterAnimParameters.UpperBodyLayerIndex)
            {
                var target = _aiming ? 1f : 0f;
                _upperBodyWeight = Mathf.MoveTowards(_upperBodyWeight, target, dt * 6f);
                _animator.SetLayerWeight(CharacterAnimParameters.UpperBodyLayerIndex, _upperBodyWeight);
            }
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
