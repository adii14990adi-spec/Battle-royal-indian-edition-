using System;
using System.Collections;
using BattleHorizons.CameraRig;
using BattleHorizons.Characters;
using BattleHorizons.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BattleHorizons.Tests.PlayMode
{
    /// <summary>
    /// PlayMode evidence that the generated FBX character visual does not break
    /// the existing movement, camera, collision or animation-hook behaviour.
    /// These tests deliberately do not create a shooting system: they exercise
    /// only the hooks that actually exist in the production runtime.
    /// </summary>
    public class FBXProductionPlayModeTests
    {
        private GameObject _player;
        private GameObject _ground;
        private GameObject _cameraObject;

        [SetUp]
        public void SetUp()
        {
            Time.captureFramerate = 60;
        }

        [TearDown]
        public void Cleanup()
        {
            Time.captureFramerate = 0;
            if (_cameraObject != null) UnityEngine.Object.Destroy(_cameraObject);
            if (_player != null) UnityEngine.Object.Destroy(_player);
            if (_ground != null) UnityEngine.Object.Destroy(_ground);
            _cameraObject = null;
            _player = null;
            _ground = null;
        }

        [UnityTest]
        public IEnumerator VisiblePlayerIsProductionFbxAndCapsuleIsHidden()
        {
            var config = CharacterAnimationConfig.Load();
            if (config == null)
            {
                Assert.Ignore("Editor-generated CharacterAnimationConfig is absent; run CharacterAnimationIntegration.BuildFromCommandLine first.");
            }

            MeshRenderer capsuleRenderer;
            ThirdPersonMotor motor;
            var visual = CreatePlayerWithVisual(out capsuleRenderer, out motor);
            yield return null;

            Assert.IsTrue(visual.TrySpawn() || visual.IsCharacterSpawned,
                "the configured production character must spawn under the player");
            var definition = visual.ActiveDefinition;
            Assert.IsNotNull(definition, "active character definition");
            Assert.IsNotNull(definition.model, "active character model");
            Assert.IsTrue(definition.displayName.IndexOf("character", StringComparison.OrdinalIgnoreCase) >= 0,
                "the active definition should be the Male/Female character FBX, got: " + definition.displayName);
            Assert.IsFalse(definition.displayName.IndexOf("GLTF", StringComparison.OrdinalIgnoreCase) >= 0,
                "the active definition must not be the isolated GLTF fallback");
            Assert.IsFalse(definition.displayName.IndexOf("Capsule", StringComparison.OrdinalIgnoreCase) >= 0,
                "the active definition must not be a capsule placeholder");
            Assert.IsFalse(definition.displayName.IndexOf("Cylinder", StringComparison.OrdinalIgnoreCase) >= 0,
                "the active definition must not be a cylinder placeholder");

            var skinned = _player.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            Assert.Greater(skinned.Length, 0, "FBX character must contain SkinnedMeshRenderer(s)");
            Assert.IsFalse(capsuleRenderer.enabled, "PlayerCapsuleVisual MeshRenderer must be disabled after FBX spawn");
            Assert.IsNotNull(visual.CharacterAnimator, "spawned FBX must expose an Animator");
            Assert.IsTrue(visual.CharacterAnimator.avatar != null
                && visual.CharacterAnimator.avatar.isValid
                && visual.CharacterAnimator.avatar.isHuman,
                "spawned FBX must have a valid Humanoid Avatar");
            Assert.IsNotNull(visual.CharacterAnimator.runtimeAnimatorController,
                "spawned FBX must receive the generated Animator Controller");
            Assert.IsNotNull(motor, "ThirdPersonMotor must remain present");
        }

        [UnityTest]
        public IEnumerator MovementCameraOrientationAndGroundRemainFunctionalWithFbxVisual()
        {
            var config = CharacterAnimationConfig.Load();
            if (config == null)
            {
                Assert.Ignore("Editor-generated CharacterAnimationConfig is absent; run CharacterAnimationIntegration.BuildFromCommandLine first.");
            }

            _ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _ground.name = "FBXProductionGround";
            _ground.transform.position = new Vector3(0f, -0.5f, 0f);
            _ground.transform.localScale = new Vector3(400f, 1f, 400f);

            MeshRenderer capsuleRenderer;
            ThirdPersonMotor motor;
            var visual = CreatePlayerWithVisual(out capsuleRenderer, out motor);
            _player.transform.position = new Vector3(0f, 0.2f, 0f);
            Assert.IsTrue(visual.TrySpawn() || visual.IsCharacterSpawned, "FBX visual must spawn before locomotion begins");

            _cameraObject = new GameObject("FBXProductionCamera");
            _cameraObject.AddComponent<Camera>();
            var cameraRig = _cameraObject.AddComponent<ThirdPersonCameraController>();
            cameraRig.Configure(_player.transform, 0f);
            cameraRig.SnapToTarget();

            for (var i = 0; i < 20 && !motor.IsGrounded; i++)
            {
                yield return null;
            }
            Assert.IsTrue(motor.IsGrounded, "player should start grounded on the temporary floor");

            var start = _player.transform.position;
            var startCameraRotation = _cameraObject.transform.rotation;
            motor.MoveInput = Vector3.forward;
            for (var i = 0; i < 40; i++)
            {
                yield return null;
            }

            Assert.Greater(Vector3.Distance(start, _player.transform.position), 0.5f,
                "the FBX player must still walk");
            Assert.Less(Vector3.Angle(_player.transform.forward, Vector3.forward), 20f,
                "the FBX player must face its movement direction");
            Assert.IsTrue(motor.IsGrounded, "the FBX player must stay grounded while walking");
            Assert.Less(Mathf.Abs(_player.transform.position.y - 0.2f), 0.35f,
                "the FBX player must not fall through or fly above the floor");
            Assert.IsTrue(visual.IsCharacterSpawned, "the FBX visual must remain spawned during movement");
            Assert.IsFalse(capsuleRenderer.enabled, "the capsule visual must remain hidden during movement");

            var startDistance = Vector3.Distance(_cameraObject.transform.position, _player.transform.position);
            Assert.Greater(startDistance, 1f, "third-person camera should sit away from the player");
            Assert.Less(startDistance, 6f, "third-person camera should stay near the player");

            cameraRig.AddLook(new Vector2(90f, 0f));
            for (var i = 0; i < 4; i++)
            {
                yield return null;
            }
            Assert.AreEqual(90f, Mathf.Repeat(cameraRig.Yaw, 360f), 0.5f, "camera yaw must respond to look input");
            Assert.Greater(Quaternion.Angle(startCameraRotation, _cameraObject.transform.rotation), 10f,
                "camera rotation must change after a 90 degree yaw input");

            var flatOffset = _cameraObject.transform.position - _player.transform.position;
            flatOffset.y = 0f;
            Assert.Less(flatOffset.magnitude, 6f, "camera must continue following the FBX player");
        }

        [UnityTest]
        public IEnumerator AimShootReloadAnimationHooksRemainSafeWithFbxVisual()
        {
            var config = CharacterAnimationConfig.Load();
            if (config == null)
            {
                Assert.Ignore("Editor-generated CharacterAnimationConfig is absent; run CharacterAnimationIntegration.BuildFromCommandLine first.");
            }

            MeshRenderer capsuleRenderer;
            ThirdPersonMotor motor;
            var visual = CreatePlayerWithVisual(out capsuleRenderer, out motor);
            yield return null;
            Assert.IsTrue(visual.TrySpawn() || visual.IsCharacterSpawned, "FBX visual must spawn before hook test");

            var animator = visual.CharacterAnimator;
            Assert.IsNotNull(animator, "spawned FBX must expose an Animator");
            var hasShoot = false;
            var hasReload = false;
            var hasIsAiming = false;
            var parameters = animator.parameters;
            for (var i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].name == CharacterAnimParameters.Shoot) hasShoot = true;
                if (parameters[i].name == CharacterAnimParameters.Reload) hasReload = true;
                if (parameters[i].name == CharacterAnimParameters.IsAiming) hasIsAiming = true;
            }
            Assert.IsTrue(hasShoot, "generated controller must expose the Shoot trigger");
            Assert.IsTrue(hasReload, "generated controller must expose the Reload trigger");
            Assert.IsTrue(hasIsAiming, "generated controller must expose the IsAiming bool");
            Assert.Greater(animator.layerCount, CharacterAnimParameters.UpperBodyLayerIndex,
                "generated controller must expose the UpperBody layer");

            motor.AimInput = true;
            for (var i = 0; i < 20; i++)
            {
                yield return null;
            }
            Assert.IsTrue(animator.GetBool(CharacterAnimParameters.IsAiming),
                "actual motor aim state must drive the IsAiming Animator bool");
            Assert.Greater(animator.GetLayerWeight(CharacterAnimParameters.UpperBodyLayerIndex), 0.5f,
                "the existing SetAiming hook must ramp the UpperBody layer");

            motor.AimInput = false;
            for (var i = 0; i < 20; i++)
            {
                yield return null;
            }
            Assert.IsFalse(animator.GetBool(CharacterAnimParameters.IsAiming),
                "leaving aim state must clear the IsAiming Animator bool");
            Assert.Less(animator.GetLayerWeight(CharacterAnimParameters.UpperBodyLayerIndex), 0.1f,
                "leaving aim state must ramp the UpperBody layer back down");

            visual.TriggerShoot();
            visual.TriggerReload();
            visual.TriggerDeath();
            visual.ResetToDefaultState();
            yield return null;
            Assert.IsTrue(visual.IsCharacterSpawned, "animation hooks must leave the FBX visual spawned");
            Assert.IsFalse(capsuleRenderer.enabled, "animation hooks must leave the capsule hidden");
        }

        private PlayerCharacterVisual CreatePlayerWithVisual(
            out MeshRenderer capsuleRenderer,
            out ThirdPersonMotor motor)
        {
            _player = new GameObject("FBXProductionPlayer");
            _player.AddComponent<CharacterController>();
            motor = _player.AddComponent<ThirdPersonMotor>();

            var capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            capsule.name = "PlayerCapsuleVisual";
            capsule.transform.SetParent(_player.transform, false);
            capsule.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            capsule.transform.localScale = new Vector3(0.7f, 0.9f, 0.7f);
            var capsuleCollider = capsule.GetComponent<Collider>();
            if (capsuleCollider != null) UnityEngine.Object.Destroy(capsuleCollider);
            capsuleRenderer = capsule.GetComponent<MeshRenderer>();

            // The capsule exists before PlayerCharacterVisual so its Awake can
            // auto-find the fallback visual, exactly like the generated scene.
            return _player.AddComponent<PlayerCharacterVisual>();
        }
    }
}
