using UnityEngine;

namespace BattleHorizons.Characters
{
    /// <summary>
    /// Head look-at IK: the head (and slightly the body) follows the camera's
    /// aim point through built-in Humanoid look-at, so the neck never snaps —
    /// weight eases from a subtle 0.35 at hip to a locked 0.9 while aiming
    /// (<see cref="CharacterMotionMath.LookWeight"/>).
    ///
    /// This is the runtime half of the brief's "Multi-Aim Constraint" ask; the
    /// Animation Rigging editor route (Rig Builder + Multi-Aim on the head
    /// bone) is documented in docs/gameplay/CHARACTER_MOTION.md as the artist
    /// override — both must not run at once.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HeadLookIK : MonoBehaviour
    {
        [SerializeField] private Transform cameraTransform;
        [Tooltip("Aim point distance along the camera forward axis, metres.")]
        [SerializeField] private float aimDistanceM = 12f;
        [SerializeField] private float bodyWeight = 0.15f;
        [SerializeField] private float weightSmoothTime = 0.12f;

        private Animator _animator;
        private float _aimBlend;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
        }

        public void Bind(Transform camera)
        {
            cameraTransform = camera;
        }

        /// <summary>Pushed by CharacterMotionRig each frame (router aim state).</summary>
        public void SetAiming(bool aiming)
        {
            _aimBlend = CharacterMotionMath.DampTowards(_aimBlend, aiming ? 1f : 0f, weightSmoothTime, Time.deltaTime);
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (layerIndex != 0 || _animator == null || !_animator.isHuman)
            {
                return;
            }

            var headWeight = CharacterMotionMath.LookWeight(_aimBlend);
            if (cameraTransform != null)
            {
                _animator.SetLookAtPosition(cameraTransform.position + cameraTransform.forward * aimDistanceM);
            }
            else
            {
                _animator.SetLookAtPosition(transform.position + Vector3.up * 1.5f + transform.forward * aimDistanceM);
            }

            _animator.SetLookAtWeight(headWeight, bodyWeight, 1f, 0f, 0.4f);
        }
    }
}
