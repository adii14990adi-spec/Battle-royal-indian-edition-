using UnityEngine;

namespace BattleHorizons.Characters
{
    /// <summary>
    /// Procedural turning lean: banks the spine/chest into strafes and turns
    /// (max ~8°) and adds a slight forward pitch while sprinting (~6°), so
    /// movement feels like a body with mass instead of a gliding video.
    ///
    /// Implemented with <c>Animator.SetBoneLocalRotation</c> inside
    /// OnAnimatorIK (the only sanctioned place to write procedural bone poses),
    /// split half/half across Spine and Chest for a natural S-curve. Angles
    /// come from <see cref="CharacterMotionMath.ComputeLeanAnglesDeg"/> and are
    /// exponentially damped — release the stick and the body settles upright
    /// through the same curve it leaned out with (Quaternion.Slerp).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ProceduralLean : MonoBehaviour
    {
        [SerializeField] private float leanSmoothTime = 0.18f;
        [SerializeField] private float maxLateralDeg = 8f;
        [SerializeField] private float maxForwardDeg = 6f;

        private Animator _animator;
        private CharacterController _controller;
        private Vector2 _lean;
        private float _lastYaw;
        private bool _hasLastYaw;
        private float _yawRate;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            _controller = GetComponentInParent<CharacterController>();
            _lastYaw = transform.eulerAngles.y;
            _hasLastYaw = true;
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (layerIndex != 0 || _animator == null || !_animator.isHuman)
            {
                return;
            }

            var dt = Time.deltaTime;
            var velocity = ComputePlanarVelocity();

            // Yaw rate: unwrapped degrees/second of the character's facing.
            var yaw = transform.eulerAngles.y;
            if (_hasLastYaw && dt > 0f)
            {
                var rate = CharacterMotionMath.UnwrapDegrees(yaw - _lastYaw) / dt;
                _yawRate = CharacterMotionMath.DampTowards(_yawRate, rate, 0.1f, dt);
            }

            _lastYaw = yaw;
            _hasLastYaw = true;

            var localVelocity = transform.InverseTransformDirection(velocity);
            var target = CharacterMotionMath.ComputeLeanAnglesDeg(
                localVelocity.x, _yawRate, localVelocity.z, maxLateralDeg, maxForwardDeg);
            _lean = new Vector2(
                CharacterMotionMath.DampTowards(_lean.x, target.x, leanSmoothTime, dt),
                CharacterMotionMath.DampTowards(_lean.y, target.y, leanSmoothTime, dt));

            // Half the bank on the spine, half on the chest, damped via Slerp
            // from the animated local rotation so breath/idle motion survives.
            var halfLean = Quaternion.Euler(_lean.y * 0.5f, 0f, -_lean.x * 0.5f);
            ApplyBone(HumanBodyBones.Spine, halfLean);
            ApplyBone(HumanBodyBones.Chest, halfLean);
        }

        private void ApplyBone(HumanBodyBones bone, Quaternion localOffset)
        {
            var t = _animator.GetBoneTransform(bone);
            if (t == null)
            {
                return;
            }

            _animator.SetBoneLocalRotation(bone, t.localRotation * localOffset);
        }

        private Vector3 ComputePlanarVelocity()
        {
            if (_controller != null)
            {
                var v = _controller.velocity;
                v.y = 0f;
                return v;
            }

            // No controller (preview scenes): approximate from the blend speed.
            return transform.forward * _animator.GetFloat(CharacterAnimParameters.Speed);
        }
    }
}
