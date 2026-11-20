using System.Collections;
using BattleHorizons.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BattleHorizons.Tests.PlayMode
{
    /// <summary>
    /// Control semantics locked to the playtest report:
    ///   * crouch button  -> the player SITS and stays seated (no stand/sit
    ///     flicker frame-to-frame),
    ///   * sprint button  -> the player RUNS while it is toggled on,
    ///   * move joystick  -> WALK only; full deflection must not auto-sprint.
    /// The PlayerInputRouter is the single per-frame writer of motor input.
    /// </summary>
    public class ControlSemanticsTests
    {
        private GameObject _player;
        private GameObject _routerGo;
        private GameObject _stickGo;

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(_player);
            Object.Destroy(_routerGo);
            Object.Destroy(_stickGo);
        }

        private (ThirdPersonMotor motor, PlayerController controller, PlayerInputRouter router,
            VirtualJoystick stick) BuildRig()
        {
            _player = new GameObject("Player");
            _player.AddComponent<CharacterController>();
            var motor = _player.AddComponent<ThirdPersonMotor>();
            var controller = _player.AddComponent<PlayerController>();

            _routerGo = new GameObject("Router");
            var router = _routerGo.AddComponent<PlayerInputRouter>();

            _stickGo = new GameObject("MoveStick", typeof(RectTransform));
            var stick = _stickGo.AddComponent<VirtualJoystick>();
            var canvasGo = new GameObject("HudCanvas");
            canvasGo.AddComponent<Canvas>();
            var knob = new GameObject("Knob", typeof(RectTransform));
            knob.transform.SetParent(_stickGo.transform, false);
            stick.Configure((RectTransform)_stickGo.transform, (RectTransform)knob.transform,
                canvasGo.GetComponent<Canvas>(), 100f);

            router.Configure(motor, null, stick, null);
            return (motor, controller, router, stick);
        }

        [UnityTest]
        public IEnumerator CrouchButton_SitsAndStaysSeated()
        {
            var (motor, controller, _, _) = BuildRig();
            yield return null;

            controller.OnCrouchButtonClicked();
            for (var i = 0; i < 60; i++)
            {
                yield return null;
                Assert.IsTrue(motor.CrouchInput, "crouch must stay held on frame " + i);
                Assert.IsTrue(motor.IsCrouching, "player must remain seated on frame " + i);
            }

            controller.OnCrouchButtonClicked();
            for (var i = 0; i < 30; i++)
            {
                yield return null;
                Assert.IsFalse(motor.CrouchInput, "second tap must stand the player up, frame " + i);
            }
        }

        [UnityTest]
        public IEnumerator SprintButton_RunsWhileOn()
        {
            var (motor, controller, _, _) = BuildRig();
            yield return null;

            controller.OnSprintButtonClicked();
            for (var i = 0; i < 30; i++)
            {
                yield return null;
                Assert.IsTrue(motor.SprintInput, "sprint must stay on while toggled, frame " + i);
            }

            controller.OnSprintButtonClicked();
            yield return null;
            Assert.IsFalse(motor.SprintInput, "toggling sprint off must return to walk");
        }

        [UnityTest]
        public IEnumerator JoystickAlone_WalksNotRuns()
        {
            var (motor, _, _, stick) = BuildRig();
            yield return null;

            stick.SetValue(Vector2.up); // full deflection
            for (var i = 0; i < 20; i++)
            {
                yield return null;
                Assert.Greater(motor.MoveInput.magnitude, 0.9f, "stick must drive movement");
                Assert.IsFalse(motor.SprintInput, "full stick deflection must stay a walk, frame " + i);
            }
        }

        [UnityTest]
        public IEnumerator Jump_StandsUpFromCrouch()
        {
            var (motor, controller, _, _) = BuildRig();
            yield return null;

            controller.OnCrouchButtonClicked();
            yield return null;
            Assert.IsTrue(motor.IsCrouching, "player must be seated before the jump");

            controller.OnJumpButtonClicked();
            yield return null;
            Assert.IsFalse(motor.CrouchInput, "jumping must leave the crouch");
        }
    }
}
