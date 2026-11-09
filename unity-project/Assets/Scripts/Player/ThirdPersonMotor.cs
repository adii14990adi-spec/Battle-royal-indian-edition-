using UnityEngine;

namespace BattleHorizons.Player
{
    /// <summary>
    /// Third-person locomotion for Android: walk / run / jump / crouch, driven by
    /// a <see cref="CharacterController"/> so the ground contact is
    /// height-field friendly (the island is a Unity Terrain collider, and a rigid
    /// body on a 2 m-cell height field slides unpredictably).
    ///
    /// Numbers are chosen for touch input at 30 fps on the low profile:
    /// 3.2 m/s walk, 6.4 m/s sprint, 1.1 m jump height (v = sqrt(2gh)), a 0.35 s
    /// coyote time and a 0.12 s jump buffer so a tap that lands one frame early
    /// still jumps. Nothing here assumes 60 Hz; all timers are in seconds.
    ///
    /// The controller takes a *desired direction* from whoever owns the input
    /// (<see cref="PlayerInputRouter"/>), which keeps this class engine-agnostic
    /// and lets bots reuse it.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class ThirdPersonMotor : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float walkSpeed = 3.2f;
        [SerializeField] private float sprintSpeed = 6.4f;
        [SerializeField] private float crouchSpeed = 1.6f;
        [SerializeField] private float acceleration = 22f;
        [SerializeField] private float airControl = 0.35f;
        [SerializeField] private float rotationSpeedDegPerSecond = 720f;

        [Header("Jump")]
        [SerializeField] private float jumpHeight = 1.1f;
        [SerializeField] private float gravity = -22f;
        [SerializeField] private float coyoteTime = 0.35f;
        [SerializeField] private float jumpBufferTime = 0.12f;

        [Header("Body")]
        [SerializeField] private float standingHeight = 1.8f;
        [SerializeField] private float crouchHeight = 1.1f;
        [SerializeField] private float radius = 0.35f;

        private CharacterController _controller;
        private Vector3 _velocity;
        private float _verticalVelocity;
        private float _lastGroundedTime = -999f;
        private float _jumpRequestedAt = -999f;
        private bool _crouching;
        private float _targetHeight;

        /// <summary>Desired horizontal direction, world space, magnitude 0..1.</summary>
        public Vector3 MoveInput { get; set; }

        /// <summary>True while the sprint input is held.</summary>
        public bool SprintInput { get; set; }

        /// <summary>True while the crouch input is held.</summary>
        public bool CrouchInput { get; set; }

        public bool IsGrounded => _controller != null && _controller.isGrounded;
        public bool IsCrouching => _crouching;
        public Vector3 Velocity => _velocity + Vector3.up * _verticalVelocity;
        public float HorizontalSpeed => new Vector3(_velocity.x, 0f, _velocity.z).magnitude;

        /// <summary>Truthful locomotion state for animation and tests.</summary>
        public enum LocomotionState
        {
            Idle,
            Walk,
            Run,
            Crouch,
            Airborne,
        }

        public LocomotionState State
        {
            get
            {
                if (!IsGrounded)
                {
                    return LocomotionState.Airborne;
                }

                if (_crouching)
                {
                    return LocomotionState.Crouch;
                }

                var speed = HorizontalSpeed;
                if (speed < 0.15f)
                {
                    return LocomotionState.Idle;
                }

                return speed > walkSpeed + 0.4f ? LocomotionState.Run : LocomotionState.Walk;
            }
        }

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _controller.radius = radius;
            _controller.height = standingHeight;
            _controller.center = new Vector3(0f, standingHeight * 0.5f, 0f);
            _controller.slopeLimit = 50f;
            _controller.stepOffset = 0.4f;
            _controller.skinWidth = 0.03f;
            _controller.minMoveDistance = 0f;
            _targetHeight = standingHeight;
        }

        /// <summary>Queue a jump. Buffered for <see cref="jumpBufferTime"/> seconds.</summary>
        public void RequestJump()
        {
            _jumpRequestedAt = Time.time;
        }

        private void Update()
        {
            var dt = Time.deltaTime;
            if (dt <= 0f)
            {
                return;
            }

            UpdateCrouch(dt);
            UpdateHorizontal(dt);
            UpdateVertical(dt);

            var motion = _velocity * dt + Vector3.up * (_verticalVelocity * dt);
            _controller.Move(motion);

            if (_controller.isGrounded)
            {
                _lastGroundedTime = Time.time;
                if (_verticalVelocity < 0f)
                {
                    _verticalVelocity = -2f; // keeps the controller glued to slopes
                }
            }

            FaceMovementDirection(dt);
        }

        private void UpdateCrouch(float dt)
        {
            _crouching = CrouchInput;
            _targetHeight = _crouching ? crouchHeight : standingHeight;
            var height = Mathf.MoveTowards(_controller.height, _targetHeight, dt * 6f);
            if (!Mathf.Approximately(height, _controller.height))
            {
                _controller.height = height;
                _controller.center = new Vector3(0f, height * 0.5f, 0f);
            }
        }

        private void UpdateHorizontal(float dt)
        {
            var input = MoveInput;
            if (input.sqrMagnitude > 1f)
            {
                input.Normalize();
            }

            var speed = _crouching ? crouchSpeed : (SprintInput ? sprintSpeed : walkSpeed);
            var target = input * speed;
            var control = IsGrounded ? 1f : airControl;
            _velocity = Vector3.MoveTowards(_velocity, target, acceleration * control * dt);
            if (_velocity.sqrMagnitude < 0.0004f)
            {
                _velocity = Vector3.zero;
            }
        }

        private void UpdateVertical(float dt)
        {
            var canJump = Time.time - _lastGroundedTime <= coyoteTime;
            var wantsJump = Time.time - _jumpRequestedAt <= jumpBufferTime;

            if (canJump && wantsJump)
            {
                _verticalVelocity = Mathf.Sqrt(2f * Mathf.Abs(gravity) * jumpHeight);
                _jumpRequestedAt = -999f;
                _lastGroundedTime = -999f;
                _crouching = CrouchInput;
            }

            if (_controller.isGrounded && _verticalVelocity < 0f)
            {
                _verticalVelocity = -2f;
                return;
            }

            _verticalVelocity += gravity * dt;
        }

        private void FaceMovementDirection(float dt)
        {
            var flat = new Vector3(_velocity.x, 0f, _velocity.z);
            if (flat.sqrMagnitude < 0.01f)
            {
                return;
            }

            var target = Quaternion.LookRotation(flat, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target,
                rotationSpeedDegPerSecond * dt);
        }

        /// <summary>Teleport (spawn / respawn): clears velocity so the player does not inherit a fall.</summary>
        public void Teleport(Vector3 position)
        {
            _controller.enabled = false;
            transform.position = position;
            _controller.enabled = true;
            _velocity = Vector3.zero;
            _verticalVelocity = 0f;
        }

        /// <summary>Test hook: exact jump velocity for the configured height.</summary>
        public float JumpVelocityForTest => Mathf.Sqrt(2f * Mathf.Abs(gravity) * jumpHeight);
    }
}
