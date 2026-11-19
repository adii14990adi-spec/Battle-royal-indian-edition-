using System;
using System.Collections.Generic;
using System.IO;
using BattleHorizons.Characters;
using BattleHorizons.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace BattleHorizons.EditorTools
{
    /// <summary>
    /// Genuine Unity Editor Camera.Render captures of BOTH production FBX
    /// characters. This is a graphics-enabled software-GL batch-mode step. It
    /// uses each generated character definition/controller and instantiates
    /// its production FBX directly under an isolated capture root, renders
    /// each character at rest and after sampling a real Humanoid Walk clip,
    /// checks non-grey textured pixels, and writes committed PNG evidence.
    /// It writes no production asset or FBX/.fbx.meta file.
    /// </summary>
    public static class FBXProductionCapture
    {
        private const int Width = 1024;
        private const int Height = 768;
        private const int ChangedPixelThreshold = 1000;
        private const int TexturedPixelThreshold = 1000;

        public static void CaptureFromCommandLine()
        {
            var report = new FBXProductionVerifier.Report(
                "Production FBX visual capture — Male and Female, Unity Editor software GL");
            GameObject environment = null;
            CharacterAnimationConfig config = null;
            var wroteScreenshots = false;
            var screenshotPaths = new List<string>();

            try
            {
                report.Add("verification boundary", "PASS",
                    "Real Unity Editor Camera.Render output for both production FBX characters under graphics-enabled batch mode; each generated definition is instantiated directly from its production FBX to avoid Resources.Load selection caching. Not an Android device render, APK test or human visual inspection.");
                if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                {
                    report.Check("graphics device available", false, "",
                        "graphics device is Null; run without -nographics under Xvfb with software GL");
                    throw new InvalidOperationException("no graphics device is available");
                }
                report.Check("graphics device available", true,
                    SystemInfo.graphicsDeviceType + " / " + SystemInfo.graphicsDeviceName, "");

                // EditMode controller fixtures now use scratch configs, so the
                // generated config and its already-imported Humanoid clips stay
                // canonical. Rebuild the generated controllers from the exact
                // production FBX/clip sources without deleting or reimporting
                // the committed asset database. No production asset is touched.
                string integrationReport;
                var integrationFailures = CharacterAnimationIntegration.Build(out integrationReport);
                report.Check("character integration regenerated before capture",
                    integrationFailures == 0,
                    "rebuilt generated controllers from the production FBX and 74 clip sources; " + integrationFailures + " failure(s)",
                    integrationFailures + " failure(s) while regenerating generated config/controllers: " + integrationReport);
                AssetDatabase.Refresh();

                config = CharacterAnimationConfigLibrary.LoadIfExists();
                if (config == null) config = CharacterAnimationConfig.Load();
                if (config == null)
                {
                    report.Check("generated character config", false, "",
                        "CharacterAnimationConfigLibrary.LoadIfExists() and CharacterAnimationConfig.Load() returned null");
                    throw new InvalidOperationException("generated character config is missing");
                }
                report.Check("generated character config", true, AssetDatabase.GetAssetPath(config), "");

                var fixtureOverrides = 0;
                if (config.assignments != null)
                {
                    for (var i = 0; i < config.assignments.Count; i++)
                    {
                        var assignment = config.assignments[i];
                        if (assignment != null
                            && !CharacterAnimationConfig.IsSharedCharacterId(assignment.characterId))
                        {
                            fixtureOverrides++;
                        }
                    }
                }
                report.Check("generated config isolated from EditMode fixtures", fixtureOverrides == 0,
                    "0 per-character overrides in the generated config; EditMode controller fixtures use scratch configs",
                    fixtureOverrides + " per-character override(s) leaked into the generated config");

                var outputDirectory = Path.Combine(FBXProductionVerifier.OutputDirectory(), "screenshots");
                Directory.CreateDirectory(outputDirectory);

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

                var captured = new HashSet<string>();
                if (config.characters != null)
                {
                    for (var i = 0; i < config.characters.Count; i++)
                    {
                        var definition = config.characters[i];
                        if (definition == null || definition.model == null) continue;
                        var sourceModelPath = AssetDatabase.GetAssetPath(definition.model);
                        var sex = SexFromModelPath(sourceModelPath);
                        if (string.IsNullOrEmpty(sex)) continue;

                        CaptureCharacter(report, config, definition, sex, camera, cameraObject,
                            outputDirectory, screenshotPaths, ref wroteScreenshots, captured);
                    }
                }

                report.Check("both production FBX characters captured",
                    captured.Contains("Male") && captured.Contains("Female"),
                    "Male and Female each produced rest and deformed genuine Editor camera renders",
                    "captured: " + string.Join(", ", new List<string>(captured).ToArray()));
                report.NotVerified("front / facial / UV-seam quality",
                    "committed camera is a rear three-quarter view; no human close-up or front-face inspection was performed");
                report.NotVerified("Android device rendering / shader variants / FPS / memory / thermal",
                    "these are Editor software-GL renders, not an APK installed on a physical Android device");
                WriteCaptureReport(report, screenshotPaths);
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
                if (environment != null) UnityEngine.Object.DestroyImmediate(environment);
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }

        private static void CaptureCharacter(
            FBXProductionVerifier.Report report,
            CharacterAnimationConfig config,
            CharacterDefinition definition,
            string sex,
            Camera camera,
            GameObject cameraObject,
            string outputDirectory,
            List<string> screenshotPaths,
            ref bool wroteScreenshots,
            HashSet<string> captured)
        {
            GameObject player = null;
            try
            {
                var sourceModelPath = definition.model != null
                    ? AssetDatabase.GetAssetPath(definition.model)
                    : string.Empty;
                var expectedPath = string.Equals(sex, "Male", StringComparison.Ordinal)
                    ? FBXProductionVerifier.MaleModelPath
                    : FBXProductionVerifier.FemaleModelPath;
                var selectedCorrectFbx = string.Equals(sourceModelPath, expectedPath,
                    StringComparison.OrdinalIgnoreCase);
                report.Check(sex + " requested production FBX selected",
                    selectedCorrectFbx,
                    "generated definition points at " + sourceModelPath,
                    "expected " + expectedPath + " but generated definition points at " + sourceModelPath);
                if (!selectedCorrectFbx || definition.model == null)
                {
                    throw new InvalidOperationException(sex + " definition does not point at its production FBX");
                }

                MeshRenderer capsuleRenderer;
                ThirdPersonMotor motor;
                var visual = CreatePlayer(out player, out capsuleRenderer, out motor);
                visual.enabled = false;
                if (motor != null) motor.enabled = false;
                if (capsuleRenderer != null) capsuleRenderer.enabled = false;

                // Direct Editor instantiation isolates Male and Female from
                // Resources.Load's cached selectedCharacterId. The model,
                // Avatar and generated controller still come from the exact
                // production FBX definition used by the runtime spawn path.
                var spawnedVisual = UnityEngine.Object.Instantiate(definition.model, player.transform);
                spawnedVisual.name = definition.displayName + " (CharacterVisual)";
                var spawnedRoot = spawnedVisual.transform;
                spawnedRoot.localPosition = Vector3.zero;
                spawnedRoot.localRotation = Quaternion.identity;

                var animator = spawnedVisual.GetComponentInChildren<Animator>(true);
                var skinned = spawnedVisual.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                var avatarOk = animator != null && animator.avatar != null
                    && animator.avatar.isValid && animator.avatar.isHuman;
                var capsuleHidden = capsuleRenderer != null && !capsuleRenderer.enabled;
                report.Check(sex + " visible player mesh is FBX, not capsule",
                    skinned.Length > 0 && avatarOk && capsuleHidden,
                    "capture instantiated " + sourceModelPath + " from the generated definition; "
                        + skinned.Length + " SkinnedMeshRenderer(s), valid Humanoid Avatar, PlayerCapsuleVisual MeshRenderer disabled",
                    "skinned=" + skinned.Length + ", avatarOk=" + avatarOk
                        + ", capsuleHidden=" + capsuleHidden + ", modelPath=" + sourceModelPath);
                if (animator == null || !avatarOk)
                {
                    throw new InvalidOperationException(sex + " production FBX visual has no valid Humanoid Animator");
                }

                if (definition.controller != null)
                {
                    animator.runtimeAnimatorController = definition.controller;
                }
                report.Check(sex + " generated Animator controller assigned",
                    definition.controller != null && animator.runtimeAnimatorController == definition.controller,
                    "generated per-character controller assigned to the spawned Animator",
                    "generated controller is missing or was not assigned");
                report.Check(sex + " spawned visual root found", true, spawnedRoot.name, "");

                var renderers = spawnedRoot.GetComponentsInChildren<Renderer>(true);
                var materialStats = CheckMaterials(spawnedRoot.gameObject, sex);
                report.Check(sex + " production material remap", materialStats.Ok,
                    materialStats.Detail, materialStats.Detail);

                var beforeBounds = CalculateBounds(renderers);
                spawnedRoot.localPosition = new Vector3(
                    spawnedRoot.localPosition.x,
                    spawnedRoot.localPosition.y - beforeBounds.min.y,
                    spawnedRoot.localPosition.z);
                var alignedBounds = CalculateBounds(renderers);
                report.Check(sex + " FBX feet aligned to temporary floor",
                    Mathf.Abs(alignedBounds.min.y) <= 0.02f && alignedBounds.size.y >= 1.35f && alignedBounds.size.y <= 2.25f,
                    "bounds before alignment minY=" + beforeBounds.min.y.ToString("F3")
                        + "; aligned minY=" + alignedBounds.min.y.ToString("F3")
                        + ", size=" + alignedBounds.size.ToString("F3"),
                    "before minY=" + beforeBounds.min.y.ToString("F3") + ", aligned minY="
                        + alignedBounds.min.y.ToString("F3") + ", size=" + alignedBounds.size.ToString("F3"));

                var height = Mathf.Max(1.5f, alignedBounds.size.y);
                cameraObject.transform.position = new Vector3(0f, height * 0.58f, -Mathf.Max(3.8f, height * 2.4f));
                cameraObject.transform.LookAt(new Vector3(0f, height * 0.52f, 0f));

                // The generated config is canonical after the isolated
                // EditMode fixtures; use its runtime Walk lookup directly.
                var walkClip = config.FindClip(definition.id, CharacterAnimCategory.Walk);
                report.Check(sex + " runtime Walk deformation clip",
                    walkClip != null && walkClip.humanMotion && walkClip.length > 0f,
                    walkClip == null ? "" : "config.FindClip(Walk)='" + walkClip.name
                        + "', humanMotion=" + walkClip.humanMotion
                        + ", length=" + walkClip.length.ToString("F3") + " s; "
                        + (config.clips == null ? 0 : config.clips.Count) + " registered production clips",
                    "FindClip(Walk)=" + (walkClip == null ? "null"
                        : "'" + walkClip.name + "', humanMotion=" + walkClip.humanMotion
                            + ", length=" + walkClip.length.ToString("F3") + " s"));
                if (walkClip == null || !walkClip.humanMotion || walkClip.length <= 0f)
                {
                    throw new InvalidOperationException(sex + " has no Humanoid walking clip in the generated config");
                }

                var lower = sex.ToLowerInvariant();
                var restPath = Path.Combine(outputDirectory, "fbx_" + lower + "_rest.png");
                var deformedPath = Path.Combine(outputDirectory, "fbx_" + lower + "_deformed.png");
                var warmupPath = Path.Combine(outputDirectory, "fbx_" + lower + "_warmup.png");

                // URP/Lit variants are compiled lazily on the first software-GL
                // render. Two discarded Camera.Render frames prevent a dark
                // shader-compilation frame from being mistaken for the rest pose.
                Color32[] warmupPixels;
                RenderCamera(camera, warmupPath, out warmupPixels);
                RenderCamera(camera, warmupPath, out warmupPixels);
                if (File.Exists(warmupPath)) File.Delete(warmupPath);
                report.Check(sex + " URP/Lit shader warm-up", true,
                    "two discarded genuine Editor Camera.Render frames primed the material variants before evidence frames", "");

                animator.runtimeAnimatorController = null;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                var animatorWasEnabled = animator.enabled;
                animator.enabled = false;

                Color32[] restPixels;
                Color32[] deformedPixels;
                var started = false;
                try
                {
                    AnimationMode.StartAnimationMode();
                    started = true;
                    SampleClip(spawnedRoot.gameObject, walkClip, 0f);
                    Physics.SyncTransforms();
                    RenderCamera(camera, restPath, out restPixels);
                    var midTime = Mathf.Max(0f, walkClip.length * 0.5f);
                    SampleClip(spawnedRoot.gameObject, walkClip, midTime);
                    Physics.SyncTransforms();
                    RenderCamera(camera, deformedPath, out deformedPixels);
                    wroteScreenshots = true;
                    screenshotPaths.Add(restPath);
                    screenshotPaths.Add(deformedPath);

                    int changed;
                    int magenta;
                    ComparePixels(restPixels, deformedPixels, out changed, out magenta);
                    report.Check(sex + " no magenta / error-material pixels",
                        magenta == 0, "0 magenta pixels in rest and sampled frames",
                        magenta + " magenta/error-material pixel(s) detected");
                    report.Check(sex + " visual animation deformation",
                        changed >= ChangedPixelThreshold,
                        "AnimationMode sampled '" + walkClip.name + "' at 0.000 s and " + midTime.ToString("F3")
                            + " s; " + changed + " pixels changed between genuine Unity camera renders",
                        "only " + changed + " pixel(s) changed between Editor samples");

                    var chromatic = Math.Max(CountChromaticPixels(restPixels), CountChromaticPixels(deformedPixels));
                    report.Check(sex + " textured camera capture",
                        chromatic >= TexturedPixelThreshold,
                        chromatic + " chromatic pixels prove the production diffuse/normal material remap is visible, not a flat grey silhouette",
                        "only " + chromatic + " chromatic pixel(s); production character still appears untextured");
                }
                finally
                {
                    if (started) AnimationMode.StopAnimationMode();
                    animator.enabled = animatorWasEnabled;
                }

                if (sex == "Male")
                {
                    File.Copy(restPath, Path.Combine(outputDirectory, "fbx_player_rest.png"), true);
                    File.Copy(deformedPath, Path.Combine(outputDirectory, "fbx_player_deformed.png"), true);
                    screenshotPaths.Add(Path.Combine(outputDirectory, "fbx_player_rest.png"));
                    screenshotPaths.Add(Path.Combine(outputDirectory, "fbx_player_deformed.png"));
                }

                captured.Add(sex);
            }
            finally
            {
                if (player != null) UnityEngine.Object.DestroyImmediate(player);
            }
        }

        private static string SexFromModelPath(string path)
        {
            if (string.Equals(path, FBXProductionVerifier.MaleModelPath, StringComparison.OrdinalIgnoreCase)) return "Male";
            if (string.Equals(path, FBXProductionVerifier.FemaleModelPath, StringComparison.OrdinalIgnoreCase)) return "Female";
            return string.Empty;
        }

        private static PlayerCharacterVisual CreatePlayer(out GameObject player,
            out MeshRenderer capsuleRenderer, out ThirdPersonMotor motor)
        {
            player = new GameObject("FBXProductionPlayer");
            player.AddComponent<CharacterController>();
            motor = player.AddComponent<ThirdPersonMotor>();

            var capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            capsule.name = "PlayerCapsuleVisual";
            capsule.transform.SetParent(player.transform, false);
            capsule.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            capsule.transform.localScale = new Vector3(0.7f, 0.9f, 0.7f);
            var capsuleCollider = capsule.GetComponent<Collider>();
            if (capsuleCollider != null) UnityEngine.Object.DestroyImmediate(capsuleCollider);
            capsuleRenderer = capsule.GetComponent<MeshRenderer>();
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
            return visual;
        }

        private struct MaterialStats
        {
            public bool Ok;
            public string Detail;
        }

        private static MaterialStats CheckMaterials(GameObject root, string sex)
        {
            var prefix = "Assets/Characters/" + sex + "/Materials/";
            var slots = 0;
            var external = 0;
            var textureSlots = 0;
            var invalidTextures = 0;
            var nullMaterials = 0;
            var errorShaders = 0;
            var nonUrp = 0;
            var shaders = new HashSet<string>();
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                var shared = renderer.sharedMaterials;
                if (shared == null || shared.Length == 0)
                {
                    nullMaterials++;
                    continue;
                }
                for (var i = 0; i < shared.Length; i++)
                {
                    var material = shared[i];
                    if (material == null)
                    {
                        nullMaterials++;
                        continue;
                    }
                    slots++;
                    if (AssetDatabase.GetAssetPath(material).StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) external++;
                    var shader = material.shader;
                    if (shader == null || shader.name.IndexOf("Error", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        errorShaders++;
                        continue;
                    }
                    shaders.Add(shader.name);
                    if (shader.name != "Universal Render Pipeline/Lit") nonUrp++;
                    for (var p = 0; p < shader.GetPropertyCount(); p++)
                    {
                        if (shader.GetPropertyType(p) != ShaderPropertyType.Texture) continue;
                        var texture = material.GetTexture(shader.GetPropertyName(p));
                        if (texture == null) continue;
                        textureSlots++;
                        if (texture.width <= 0 || texture.height <= 0) invalidTextures++;
                    }
                }
            }
            var detail = slots + " material slots (" + external + " external URP/Lit), " + textureSlots
                + " bound texture slots, " + nullMaterials + " null, " + errorShaders + " error shader(s), "
                + nonUrp + " non-URP/Lit shader(s), " + invalidTextures + " invalid texture(s), shaders: "
                + string.Join(", ", new List<string>(shaders).ToArray());
            return new MaterialStats
            {
                Ok = slots >= 3 && external == slots && textureSlots >= 6 && nullMaterials == 0
                    && errorShaders == 0 && nonUrp == 0 && invalidTextures == 0,
                Detail = detail,
            };
        }

        private static int CountChromaticPixels(Color32[] pixels)
        {
            var count = 0;
            for (var i = 0; i < pixels.Length; i++)
            {
                var color = pixels[i];
                var max = Math.Max(color.r, Math.Max(color.g, color.b));
                var min = Math.Min(color.r, Math.Min(color.g, color.b));
                if (max - min > 35 && max > 60) count++;
            }
            return count;
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

        private static void WriteCaptureReport(FBXProductionVerifier.Report report, List<string> paths)
        {
            var shortPaths = new List<string>();
            for (var i = 0; i < paths.Count; i++) shortPaths.Add(ShortPath(paths[i]));
            report.Info("screenshot paths", string.Join("; ", shortPaths.ToArray()));
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
