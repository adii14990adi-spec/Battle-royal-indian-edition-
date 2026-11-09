using System.Collections;
using BattleHorizons.CameraRig;
using BattleHorizons.Core;
using BattleHorizons.Player;
using BattleHorizons.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BattleHorizons.Tests.PlayMode
{
    /// <summary>
    /// Runtime behaviour: the world actually builds, the terrain collider stops a
    /// falling body, the camera follows, and the motor moves and jumps.
    ///
    /// These are real play-mode tests (they run the Update/LateUpdate/FixedUpdate
    /// loop), so they verify behaviour rather than source shape. What they cannot
    /// verify is touch input on glass or frame rate on a phone — those are device
    /// gates and are reported as such.
    /// </summary>
    public class WorldAndPlayerPlayModeTests
    {
        private GameObject _world;
        private GameObject _player;
        private GameObject _cameraObject;

        [TearDown]
        public void Cleanup()
        {
            if (_world != null) Object.Destroy(_world);
            if (_player != null) Object.Destroy(_player);
            if (_cameraObject != null) Object.Destroy(_cameraObject);
            _world = null;
            _player = null;
            _cameraObject = null;
        }

        private SiranWorldBuilder BuildWorld()
        {
            _world = new GameObject("World");
            var builder = _world.AddComponent<SiranWorldBuilder>();
            Assert.IsTrue(builder.Build(), "SiranWorldBuilder.Build() failed - see the [bh][world] log line");
            return builder;
        }

        [UnityTest]
        public IEnumerator WorldBuildsTerrainStreamerAndWater()
        {
            var builder = BuildWorld();
            yield return null;

            Assert.IsNotNull(builder.Data, "island data");
            Assert.IsTrue(builder.Data.IsLoaded);
            Assert.IsNotNull(builder.Terrain, "terrain was not created");
            Assert.IsNotNull(builder.Terrain.GetComponent<TerrainCollider>(), "terrain needs collision");
            Assert.IsNotNull(builder.Water, "water was not created");
            Assert.GreaterOrEqual(builder.Water.SurfaceCount, 2, "sea + lake at minimum");
            Assert.Greater(builder.Data.RoadRibbons.Count, 0);
        }

        [UnityTest]
        public IEnumerator StreamerKeepsChunksResidentAroundTheFocus()
        {
            var builder = BuildWorld();
            var focus = new GameObject("Focus").transform;
            focus.position = new Vector3(40f, 20f, -30f);
            builder.Streamer.SetFocus(focus);
            builder.Streamer.ForceTickAt(focus.position);
            yield return null;

            Assert.GreaterOrEqual(builder.Streamer.ResidentChunkCount, 9,
                "keep_ring 1 means at least a 3x3 block of 500 m chunks");

            // Moving to the far corner must release chunks behind us.
            focus.position = new Vector3(1900f, 20f, 1900f);
            builder.Streamer.ForceTickAt(focus.position);
            yield return null;
            Assert.LessOrEqual(builder.Streamer.ResidentChunkCount, 9);

            Object.Destroy(focus.gameObject);
        }

        [UnityTest]
        public IEnumerator PlayerFallsOntoTheTerrainCollider()
        {
            var builder = BuildWorld();
            yield return null;

            _player = new GameObject("Player");
            _player.transform.position = builder.Data.SpawnPoint() + Vector3.up * 30f;
            var controller = _player.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.35f;
            controller.center = new Vector3(0f, 0.9f, 0f);
            _player.AddComponent<ThirdPersonMotor>();

            // Let gravity do its work on the terrain collider.
            for (var i = 0; i < 180; i++)
            {
                yield return null;
            }

            var expectedGround = builder.Data.SampleHeight(_player.transform.position.x, _player.transform.position.z);
            Assert.AreEqual(expectedGround, _player.transform.position.y, 0.6f,
                "the player should come to rest on the baked ground height");
        }

        [UnityTest]
        public IEnumerator MotorWalksRunsAndJumps()
        {
            var ground = new GameObject("Ground");
            ground.transform.position = Vector3.zero;
            var collider = ground.AddComponent<BoxCollider>();
            collider.size = new Vector3(400f, 1f, 400f);
            ground.transform.position = new Vector3(0f, -0.5f, 0f);

            _player = new GameObject("Player");
            _player.transform.position = new Vector3(0f, 0.2f, 0f);
            _player.AddComponent<CharacterController>();
            var motor = _player.AddComponent<ThirdPersonMotor>();
            yield return null;
            yield return null;

            Assert.IsTrue(motor.IsGrounded, "player should start grounded");

            var start = _player.transform.position;
            motor.MoveInput = Vector3.forward;
            for (var i = 0; i < 40; i++)
            {
                yield return null;
            }

            Assert.Greater(Vector3.Distance(start, _player.transform.position), 0.5f, "walk moved the player");
            Assert.AreEqual(ThirdPersonMotor.LocomotionState.Walk, motor.State);

            motor.MoveInput = Vector3.forward;
            motor.SprintInput = true;
            for (var i = 0; i < 30; i++)
            {
                yield return null;
            }

            Assert.AreEqual(ThirdPersonMotor.LocomotionState.Run, motor.State, "sprint should reach Run");

            motor.MoveInput = Vector3.zero;
            motor.SprintInput = false;
            yield return null;
            motor.RequestJump();
            var groundY = _player.transform.position.y;
            var peak = groundY;
            for (var i = 0; i < 40; i++)
            {
                yield return null;
                peak = Mathf.Max(peak, _player.transform.position.y);
            }

            Assert.Greater(peak - groundY, 0.5f, "jump should lift the player");
            Assert.Less(peak - groundY, 2.0f, "jump height should stay near the 1.1 m budget");

            Object.Destroy(ground);
        }

        [UnityTest]
        public IEnumerator CameraFollowsThePlayerAndPushesIn()
        {
            var builder = BuildWorld();
            yield return null;

            _player = new GameObject("Player");
            _player.transform.position = builder.Data.SpawnPoint() + Vector3.up * 2f;
            var target = _player.transform;

            _cameraObject = new GameObject("Camera");
            _cameraObject.AddComponent<Camera>();
            var rig = _cameraObject.AddComponent<ThirdPersonCameraController>();
            rig.Configure(target, 0f);
            rig.SnapToTarget();
            yield return null;
            yield return null;

            var startDistance = Vector3.Distance(_cameraObject.transform.position, target.position);
            Assert.Greater(startDistance, 1f);
            Assert.Less(startDistance, 6f);

            // Moving the player moves the camera with it.
            target.position += new Vector3(12f, 0f, 8f);
            for (var i = 0; i < 20; i++)
            {
                yield return null;
            }

            var flatOffset = _cameraObject.transform.position - target.position;
            flatOffset.y = 0f;
            Assert.Less(flatOffset.magnitude, 6f, "camera stayed near the player");

            rig.AddLook(new Vector2(90f, 0f));
            Assert.AreEqual(90f, rig.Yaw % 360f, 0.5f);
        }

        [UnityTest]
        public IEnumerator RenderScaleAndQualityApplyWithoutError()
        {
            QualityProfiles.Apply(QualityProfile.Low);
            Assert.AreEqual(QualityProfile.Low, QualityProfiles.Active);
            Assert.AreEqual(30, Application.targetFrameRate, "low profile is capped at 30 fps");

            QualityProfiles.Apply(QualityProfile.High);
            Assert.AreEqual(QualityProfile.High, QualityProfiles.Active);
            Assert.GreaterOrEqual(Application.targetFrameRate, 30);

            QualityProfiles.SetOverride(QualityProfiles.AutoDetect);
            QualityProfiles.Apply(QualityProfile.Low);
            yield return null;
        }
    }
}
