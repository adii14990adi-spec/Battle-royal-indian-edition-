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

        [SetUp]
        public void SetUp()
        {
            Time.captureFramerate = 60;
        }

        [TearDown]
        public void Cleanup()
        {
            Time.captureFramerate = 0;
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
            Assert.IsFalse(builder.Terrain.drawHeightmap,
                "the full-map Terrain must not render over the radius-clipped chunk meshes");
            Assert.AreEqual(-SiranMapConfig.HalfExtent, builder.Terrain.transform.position.x, 0.01f,
                "Terrain lower-left origin must align to the centred island grid");
            Assert.AreEqual(-SiranMapConfig.HalfExtent, builder.Terrain.transform.position.z, 0.01f,
                "Terrain lower-left origin must align to the centred island grid");
            Assert.IsNotNull(builder.Water, "water was not created");
            Assert.GreaterOrEqual(builder.Water.SurfaceCount, 2, "sea + lake at minimum");
            Assert.Greater(builder.Data.RoadRibbons.Count, 0);

            // The island itself is drawn by a matte overview mesh plus the 10 m
            // detail disk. Without it the camera only sees sea and sky.
            Assert.IsNotNull(builder.Overview, "the full-island overview mesh was not created");
            var overviewRenderer = builder.Overview.GetComponent<MeshRenderer>();
            Assert.IsNotNull(overviewRenderer, "overview needs a renderer");
            Assert.IsNotNull(overviewRenderer.sharedMesh, "overview needs a mesh");
            Assert.Greater(overviewRenderer.sharedMesh.triangles.Length, 0);
            var overviewMaterial = overviewRenderer.sharedMaterial;
            Assert.IsNotNull(overviewMaterial);
            Assert.AreEqual(0f, overviewMaterial.GetFloat("_Smoothness"), 0.001f,
                "the ground must be matte, not glossy");
            Assert.AreEqual(0f, overviewMaterial.GetFloat("_Metallic"), 0.001f,
                "the ground must not be metallic");
            Assert.GreaterOrEqual(overviewMaterial.GetFloat("_StreamClipMode"), 1.5f,
                "the island shell must draw outside the 50 m detail disk");

            // Every visible world mesh must use the same radial clip shader;
            // otherwise roads or water could leak beyond the terrain disk.
            var renderers = _world.GetComponentsInChildren<MeshRenderer>();
            Assert.Greater(renderers.Length, 0, "expected streamed terrain, roads and water renderers");
            foreach (var renderer in renderers)
            {
                Assert.IsNotNull(renderer.sharedMaterial, renderer.name + " has no material");
                Assert.AreEqual(SiranRadiusClip.ShaderName, renderer.sharedMaterial.shader.name,
                    renderer.name + " must be clipped to the shared 50 m disk");
            }
        }

        [UnityTest]
        public IEnumerator StreamerLoadsOnlyTilesIntersectingTheFiftyMetreDisk()
        {
            var builder = BuildWorld();
            var focus = new GameObject("Focus").transform;
            focus.position = new Vector3(40f, 20f, -30f);
            builder.Streamer.SetFocus(focus);
            builder.Streamer.ForceTickAt(focus.position);
            yield return null;

            Assert.AreEqual(50f, builder.Streamer.StreamRadiusM);
            Assert.AreEqual(50f, Shader.GetGlobalFloat(Shader.PropertyToID("_BHStreamRadius")), 0.001f,
                "the GPU clip radius must match the tile selector");
            var clipFocus = Shader.GetGlobalVector(Shader.PropertyToID("_BHStreamFocusXZ"));
            Assert.AreEqual(40f, clipFocus.x, 0.001f);
            Assert.AreEqual(-30f, clipFocus.y, 0.001f);
            Assert.AreEqual(4, builder.Streamer.ResidentChunkCount,
                "the circle touches the current 500 m tile and three neighbours at this grid corner");
            Assert.IsTrue(builder.Streamer.IsChunkResident(new Vector2Int(4, 3)),
                "focus (40, -30) belongs to chunk (4, 3)");
            Assert.IsTrue(builder.Streamer.IsChunkResident(new Vector2Int(3, 4)),
                "the diagonal tile is included at the exact 50 m tangent corner");

            // Moving well inside another tile leaves only that tile resident.
            focus.position = new Vector3(1900f, 20f, 1900f);
            builder.Streamer.ForceTickAt(focus.position);
            yield return null;
            Assert.AreEqual(1, builder.Streamer.ResidentChunkCount,
                "tiles outside the 50 m disk must be released");

            Object.Destroy(focus.gameObject);
        }

        [UnityTest]
        public IEnumerator StreamerRecomputesWhenTheDiskCrossesATileEdge()
        {
            var builder = BuildWorld();
            var focus = new GameObject("Focus").transform;
            focus.position = new Vector3(250f, 20f, 250f);
            builder.Streamer.SetFocus(focus);
            builder.Streamer.ForceTickAt(focus.position);
            Assert.AreEqual(1, builder.Streamer.ResidentChunkCount);

            // Both positions remain in the same 500 m grid cell. Membership still
            // changes because the circle starts/stops intersecting the next tile.
            focus.position = new Vector3(460f, 20f, 250f);
            builder.Streamer.ForceTickAt(focus.position);
            Assert.AreEqual(2, builder.Streamer.ResidentChunkCount);

            focus.position = new Vector3(430f, 20f, 250f);
            builder.Streamer.ForceTickAt(focus.position);
            Assert.AreEqual(1, builder.Streamer.ResidentChunkCount);
            yield return null;

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
            for (var i = 0; i < 20 && !motor.IsGrounded; i++)
            {
                yield return null;
            }

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

        [UnityTest]
        public IEnumerator MainMenuBuildsEventSystemRaycasterAndButtons()
        {
            var menuRoot = new GameObject("MainMenu");
            menuRoot.AddComponent<BattleHorizons.UI.MainMenuController>();
            yield return null;

            var eventSystem = Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>();
            Assert.IsNotNull(eventSystem, "MainMenu must ensure an EventSystem exists in the scene");
            Assert.IsNotNull(eventSystem.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>(),
                "EventSystem must have InputSystemUIInputModule attached");

            var canvas = menuRoot.GetComponentInChildren<Canvas>();
            Assert.IsNotNull(canvas, "MainMenuCanvas must be created");
            Assert.AreEqual(RenderMode.ScreenSpaceOverlay, canvas.renderMode);
            Assert.IsNotNull(canvas.GetComponent<UnityEngine.UI.GraphicRaycaster>(),
                "MainMenuCanvas must have a GraphicRaycaster");

            var scaler = canvas.GetComponent<UnityEngine.UI.CanvasScaler>();
            Assert.IsNotNull(scaler);
            Assert.AreEqual(UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize, scaler.uiScaleMode);

            var background = canvas.transform.Find("Background");
            Assert.IsNotNull(background);
            Assert.IsFalse(background.GetComponent<UnityEngine.UI.Image>().raycastTarget,
                "Background image must not block raycasts");

            var homeFrame = canvas.transform.Find("HomeArtworkFrame");
            Assert.IsNotNull(homeFrame, "HomeArtworkFrame must exist on the Home Screen");
            Assert.IsNotNull(homeFrame.GetComponent<UnityEngine.UI.Image>().sprite,
                "Home Screen background sprite must load from Resources");

            var enterButton = canvas.transform.Find("HomeArtworkFrame/EnterSiranIsland");
            Assert.IsNotNull(enterButton, "START (EnterSiranIsland) button must exist on the Home Screen");
            Assert.IsTrue(enterButton.GetComponent<UnityEngine.UI.Image>().raycastTarget);
            Assert.IsTrue(enterButton.GetComponent<UnityEngine.UI.Button>().interactable);

            var menuController = menuRoot.GetComponent<BattleHorizons.UI.MainMenuController>();
            Assert.AreEqual(BattleHorizons.UI.MainMenuController.MatchMode.Squad, menuController.SelectedMode);
            Assert.AreEqual("Battle Royale - Squad", menuController.ModeHeaderText);

            var soloButton = canvas.transform.Find("HomeArtworkFrame/ModeSolo").GetComponent<UnityEngine.UI.Button>();
            soloButton.onClick.Invoke();
            Assert.AreEqual(BattleHorizons.UI.MainMenuController.MatchMode.Solo, menuController.SelectedMode);
            Assert.AreEqual("Battle Royale - Solo", menuController.ModeHeaderText);

            var settingsBtn = canvas.transform.Find("HomeArtworkFrame/SettingsButton").GetComponent<UnityEngine.UI.Button>();
            settingsBtn.onClick.Invoke();
            var lobbyModal = canvas.transform.Find("LobbyModal");
            Assert.IsNotNull(lobbyModal);
            Assert.IsTrue(lobbyModal.gameObject.activeSelf, "Settings button must open LobbyModal");

            Object.Destroy(menuRoot);
        }

        [UnityTest]
        public IEnumerator PlayerControllerAndTouchUiWireCorrectly()
        {
            var ground = new GameObject("Ground");
            var collider = ground.AddComponent<BoxCollider>();
            collider.size = new Vector3(400f, 1f, 400f);
            ground.transform.position = new Vector3(0f, -0.5f, 0f);

            _player = new GameObject("Player");
            _player.transform.position = new Vector3(0f, 0.1f, 0f);
            _player.AddComponent<CharacterController>();
            var motor = _player.AddComponent<ThirdPersonMotor>();
            var playerController = _player.AddComponent<PlayerController>();

            var routerObj = new GameObject("Router");
            var router = routerObj.AddComponent<PlayerInputRouter>();

            var touchObj = new GameObject("TouchControls");
            var rig = BattleHorizons.UI.MobileControlsBuilder.Build(touchObj.transform, router, motor, playerController);
            yield return null;

            Assert.IsNotNull(_player.transform.Find("PlayerCapsuleVisual"), "Player must have a visible 3D Capsule");
            Assert.IsNotNull(rig.MoveStick, "MoveStick must exist");
            Assert.IsNotNull(rig.JumpButton, "JumpButton must exist");
            Assert.IsNotNull(rig.SprintUiButton, "SprintButton must exist");
            Assert.IsNotNull(rig.CrouchUiButton, "CrouchButton must exist");

            rig.SprintUiButton.onClick.Invoke();
            Assert.IsTrue(playerController.IsSprinting, "Sprint button onClick must toggle sprinting on");

            rig.CrouchUiButton.onClick.Invoke();
            Assert.IsTrue(playerController.IsCrouching, "Crouch button onClick must toggle crouching on");
            Assert.IsFalse(playerController.IsSprinting, "Crouching must clear sprinting");

            Object.Destroy(touchObj);
            Object.Destroy(routerObj);
            Object.Destroy(ground);
        }

        [UnityTest]
        public IEnumerator LoadingScreenBuildsAndUpdatesProgressFromZeroToHundred()
        {
            var view = BattleHorizons.UI.LoadingScreenView.AttachOrBuild();
            yield return null;

            Assert.IsNotNull(view);
            Assert.AreEqual("0%", view.PercentString);

            var artwork = view.transform.Find("LoadingScreenRoot/ArtworkFrame");
            Assert.IsNotNull(artwork, "LoadingScreenRoot/ArtworkFrame must exist");
            Assert.IsNotNull(artwork.GetComponent<UnityEngine.UI.Image>().sprite,
                "Loading screen background artwork sprite must be loaded");

            view.SetProgress(0.47f);
            Assert.AreEqual("47%", view.PercentString);

            view.SetProgress(1.0f);
            Assert.AreEqual("100%", view.PercentString);

            Object.Destroy(view.gameObject);
        }
    }
}
