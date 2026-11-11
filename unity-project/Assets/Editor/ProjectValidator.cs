using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BattleHorizons.Core;
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
    /// Everything the project claims, checked against the project itself.
    ///
    /// Each check prints PASS / FAIL / SKIP with the reason, and the method
    /// returns the number of failures so the CI job can exit non-zero. The
    /// distinction matters: "URP is configured" is *checked*, "the game runs at
    /// 30 fps on a Snapdragon 6s" is *not checkable here* and is reported as a
    /// device gate, never as a pass.
    /// </summary>
    public static class ProjectValidator
    {
        private sealed class Result
        {
            public string Name;
            public string Status;   // PASS / FAIL / SKIP
            public string Detail;
        }

        private static readonly List<Result> Results = new List<Result>();

        public static int ValidateAll(out string report)
        {
            Results.Clear();

            CheckProjectStructure();
            CheckRenderPipeline();
            CheckPackages();
            CheckAndroidConfiguration();
            CheckQualityProfiles();
            CheckIslandData();
            CheckIslandGeometry();
            CheckScenes();
            CheckNoMissingScripts();
            CheckMaterials();
            CheckBlenderPipeline();
            DeviceGates();

            var failures = Results.Count(r => r.Status == "FAIL");
            var skipped = Results.Count(r => r.Status == "SKIP");
            var passed = Results.Count(r => r.Status == "PASS");

            var builder = new StringBuilder();
            builder.AppendLine("=========================================================");
            builder.AppendLine(" Battle Horizons - Unity project validation");
            builder.AppendLine("=========================================================");
            foreach (var result in Results)
            {
                builder.AppendFormat("  {0,-4} {1}{2}", result.Status, result.Name,
                    string.IsNullOrEmpty(result.Detail) ? string.Empty : "  -- " + result.Detail).AppendLine();
            }

            builder.AppendLine("---------------------------------------------------------");
            builder.AppendFormat("  {0} passed, {1} failed, {2} skipped/not-testable-here", passed, failures, skipped)
                .AppendLine();
            if (failures > 0)
            {
                builder.AppendLine("  FAILURES:");
                foreach (var result in Results.Where(r => r.Status == "FAIL"))
                {
                    builder.AppendLine("   * " + result.Name + ": " + result.Detail);
                }
            }

            builder.AppendLine("=========================================================");
            report = builder.ToString();
            return failures;
        }

        // --- individual checks -------------------------------------------------

        private static void Pass(string name, string detail = "") => Add(name, "PASS", detail);
        private static void Fail(string name, string detail) => Add(name, "FAIL", detail);
        private static void Skip(string name, string detail) => Add(name, "SKIP", detail);

        private static void Add(string name, string status, string detail) =>
            Results.Add(new Result { Name = name, Status = status, Detail = detail });

        private static void CheckProjectStructure()
        {
            var required = new[]
            {
                "Assets/Scripts/Core", "Assets/Scripts/Player", "Assets/Scripts/Camera",
                "Assets/Scripts/World", "Assets/Scripts/UI", "Assets/Scenes", "Assets/Materials",
                "Assets/Settings", "Assets/Resources/Config",
                "Packages/manifest.json", "ProjectSettings/ProjectVersion.txt",
            };

            var missing = required.Where(path => !File.Exists(path) && !Directory.Exists(path)).ToArray();
            if (missing.Length == 0)
            {
                Pass("project structure", required.Length + " required paths present");
            }
            else
            {
                Fail("project structure", "missing: " + string.Join(", ", missing));
            }

            var version = File.Exists("ProjectSettings/ProjectVersion.txt")
                ? File.ReadAllText("ProjectSettings/ProjectVersion.txt")
                : string.Empty;
            if (version.Contains("6000."))
            {
                Pass("Unity 6 project version", version.Trim().Replace("\n", " "));
            }
            else
            {
                Fail("Unity 6 project version", "ProjectVersion.txt does not say 6000.x: " + version.Trim());
            }
        }

        private static void CheckRenderPipeline()
        {
            var pipeline = GraphicsSettings.defaultRenderPipeline;
            if (pipeline == null)
            {
                Fail("URP configured", "GraphicsSettings.defaultRenderPipeline is empty (still on Built-in)");
                return;
            }

            if (!(pipeline is UniversalRenderPipelineAsset urp))
            {
                Fail("URP configured", "render pipeline is " + pipeline.GetType().Name + ", expected UniversalRenderPipelineAsset (HDRP/Built-in not allowed)");
                return;
            }

            Pass("URP configured", AssetDatabase.GetAssetPath(urp));
            var qualityPipeline = QualitySettings.renderPipeline;
            if (qualityPipeline == null || qualityPipeline == urp)
            {
                Pass("URP assigned to QualitySettings", qualityPipeline == null ? "inherits from default" : "same asset");
            }
            else
            {
                Fail("URP assigned to QualitySettings", "QualitySettings.renderPipeline is a different asset");
            }

            var serialized = new SerializedObject(urp);
            var hdr = serialized.FindProperty("m_SupportsHDR");
            if (hdr != null && hdr.boolValue)
            {
                Fail("URP HDR disabled", "HDR is on: a full HDR target costs memory and a tone-map pass on mobile");
            }
            else
            {
                Pass("URP HDR disabled");
            }

            var msaa = serialized.FindProperty("m_MSAA");
            var msaaValue = msaa != null ? msaa.intValue : 1;
            if (msaaValue > 2)
            {
                Fail("URP MSAA budget", "MSAA is " + msaaValue + "x; docs/UNISOC_BUDGET.md allows up to 2x");
            }
            else
            {
                Pass("URP MSAA budget", msaaValue + "x");
            }

            var extraLights = serialized.FindProperty("m_AdditionalLightsPerObjectLimit");
            if (extraLights != null && extraLights.intValue > 2)
            {
                Fail("URP additional light limit", extraLights.intValue + " > 2 (budget: max 2 lights per object)");
            }
            else
            {
                Pass("URP additional light limit", extraLights != null ? extraLights.intValue.ToString() : "default");
            }

            if (GraphicsSettings.GetSettingsForRenderPipeline<UniversalRenderPipeline>() == null)
            {
                Fail("URP global settings", "no URP global settings asset registered");
            }
            else
            {
                Pass("URP global settings");
            }
        }

        private static void CheckPackages()
        {
            var manifestPath = "Packages/manifest.json";
            if (!File.Exists(manifestPath))
            {
                Fail("package manifest", manifestPath + " is missing");
                return;
            }

            var manifest = File.ReadAllText(manifestPath);
            var requiredPackages = new Dictionary<string, string>
            {
                ["com.unity.render-pipelines.universal"] = "URP",
                ["com.unity.inputsystem"] = "Input System",
                ["com.unity.test-framework"] = "Test Framework",
                ["com.unity.ugui"] = "uGUI",
            };

            foreach (var pair in requiredPackages)
            {
                if (manifest.Contains(pair.Key))
                {
                    Pass("package " + pair.Value, pair.Key);
                }
                else
                {
                    Fail("package " + pair.Value, pair.Key + " missing from Packages/manifest.json");
                }
            }

            if (manifest.Contains("com.unity.render-pipelines.high-definition"))
            {
                Fail("no HDRP", "HDRP is referenced in the manifest; this project is Android-first URP only");
            }
            else
            {
                Pass("no HDRP");
            }
        }

        private static void CheckAndroidConfiguration()
        {
            Check("Android build target supported", BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android),
                BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android)
                    ? "Android module installed"
                    : "Android build support is NOT installed in this editor (builds will fail; validation still ran)");

            var identifier = PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Android);
            Check("Android application id", identifier == "com.aurastudios.battlehorizons", identifier);

            var architectures = PlayerSettings.Android.targetArchitectures;
            Check("Android ARM64 only", architectures == AndroidArchitecture.ARM64, architectures.ToString());

            Check("Android min SDK", PlayerSettings.Android.minSdkVersion >= AndroidSdkVersions.AndroidApiLevel26,
                PlayerSettings.Android.minSdkVersion.ToString());

            Check("Android IL2CPP backend",
                PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android) == ScriptingImplementation.IL2CPP,
                PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android).ToString());

            Check("Android landscape orientation",
                PlayerSettings.defaultInterfaceOrientation == UIOrientation.LandscapeLeft &&
                !PlayerSettings.allowedAutorotateToPortrait &&
                !PlayerSettings.allowedAutorotateToPortraitUpsideDown,
                PlayerSettings.defaultInterfaceOrientation.ToString());

            Check("no INTERNET permission",
                !PlayerSettings.Android.forceInternetPermission,
                PlayerSettings.Android.forceInternetPermission
                    ? "forceInternetPermission is ON - the game is offline by design"
                    : "offline");

            Check("Linear colour space", PlayerSettings.colorSpace == ColorSpace.Linear,
                PlayerSettings.colorSpace.ToString());
        }

        private static void CheckQualityProfiles()
        {
            var names = QualitySettings.names;
            Check("two quality profiles",
                names.Length >= 2 && names[0] == "BattleHorizons_Low" && names[1] == "BattleHorizons_High",
                string.Join(" | ", names));
            Check("fixed 50 m circular stream radius",
                Mathf.Approximately(QualityProfiles.StreamRadius(QualityProfile.Low), 50f) &&
                Mathf.Approximately(QualityProfiles.StreamRadius(QualityProfile.High), 50f),
                "the visual radius must not vary by quality profile");

            // The detection rules are pure, so they can be asserted.
            var t606 = QualityProfiles.Detect(4000, 8, 0);
            var snap6s = QualityProfiles.Detect(7000, 8, 0);
            Check("profile detect: Unisoc T606 -> Low", t606 == QualityProfile.Low, t606.ToString());
            Check("profile detect: Snapdragon 6s (8 GB) -> High", snap6s == QualityProfile.High, snap6s.ToString());
        }

        private static void CheckIslandData()
        {
            if (!SiranIslandData.TryLoad(out var data, out var error))
            {
                Fail("island data loads", error);
                return;
            }

            Pass("island data loads", string.Format("{0}x{0} grid, {1} road ribbons", data.Size, data.RoadRibbons.Count));

            Check("island grid resolution", data.Size == 401, data.Size + " samples/axis (expected 401)");
            Check("island spacing", Mathf.Approximately(data.SpacingM, 10f), data.SpacingM + " m");
            Check("island extent", Mathf.Approximately(data.HalfExtentM, 2000f), data.HalfExtentM + " m half extent");
            Check("island chunk grid", data.ChunksPerSide == 8, data.ChunksPerSide + " chunks/side");
            Check("island spawn ground height",
                Mathf.Abs(data.SpawnPoint().y - (data.SpawnGroundHeightM + 1.2f)) < 0.5f,
                string.Format("spawn y {0:F2}, baked ground {1:F2}", data.SpawnPoint().y, data.SpawnGroundHeightM));

            // Every zone pad must be on land: the failure mode this catches is a
            // zone that ends up underwater after an edit.
            var flooded = new List<string>();
            foreach (var zone in SiranMapConfig.Zones)
            {
                var height = data.SampleHeight(zone.Center.x, zone.Center.y);
                var tolerance = zone.Kind == ZoneKind.Dock ? 0.5f : 1.5f;
                if (height < SiranMapConfig.SeaLevel + tolerance)
                {
                    flooded.Add(string.Format("{0} ({1:F1} m)", zone.Id, height));
                }
            }

            Check("all zone pads are on land", flooded.Count == 0,
                flooded.Count == 0 ? "5 zones above sea level" : string.Join(", ", flooded));

            // The runway is the one pad that claims perfect flatness.
            var airfield = SiranMapConfig.ZoneById("aircraft_industry");
            if (airfield != null)
            {
                var worst = 0f;
                for (var i = 0; i < 40; i++)
                {
                    var t = i / 39f;
                    var x = Mathf.Lerp(airfield.Center.x - airfield.Size.x * 0.5f,
                        airfield.Center.x + airfield.Size.x * 0.5f, t);
                    for (var j = 0; j < 40; j++)
                    {
                        var s = j / 39f;
                        var z = Mathf.Lerp(airfield.Center.y - airfield.Size.y * 0.5f,
                            airfield.Center.y + airfield.Size.y * 0.5f, s);
                        if (IslandShapeMath.SignedDistance(airfield, x, z) > -10f)
                        {
                            continue;
                        }

                        worst = Mathf.Max(worst, Mathf.Abs(data.SampleHeight(x, z) - airfield.FlatHeight));
                    }
                }

                Check("runway is flat", worst < 0.2f, string.Format("worst deviation {0:F3} m", worst));
            }
        }

        private static void CheckIslandGeometry()
        {
            if (!SiranIslandData.TryLoad(out var data, out var geometryError))
            {
                Fail("chunk geometry", geometryError);
                return;
            }

            var vertices = new List<Vector3>(2601);
            var normals = new List<Vector3>(2601);
            var colors = new List<Color32>(2601);
            var indices = new List<int>(15000);

            var totalTriangles = 0;
            var woundDown = 0;
            var spanErrors = 0;
            var heightErrors = 0;

            for (var cz = 0; cz < data.ChunksPerSide; cz++)
            {
                for (var cx = 0; cx < data.ChunksPerSide; cx++)
                {
                    data.BuildChunkMesh(cx, cz, vertices, normals, colors, indices);
                    totalTriangles += indices.Count / 3;

                    var minX = float.MaxValue;
                    var maxX = float.MinValue;
                    var minZ = float.MaxValue;
                    var maxZ = float.MinValue;
                    for (var i = 0; i < vertices.Count; i++)
                    {
                        minX = Mathf.Min(minX, vertices[i].x);
                        maxX = Mathf.Max(maxX, vertices[i].x);
                        minZ = Mathf.Min(minZ, vertices[i].z);
                        maxZ = Mathf.Max(maxZ, vertices[i].z);
                    }

                    if (Mathf.Abs(maxX - minX - 500f) > 0.01f || Mathf.Abs(maxZ - minZ - 500f) > 0.01f)
                    {
                        spanErrors++;
                    }

                    // A triangle wound (a, b, c) with cross(b-a, c-a).y < 0 faces up.
                    for (var t = 0; t < indices.Count; t += 3)
                    {
                        var a = vertices[indices[t]];
                        var b = vertices[indices[t + 1]];
                        var c = vertices[indices[t + 2]];
                        if (Vector3.Cross(b - a, c - a).y > 0f)
                        {
                            woundDown++;
                        }
                    }

                    // Spot-check the mesh against the height grid.
                    for (var i = 0; i < vertices.Count; i += 97)
                    {
                        var worldX = vertices[i].x + SiranMapConfig.ChunkCenter(cx, cz).x;
                        var worldZ = vertices[i].z + SiranMapConfig.ChunkCenter(cx, cz).y;
                        if (Mathf.Abs(data.SampleHeight(worldX, worldZ) - vertices[i].y) > 0.05f)
                        {
                            heightErrors++;
                        }
                    }
                }
            }

            Check("64 chunk meshes build", totalTriangles == 64 * 5000,
                totalTriangles + " triangles (expected 320000)");
            Check("every chunk spans 500 m", spanErrors == 0, spanErrors + " chunks with a wrong span");
            Check("mesh winding faces up", woundDown == 0,
                woundDown + " triangles wound the wrong way");
            Check("mesh heights match the height grid", heightErrors == 0,
                heightErrors + " sample mismatches");
        }

        private static void CheckScenes()
        {
            foreach (var path in ProjectBootstrap.ScenePaths)
            {
                if (!File.Exists(path))
                {
                    Fail("scene " + Path.GetFileNameWithoutExtension(path), path + " is missing");
                    continue;
                }

                var alreadyLoaded = SceneManager.GetSceneByPath(path).isLoaded;
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                var missingScripts = CountMissingScripts(scene);
                if (missingScripts > 0)
                {
                    Fail("scene " + Path.GetFileNameWithoutExtension(path),
                        missingScripts + " missing script reference(s)");
                }
                else
                {
                    Pass("scene " + Path.GetFileNameWithoutExtension(path) + " opens",
                        scene.rootCount + " root objects, no missing scripts");
                }

                if (!alreadyLoaded && SceneManager.sceneCount > 1)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }

            var buildScenes = EditorBuildSettings.scenes;
            Check("build settings list the scenes", buildScenes.Length >= 4,
                buildScenes.Length + " scene(s) in EditorBuildSettings");
            if (buildScenes.Length > 0)
            {
                Check("boot scene is first", buildScenes[0].path == ProjectBootstrap.ScenePaths[0],
                    buildScenes[0].path);
            }
        }

        private static void CheckNoMissingScripts()
        {
            var broken = new List<string>();
            var known = new HashSet<string>(ProjectBootstrap.ScenePaths);
            foreach (var path in Directory.GetFiles("Assets/Scenes", "*.unity", SearchOption.AllDirectories))
            {
                var normalised = path.Replace('\\', '/');
                if (known.Contains(normalised))
                {
                    continue; // already validated in CheckScenes
                }

                var alreadyLoaded = SceneManager.GetSceneByPath(normalised).isLoaded;
                var scene = EditorSceneManager.OpenScene(normalised, OpenSceneMode.Additive);
                var missing = CountMissingScripts(scene);
                if (missing > 0)
                {
                    broken.Add(Path.GetFileName(normalised) + " (" + missing + ")");
                }

                if (!alreadyLoaded && SceneManager.sceneCount > 1)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }

            Check("no missing scripts in any scene", broken.Count == 0,
                broken.Count == 0 ? "all component references resolve" : string.Join(", ", broken));
        }

        private static int CountMissingScripts(Scene scene)
        {
            var missing = 0;
            foreach (var root in scene.GetRootGameObjects())
            {
                var components = root.GetComponentsInChildren<Component>(true);
                for (var i = 0; i < components.Length; i++)
                {
                    if (components[i] == null)
                    {
                        missing++;
                    }
                }
            }

            return missing;
        }

        private static void CheckMaterials()
        {
            var paths = new[]
            {
                ProjectBootstrap.TerrainMaterialPath,
                ProjectBootstrap.ChunkMaterialPath,
                ProjectBootstrap.WaterMaterialPath,
                ProjectBootstrap.RoadMaterialPath,
            };

            foreach (var path in paths)
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    Fail("material " + Path.GetFileNameWithoutExtension(path), path + " is missing");
                    continue;
                }

                if (material.shader == null || material.shader.name == "Hidden/InternalErrorShader")
                {
                    Fail("material " + Path.GetFileNameWithoutExtension(path), "shader is the error shader");
                    continue;
                }

                var isTerrain = path == ProjectBootstrap.TerrainMaterialPath;
                var validShader = isTerrain
                    ? material.shader.name == "Universal Render Pipeline/Terrain/Lit"
                    : material.shader.name == SiranRadiusClip.ShaderName;
                Check("material " + Path.GetFileNameWithoutExtension(path) + " uses the expected renderer",
                    validShader, material.shader.name);
            }

            var waterNormal = AssetDatabase.LoadAssetAtPath<Texture2D>(
                "Assets/Resources/Art/Textures/water_normal_256.png");
            Check("water normal map present", waterNormal != null,
                waterNormal != null
                    ? waterNormal.width + "x" + waterNormal.height + " committed Unity texture"
                    : "missing Assets/Resources/Art/Textures/water_normal_256.png");
        }

        private static void CheckBlenderPipeline()
        {
            // The pipeline is maintained outside the Unity project; its own
            // validator proves generated assets, while this check confirms the
            // entry point and Unity import destination exist or are empty.
            var validator = "../processing/blender/validators/validate_assets.py";
            Check("blender pipeline present", File.Exists(validator),
                File.Exists(validator) ? validator : validator + " is missing");

            var charactersFolder = "Assets/Art/Characters";
            if (Directory.Exists(charactersFolder) && Directory.GetFiles(charactersFolder).Length > 0)
            {
                var files = Directory.GetFiles(charactersFolder);
                Pass("character sources found", files.Length + " file(s) in Assets/Art/Characters");
            }
            else
            {
                Skip("character sources", "Assets/Art/Characters is empty; no shippable character source is committed yet");
            }
        }

        private static void DeviceGates()
        {
            Skip("frame rate on target hardware", "requires a physical device (Snapdragon 6s Gen 3 / Android 16)");
            Skip("APK install + boot", "requires a signed build and a device");
            Skip("thermal soak (10 min)", "requires a physical device");
            Skip("Touch input on device", "requires a physical device; editor checks cover the code path only");
        }

        private static void Check(string name, bool condition, string detail)
        {
            if (condition)
            {
                Pass(name, detail);
            }
            else
            {
                Fail(name, detail);
            }
        }
    }
}
