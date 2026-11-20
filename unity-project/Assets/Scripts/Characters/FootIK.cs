using UnityEngine;

namespace BattleHorizons.Characters
{
    /// <summary>
    /// Ground-planting foot IK (built-in Humanoid avatar IK — no package, no
    /// GC). Each foot raycasts the terrain from just above its animated
    /// position; the IK goal moves to the hit point (within a clamp) and the
    /// ankle aligns to the ground plane, so feet stop floating on slopes and
    /// steps. Weight fades out while running (<see cref="CharacterMotionMath.FootIkWeight"/>)
    /// so sprint cycles are never dragged, and drops to zero airborne.
    ///
    /// Must live on the same GameObject as the Animator (CharacterMotionRig
    /// attaches it there). Generic rigs are ignored — IK needs the Humanoid
    /// avatar the import gate already enforces.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FootIK : MonoBehaviour
    {
        [Header("Ground probing")]
        [SerializeField] private LayerMask groundMask = 1 << 8; // Terrain layer (+Default added at runtime)
        [SerializeField] private float probeUpM = 0.6f;
        [SerializeField] private float probeDownM = 0.9f;
        [Tooltip("Max foot raise/lower from the animated pose, metres (steps, not cliffs).")]
        [SerializeField] private float maxAdjustM = 0.3f;
        [SerializeField] private float footClearanceM = 0.02f;

        [Header("Blending")]
        [SerializeField] private float weightSmoothTime = 0.12f;

        private Animator _animator;
        private CharacterController _controller;
        private Transform _selfRoot;
        private float _weight;
        private float _lastSpeed;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            _selfRoot = transform.root;
            groundMask |= 1; // include Default so ProBuilder ground slabs count
        }

        /// <summary>CharacterMotionRig wires the player's controller for grounded/speed state.</summary>
        public void Bind(CharacterController controller)
        {
            _controller = controller;
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (layerIndex != 0 || _animator == null || !_animator.isHuman)
            {
                return;
            }

            var grounded = _controller == null || _controller.isGrounded;
            var speed = _animator.GetFloat(CharacterAnimParameters.Speed);
            _lastSpeed = speed;

            var targetWeight = CharacterMotionMath.FootIkWeight(grounded, speed);
            _weight = CharacterMotionMath.DampTowards(_weight, targetWeight, weightSmoothTime, Time.deltaTime);
            if (_weight < 0.01f)
            {
                return;
            }

            PlantFoot(AvatarIKGoal.LeftFoot);
            PlantFoot(AvatarIKGoal.RightFoot);
        }

        private void PlantFoot(AvatarIKGoal goal)
        {
            var footPosition = _animator.GetIKPosition(goal);
            var origin = footPosition + Vector3.up * probeUpM;

            if (!Physics.Raycast(origin, Vector3.down, out var hit, probeUpM + probeDownM,
                    groundMask, QueryTriggerInteraction.Ignore))
            {
                return;
            }

            if (hit.collider != null && hit.collider.transform.root == _selfRoot)
            {
                return; // never plant on the character's own colliders
            }

            var animatedHeight = footPosition.y;
            var targetHeight = Mathf.Clamp(
                hit.point.y + footClearanceM,
                animatedHeight - maxAdjustM,
                animatedHeight + maxAdjustM);

            var target = new Vector3(footPosition.x, targetHeight, footPosition.z);
            _animator.SetIKPosition(goal, Vector3.Lerp(footPosition, target, _weight));

            // Align the sole with the ground plane, blended by the same weight.
            var groundRotation = Quaternion.FromToRotation(Vector3.up, hit.normal) * _animator.GetIKRotation(goal);
            _animator.SetIKRotation(goal, Quaternion.Slerp(_animator.GetIKRotation(goal), groundRotation, _weight * 0.7f));
        }
    }
}
