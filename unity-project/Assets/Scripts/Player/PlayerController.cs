using BattleHorizons.CameraRig;
using BattleHorizons.World;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace BattleHorizons.Player
{
    /// <summary>
    /// Phase 1 3D Player Controller for Siran Island Greybox.
    /// Handles Walk/Sprint movement from the on-screen VirtualJoystick (or keyboard),
    /// Jumping, and Crouching (with visible 3D Capsule height scaling), and exposes
    /// UI Button onClick handlers for SPRINT, JUMP, and CROUCH.
    /// </summary>
    [DefaultExecutionOrder(-5)]
    [RequireComponent(typeof(CharacterController))]
    [DisallowMultipleComponent]
    public sealed class PlayerController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private ThirdPersonMotor motor;
        [SerializeField] private PlayerInputRouter inputRouter;
        [SerializeField] private ThirdPersonCameraController cameraRig;
        [SerializeField] private VirtualJoystick moveJoystick;
        [SerializeField] private Transform visualCapsule;
        [SerializeField] private Button sprintButton;
        [SerializeField] private Button jumpButton;
        [SerializeField] private Button crouchButton;

        [Header("Movement Settings")]
        [SerializeField] private float walkSpeed = 4.0f;
        [SerializeField] private float sprintSpeed = 7.2f;
        [SerializeField] private float crouchSpeed = 2.0f;
        [SerializeField] private float jumpHeight = 1.25f;
        [SerializeField] private float gravity = -22.0f;
        [SerializeField] private float standingHeight = 1.8f;
        [SerializeField] private float crouchHeight = 1.1f;

        private CharacterController _controller;
        private Vector3 _velocity;
        private float _verticalVelocity;
        private bool _isSprinting;
        private bool _isCrouching;

        public bool IsSprinting => _isSprinting;
        public bool IsCrouching => _isCrouching;
        public bool IsGrounded => _controller != null && _controller.isGrounded;

        public void Configure(
            ThirdPersonMotor targetMotor,
            PlayerInputRouter router,
            ThirdPersonCameraController targetCamera,
            VirtualJoystick joystick,
            Transform capsule,
            Button sprintBtn = null,
            Button jumpBtn = null,
            Button crouchBtn = null)
        {
            motor = targetMotor;
            inputRouter = router;
            cameraRig = targetCamera;
            moveJoystick = joystick;
            visualCapsule = capsule;
            sprintButton = sprintBtn;
            jumpButton = jumpBtn;
            crouchButton = crouchBtn;
        }

        public void BindUiControls(VirtualJoystick joystick, Button sprintBtn, Button jumpBtn, Button crouchBtn)
        {
            moveJoystick = joystick;
            sprintButton = sprintBtn;
            jumpButton = jumpBtn;
            crouchButton = crouchBtn;
            WireButtons();
        }

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            if (motor == null)
            {
                motor = GetComponent<ThirdPersonMotor>();
            }

            EnsureVisualCapsule();
        }

        private void Start()
        {
            if (cameraRig == null)
            {
                cameraRig = FindFirstObjectByType<ThirdPersonCameraController>();
            }

            if (inputRouter == null)
            {
                inputRouter = FindFirstObjectByType<PlayerInputRouter>();
            }

            if (moveJoystick == null)
            {
                var sticks = FindObjectsByType<VirtualJoystick>(FindObjectsSortMode.None);
                for (var i = 0; i < sticks.Length; i++)
                {
                    if (sticks[i].name.Contains("Move") || moveJoystick == null)
                    {
                        moveJoystick = sticks[i];
                    }
                }
            }

            SnapToTerrainGround();
            WireButtons();
            UpdateButtonVisuals();

            if (cameraRig != null)
            {
                if (cameraRig.Target == null)
                {
                    cameraRig.Configure(transform, transform.eulerAngles.y);
                }
                cameraRig.SnapToTarget();
            }
        }

        private void WireButtons()
        {
            if (jumpButton != null)
            {
                jumpButton.onClick.RemoveListener(OnJumpButtonClicked);
                jumpButton.onClick.AddListener(OnJumpButtonClicked);
            }

            if (sprintButton != null)
            {
                sprintButton.onClick.RemoveListener(OnSprintButtonClicked);
                sprintButton.onClick.AddListener(OnSprintButtonClicked);
            }

            if (crouchButton != null)
            {
                crouchButton.onClick.RemoveListener(OnCrouchButtonClicked);
                crouchButton.onClick.AddListener(OnCrouchButtonClicked);
            }
        }

        private void SnapToTerrainGround()
        {
            var world = FindFirstObjectByType<SiranWorldBuilder>();
            if (world != null)
            {
                world.Build();
                if (world.Data != null && world.Data.IsLoaded)
                {
                    var pos = transform.position;
                    var groundY = world.Data.SampleHeight(pos.x, pos.z);
                    var targetPos = new Vector3(pos.x, groundY + 0.35f, pos.z);
                    if (motor != null)
                    {
                        motor.Teleport(targetPos);
                    }
                    else
                    {
                        _controller.enabled = false;
                        transform.position = targetPos;
                        _controller.enabled = true;
                    }
                }
            }
        }

        private void EnsureVisualCapsule()
        {
            if (visualCapsule == null)
            {
                var existing = transform.Find("PlayerCapsuleVisual");
                if (existing != null)
                {
                    visualCapsule = existing;
                }
            }

            if (visualCapsule == null)
            {
                var capsuleObj = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                capsuleObj.name = "PlayerCapsuleVisual";
                capsuleObj.transform.SetParent(transform, false);
                var capsuleCollider = capsuleObj.GetComponent<Collider>();
                if (capsuleCollider != null)
                {
                    if (Application.isPlaying)
                    {
                        Destroy(capsuleCollider);
                    }
                    else
                    {
                        DestroyImmediate(capsuleCollider);
                    }
                }

                visualCapsule = capsuleObj.transform;
            }

            visualCapsule.localPosition = new Vector3(0f, standingHeight * 0.5f, 0f);
            visualCapsule.localScale = new Vector3(0.7f, standingHeight * 0.5f, 0.7f);

            var renderer = visualCapsule.GetComponent<MeshRenderer>();
            if (renderer != null && (renderer.sharedMaterial == null || renderer.sharedMaterial.name.StartsWith("Default")))
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Sprites/Default");
                if (shader != null)
                {
                    var mat = new Material(shader)
                    {
                        name = "PlayerBrightCapsuleRuntimeMat",
                        color = new Color(1.0f, 0.42f, 0.05f, 1.0f),
                    };
                    mat.SetColor("_BaseColor", new Color(1.0f, 0.42f, 0.05f, 1.0f));
                    mat.SetFloat("_Smoothness", 0.35f);
                    renderer.sharedMaterial = mat;
                }
            }
        }

        private void Update()
        {
            var move2D = ReadMoveInput();

            if (motor != null)
            {
                var yaw = cameraRig != null ? cameraRig.Yaw : transform.eulerAngles.y;
                var worldMove = Quaternion.Euler(0f, yaw, 0f) * new Vector3(move2D.x, 0f, move2D.y);
                if (worldMove.sqrMagnitude > 0.0001f)
                {
                    motor.MoveInput = Vector3.ClampMagnitude(worldMove, 1f);
                }
                else if (inputRouter == null)
                {
                    motor.MoveInput = Vector3.zero;
                }

                if (_isSprinting)
                {
                    motor.SprintInput = true;
                }

                if (_isCrouching)
                {
                    motor.CrouchInput = true;
                }

                UpdateVisualCapsuleScale(motor.IsCrouching ? crouchHeight : standingHeight);
                return;
            }

            UpdateStandaloneMovement(move2D, Time.deltaTime);
            UpdateVisualCapsuleScale(_isCrouching ? crouchHeight : standingHeight);
        }

        private Vector2 ReadMoveInput()
        {
            var stick = moveJoystick != null ? moveJoystick.Value : Vector2.zero;
            if (stick.sqrMagnitude > 0.001f)
            {
                return stick;
            }

            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                var x = 0f;
                var y = 0f;
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) x -= 1f;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) x += 1f;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) y -= 1f;
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) y += 1f;
                var kb = new Vector2(x, y);
                if (kb.sqrMagnitude > 1f)
                {
                    kb.Normalize();
                }
                return kb;
            }

            return Vector2.zero;
        }

        private void UpdateStandaloneMovement(Vector2 move2D, float dt)
        {
            if (dt <= 0f || _controller == null)
            {
                return;
            }

            var targetHeight = _isCrouching ? crouchHeight : standingHeight;
            _controller.height = Mathf.MoveTowards(_controller.height, targetHeight, dt * 6f);
            _controller.center = new Vector3(0f, _controller.height * 0.5f, 0f);

            var yaw = cameraRig != null ? cameraRig.Yaw : transform.eulerAngles.y;
            var worldDir = Quaternion.Euler(0f, yaw, 0f) * new Vector3(move2D.x, 0f, move2D.y);
            if (worldDir.sqrMagnitude > 1f)
            {
                worldDir.Normalize();
            }

            var speed = _isCrouching ? crouchSpeed : (_isSprinting ? sprintSpeed : walkSpeed);
            _velocity = worldDir * speed;

            if (_controller.isGrounded && _verticalVelocity < 0f)
            {
                _verticalVelocity = -2f;
            }
            else
            {
                _verticalVelocity += gravity * dt;
            }

            var motion = (_velocity + Vector3.up * _verticalVelocity) * dt;
            _controller.Move(motion);

            if (worldDir.sqrMagnitude > 0.01f)
            {
                var targetRot = Quaternion.LookRotation(worldDir, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, 720f * dt);
            }
        }

        private void UpdateVisualCapsuleScale(float currentHeight)
        {
            if (visualCapsule == null)
            {
                return;
            }

            var halfH = currentHeight * 0.5f;
            visualCapsule.localScale = Vector3.Lerp(
                visualCapsule.localScale,
                new Vector3(0.7f, halfH, 0.7f),
                Time.deltaTime * 12f);
            visualCapsule.localPosition = Vector3.Lerp(
                visualCapsule.localPosition,
                new Vector3(0f, halfH, 0f),
                Time.deltaTime * 12f);
        }

        public void OnJumpButtonClicked() => Jump();

        public void OnSprintButtonClicked() => ToggleSprint();

        public void OnCrouchButtonClicked() => ToggleCrouch();

        public void Jump()
        {
            if (_isCrouching)
            {
                SetCrouch(false);
            }

            if (motor != null)
            {
                motor.RequestJump();
                return;
            }

            if (_controller != null && _controller.isGrounded)
            {
                _verticalVelocity = Mathf.Sqrt(2f * Mathf.Abs(gravity) * jumpHeight);
            }
        }

        public void ToggleSprint()
        {
            SetSprint(!_isSprinting);
        }

        public void SetSprint(bool sprinting)
        {
            _isSprinting = sprinting;
            if (_isSprinting && _isCrouching)
            {
                _isCrouching = false;
            }

            if (inputRouter != null)
            {
                inputRouter.SetSprintHeld(_isSprinting);
                inputRouter.SetCrouchHeld(_isCrouching);
            }

            if (motor != null)
            {
                motor.SprintInput = _isSprinting;
                motor.CrouchInput = _isCrouching;
            }

            UpdateButtonVisuals();
        }

        public void ToggleCrouch()
        {
            SetCrouch(!_isCrouching);
        }

        public void SetCrouch(bool crouching)
        {
            _isCrouching = crouching;
            if (_isCrouching && _isSprinting)
            {
                _isSprinting = false;
            }

            if (inputRouter != null)
            {
                inputRouter.SetCrouchHeld(_isCrouching);
                inputRouter.SetSprintHeld(_isSprinting);
            }

            if (motor != null)
            {
                motor.CrouchInput = _isCrouching;
                motor.SprintInput = _isSprinting;
            }

            UpdateButtonVisuals();
        }

        private void UpdateButtonVisuals()
        {
            if (sprintButton != null && sprintButton.targetGraphic != null)
            {
                sprintButton.targetGraphic.color = _isSprinting
                    ? new Color(0.2f, 0.95f, 0.35f, 0.85f)
                    : new Color(0.6f, 1f, 0.7f, 0.45f);
            }

            if (crouchButton != null && crouchButton.targetGraphic != null)
            {
                crouchButton.targetGraphic.color = _isCrouching
                    ? new Color(0.25f, 0.65f, 1.0f, 0.85f)
                    : new Color(0.55f, 0.75f, 1f, 0.45f);
            }
        }
    }
}
