using BattleHorizons.CameraRig;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BattleHorizons.Player
{
    /// <summary>
    /// One place that turns *devices* into the motor's intent, using the Input
    /// System (the Unity 6 default; the package is pinned in Packages/manifest.json).
    ///
    /// Three sources feed the same two floats + three buttons:
    ///   * an on-screen twin stick (<see cref="VirtualJoystick"/> x2),
    ///   * a keyboard (WASD / arrows + Shift + Space + C, for editor testing),
    ///   * a gamepad (left stick + right stick + South/A button).
    ///
    /// Camera-relative movement: the left stick is interpreted in the camera's
    /// yaw frame, which is what makes a third-person touch control usable at all.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerInputRouter : MonoBehaviour
    {
        [Header("Bindings")]
        [SerializeField] private ThirdPersonMotor motor;
        [SerializeField] private ThirdPersonCameraController cameraRig;

        [Header("Touch (optional)")]
        [SerializeField] private VirtualJoystick moveStick;
        [SerializeField] private VirtualJoystick lookStick;
        [SerializeField] private bool keyboardFallbackWhenNoTouch = true;

        [Header("Look")]
        [SerializeField] private float touchLookSensitivity = 2.4f;
        [SerializeField] private float stickLookSensitivity = 130f;

        private Vector2 _lookDelta;
        private bool _touchCrouchHeld;
        private bool _touchSprintHeld;

        /// <summary>Last look input, degrees accumulated this frame (for the camera).</summary>
        public Vector2 LookDelta => _lookDelta;

        /// <summary>
        /// On-screen hold buttons (crouch, sprint). They are ORed with the physical
        /// bindings rather than replacing them, so a phone with a gamepad attached
        /// behaves the same as one without.
        /// </summary>
        public void SetCrouchHeld(bool held) => _touchCrouchHeld = held;

        /// <summary>See <see cref="SetCrouchHeld"/>.</summary>
        public void SetSprintHeld(bool held) => _touchSprintHeld = held;

        public void Configure(ThirdPersonMotor targetMotor, ThirdPersonCameraController targetCamera,
            VirtualJoystick move, VirtualJoystick look)
        {
            motor = targetMotor;
            cameraRig = targetCamera;
            moveStick = move;
            lookStick = look;
        }

        private void Update()
        {
            if (motor == null)
            {
                return;
            }

            var move = ReadMove();
            var look = ReadLook();
            _lookDelta = look;

            motor.MoveInput = CameraRelative(move);
            motor.SprintInput = ReadSprint(move);
            motor.CrouchInput = ReadCrouch();

            if (ReadJump())
            {
                motor.RequestJump();
            }

            if (cameraRig != null && look.sqrMagnitude > 0f)
            {
                cameraRig.AddLook(look);
            }
        }

        private Vector2 ReadMove()
        {
            if (moveStick != null && moveStick.IsEngaged)
            {
                return moveStick.Value;
            }

            var gamepad = Gamepad.current;
            if (gamepad != null)
            {
                var stick = gamepad.leftStick.ReadValue();
                if (stick.sqrMagnitude > 0.01f)
                {
                    return stick;
                }
            }

            if (keyboardFallbackWhenNoTouch || Application.isEditor)
            {
                var keyboard = Keyboard.current;
                if (keyboard != null)
                {
                    var x = (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1f : 0f)
                            - (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1f : 0f);
                    var y = (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1f : 0f)
                            - (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed ? 1f : 0f);
                    return new Vector2(x, y);
                }
            }

            return Vector2.zero;
        }

        private Vector2 ReadLook()
        {
            if (lookStick != null && lookStick.IsEngaged)
            {
                return lookStick.Value * (stickLookSensitivity * Time.unscaledDeltaTime);
            }

            var gamepad = Gamepad.current;
            if (gamepad != null)
            {
                var stick = gamepad.rightStick.ReadValue();
                if (stick.sqrMagnitude > 0.01f)
                {
                    return stick * (stickLookSensitivity * Time.unscaledDeltaTime);
                }
            }

            // Desktop fallback: mouse delta while the right button is held, or always
            // in the editor so the camera can be checked without a phone.
            var mouse = Mouse.current;
            if (mouse != null && (Application.isEditor || mouse.rightButton.isPressed))
            {
                return mouse.delta.ReadValue() * (touchLookSensitivity * 0.1f);
            }

            return Vector2.zero;
        }

        private bool ReadSprint(Vector2 move)
        {
            if (_touchSprintHeld)
            {
                return true;
            }

            if (move.magnitude > 0.92f)
            {
                return true; // full stick deflection = run, standard mobile BR control
            }

            var keyboard = Keyboard.current;
            if (keyboard != null && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed))
            {
                return true;
            }

            var gamepad = Gamepad.current;
            return gamepad != null && gamepad.leftStickButton.isPressed;
        }

        private bool ReadCrouch()
        {
            if (_touchCrouchHeld)
            {
                return true;
            }

            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.cKey.isPressed)
            {
                return true;
            }

            var gamepad = Gamepad.current;
            return gamepad != null && gamepad.buttonEast.isPressed;
        }

        private bool ReadJump()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
            {
                return true;
            }

            var gamepad = Gamepad.current;
            return gamepad != null && gamepad.buttonSouth.wasPressedThisFrame;
        }

        private Vector3 CameraRelative(Vector2 move)
        {
            if (move.sqrMagnitude < 0.0001f)
            {
                return Vector3.zero;
            }

            var yaw = cameraRig != null ? cameraRig.Yaw : transform.eulerAngles.y;
            var forward = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            var right = Quaternion.Euler(0f, yaw, 0f) * Vector3.right;
            var direction = forward * move.y + right * move.x;
            return direction.sqrMagnitude > 1f ? direction.normalized : direction;
        }
    }
}
