using UnityEngine;
using BattleHorizons.Core;

namespace BattleHorizons.Characters
{
    /// <summary>
    /// Anti-slide locomotion driver ("PlayerLocomotion" from the motion brief).
    ///
    /// The generated controller blends Idle/Walk/Run/Sprint off the `Speed`
    /// parameter, and <see cref="PlayerCharacterVisual"/> already feeds it the
    /// motor's *intended* speed. Intended ≠ actual during acceleration, in the
    /// air, or against a wall — that mismatch is the classic moon-walk slide.
    ///
    /// This component measures the CharacterController's **real displacement**
    /// each frame, damps it exponentially (never overshoots) and writes the
    /// Animator `Speed` parameter in LateUpdate — after every Update writer —
    /// so the blend tracks the ground truth. Feet stop skating, and Idle↔Walk↔Run
    /// zone changes read as one continuous accel/decel curve.
    /// </summary>
    [DefaultExecutionOrder(50)]
    [DisallowMultipleComponent]
    public sealed class PlayerLocomotion : MonoBehaviour
    {
        [Header("Sources (auto-resolved once in Start when empty)")]
        [SerializeField] private Animator characterAnimator;
        [SerializeField] private CharacterController controller;

        [Header("Smoothing")]
        [Tooltip("≈0.14 s feels responsive on touch without flickering between zones.")]
        [SerializeField] private float speedSmoothTime = 0.14f;
        [Tooltip("Displacement below this (m/s) counts as standing still.")]
        [SerializeField] private float standStillThreshold = 0.12f;

        private Vector3 _lastPosition;
        private bool _hasLastPosition;
        private float _smoothedSpeed;

        public float SmoothedSpeed => _smoothedSpeed;

        private void Start()
        {
            if (controller == null)
            {
                controller = GetComponent<CharacterController>();
            }

            if (characterAnimator == null)
            {
                characterAnimator = GetComponentInChildren<Animator>();
            }

            _lastPosition = transform.position;
            _hasLastPosition = true;
        }

        /// <summary>Re-point at a freshly spawned character (Character Bay swap).</summary>
        public void Bind(Animator animator, CharacterController characterController)
        {
            characterAnimator = animator;
            controller = characterController;
            _hasLastPosition = false;
        }

        private void LateUpdate()
        {
            var position = transform.position;
            var dt = Time.deltaTime;

            float actualSpeed;
            if (_hasLastPosition && dt > 0f)
            {
                var delta = position - _lastPosition;
                delta.y = 0f; // vertical motion belongs to Jump/Fall, not Speed
                actualSpeed = delta.magnitude / dt;
                if (actualSpeed < standStillThreshold)
                {
                    actualSpeed = 0f;
                }
            }
            else
            {
                actualSpeed = _smoothedSpeed;
            }

            _lastPosition = position;
            _hasLastPosition = true;

            _smoothedSpeed = CharacterMotionMath.DampTowards(_smoothedSpeed, actualSpeed, speedSmoothTime, dt);

            if (characterAnimator != null)
            {
                characterAnimator.SetFloat(CharacterAnimParameters.Speed, _smoothedSpeed);
            }
        }
    }
}
