using UnityEngine;

namespace BattleHorizons.CameraRig
{
    /// <summary>
    /// Third-person orbit camera with collision/occlusion push-back.
    ///
    /// Ported requirements from the Godot spectator (`debug_spectator.gd`) made
    /// player-facing: a yaw/pitch orbit around a shoulder-height pivot, a spring
    /// arm that shortens when terrain is in the way, and pitch clamps that keep
    /// the horizon usable on a phone held in landscape.
    ///
    /// Look input arrives already scaled (<see cref="Player.PlayerInputRouter"/>),
    /// so this class has no device knowledge. The streamer follows this
    /// transform, so the camera is also what decides which chunks are resident —
    /// that is why it exposes <see cref="Yaw"/> and a smoothed position.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ThirdPersonCameraController : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 pivotOffset = new Vector3(0f, 1.55f, 0f);

        [Header("Orbit")]
        [SerializeField] private float distance = 4.2f;
        [SerializeField] private float minPitch = -25f;
        [SerializeField] private float maxPitch = 65f;
        [SerializeField] private float followSharpness = 12f;
        [SerializeField] private float shoulderOffset = 0.45f;

        [Header("Collision")]
        [SerializeField] private LayerMask collisionMask = ~0;
        [SerializeField] private float collisionRadius = 0.22f;
        [SerializeField] private float minimumDistance = 0.6f;

        private float _yaw;
        private float _pitch = 12f;
        private float _currentDistance;
        private Vector3 _smoothedPivot;
        private readonly RaycastHit[] _hits = new RaycastHit[8];

        public float Yaw => _yaw;
        public float Pitch => _pitch;
        public float CurrentDistance => _currentDistance;
        public Transform Target => target;

        public void Configure(Transform followTarget, float startYaw = 0f)
        {
            target = followTarget;
            _yaw = startYaw;
            _smoothedPivot = Pivot();
            _currentDistance = distance;
        }

        /// <summary>Adds look input in degrees (already frame-scaled by the router).</summary>
        public void AddLook(Vector2 delta)
        {
            _yaw += delta.x;
            _pitch = Mathf.Clamp(_pitch - delta.y, minPitch, maxPitch);
        }

        /// <summary>Snap the rig so it does not fly across the island after a teleport.</summary>
        public void SnapToTarget()
        {
            if (target == null)
            {
                return;
            }

            _smoothedPivot = Pivot();
            transform.position = ComputePosition(_smoothedPivot, out var hit, out var resolved);
            _currentDistance = resolved;
            transform.LookAt(_smoothedPivot);
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            _smoothedPivot = Vector3.Lerp(_smoothedPivot, Pivot(),
                1f - Mathf.Exp(-followSharpness * Time.unscaledDeltaTime));

            transform.position = ComputePosition(_smoothedPivot, out var hit, out var resolved);
            _currentDistance = resolved;

            var lookDirection = _smoothedPivot - transform.position;
            if (lookDirection.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(lookDirection, Vector3.up);
            }
        }

        private Vector3 Pivot() => target != null ? target.position + pivotOffset : transform.position;

        private Vector3 ComputePosition(Vector3 pivot, out bool hitSomething, out float resolved)
        {
            var rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            var back = rotation * Vector3.back;
            var shoulder = rotation * new Vector3(shoulderOffset, 0f, 0f);
            var desired = pivot + back * distance + shoulder;

            hitSomething = false;
            resolved = distance;
            var from = pivot;
            var direction = (desired - from).normalized;
            var span = Vector3.Distance(from, desired);

            // SphereCastAll + nearest, not SphereCast: the island terrain collider
            // is one huge object, so a single cast that starts inside it is useless.
            var count = Physics.SphereCastNonAlloc(from, collisionRadius, direction, _hits, span,
                collisionMask, QueryTriggerInteraction.Ignore);
            var nearest = float.MaxValue;
            for (var i = 0; i < count; i++)
            {
                var hit = _hits[i];
                if (hit.transform == null || hit.transform.IsChildOf(target))
                {
                    continue;
                }

                if (hit.distance < nearest)
                {
                    nearest = hit.distance;
                }
            }

            if (nearest < float.MaxValue)
            {
                hitSomething = true;
                resolved = Mathf.Max(minimumDistance, nearest);
                return pivot + back * resolved + shoulder * (resolved / Mathf.Max(distance, 0.001f));
            }

            return desired;
        }
    }
}
