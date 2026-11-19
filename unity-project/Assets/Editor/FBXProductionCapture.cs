using System;
using System.IO;
using System.Text;
using BattleHorizons.Characters;
using BattleHorizons.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace BattleHorizons.EditorTools
{
    /// <summary>
    /// Genuine Unity Editor camera renders of the production FBX player visual.
    /// This is a graphics-enabled, software-GL batch-mode step. It creates a
    /// temporary scene, uses the real PlayerCharacterVisual.TrySpawn path, hides
    /// the capsule, renders the FBX character at rest and after sampling a real
    /// Humanoid clip, and compares the two images. It writes no production asset.
    /// </summary>
    public static class FBXProductionCapture
    {
        private const int Width = 1024;
        private const int Height = 768;
        private const int ChangedPixelThreshold = 1000;

        public static void CaptureFromCommandLine()
        {
            var report = new FBXProductionVerifier.Report(
                "Production FBX visual capture — Unity Editor software GL");
            GameObject environment = null;
            GameObject player = null;
            var wroteScreenshots = false;

            try
            {
                report.Add("verification boundary", "PASS",
                    "Real Unity Editor Camera.Render output under graphics-enabled batch mode; not an Android device render, APK test or human visual inspection.");
                if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                {
                    report.Check("graphics device available", false, "",
                        "graphics device is Null; run without -nographics under Xvfb with software GL");
                    throw new InvalidOperationException("no graphics device is available");
                }
                report.Check("graphics device available", true,
                    SystemInfo.graphicsDeviceType + " / " + SystemInfo.graphicsDeviceName, "");

                var config = CharacterAnimationConfig.Load();
                if (config == null)
                {
                    report.Check("generated character config", false, "",
                        "CharacterAnimationConfig.Load() returned null; run CharacterAnimationIntegration.BuildFromCommandLine first");
                    throw new InvalidOperationException("generated character config is missing");
                }
                report.Check("generated character config", true,
                    AssetDatabase.GetAssetPath(config), "");

                var outputDirectory = Path.Combine(FBXProductionVerifier.OutputDirectory(), "screenshots");
                Directory.CreateDirectory(outputDirectory);
                var restPath = Path.Combine(outputDirectory, "fbx_player_rest.png");
                var deformedPath = Path.Combine(outputDirectory, "fbx_player_deformed.png");

                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                environment = new GameObject("FBX_Production_Capture_Environment");
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.42f, 0.45f, 0.50f, 1f);

                var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                floor.name = "Floor";
                floor.transform.SetParent(environment.transform, false);
                floor.transform.localPosition = new Vector3(0f, -0.06f, 0f);
                floor.transform.localScale = new Vector3(8f, 0.12f, 8f);
                var floorRenderer = floor.GetComponent<Renderer>();
                if (floorRenderer != null)
                {
                    var block = new MaterialPropertyBlock();
                    block.SetColor("_BaseColor", new Color(0.30f, 0.32f, 0.35f, 1f));
                    block.SetColor("_Color", new Color(0.30f, 0.32f, 0.35f, 1f));
                    floorRenderer.SetPropertyBlock(block);
                }

                var sunObject = new GameObject("DirectionalLight");
                sunObject.transform.SetParent(environment.transform, false);
                sunObject.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
                var sun = sunObject.AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.intensity = 1.25f;
                sun.color = new Color(1f, 0.97f, 0.92f, 1f);

                var fillObject = new GameObject("FillLight");
                fillObject.transform.SetParent(environment.transform, false);
                fillObject.transform.position = new Vector3(0f, 2.8f, -2.4f);
                var fill = fillObject.AddComponent<Light>();
                fill.type = LightType.Point;
                fill.range = 9f;
                fill.intensity = 0.65f;
                fill.color = new Color(0.78f, 0.86f, 1f, 1f);

                var cameraObject = new GameObject("FBXProductionCamera");
                cameraObject.transform.SetParent(environment.transform, false);
                var camera = cameraObject.AddComponent<Camera>();
                camera.enabled = true;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.11f, 0.13f, 0.16f, 1f);
                camera.fieldOfView = 38f;
                camera.nearClipPlane = 0.05f;
                camera.farClipPlane = 80f;

                player = new GameObject("FBXProductionPlayer");
                player.AddComponent<CharacterController>();
                var motor = player.AddComponent<ThirdPersonMotor>();

                var capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                capsule.name = "PlayerCapsuleVisual";
                capsule.transform.SetParent(player.transform, false);
                capsule.transform.localPosition = new Vector3(0f, 0.9f, 0f);
                capsule.transform.localScale = new Vector3(0.7f, 0.9f, 0.7f);
                var capsuleCollider = capsule.GetComponent<Collider>();
                if (capsuleCollider != null) UnityEngine.Object.DestroyImmediate(capsuleCollider);
                var capsuleRenderer = capsule.GetComponent<MeshRenderer>();
                if (capsuleRenderer != null)
                {
                    var block = new MaterialPropertyBlock();
                    block.SetColor("_BaseColor", new Color(0.9f, 0.2f, 0.2f, 1f));
                    block.SetColor("_Color", new Color(0.9f, 0.2f, 0.2f, 1f));
                    capsuleRenderer.SetPropertyBlock(block);
                }

                var visual = player.AddComponent<PlayerCharacterVisual>();
                var serialized = new SerializedObject(visual);
                var motorProperty = serialized.FindProperty("motor");
                if (motorProperty != null) motorProperty.objectReferenceValue = motor;
                var capsuleProperty = serialized.FindProperty("visualCapsule");
                if (capsuleProperty != null) capsuleProperty.objectReferenceValue = capsule.transform;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                var spawned = visual.TrySpawn();
                var definition = visual.ActiveDefinition;
                var modelPath = definition != null && definition.model != null
                    ? AssetDatabase.GetAssetPath(definition.model) : string.Empty;
                var animator = visual.CharacterAnimator;
                var skinned = player.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                var capsuleHidden = capsuleRenderer != null && !capsuleRenderer.enabled;
                var productionFbx = string.Equals(modelPath, FBXProductionVerifier.MaleModelPath, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(modelPath, FBXProductionVerifier.FemaleModelPath, StringComparison.OrdinalIgnoreCase);
                var avatarOk = animator != null && animator.avatar != null && animator.avatar.isValid && animator.avatar.isHuman;

                report.Check("visible player mesh is FBX, not capsule",
                    spawned && productionFbx && skinned.Length > 0 && avatarOk && capsuleHidden,
                    "PlayerCharacterVisual.TrySpawn instantiated " + modelPath + "; " + skinned.Length
                        + " SkinnedMeshRenderer(s), valid Humanoid Avatar, PlayerCapsuleVisual MeshRenderer disabled",
                    "spawned=" + spawned + ", modelPath=" + modelPath + ", skinned=" + skinned.Length
                        + ", avatarOk=" + avatarOk + ", capsuleHidden=" + capsuleHidden);
                if (!spawned || definition == null || animator == null)
                {
                    throw new InvalidOperationException("the production FBX player visual could not be spawned");
                }

                var spawnedName = definition.displayName + " (CharacterVisual)";
                var spawnedRoot = FindChildByName(player.transform, spawnedName);
                if (spawnedRoot == null)
                {
                    report.Check("spawned visual root found", false, "", "could not find " + spawnedName);
                    throw new InvalidOperationException("spawned visual root was not found");
                }
                report.Check("spawned visual root found", true, spawnedRoot.name, "");

                var renderers = spawnedRoot.GetComponentsInChildren<Renderer>(true);
                var beforeBounds = CalculateBounds(renderers);
                spawnedRoot.localPosition = new Vector3(
                    spawnedRoot.localPosition.x,
                    spawnedRoot.localPosition.y - beforeBounds.min.y,
                    spawnedRoot.localPosition.z);
                var alignedBounds = CalculateBounds(renderers);
                report.Check("FBX feet aligned to temporary floor",
                    Mathf.Abs(alignedBounds.min.y) <= 0.02f && alignedBounds.size.y >= 1.35f && alignedBounds.size.y <= 2.25f,
                    "bounds before alignment minY=" + beforeBounds.min.y.ToString("F3")
                        + "; aligned minY=" + alignedBounds.min.y.ToString("F3")
                        + ", size=" + alignedBounds.size.ToString("F3"),
                    "before minY=" + beforeBounds.min.y.ToString("F3") + ", aligned minY="
                        + alignedBounds.min.y.ToString("F3") + ", size=" + alignedBounds.size.ToString("F3"));

                var height = Mathf.Max(1.5f, alignedBounds.size.y);
                cameraObject.transform.position = new Vector3(0f, height * 0.58f, -Mathf.Max(3.8f, height * 2.4f));
                cameraObject.transform.LookAt(new Vector3(0f, height * 0.52f, 0f));

                var walkClip = config.FindClip(definition.id, CharacterAnimCategory.Walk);
                if (walkClip == null || !walkClip.humanMotion)
                {
                    report.NotVerified("visual animation deformation",
                        "no Humanoid Walk clip was available in the generated config; only the rest-pose screenshot was rendered");
                    Color32[] restOnly;
                    RenderCamera(camera, restPath, out restOnly);
                    wroteScreenshots = true;
                    WriteCaptureReport(report, restPath, null);
                    Finish(report);
                    return;
                }

                animator.runtimeAnimatorController = null;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.enabled = true;

                Color32[] restPixels;
                Color32[] deformedPixels;
                var started = false;
                try
                {
                    AnimationMode.StartAnimationMode();
                    started = true;
                    SampleClip(spawnedRoot.gameObject, walkClip, 0f);
                    RenderCamera(camera, restPath, out restPixels);
                    var midTime = Mathf.Max(0f, walkClip.length * 0.5f);
                    SampleClip(spawnedRoot.gameObject, walkClip, midTime);
                    RenderCamera(camera, deformedPath, out deformedPixels);
                    wroteScreenshots = true;

                    int changed;
                    int magenta;
                    ComparePixels(restPixels, deformedPixels, out changed, out magenta);
                    report.Check("no magenta / error-material pixels",
                        magenta == 0, "0 magenta pixels in rest and sampled frames",
                        magenta + " magenta/error-material pixel(s) detected");
                    if (changed >= ChangedPixelThreshold)
                    {
                        report.Add("visual animation deformation", "PASS",
                            "AnimationMode sampled '" + walkClip.name + "' at 0.000 s and " + midTime.ToString("F3")
                                + " s; " + changed + " pixels changed between genuine Unity camera renders");
                    }
                    else
                    {
                        report.NotVerified("visual animation deformation",
                            "only " + changed + " pixel(s) changed between Editor samples; AnimationMode may not expose this Humanoid pose visually, so manual/PlayMode review is required");
                    }
                }
                finally
                {
                    if (started) AnimationMode.StopAnimationMode();
                }

                report.NotVerified("front / facial / UV-seam quality",
                    "committed camera is a rear three-quarter view; no human close-up or front-face inspection was performed");
                report.NotVerified("Android device rendering / shader variants / FPS / memory / thermal",
                    "these are Editor software-GL renders, not an APK installed on a physical Android device");
                WriteCaptureReport(report, restPath, deformedPath);
                Finish(report);
            }
            catch (Exception exception)
            {
                report.Check("FBX visual capture completed", false, "", exception.Message);
                if (!wroteScreenshots)
                {
                    report.NotVerified("screenshot files", "capture failed before writing PNG files");
                }
                FBXProductionVerifier.WriteReportFiles(report, "CAPTURE_RESULT");
                Debug.LogError("[bh][fbx-production] visual capture failed: " + exception);
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
            finally
            {
                if (player != null) UnityEngine.Object.DestroyImmediate(player);
                if (environment != null) UnityEngine.Object.DestroyImmediate(environment);
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }

        private static void Finish(FBXProductionVerifier.Report report)
        {
            FBXProductionVerifier.WriteReportFiles(report, "CAPTURE_RESULT");
            Debug.Log("[bh][fbx-production]\n" + report.Markdown());
            if (report.FailureCount > 0)
            {
                Debug.LogError("[bh][fbx-production] visual capture reported " + report.FailureCount + " FAIL row(s)");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        private static void WriteCaptureReport(
            FBXProductionVerifier.Report report,
            string restPath,
            string deformedPath)
        {
            report.Info("screenshot paths",
                "rest=" + ShortPath(restPath) + (deformedPath == null ? "; deformed pose not captured" : "; deformed=" + ShortPath(deformedPath)));
            report.Info("graphics device", SystemInfo.graphicsDeviceType + " / " + SystemInfo.graphicsDeviceName);
        }

        private static void SampleClip(GameObject root, AnimationClip clip, float time)
        {
            AnimationMode.BeginSampling();
            try
            {
                AnimationMode.SampleAnimationClip(root, clip, time);
            }
            finally
            {
                AnimationMode.EndSampling();
            }
        }

        private static Transform FindChildByName(Transform root, string name)
        {
            var transforms = root.GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < transforms.Length; i++)
            {
                if (string.Equals(transforms[i].name, name, StringComparison.Ordinal)) return transforms[i];
            }
            return null;
        }

        private static Bounds CalculateBounds(Renderer[] renderers)
        {
            var bounds = new Bounds(Vector3.zero, Vector3.zero);
            var has = false;
            for (var i = 0; i < renderers.Length; i++)
            {
                if (!has)
                {
                    bounds = renderers[i].bounds;
                    has = true;
                }
                else
                {
                    bounds.Encapsulate(renderers[i].bounds);
                }
            }
            return bounds;
        }

        private static void RenderCamera(Camera camera, string path, out Color32[] pixels)
        {
            pixels = new Color32[0];
            var oldTarget = camera.targetTexture;
            var oldActive = RenderTexture.active;
            var texture = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            var output = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            try
            {
                texture.Create();
                camera.targetTexture = texture;
                camera.Render();
                RenderTexture.active = texture;
                output.ReadPixels(new Rect(0f, 0f, Width, Height), 0, 0);
                output.Apply();
                pixels = output.GetPixels32();
                File.WriteAllBytes(path, output.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = oldTarget;
                RenderTexture.active = oldActive;
                UnityEngine.Object.DestroyImmediate(output);
                texture.Release();
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static void ComparePixels(Color32[] first, Color32[] second, out int changed, out int magenta)
        {
            changed = 0;
            magenta = 0;
            var count = Math.Min(first.Length, second.Length);
            for (var i = 0; i < count; i++)
            {
                var a = first[i];
                var b = second[i];
                var delta = Math.Abs(a.r - b.r) + Math.Abs(a.g - b.g) + Math.Abs(a.b - b.b);
                if (delta > 24) changed++;
                if (IsMagenta(a) || IsMagenta(b)) magenta++;
            }
        }

        private static bool IsMagenta(Color32 color)
        {
            return color.r > 180 && color.b > 180 && color.g < 110 && Math.Abs(color.r - color.b) < 45;
        }

        private static string ShortPath(string path)
        {
            var normalized = path.Replace('\\', '/');
            var marker = "/docs/";
            var index = normalized.LastIndexOf(marker, StringComparison.OrdinalIgnoreCase);
            return index >= 0 ? normalized.Substring(index + 1) : normalized;
        }
    }
}
