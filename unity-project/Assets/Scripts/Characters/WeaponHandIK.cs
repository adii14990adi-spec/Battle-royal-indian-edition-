using UnityEngine;

namespace BattleHorizons.Characters
{
    /// <summary>
    /// Left-hand grip IK: the support hand actually closes on the weapon's
    /// foregrip point instead of floating near it. Uses built-in Humanoid hand
    /// IK goals (no Animation Rigging dependency at runtime) and ramps weight
    /// from a relaxed carry (0.5) to a firm aim grip (0.85) via
    /// <see cref="CharacterMotionMath.HandIkWeight"/> — aiming feels braced,
    /// hip-fire stays loose.
    ///
    /// Attach to the Animator GameObject (CharacterMotionRig does this) and
    /// give it a grip target parented under the weapon mount; when the target
    /// is null a default foregrip point is created under the mount.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WeaponHandIK : MonoBehaviour
    {
        [SerializeField] private Transform gripTarget;
        [SerializeField] private Transform weaponMount;
        [Tooltip("Local offset of the default foregrip point under the mount (left-hand side of a rifle).")]
        [SerializeField] private Vector3 defaultGripOffset = new Vector3(-0.06f, -0.02f, 0.22f);
        [SerializeField] private float weightSmoothTime = 0.1f;

        private Animator _animator;
        private float _weight;
        private float _aimBlend;
        private bool _hasWeapon;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
        }

        public void Bind(Transform mount)
        {
            weaponMount = mount;
            gripTarget = null; // rebuilt lazily under the new mount
        }

        /// <summary>WeaponController state, pushed by CharacterMotionRig each frame.</summary>
        public void SetWeaponState(bool hasWeapon, bool aiming)
        {
            _hasWeapon = hasWeapon;
            _aimBlend = CharacterMotionMath.DampTowards(_aimBlend, aiming ? 1f : 0f, weightSmoothTime, Time.deltaTime);
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (layerIndex != 0 || _animator == null || !_animator.isHuman)
            {
                return;
            }

            var target = CharacterMotionMath.HandIkWeight(_hasWeapon, _aimBlend);
            _weight = CharacterMotionMath.DampTowards(_weight, target, weightSmoothTime, Time.deltaTime);
            if (_weight < 0.01f)
            {
                return;
            }

            var grip = EnsureGripTarget();
            if (grip == null)
            {
                return;
            }

            var hand = _animator.GetIKPosition(AvatarIKGoal.LeftHand);
            _animator.SetIKPosition(AvatarIKGoal.LeftHand, Vector3.Lerp(hand, grip.position, _weight));
            _animator.SetIKRotation(
                AvatarIKGoal.LeftHand,
                Quaternion.Slerp(_animator.GetIKRotation(AvatarIKGoal.LeftHand), grip.rotation, _weight));
        }

        private Transform EnsureGripTarget()
        {
            if (gripTarget != null)
            {
                return gripTarget;
            }

            if (weaponMount == null)
            {
                return null;
            }

            var go = new GameObject("HandGripTarget");
            go.transform.SetParent(weaponMount, false);
            go.transform.localPosition = defaultGripOffset;
            go.transform.localRotation = Quaternion.identity;
            gripTarget = go.transform;
            return gripTarget;
        }
    }
}
