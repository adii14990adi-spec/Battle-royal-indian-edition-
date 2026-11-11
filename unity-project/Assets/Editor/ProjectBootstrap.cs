using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BattleHorizons.CameraRig;
using BattleHorizons.Core;
using BattleHorizons.Player;
using BattleHorizons.UI;
using BattleHorizons.World;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace BattleHorizons.EditorTools
{
    /// <summary>
    /// One-command project setup: URP asset + Android player settings + quality
    /// profiles + materials + scenes + build settings.
    ///
    /// Run it from the menu (<b>Battle Horizons &gt; Project Setup &gt; Configure</b>)
    /// or headless:
    /// <code>
    /// Unity -batchmode -quit -projectPath unity-project \
    ///       -executeMethod BattleHorizons.EditorTools.ProjectBootstrap.ConfigureFromCommandLine
    /// </code>
    ///
    /// Everything is idempotent: it creates what is missing and reuses what
    /// exists. CI runs the same method in batch mode as the editor menu.
    /// </summary>
    public static class ProjectBootstrap
    {
        public const string SettingsFolder = "Assets/Settings";
        public const string ScenesFolder = "Assets/Scenes";
        public const string MaterialsFolder = "Assets/Materials";
        public const string UrpAssetPath = SettingsFolder + "/BattleHorizonsURP.asset";
        public const string UrpRendererPath = SettingsFolder + "/BattleHorizonsURPRenderer.asset";
        public const string TerrainMaterialPath = MaterialsFolder + "/SiranTerrainMat.mat";
        public const string ChunkMaterialPath = MaterialsFolder + "/SiranChunkMat.mat";
        public const string WaterMaterialPath = MaterialsFolder + "/SiranWaterMat.mat";
        public const string RoadMaterialPath = MaterialsFolder + "/SiranRoadMat.mat";
        public const string PlayerCapsuleMaterialPath = MaterialsFolder + "/PlayerCapsuleMat.mat";
        public const string UiLogoResourceAssetPath = "Assets/Resources/UI/battle_horizons_logo.png";
        public const string HomeBgResourceAssetPath = "Assets/Resources/UI/battle_horizons_home_bg.jpg";
        public const string LoadingBgResourceAssetPath = "Assets/Resources/UI/battle_horizons_loading_bg.jpg";
        public const string LoadingBarResourceAssetPath = "Assets/Resources/UI/battle_horizons_loading_bar.png";
        public const string LoadingSpinnerResourceAssetPath = "Assets/Resources/UI/battle_horizons_loading_spinner.png";
        public const string AppIconPath = "Assets/UI/Icons/battle_horizons_icon.png";

        public static readonly string[] ScenePaths =
        {
            ScenesFolder + "/Boot.unity",
            ScenesFolder + "/SiranIsland_Greybox.unity",
            ScenesFolder + "/MainMenu/MainMenu.unity",
            ScenesFolder + "/Gameplay/SiranIsland.unity",
            ScenesFolder + "/Test/PlayerTest.unity",
        };

        private static readonly StringBuilder Report = new StringBuilder();

        [MenuItem("Battle Horizons/Project Setup/Configure", priority = 0)]
        public static void Configure()
        {
            Report.Clear();
            try
            {
                EnsureFolders();
                EnsureBrandingAssets();
                var urpAsset = EnsureRenderPipeline();
                var materials = EnsureMaterials();
                EnsureScenes(materials);
                EnsureBuildSettings();
                ConfigureAndroidPlayerSettings();
                ConfigureApplicationIcons();

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Line("Project configuration complete.");
                Debug.Log("[bh][setup]\n" + Report);
            }
            catch (Exception exception)
            {
                Debug.LogError("[bh][setup] FAILED: " + exception.Message + "\n" + Report);
                throw;
            }
        }

        /// <summary>Headless entry point used by .github/workflows/unity.yml.</summary>
        public static void ConfigureFromCommandLine()
        {
            try
            {
                Configure();
                Debug.Log("[bh][setup] batch-mode configuration finished");
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(0);
                }
            }
            catch (Exception exception)
            {
                Debug.LogError("[bh][setup] configuration failed: " + exception);
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }
            }
        }

        /// <summary>Validation-only entry point: exits non-zero on failure.</summary>
        public static void ValidateFromCommandLine()
        {
            var failures = ProjectValidator.ValidateAll(out var report);
            Debug.Log("[bh][validate]\n" + report);
            if (failures > 0)
            {
                Debug.LogError(string.Format("[bh][validate] {0} validation failure(s)", failures));
                EditorApplication.Exit(1);
                return;
            }

            Debug.Log("[bh][validate] all project checks passed");
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(0);
            }
        }

        private static void Line(string message)
        {
            Report.Append("  ").Append(message).Append('\n');
        }

        private static void EnsureFolders()
        {
            string[] folders =
            {
                SettingsFolder, MaterialsFolder, ScenesFolder,
                ScenesFolder + "/MainMenu", ScenesFolder + "/Gameplay", ScenesFolder + "/Test",
                "Assets/Prefabs", "Assets/Animations", "Assets/Audio", "Assets/Shaders",
            };

            for (var i = 0; i < folders.Length; i++)
            {
                if (AssetDatabase.IsValidFolder(folders[i]))
                {
                    continue;
                }

                var split = folders[i].LastIndexOf('/');
                AssetDatabase.CreateFolder(folders[i].Substring(0, split), folders[i].Substring(split + 1));
                Line("created folder " + folders[i]);
            }
        }

        /// <summary>
        /// Creates or repairs the URP pipeline asset and makes it the project's
        /// render pipeline. Conservative mobile settings are applied before the
        /// runtime device profile adjusts its supported quality knobs.
        /// </summary>
        private static UniversalRenderPipelineAsset EnsureRenderPipeline()
        {
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(UrpRendererPath);
            if (renderer == null)
            {
                renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer, UrpRendererPath);
                ResourceReloader.ReloadAllNullIn(renderer, UniversalRenderPipelineAsset.packagePath);
                EditorUtility.SetDirty(renderer);
                Line("created " + UrpRendererPath);
            }

            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(UrpAssetPath);
            if (asset == null)
            {
                asset = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(asset, UrpAssetPath);
                Line("created " + UrpAssetPath);
            }

            var serialized = new SerializedObject(asset);

            // Start from the safe/default pipeline values. QualityProfiles.Apply
            // adjusts render scale, cascades, shadow distance/resolution, MSAA and
            // the per-object additional-light cap after device detection.
            SetFloat(serialized, "m_RenderScale", 1.0f);
            SetBool(serialized, "m_SupportsHDR", false);
            SetBool(serialized, "m_RequireDepthTexture", false);
            SetBool(serialized, "m_RequireOpaqueTexture", false);
            SetInt(serialized, "m_MSAA", 1);                       // 1x: MSAA is 15-30% frame time on tiled mobile GPUs
            SetInt(serialized, "m_ShadowCascadeCount", 2);         // low/high both fit in 2 cascades at 60/120 m
            SetFloat(serialized, "m_ShadowDistance", 60f);
            SetFloat(serialized, "m_ShadowDepthBias", 1.0f);
            SetFloat(serialized, "m_ShadowNormalBias", 0.4f);
            SetInt(serialized, "m_MainLightShadowmapResolution", 512);
            SetInt(serialized, "m_AdditionalLightsRenderingMode", 1);   // per-vertex: cheapest forward path
            SetInt(serialized, "m_AdditionalLightsPerObjectLimit", 2);  // matches max_lights_per_object = 2
            SetBool(serialized, "m_AdditionalLightShadowsSupported", false);
            SetInt(serialized, "m_AdditionalLightsShadowmapResolution", 512);
            SetBool(serialized, "m_SoftShadowsSupported", false);
            SetBool(serialized, "m_UseSRPBatcher", true);            // fewer binds = fewer draw-call costs
            SetInt(serialized, "m_ColorGradingMode", 0);             // LDR grading: no HDR tone-map pass
            SetInt(serialized, "m_StoreActionsOptimization", 0);
            SetBool(serialized, "m_SupportsTerrainHoles", false);
            SetBool(serialized, "m_SupportDataDrivenLensFlare", false);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);

            GraphicsSettings.defaultRenderPipeline = asset;
            QualitySettings.renderPipeline = asset;

            // The URP global settings object must exist or URP logs a warning on boot.
            if (GraphicsSettings.GetSettingsForRenderPipeline<UniversalRenderPipeline>() == null)
            {
                var globalSettingsType = typeof(UniversalRenderPipeline).Assembly.GetType(
                    "UnityEngine.Rendering.Universal.UniversalRenderPipelineGlobalSettings");
                var ensureMethod = globalSettingsType?.GetMethod(
                    "Ensure",
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
                ensureMethod?.Invoke(null, new object[] { true });
            }

            if (GraphicsSettings.GetSettingsForRenderPipeline<UniversalRenderPipeline>() == null)
            {
                var settings = ScriptableObject.CreateInstance("UniversalRenderPipelineGlobalSettings") as RenderPipelineGlobalSettings;
                if (settings != null)
                {
                    settings.name = "URP Global Settings";
                    var settingsPath = SettingsFolder + "/URPGlobalSettings.asset";
                    AssetDatabase.CreateAsset(settings, settingsPath);
                    GraphicsSettings.RegisterRenderPipelineSettings<UniversalRenderPipeline>(settings);
                    Line("created " + settingsPath);
                }
            }

            Line("URP base asset: HDR off, MSAA 1x, 2 cascades @ 60 m, 512 px shadows, per-vertex additional lights (limit 2), SRP batcher on; runtime profiles override their budget knobs");
            return asset;
        }

        private static Dictionary<string, Material> EnsureMaterials()
        {
            var materials = new Dictionary<string, Material>
            {
                ["terrain"] = EnsureMaterial(TerrainMaterialPath, "Universal Render Pipeline/Terrain/Lit",
                    material =>
                    {
                        material.SetFloat("_Smoothness", 0.05f);
                        material.SetColor("_BaseColor", Color.white);
                        material.enableInstancing = true;
                    }),
                ["chunk"] = EnsureMaterial(ChunkMaterialPath, "BattleHorizons/Radius Clipped Lit",
                    material =>
                    {
                        material.SetFloat("_UseVertexColor", 1f);
                        material.SetFloat("_Smoothness", 0.05f);
                        material.SetFloat("_Metallic", 0f);
                        material.enableInstancing = true;
                    }),
                ["road"] = EnsureMaterial(RoadMaterialPath, "BattleHorizons/Radius Clipped Lit",
                    material =>
                    {
                        material.SetFloat("_UseVertexColor", 1f);
                        material.SetFloat("_Smoothness", 0.12f);
                        material.SetFloat("_Metallic", 0f);
                        material.enableInstancing = true;
                    }),
                ["water"] = EnsureMaterial(WaterMaterialPath, "BattleHorizons/Radius Clipped Lit",
                    material =>
                    {
                        material.SetColor("_BaseColor", new Color(0.20f, 0.44f, 0.50f, 1f));
                        material.SetFloat("_Smoothness", 0.92f);
                        material.SetFloat("_Metallic", 0f);
                        var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(
                            "Assets/Resources/Art/Textures/water_normal_256.png");
                        if (normal != null)
                        {
                            material.SetTexture("_BumpMap", normal);
                            material.SetFloat("_BumpScale", 0.45f);
                            material.EnableKeyword("_NORMALMAP");
                        }

                        material.SetFloat("_Surface", 0f); // opaque: no transparency sorting
                    }),
                ["player"] = EnsureMaterial(PlayerCapsuleMaterialPath, "Universal Render Pipeline/Lit",
                    material =>
                    {
                        var brightColor = new Color(1.0f, 0.42f, 0.05f, 1.0f);
                        material.color = brightColor;
                        material.SetColor("_BaseColor", brightColor);
                        material.SetFloat("_Smoothness", 0.35f);
                        material.SetFloat("_Metallic", 0.0f);
                    }),
            };

            AssetDatabase.SaveAssets();
            return materials;
        }

        private static Material EnsureMaterial(string path, string shaderName, Action<Material> configure)
        {
            var shader = Shader.Find(shaderName);
            if (shader == null)
            {
                throw new InvalidOperationException(
                    "shader '" + shaderName + "' not found — is com.unity.render-pipelines.universal installed?");
            }

            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
                Line("created " + path);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
                Line("re-pointed " + path + " at " + shaderName);
            }

            configure(material);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void EnsureScenes(Dictionary<string, Material> materials)
        {
            if (Directory.Exists(ScenesFolder))
            {
                var valid = new HashSet<string>(ScenePaths);
                foreach (var existing in Directory.GetFiles(ScenesFolder, "*.unity", SearchOption.AllDirectories))
                {
                    var normalized = existing.Replace('\\', '/');
                    if (!valid.Contains(normalized))
                    {
                        AssetDatabase.DeleteAsset(normalized);
                    }
                }
            }

            EnsureSiranIslandGreyboxScene(materials);
            EnsureBootScene();
            EnsureMainMenuScene();
            EnsureSiranIslandScene(materials);
            EnsurePlayerTestScene(materials);
        }

        private static Scene NewScene(string path)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            return scene;
        }

        private static void SaveScene(Scene scene, string path)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, path);
            Line("wrote " + path);
        }

        private static void EnsureSiranIslandGreyboxScene(Dictionary<string, Material> materials)
        {
            var path = ScenesFolder + "/SiranIsland_Greybox.unity";
            var scene = NewScene(path);

            PopulatePlayableIslandScene(materials);

            SaveScene(scene, path);
        }

        private static void EnsureBootScene()
        {
            var path = ScenesFolder + "/Boot.unity";
            var scene = NewScene(path);

            var camera = new GameObject("BootCamera");
            var cameraComponent = camera.AddComponent<Camera>();
            cameraComponent.clearFlags = CameraClearFlags.SolidColor;
            cameraComponent.backgroundColor = Color.black;
            cameraComponent.cullingMask = 0;
            camera.tag = "MainCamera";

            var loadingCanvasObject = new GameObject(
                "BootLoadingCanvas", typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler));
            var loadingCanvas = loadingCanvasObject.GetComponent<Canvas>();
            loadingCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var loadingScaler = loadingCanvasObject.GetComponent<UnityEngine.UI.CanvasScaler>();
            loadingScaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            loadingScaler.referenceResolution = new Vector2(1920f, 1080f);

            var bootLogoSprite = AssetDatabase.LoadAssetAtPath<Sprite>(UiLogoResourceAssetPath);
            if (bootLogoSprite != null)
            {
                var bootLogoObject = new GameObject(
                    "BootLogo", typeof(RectTransform), typeof(UnityEngine.UI.Image));
                bootLogoObject.transform.SetParent(loadingCanvasObject.transform, false);
                var bootLogoRect = (RectTransform)bootLogoObject.transform;
                bootLogoRect.anchorMin = new Vector2(0.5f, 0.5f);
                bootLogoRect.anchorMax = new Vector2(0.5f, 0.5f);
                bootLogoRect.pivot = new Vector2(0.5f, 0.5f);
                bootLogoRect.sizeDelta = new Vector2(260f, 260f);
                bootLogoRect.anchoredPosition = new Vector2(0f, 110f);
                var bootLogoImage = bootLogoObject.GetComponent<UnityEngine.UI.Image>();
                bootLogoImage.sprite = bootLogoSprite;
                bootLogoImage.preserveAspect = true;
                bootLogoImage.raycastTarget = false;
            }

            var loadingLabelObject = new GameObject(
                "LoadingLabel", typeof(RectTransform), typeof(UnityEngine.UI.Text));
            loadingLabelObject.transform.SetParent(loadingCanvasObject.transform, false);
            var loadingLabelRect = (RectTransform)loadingLabelObject.transform;
            loadingLabelRect.anchorMin = new Vector2(0.5f, 0.5f);
            loadingLabelRect.anchorMax = new Vector2(0.5f, 0.5f);
            loadingLabelRect.pivot = new Vector2(0.5f, 0.5f);
            loadingLabelRect.sizeDelta = new Vector2(1500f, 180f);
            loadingLabelRect.anchoredPosition = new Vector2(0f, -110f);
            var loadingLabel = loadingLabelObject.GetComponent<UnityEngine.UI.Text>();
            loadingLabel.text = "BATTLE HORIZONS: LAST SURVIVE\nLOADING...";
            loadingLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            loadingLabel.fontSize = 38;
            loadingLabel.alignment = TextAnchor.MiddleCenter;
            loadingLabel.color = new Color(1f, 1f, 1f, 0f);
            loadingLabel.raycastTarget = false;

            LoadingScreenView.AttachOrBuild(loadingCanvas);

            var bootstrap = new GameObject("GameBootstrap");
            var component = bootstrap.AddComponent<GameBootstrap>();
            var serialized = new SerializedObject(component);
            var nextScene = serialized.FindProperty("nextScene");
            if (nextScene != null)
            {
                nextScene.stringValue = GameBootstrap.SceneNames.MainMenu;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            SaveScene(scene, path);
        }

        private static void EnsureMainMenuScene()
        {
            var path = ScenesFolder + "/MainMenu/MainMenu.unity";
            var scene = NewScene(path);

            var camera = new GameObject("MainCamera");
            var cameraComponent = camera.AddComponent<Camera>();
            cameraComponent.clearFlags = CameraClearFlags.SolidColor;
            cameraComponent.backgroundColor = new Color(0.63f, 0.73f, 0.84f);
            camera.tag = "MainCamera";
            camera.transform.position = new Vector3(0f, 2f, -8f);

            MobileControlsBuilder.EnsureEventSystem();

            var menuObject = new GameObject("MainMenu");
            var menu = menuObject.AddComponent<MainMenuController>();

            SaveScene(scene, path);
        }

        private static void EnsureSiranIslandScene(Dictionary<string, Material> materials)
        {
            var path = ScenesFolder + "/Gameplay/SiranIsland.unity";
            var scene = NewScene(path);

            PopulatePlayableIslandScene(materials);

            SaveScene(scene, path);
        }

        private static void EnsurePlayerTestScene(Dictionary<string, Material> materials)
        {
            var path = ScenesFolder + "/Test/PlayerTest.unity";
            var scene = NewScene(path);

            PopulatePlayableIslandScene(materials);

            SaveScene(scene, path);
        }

        private static void PopulatePlayableIslandScene(Dictionary<string, Material> materials)
        {
            var worldObject = new GameObject("World");
            var builder = worldObject.AddComponent<SiranWorldBuilder>();
            var worldSerialized = new SerializedObject(builder);
            worldSerialized.FindProperty("terrainMaterial").objectReferenceValue = materials["terrain"];
            worldSerialized.FindProperty("chunkMaterial").objectReferenceValue = materials["chunk"];
            worldSerialized.FindProperty("waterMaterial").objectReferenceValue = materials["water"];
            worldSerialized.FindProperty("roadMaterial").objectReferenceValue = materials["road"];
            worldSerialized.FindProperty("showFullTerrainBase").boolValue = true;
            worldSerialized.ApplyModifiedPropertiesWithoutUndo();

            var player = new GameObject("Player");
            player.tag = "Player";
            var controller = player.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.35f;
            controller.center = new Vector3(0f, 0.9f, 0f);
            var motor = player.AddComponent<ThirdPersonMotor>();
            var playerController = player.AddComponent<PlayerController>();

            var capsuleVisual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            capsuleVisual.name = "PlayerCapsuleVisual";
            capsuleVisual.transform.SetParent(player.transform, false);
            capsuleVisual.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            capsuleVisual.transform.localScale = new Vector3(0.7f, 0.9f, 0.7f);
            var capsuleCollider = capsuleVisual.GetComponent<Collider>();
            if (capsuleCollider != null)
            {
                UnityEngine.Object.DestroyImmediate(capsuleCollider);
            }
            var capsuleRenderer = capsuleVisual.GetComponent<MeshRenderer>();
            if (capsuleRenderer != null && materials.TryGetValue("player", out var playerMat))
            {
                capsuleRenderer.sharedMaterial = playerMat;
            }

            // Start near the committed island spawn point, snapped above the sampled ground height.
            var spawn = SiranMapConfig.SpawnPoint;
            var spawnY = spawn.y;
            if (SiranIslandData.TryLoad(out var islandData, out _) && islandData != null)
            {
                spawnY = islandData.SampleHeight(spawn.x, spawn.z);
            }
            player.transform.position = new Vector3(spawn.x, spawnY + 0.5f, spawn.z);

            var cameraObject = new GameObject("PlayerCamera");
            cameraObject.tag = "MainCamera";
            var cameraComponent = cameraObject.AddComponent<Camera>();
            cameraComponent.nearClipPlane = 0.1f;
            cameraComponent.farClipPlane = 1000f;
            cameraComponent.fieldOfView = 68f;
            cameraComponent.clearFlags = CameraClearFlags.SolidColor;
            cameraComponent.backgroundColor = new Color(0.55f, 0.72f, 0.88f, 1f);
            var cameraRig = cameraObject.AddComponent<ThirdPersonCameraController>();
            cameraRig.Configure(player.transform, 0f);
            cameraRig.SnapToTarget();

            var routerObject = new GameObject("InputRouter");
            var router = routerObject.AddComponent<PlayerInputRouter>();

            var cameraSerialized = new SerializedObject(cameraRig);
            cameraSerialized.FindProperty("target").objectReferenceValue = player.transform;
            cameraSerialized.ApplyModifiedPropertiesWithoutUndo();

            // The world builder's streamer should follow the camera, not the origin.
            worldSerialized.FindProperty("focusOverride").objectReferenceValue = cameraObject.transform;
            worldSerialized.ApplyModifiedPropertiesWithoutUndo();

            MobileControlsBuilder.EnsureEventSystem();

            // Build the mobile touch UI directly into the scene and wire all serialized references.
            var touchObject = new GameObject("TouchControls");
            var touchHost = touchObject.AddComponent<TouchControlsHost>();
            var rig = MobileControlsBuilder.Build(touchObject.transform, router, motor, playerController);

            if (rig.JumpButton != null)
            {
                UnityEditor.Events.UnityEventTools.AddPersistentListener(
                    rig.JumpButton.onClick, playerController.OnJumpButtonClicked);
            }
            if (rig.SprintUiButton != null)
            {
                UnityEditor.Events.UnityEventTools.AddPersistentListener(
                    rig.SprintUiButton.onClick, playerController.OnSprintButtonClicked);
            }
            if (rig.CrouchUiButton != null)
            {
                UnityEditor.Events.UnityEventTools.AddPersistentListener(
                    rig.CrouchUiButton.onClick, playerController.OnCrouchButtonClicked);
            }

            var routerSerialized = new SerializedObject(router);
            routerSerialized.FindProperty("motor").objectReferenceValue = motor;
            routerSerialized.FindProperty("cameraRig").objectReferenceValue = cameraRig;
            routerSerialized.FindProperty("moveStick").objectReferenceValue = rig.MoveStick;
            routerSerialized.FindProperty("lookStick").objectReferenceValue = rig.LookStick;
            routerSerialized.ApplyModifiedPropertiesWithoutUndo();

            var playerControllerSerialized = new SerializedObject(playerController);
            playerControllerSerialized.FindProperty("motor").objectReferenceValue = motor;
            playerControllerSerialized.FindProperty("inputRouter").objectReferenceValue = router;
            playerControllerSerialized.FindProperty("cameraRig").objectReferenceValue = cameraRig;
            playerControllerSerialized.FindProperty("moveJoystick").objectReferenceValue = rig.MoveStick;
            playerControllerSerialized.FindProperty("visualCapsule").objectReferenceValue = capsuleVisual.transform;
            playerControllerSerialized.FindProperty("sprintButton").objectReferenceValue = rig.SprintUiButton;
            playerControllerSerialized.FindProperty("jumpButton").objectReferenceValue = rig.JumpButton;
            playerControllerSerialized.FindProperty("crouchButton").objectReferenceValue = rig.CrouchUiButton;
            playerControllerSerialized.ApplyModifiedPropertiesWithoutUndo();

            var touchHostSerialized = new SerializedObject(touchHost);
            touchHostSerialized.FindProperty("motor").objectReferenceValue = motor;
            touchHostSerialized.FindProperty("router").objectReferenceValue = router;
            touchHostSerialized.FindProperty("playerController").objectReferenceValue = playerController;
            touchHostSerialized.ApplyModifiedPropertiesWithoutUndo();

            var hud = new GameObject("HUD");
            var hudComponent = hud.AddComponent<WorldDebugHud>();
        }

        private static void EnsureBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>();
            for (var i = 0; i < ScenePaths.Length; i++)
            {
                if (!File.Exists(ScenePaths[i]))
                {
                    continue;
                }

                scenes.Add(new EditorBuildSettingsScene(ScenePaths[i], true));
            }

            EditorBuildSettings.scenes = scenes.ToArray();
            Line(string.Format("EditorBuildSettings: {0} scenes (first = {1})",
                scenes.Count, scenes.Count > 0 ? scenes[0].path : "none"));
        }

        private static void ConfigureAndroidPlayerSettings()
        {
            PlayerSettings.companyName = "Aura Studios";
            PlayerSettings.productName = "Battle Horizons: Last Survive";
            var requestedVersion = AndroidBuild.ResolveSetting("VERSION", "-buildVersion");
            PlayerSettings.bundleVersion = string.IsNullOrEmpty(requestedVersion) ? "0.1.0" : requestedVersion;
            PlayerSettings.SplashScreen.show = false;
            PlayerSettings.SplashScreen.showUnityLogo = false;
            PlayerSettings.SplashScreen.backgroundColor = Color.black;
            PlayerSettings.SplashScreen.overlayOpacity = 0f;
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.aurastudios.battlehorizons");
            var requestedVersionCode = AndroidBuild.ResolveSetting("ANDROID_VERSION_CODE", "-androidVersionCode");
            if (!int.TryParse(requestedVersionCode, out var versionCode) || versionCode < 1)
            {
                versionCode = 1;
            }
            PlayerSettings.Android.bundleVersionCode = versionCode;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            var requestedTargetSdk = AndroidBuild.ResolveSetting("ANDROID_TARGET_SDK_VERSION", "-androidTargetSdkVersion");
            if (int.TryParse(requestedTargetSdk, out var targetSdkInt) && targetSdkInt >= 26)
            {
                PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)targetSdkInt;
            }
            else
            {
                PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)35;
            }
            // arm64-v8a only: every 64-bit Android from the Unisoc T606 to the
            // Snapdragon 6s Gen 3 is supported, and 32-bit costs a whole second ABI.
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.forceInternetPermission = false;   // the game is offline
            PlayerSettings.Android.minifyRelease = false;
            PlayerSettings.Android.minifyDebug = false;
            PlayerSettings.Android.useCustomKeystore = false;

            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetApiCompatibilityLevel(BuildTargetGroup.Android, ApiCompatibilityLevel.NET_Standard_2_0);
            PlayerSettings.SetIl2CppCompilerConfiguration(BuildTargetGroup.Android, Il2CppCompilerConfiguration.Release);
            PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.Android, ManagedStrippingLevel.Medium);

            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.Android.androidTVCompatibility = false;
            PlayerSettings.Android.startInFullscreen = true;
            PlayerSettings.Android.renderOutsideSafeArea = true;
            PlayerSettings.gpuSkinning = true;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[]
            {
                UnityEngine.Rendering.GraphicsDeviceType.Vulkan,
                UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3,
            });
            PlayerSettings.SetMobileMTRendering(BuildTargetGroup.Android, true);

            // Android texture compression: ASTC first, ETC2 as the fallback on
            // devices whose GPU does not support ASTC.
            EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC;
            EditorUserBuildSettings.androidBuildSystem = AndroidBuildSystem.Gradle;

            QualitySettings.SetQualityLevel(QualitySettings.names.Length - 1, false);
            Line(string.Format(
                "Android: {0}, IL2CPP + ARM64, minApi 26, ASTC, landscape, no INTERNET permission",
                "com.aurastudios.battlehorizons"));
        }

        private static void EnsureBrandingAssets()
        {
            string[] brandingPaths =
            {
                UiLogoResourceAssetPath,
                HomeBgResourceAssetPath,
                LoadingBgResourceAssetPath,
                LoadingBarResourceAssetPath,
                LoadingSpinnerResourceAssetPath,
                AppIconPath,
            };

            foreach (var path in brandingPaths)
            {
                if (File.Exists(path))
                {
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                }
            }
        }

        private static void ConfigureApplicationIcons()
        {
            var appIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(AppIconPath);
            if (appIcon == null)
            {
                Line("WARNING: app icon texture missing at " + AppIconPath);
                return;
            }

            var defaultSizes = PlayerSettings.GetIconSizes(NamedBuildTarget.Unknown, IconKind.Any);
            var slotCount = defaultSizes != null && defaultSizes.Length > 0 ? defaultSizes.Length : 1;
            var defaultIcons = new Texture2D[slotCount];
            for (var i = 0; i < slotCount; i++)
            {
                defaultIcons[i] = appIcon;
            }

            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, defaultIcons, IconKind.Any);
            Line("Configured Battle Horizons default application icon (" + AppIconPath + ").");
        }

        private static void SetFloat(SerializedObject serialized, string property, float value)
        {
            var found = serialized.FindProperty(property);
            if (found != null)
            {
                found.floatValue = value;
            }
        }

        private static void SetBool(SerializedObject serialized, string property, bool value)
        {
            var found = serialized.FindProperty(property);
            if (found != null)
            {
                found.boolValue = value;
            }
        }

        private static void SetInt(SerializedObject serialized, string property, int value)
        {
            var found = serialized.FindProperty(property);
            if (found != null)
            {
                found.intValue = value;
            }
        }
    }
}
