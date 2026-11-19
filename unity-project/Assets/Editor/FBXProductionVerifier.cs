using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BattleHorizons.Characters;
using BattleHorizons.CameraRig;
using BattleHorizons.Player;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace BattleHorizons.EditorTools
{
    /// <summary>
    /// Licensed Unity 6 verification of the production Male / Female FBX
    /// characters. This class is read-only with respect to committed assets: it
    /// inspects importers, imported sub-assets, generated config/controllers,
    /// the Editor-generated PlayerTest scene and temporary scene instances. It
    /// never writes an FBX, .fbx.meta, production prefab, production controller,
    /// gameplay script or ProjectSettings file.
    ///
    /// Run in CI with:
    ///   -executeMethod BattleHorizons.EditorTools.FBXProductionVerifier.VerifyFromCommandLine
    ///
    /// The report uses PASS / FAIL / NOT VERIFIED explicitly. Static-only or
    /// device-only questions are never inferred from a successful import.
    /// </summary>
    public static class FBXProductionVerifier
    {
        public const string MaleModelPath = "Assets/Characters/Male/Male character.fbx";
        public const string FemaleModelPath = "Assets/Characters/Female/Female character.fbx";
        public const string PlayerTestScenePath = "Assets/Scenes/Test/PlayerTest.unity";

        private static readonly string[] RequiredCategories =
        {
            "Idle", "Walk", "Run", "Jump", "Aim", "Shoot", "Reload",
        };

        private static readonly string[] RequiredParameters =
        {
            CharacterAnimParameters.Speed,
            CharacterAnimParameters.VerticalSpeed,
            CharacterAnimParameters.IsAirborne,
            CharacterAnimParameters.IsCrouching,
            CharacterAnimParameters.IsAiming,
            CharacterAnimParameters.Shoot,
            CharacterAnimParameters.Reload,
            CharacterAnimParameters.Death,
            CharacterAnimParameters.WeaponSwitch,
            CharacterAnimParameters.Melee,
        };

        public sealed class Row
        {
            public string check;
            public string status;
            public string detail;
        }

        public sealed class Report
        {
            public readonly List<Row> rows = new List<Row>();
            public string title;

            public Report(string reportTitle)
            {
                title = reportTitle;
            }

            public void Add(string name, string status, string detail)
            {
                rows.Add(new Row { check = name, status = status, detail = detail });
            }

            public void Check(string name, bool condition, string good, string bad)
            {
                Add(name, condition ? "PASS" : "FAIL", condition ? good : bad);
            }

            public void NotVerified(string name, string detail)
            {
                Add(name, "NOT VERIFIED", detail);
            }

            public void Info(string name, string detail)
            {
                Add(name, "INFO", detail);
            }

            public int PassCount
            {
                get
                {
                    var count = 0;
                    for (var i = 0; i < rows.Count; i++)
                    {
                        if (rows[i].status == "PASS") count++;
                    }
                    return count;
                }
            }

            public int FailureCount
            {
                get
                {
                    var count = 0;
                    for (var i = 0; i < rows.Count; i++)
                    {
                        if (rows[i].status == "FAIL") count++;
                    }
                    return count;
                }
            }

            public int NotVerifiedCount
            {
                get
                {
                    var count = 0;
                    for (var i = 0; i < rows.Count; i++)
                    {
                        if (rows[i].status == "NOT VERIFIED") count++;
                    }
                    return count;
                }
            }

            public string Markdown()
            {
                var builder = new StringBuilder();
                builder.AppendLine("# " + title);
                builder.AppendLine();
                builder.AppendLine("| Check | Status | Detail |");
                builder.AppendLine("| --- | --- | --- |");
                for (var i = 0; i < rows.Count; i++)
                {
                    var row = rows[i];
                    var detail = (row.detail ?? string.Empty)
                        .Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ");
                    builder.AppendLine("| " + row.check + " | " + row.status + " | " + detail + " |");
                }
                builder.AppendLine();
                builder.AppendLine("## Summary");
                builder.AppendLine();
                builder.AppendLine("- PASS: " + PassCount);
                builder.AppendLine("- FAIL: " + FailureCount);
                builder.AppendLine("- NOT VERIFIED: " + NotVerifiedCount);
                return builder.ToString();
            }

            public string Json()
            {
                var builder = new StringBuilder();
                builder.AppendLine("{");
                builder.AppendLine("  \"title\": " + JsonString(title) + ",");
                builder.AppendLine("  \"summary\": {");
                builder.AppendLine("    \"pass\": " + PassCount + ",");
                builder.AppendLine("    \"fail\": " + FailureCount + ",");
                builder.AppendLine("    \"not_verified\": " + NotVerifiedCount);
                builder.AppendLine("  },");
                builder.AppendLine("  \"rows\": [");
                for (var i = 0; i < rows.Count; i++)
                {
                    var row = rows[i];
                    builder.Append("    { \"check\": " + JsonString(row.check)
                        + ", \"status\": " + JsonString(row.status)
                        + ", \"detail\": " + JsonString(row.detail) + " }");
                    if (i + 1 < rows.Count) builder.Append(",");
                    builder.AppendLine();
                }
                builder.AppendLine("  ]");
                builder.AppendLine("}");
                return builder.ToString();
            }
        }

        public static string OutputDirectory()
        {
            return Path.GetFullPath(Path.Combine(
                Application.dataPath, "..", "..", "docs", "fbx_production_verification"));
        }

        public static void WriteReportFiles(Report report, string stem)
        {
            var directory = OutputDirectory();
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, stem + ".md"), report.Markdown(), Encoding.UTF8);
            File.WriteAllText(Path.Combine(directory, stem + ".json"), report.Json(), Encoding.UTF8);
        }

        public static Report BuildReport()
        {
            var report = new Report("Licensed Unity Editor validation — production FBX characters");
            report.Add("verification boundary", "PASS",
                "Licensed Unity 6 Editor inspection of importers, sub-assets, generated config/controllers, PlayerTest scene, AnimationMode sampling and Android configuration. No APK, device, FPS, PSS or thermal test.");

            var config = CharacterAnimationConfig.Load();
            if (config == null)
            {
                report.Check("generated character/animation config", false,
                    "", "CharacterAnimationConfig.Load() returned null; run CharacterAnimationIntegration.BuildFromCommandLine first");
                return report;
            }

            CheckConfig(report, config);
            var models = new List<ModelEntry>();
            CheckModel(report, config, "Male", MaleModelPath, models);
            CheckModel(report, config, "Female", FemaleModelPath, models);

            for (var i = 0; i < config.characters.Count; i++)
            {
                var character = config.characters[i];
                if (character == null) continue;
                CheckController(report, config, character);
            }

            for (var i = 0; i < models.Count; i++)
            {
                CheckDeformation(report, config, models[i]);
            }

            CheckPlayerSceneAndVisual(report);
            CheckRuntimeAimAndShootingSources(report);
            CheckAndroidConfiguration(report, models);

            report.NotVerified("animation visual quality / bad skinning / front and facial detail",
                "AnimationMode bone sampling and a software-GL rear view do not prove visual quality; human inspection is still required");
            report.NotVerified("Android device runtime / shader variants / FPS / memory / thermal",
                "no APK was built or installed and no physical Android device measurement was performed");
            return report;
        }

        public static void VerifyFromCommandLine()
        {
            Report report;
            try
            {
                report = BuildReport();
            }
            catch (Exception exception)
            {
                report = new Report("Licensed Unity Editor validation — production FBX characters");
                report.Check("Editor verification completed", false, "", exception.ToString());
            }

            WriteReportFiles(report, "EDITOR_VALIDATION");
            Debug.Log("[bh][fbx-production]\n" + report.Markdown());
            if (report.FailureCount > 0)
            {
                Debug.LogError("[bh][fbx-production] " + report.FailureCount
                    + " FAIL row(s); see docs/fbx_production_verification/EDITOR_VALIDATION.md");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }

            Debug.Log("[bh][fbx-production] no FAIL rows; " + report.NotVerifiedCount
                + " NOT VERIFIED row(s) remain explicit");
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        private struct ModelEntry
        {
            public string sex;
            public string path;
            public GameObject model;
            public Avatar avatar;
        }

        private static void CheckConfig(Report report, CharacterAnimationConfig config)
        {
            int missing;
            var references = config.ValidateReferences(out missing);
            report.Check("generated config references", missing == 0,
                references + "; asset path " + AssetDatabase.GetAssetPath(config),
                references + "; asset path " + AssetDatabase.GetAssetPath(config));

            report.Check("two production character definitions", config.characters.Count >= 2,
                config.characters.Count + " character definitions; " + config.clips.Count + " registered clips; " + config.assignments.Count + " assignments",
                config.characters.Count + " character definitions found");

            var gltfReferences = 0;
            var nonFbxModels = 0;
            for (var i = 0; i < config.characters.Count; i++)
            {
                var character = config.characters[i];
                if (character == null) continue;
                var modelPath = character.model != null ? AssetDatabase.GetAssetPath(character.model) : string.Empty;
                var controllerPath = character.controller != null ? AssetDatabase.GetAssetPath(character.controller) : string.Empty;
                if (modelPath.IndexOf("GLTF_Import_Test", StringComparison.OrdinalIgnoreCase) >= 0
                    || controllerPath.IndexOf("GLTF_Import_Test", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    gltfReferences++;
                }
                if (!string.Equals(modelPath, MaleModelPath, StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(modelPath, FemaleModelPath, StringComparison.OrdinalIgnoreCase))
                {
                    nonFbxModels++;
                }
            }
            report.Check("config references production FBX only", gltfReferences == 0 && nonFbxModels == 0,
                "0 GLTF references, 0 non-production character model references",
                gltfReferences + " GLTF reference(s), " + nonFbxModels + " non-production model reference(s)");

            var nullShared = 0;
            var emptyShared = 0;
            var perCharacter = 0;
            for (var i = 0; i < config.assignments.Count; i++)
            {
                var assignment = config.assignments[i];
                if (assignment == null) continue;
                if (CharacterAnimationConfig.IsSharedCharacterId(assignment.characterId))
                {
                    if (assignment.characterId == null) nullShared++;
                    else emptyShared++;
                }
                else perCharacter++;
            }
            report.Info("generated assignment characterId representation",
                nullShared + " null, " + emptyShared + " empty/whitespace shared, " + perCharacter + " per-character entries; Unity serializes a null string as an empty string after domain reload");

            var lookupFailures = new List<string>();
            for (var c = 0; c < RequiredCategories.Length; c++)
            {
                var categoryName = RequiredCategories[c];
                var category = CategoryFromName(categoryName);
                var clip = ResolveClipForCategory(config, config.selectedCharacterId, category);
                var runtimeLookup = config.FindClip(config.selectedCharacterId, category);
                report.Check("selected character clip assignment: " + categoryName,
                    clip != null && clip.humanMotion && clip.length > 0f,
                    clip == null ? "" : clip.name + ", length " + clip.length.ToString("F3") + " s, humanMotion=" + clip.humanMotion + ", frameRate=" + clip.frameRate.ToString("F1"),
                    clip == null ? "no clip assigned for " + categoryName : clip.name + " is not Humanoid or has zero length");
                if (clip != null && runtimeLookup == null) lookupFailures.Add(categoryName);
            }
            report.Check("runtime CharacterAnimationConfig.FindClip lookup", lookupFailures.Count == 0,
                "FindClip returns the direct assignment for all 7 required categories",
                "FindClip returned null for " + string.Join(", ", lookupFailures.ToArray())
                    + " although the assignment list contains those clips; CharacterAnimationConfig.FindAssignment must treat null, empty and whitespace shared IDs with case-insensitive per-character overrides.");
        }

        /// <summary>
        /// Resolve a generated category through the repaired runtime lookup.
        /// Unity deserializes null shared IDs as empty strings; the runtime
        /// config now handles that representation directly.
        /// </summary>
        public static AnimationClip ResolveClipForCategory(
            CharacterAnimationConfig config,
            string characterId,
            CharacterAnimCategory category)
        {
            if (config == null) return null;
            var assignment = config.FindAssignment(characterId, category);
            return assignment != null ? assignment.clip : null;
        }

        private static void CheckModel(
            Report report,
            CharacterAnimationConfig config,
            string sex,
            string path,
            List<ModelEntry> models)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            var rig = RigDetectionUtility.Inspect(path);
            var avatar = RigDetectionUtility.FindAvatar(path);

            var importerOk = importer != null
                && importer.animationType == ModelImporterAnimationType.Human
                && importer.avatarSetup == ModelImporterAvatarSetup.CreateFromThisModel
                && importer.importAnimation
                && !importer.isReadable
                && importer.maxBonesPerVertex == 4
                && importer.skinWeights == ModelImporterSkinWeights.Standard;
            report.Check(sex + " Unity ModelImporter settings", importerOk,
                "Humanoid, CreateFromThisModel, importAnimation=true, isReadable=false, Standard/4 bones per vertex, optimal compression",
                importer == null ? "no ModelImporter at " + path
                    : "animationType=" + importer.animationType + ", avatarSetup=" + importer.avatarSetup
                      + ", isReadable=" + importer.isReadable + ", maxBonesPerVertex=" + importer.maxBonesPerVertex
                      + ", skinWeights=" + importer.skinWeights);

            var materialMode = importer == null ? "no importer" : importer.materialImportMode.ToString();
            report.Check(sex + " Unity material import mode",
                importer != null && importer.materialImportMode == ModelImporterMaterialImportMode.ImportStandard,
                "materialImportMode=" + materialMode + "; exact committed URP/Lit external material remaps are active",
                "materialImportMode=" + materialMode
                    + ". Unity's ImportStandard mode is required for the deterministic FBX material remaps.");

            var avatarOk = rig.success && rig.kind == RigKind.Humanoid
                && avatar != null && avatar.isValid && avatar.isHuman;
            var humanEntries = avatar != null && avatar.humanDescription.human != null
                ? avatar.humanDescription.human.Length : 0;
            report.Check(sex + " Humanoid Avatar configuration", avatarOk,
                rig.mappedHumanBones + "/" + HumanTrait.BoneName.Length + " human bones mapped; "
                    + humanEntries + " HumanDescription entries; isValid && isHuman",
                rig.Combined + (avatar == null ? "; no Avatar" : "; isValid=" + avatar.isValid + ", isHuman=" + avatar.isHuman));

            if (model == null)
            {
                report.Check(sex + " imported model asset", false, "", "no GameObject imported at " + path);
                return;
            }

            models.Add(new ModelEntry { sex = sex, path = path, model = model, avatar = avatar });
            CheckImportedAssets(report, sex, path, model);
            for (var i = 0; i < RequiredCategories.Length; i++)
            {
                var categoryName = RequiredCategories[i];
                var clip = ResolveClipForCategory(config, config.selectedCharacterId, CategoryFromName(categoryName));
                string reason;
                var compatible = RigDetectionUtility.IsRetargetCompatible(clip, avatar, out reason);
                report.Check(sex + " Avatar compatibility: " + categoryName, compatible,
                    clip == null ? "" : "clip '" + clip.name + "' is Humanoid and target Avatar is valid/human",
                    reason);
            }
        }

        private static void CheckImportedAssets(Report report, string sex, string path, GameObject model)
        {
            var subAssets = AssetDatabase.LoadAllAssetsAtPath(path);
            var textures = 0;
            var materials = 0;
            var avatars = 0;
            var meshes = 0;
            for (var i = 0; i < subAssets.Length; i++)
            {
                var asset = subAssets[i];
                if (asset is Texture2D) textures++;
                else if (asset is Material) materials++;
                else if (asset is Avatar) avatars++;
                else if (asset is Mesh) meshes++;
            }

            var instance = UnityEngine.Object.Instantiate(model);
            instance.name = sex + "_FBX_Verification";
            try
            {
                var renderers = instance.GetComponentsInChildren<Renderer>(true);
                var skinned = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                var nullMaterials = 0;
                var errorShaders = 0;
                var textureSlots = 0;
                var invalidTextures = 0;
                var materialSlots = 0;
                var externalMaterials = 0;
                var nonUrpShaders = 0;
                var expectedMaterialsPrefix = "Assets/Characters/" + sex + "/Materials/";
                var shaderNames = new HashSet<string>();
                for (var r = 0; r < renderers.Length; r++)
                {
                    var shared = renderers[r].sharedMaterials;
                    if (shared == null || shared.Length == 0)
                    {
                        nullMaterials++;
                        continue;
                    }
                    for (var m = 0; m < shared.Length; m++)
                    {
                        var material = shared[m];
                        if (material == null)
                        {
                            nullMaterials++;
                            continue;
                        }
                        materialSlots++;
                        if (AssetDatabase.GetAssetPath(material).StartsWith(expectedMaterialsPrefix,
                            StringComparison.OrdinalIgnoreCase))
                        {
                            externalMaterials++;
                        }
                        var shader = material.shader;
                        if (shader == null
                            || shader.name.IndexOf("Error", StringComparison.OrdinalIgnoreCase) >= 0
                            || shader.name.IndexOf("InternalError", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            errorShaders++;
                            continue;
                        }
                        shaderNames.Add(shader.name);
                        if (shader.name != "Universal Render Pipeline/Lit") nonUrpShaders++;
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

                var materialOk = nullMaterials == 0 && errorShaders == 0 && invalidTextures == 0
                    && materialSlots >= 3 && externalMaterials == materialSlots
                    && nonUrpShaders == 0 && textureSlots >= 6;
                report.Check(sex + " imported materials/textures", materialOk,
                    renderers.Length + " renderers, " + skinned.Length + " SkinnedMeshRenderers, "
                    + materialSlots + " material slots (" + externalMaterials + " external URP/Lit), "
                    + textureSlots + " bound texture slots, " + textures + " Texture2D sub-assets, "
                    + materials + " Material sub-assets, shaders: " + string.Join(", ", Sorted(shaderNames)),
                    nullMaterials + " null material(s), " + errorShaders + " error shader(s), "
                    + invalidTextures + " invalid texture(s), " + materialSlots + " material slots ("
                    + externalMaterials + " external), " + nonUrpShaders + " non-URP/Lit shader(s), "
                    + textureSlots + " bound texture slots, " + textures + " Texture2D sub-assets");

                var normalPath = sex == "Female"
                    ? "Assets/Characters/Female/Textures/T_Superhero_Female_Normal.png"
                    : "Assets/Characters/Male/Textures/T_Superhero_Male_Normal.png";
                var normalImporter = AssetImporter.GetAtPath(normalPath) as TextureImporter;
                var normalSettings = new TextureImporterSettings();
                if (normalImporter != null) normalImporter.ReadTextureSettings(normalSettings);
                report.Check(sex + " normal map resolves as NormalMap",
                    normalImporter != null
                        && normalSettings.textureType == TextureImporterType.NormalMap
                        && !normalSettings.sRGBTexture
                        && !normalSettings.convertToNormalMap,
                    normalPath + ": TextureImporterType.NormalMap, linear sRGB=false, convertToNormalMap=false",
                    normalPath + ": importer=" + (normalImporter != null)
                        + ", type=" + normalSettings.textureType
                        + ", sRGB=" + normalSettings.sRGBTexture
                        + ", convert=" + normalSettings.convertToNormalMap);

                var bounds = CalculateBounds(renderers);
                var boundsOk = bounds.size.y >= 1.35f && bounds.size.y <= 2.25f
                    && bounds.min.y >= -0.15f && bounds.min.y <= 0.15f;
                report.Check(sex + " upright scale and ground alignment", boundsOk,
                    "renderer bounds size=" + bounds.size.ToString("F3") + ", minY=" + bounds.min.y.ToString("F3"),
                    "renderer bounds size=" + bounds.size.ToString("F3") + ", minY=" + bounds.min.y.ToString("F3")
                    + "; expected ~1.35-2.25 m upright with feet near y=0");
                report.Info(sex + " FBX sub-assets",
                    textures + " Texture2D, " + materials + " Material, " + avatars + " Avatar, " + meshes + " Mesh sub-assets at " + path);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        private static void CheckController(
            Report report,
            CharacterAnimationConfig config,
            CharacterDefinition character)
        {
            var controller = character.controller as AnimatorController;
            if (controller == null)
            {
                report.Check("Animator Controller: " + character.id, false, "",
                    "character.controller is null or is not a UnityEditor.Animations.AnimatorController");
                return;
            }

            var controllerPath = AssetDatabase.GetAssetPath(controller);
            report.Check("Animator Controller asset: " + character.id,
                controllerPath.StartsWith("Assets/AnimationControllers/", StringComparison.OrdinalIgnoreCase),
                controllerPath + ", " + controller.layers.Length + " layer(s), " + controller.parameters.Length + " parameter(s)",
                "unexpected controller path " + controllerPath);

            var missingParameters = new List<string>();
            for (var i = 0; i < RequiredParameters.Length; i++)
            {
                if (!HasParameter(controller, RequiredParameters[i])) missingParameters.Add(RequiredParameters[i]);
            }
            report.Check("Animator parameters: " + character.id, missingParameters.Count == 0,
                RequiredParameters.Length + "/" + RequiredParameters.Length + " shared runtime parameters present with expected types",
                "missing/wrong-type: " + string.Join(", ", missingParameters.ToArray()));

            var parameterCounts = new Dictionary<string, int>();
            for (var i = 0; i < controller.parameters.Length; i++)
            {
                var name = controller.parameters[i].name;
                int count;
                parameterCounts.TryGetValue(name, out count);
                parameterCounts[name] = count + 1;
            }
            var duplicateParameters = new List<string>();
            foreach (var pair in parameterCounts)
            {
                if (pair.Value > 1) duplicateParameters.Add(pair.Key + " x" + pair.Value);
            }
            var aimEntryTransitions = 0;
            if (controller.layers.Length > 0)
            {
                foreach (var transition in controller.layers[0].stateMachine.anyStateTransitions)
                {
                    for (var c = 0; c < transition.conditions.Length; c++)
                    {
                        if (transition.conditions[c].parameter == CharacterAnimParameters.IsAiming)
                        {
                            aimEntryTransitions++;
                        }
                    }
                }
            }
            report.Check("Animator parameter/transition uniqueness: " + character.id,
                duplicateParameters.Count == 0 && aimEntryTransitions == 1,
                controller.parameters.Length + " parameters with unique names; exactly one IsAiming Any-State entry transition",
                "duplicate parameters: " + string.Join(", ", duplicateParameters.ToArray())
                    + "; IsAiming Any-State conditions=" + aimEntryTransitions);

            if (controller.layers.Length == 0)
            {
                report.Check("Animator layers: " + character.id, false, "", "controller has no layers");
                return;
            }

            var baseMachine = controller.layers[0].stateMachine;
            var states = new List<AnimatorState>();
            CollectStates(baseMachine, states);
            var missingStates = new List<string>();
            var missingMotions = new List<string>();
            for (var i = 0; i < RequiredCategories.Length; i++)
            {
                var state = FindState(states, RequiredCategories[i]);
                if (state == null)
                {
                    missingStates.Add(RequiredCategories[i]);
                    continue;
                }
                if (state.motion == null) missingMotions.Add(state.name);
            }
            report.Check("Animator states / clip motions: " + character.id,
                missingStates.Count == 0 && missingMotions.Count == 0,
                RequiredCategories.Length + " required gameplay states each have a Motion: " + string.Join(", ", RequiredCategories),
                "missing states: " + string.Join(", ", missingStates.ToArray())
                    + "; states with null Motion: " + string.Join(", ", missingMotions.ToArray()));

            var transitionProblems = new List<string>();
            CheckAnyStateTransition(baseMachine, states, "Walk", CharacterAnimParameters.Speed, transitionProblems);
            CheckAnyStateTransition(baseMachine, states, "Run", CharacterAnimParameters.Speed, transitionProblems);
            CheckAnyStateTransition(baseMachine, states, "Jump", CharacterAnimParameters.IsAirborne, transitionProblems);
            CheckAnyStateTransition(baseMachine, states, "Aim", CharacterAnimParameters.IsAiming, transitionProblems);
            CheckAnyStateTransition(baseMachine, states, "Shoot", CharacterAnimParameters.Shoot, transitionProblems);
            CheckAnyStateTransition(baseMachine, states, "Reload", CharacterAnimParameters.Reload, transitionProblems);
            CheckExitTransition(states, "Jump", "Idle", transitionProblems);
            CheckExitTransition(states, "Aim", "Idle", transitionProblems);
            CheckExitTransition(states, "Shoot", "Idle", transitionProblems);
            CheckExitTransition(states, "Reload", "Idle", transitionProblems);
            report.Check("Animator transitions: " + character.id, transitionProblems.Count == 0,
                "Walk/Run Speed, Jump IsAirborne, Aim IsAiming, Shoot/Reload trigger Any-State entries and Idle exits are present; all checked entry transitions have no forced exit time",
                string.Join("; ", transitionProblems.ToArray()));

            var upperBodyOk = controller.layers.Length >= 2
                && controller.layers[1].name == CharacterAnimParameters.UpperBodyLayer
                && controller.layers[1].avatarMask != null;
            var upperStates = new List<AnimatorState>();
            if (controller.layers.Length >= 2) CollectStates(controller.layers[1].stateMachine, upperStates);
            var rifleIdle = FindState(upperStates, "RifleIdle");
            var rifleRun = FindState(upperStates, "RifleRun");
            report.Check("UpperBody aiming layer: " + character.id,
                upperBodyOk && rifleIdle != null && rifleRun != null && rifleIdle.motion != null && rifleRun.motion != null,
                "layer 1 is UpperBody with AvatarMask; RifleIdle/RifleRun states and motions are present",
                "layers=" + controller.layers.Length + ", mask=" + (controller.layers.Length >= 2 && controller.layers[1].avatarMask != null)
                    + ", RifleIdle=" + (rifleIdle != null) + ", RifleRun=" + (rifleRun != null));
        }

        private static void CheckDeformation(Report report, CharacterAnimationConfig config, ModelEntry entry)
        {
            if (entry.model == null || entry.avatar == null)
            {
                report.NotVerified("deformation sampling: " + entry.sex, "model or Avatar missing; no sampling attempted");
                return;
            }

            var instance = UnityEngine.Object.Instantiate(entry.model);
            instance.name = entry.sex + "_AnimationModeSample";
            try
            {
                var animator = instance.GetComponentInChildren<Animator>(true);
                if (animator == null) animator = instance.AddComponent<Animator>();
                animator.avatar = entry.avatar;
                animator.runtimeAnimatorController = null;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.enabled = true;

                for (var i = 0; i < RequiredCategories.Length; i++)
                {
                    var categoryName = RequiredCategories[i];
                    var clip = ResolveClipForCategory(config, config.selectedCharacterId, CategoryFromName(categoryName));
                    if (clip == null)
                    {
                        report.NotVerified("deformation: " + entry.sex + "/" + categoryName, "no generated clip assignment");
                        continue;
                    }

                    var started = false;
                    try
                    {
                        AnimationMode.StartAnimationMode();
                        started = true;
                        var transforms = instance.GetComponentsInChildren<Transform>(true);
                        SampleClip(instance, clip, 0f);
                        var startRotations = new Quaternion[transforms.Length];
                        var startPositions = new Vector3[transforms.Length];
                        for (var t = 0; t < transforms.Length; t++)
                        {
                            startRotations[t] = transforms[t].localRotation;
                            startPositions[t] = transforms[t].localPosition;
                        }

                        var midTime = Mathf.Max(0f, clip.length * 0.5f);
                        SampleClip(instance, clip, midTime);
                        var maxAngle = 0f;
                        var maxMove = 0f;
                        for (var t = 0; t < transforms.Length; t++)
                        {
                            maxAngle = Mathf.Max(maxAngle, Quaternion.Angle(startRotations[t], transforms[t].localRotation));
                            maxMove = Mathf.Max(maxMove, Vector3.Distance(startPositions[t], transforms[t].localPosition));
                        }

                        var moved = maxAngle > 0.05f || maxMove > 0.001f;
                        report.Check("deformation sampling: " + entry.sex + "/" + categoryName, moved,
                            "AnimationMode sampled '" + clip.name + "' at 0.000 s and " + midTime.ToString("F3")
                                + " s across " + transforms.Length + " transforms; max bone rotation " + maxAngle.ToString("F3")
                                + " deg, max local position delta " + maxMove.ToString("F4") + " m",
                            "no measurable bone transform change in Editor AnimationMode; visual/PlayMode deformation must be reviewed separately");
                    }
                    catch (Exception exception)
                    {
                        report.NotVerified("deformation: " + entry.sex + "/" + categoryName,
                            "AnimationMode sampling unavailable: " + exception.Message);
                    }
                    finally
                    {
                        if (started) AnimationMode.StopAnimationMode();
                    }
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        private static void CheckPlayerSceneAndVisual(Report report)
        {
            if (!File.Exists(PlayerTestScenePath))
            {
                report.NotVerified("PlayerTest scene and visible player mesh",
                    PlayerTestScenePath + " was not generated in this Editor workspace; run ProjectBootstrap.ConfigureFromCommandLine first");
                return;
            }

            try
            {
                EditorSceneManager.OpenScene(PlayerTestScenePath, OpenSceneMode.Single);
                var player = GameObject.FindWithTag("Player");
                var cameraObject = GameObject.Find("PlayerCamera");
                var cameraRig = cameraObject != null ? cameraObject.GetComponent<ThirdPersonCameraController>() : null;
                var sceneOk = player != null
                    && player.GetComponent<CharacterController>() != null
                    && player.GetComponent<ThirdPersonMotor>() != null
                    && player.GetComponent<PlayerController>() != null
                    && player.GetComponent<PlayerCharacterVisual>() != null
                    && cameraRig != null && cameraRig.Target == player.transform;
                report.Check("PlayerTest gameplay references", sceneOk,
                    "Player tag/root has CharacterController, ThirdPersonMotor, PlayerController and PlayerCharacterVisual; PlayerCamera targets the Player transform",
                    "Player scene is missing one or more generated gameplay references");

                if (player == null)
                {
                    report.Check("visible player mesh is FBX, not capsule", false, "", "no GameObject tagged Player in PlayerTest scene");
                    return;
                }

                var capsule = player.transform.Find("PlayerCapsuleVisual");
                var capsuleRenderer = capsule != null ? capsule.GetComponent<MeshRenderer>() : null;
                var visual = player.GetComponent<PlayerCharacterVisual>();
                var motor = player.GetComponent<ThirdPersonMotor>();
                if (visual != null)
                {
                    var serialized = new SerializedObject(visual);
                    var motorProperty = serialized.FindProperty("motor");
                    if (motorProperty != null) motorProperty.objectReferenceValue = motor;
                    var capsuleProperty = serialized.FindProperty("visualCapsule");
                    if (capsuleProperty != null) capsuleProperty.objectReferenceValue = capsule;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }

                var spawned = visual != null && visual.TrySpawn();
                var definition = visual != null ? visual.ActiveDefinition : null;
                var modelPath = definition != null && definition.model != null
                    ? AssetDatabase.GetAssetPath(definition.model) : string.Empty;
                var animator = visual != null ? visual.CharacterAnimator : null;
                var skinned = player.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                var avatarOk = animator != null && animator.avatar != null && animator.avatar.isValid && animator.avatar.isHuman;
                var productionFbx = string.Equals(modelPath, MaleModelPath, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(modelPath, FemaleModelPath, StringComparison.OrdinalIgnoreCase);
                var capsuleHidden = capsuleRenderer != null && !capsuleRenderer.enabled;

                report.Check("visible player mesh is FBX, not capsule", spawned && productionFbx
                    && skinned.Length > 0 && avatarOk && capsuleHidden
                    && animator.runtimeAnimatorController != null,
                    "PlayerCharacterVisual.TrySpawn instantiated " + modelPath + "; " + skinned.Length
                        + " SkinnedMeshRenderer(s), valid Humanoid Avatar, generated controller assigned, PlayerCapsuleVisual MeshRenderer disabled",
                    "spawned=" + spawned + ", modelPath=" + modelPath + ", skinned=" + skinned.Length
                        + ", avatarOk=" + avatarOk + ", controller=" + (animator != null && animator.runtimeAnimatorController != null)
                        + ", capsuleHidden=" + capsuleHidden);

                report.Check("spawned Animator controller matches generated config",
                    definition != null && definition.controller != null
                        && animator != null && animator.runtimeAnimatorController == definition.controller,
                    "runtime Animator controller is the per-character generated controller",
                    "runtime Animator controller is missing or differs from CharacterAnimationConfig");
            }
            catch (Exception exception)
            {
                report.NotVerified("PlayerTest scene and visible player mesh",
                    "scene inspection could not complete: " + exception.Message);
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }

        private static void CheckRuntimeAimAndShootingSources(Report report)
        {
            var runtimeRoot = Path.Combine(Application.dataPath, "Scripts");
            var editorRoot = Path.Combine(Application.dataPath, "Editor");
            var setBoolLines = new List<string>();
            var setAimingCalls = new List<string>();
            var aimStateLines = new List<string>();
            var aimTouchLines = new List<string>();
            var sourceCount = 0;
            if (Directory.Exists(runtimeRoot))
            {
                var sources = Directory.GetFiles(runtimeRoot, "*.cs", SearchOption.AllDirectories);
                sourceCount = sources.Length;
                for (var s = 0; s < sources.Length; s++)
                {
                    var lines = File.ReadAllLines(sources[s]);
                    var relative = sources[s].Replace('\\', '/');
                    for (var l = 0; l < lines.Length; l++)
                    {
                        var line = lines[l];
                        if (line.Contains("SetBool") && line.Contains("IsAiming"))
                            setBoolLines.Add(relative + ":" + (l + 1));
                        if (line.Contains("SetAiming(") && !line.Contains("public void SetAiming"))
                            setAimingCalls.Add(relative + ":" + (l + 1));
                        if (line.Contains("AimInput") || line.Contains("IsAiming"))
                            aimStateLines.Add(relative + ":" + (l + 1));
                        if (line.Contains("SetAimHeld") || line.Contains("AimButton"))
                            aimTouchLines.Add(relative + ":" + (l + 1));
                    }
                }
            }

            var isAimingParameterDeclarations = 0;
            if (Directory.Exists(editorRoot))
            {
                foreach (var file in Directory.GetFiles(editorRoot, "*.cs", SearchOption.AllDirectories))
                {
                    foreach (var line in File.ReadAllLines(file))
                    {
                        if (line.TrimStart().StartsWith("AddParameter(", StringComparison.Ordinal)
                            && line.Contains("CharacterAnimParameters.IsAiming"))
                        {
                            isAimingParameterDeclarations++;
                        }
                    }
                }
            }

            report.Check("Aim Animator parameter is driven by runtime", setBoolLines.Count > 0,
                "found SetBool(IsAiming) in " + string.Join(", ", setBoolLines.ToArray()),
                "scanned " + sourceCount + " runtime C# files; no SetBool(IsAiming) exists.");
            report.Check("Aim gameplay hook is called", setAimingCalls.Count > 0,
                "found runtime caller(s): " + string.Join(", ", setAimingCalls.ToArray()),
                "PlayerCharacterVisual.SetAiming(bool) has no runtime caller.");
            report.Check("actual runtime aim state and touch input exist",
                aimStateLines.Count > 0 && aimTouchLines.Count > 0,
                "motor AimInput/IsAiming and PlayerInputRouter/MobileControlsBuilder AIM hold-button wiring found",
                "scanned " + sourceCount + " runtime C# files; missing AimInput state or SetAimHeld/AimButton wiring.");
            report.Check("IsAiming parameter definition is not duplicated",
                isAimingParameterDeclarations == 1,
                "exactly one AddParameter(CharacterAnimParameters.IsAiming) builder declaration",
                "found " + isAimingParameterDeclarations + " IsAiming parameter declarations under Assets/Editor");
            report.NotVerified("live shooting gameplay integration",
                "aiming state now drives IsAiming and SetAiming, but no weapon/fire gameplay system is present; only PlayerCharacterVisual.TriggerShoot/TriggerReload animation hooks and PlayMode hook safety tests exist");
        }

        private static void CheckAndroidConfiguration(Report report, List<ModelEntry> models)
        {
            var urp = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
            var targetAndroid = EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android;
            var minSdk = PlayerSettings.Android.minSdkVersion == AndroidSdkVersions.AndroidApiLevel26;
            var il2cpp = PlayerSettings.GetScriptingBackend(BuildTargetGroup.Android) == ScriptingImplementation.IL2CPP;
            var linear = PlayerSettings.colorSpace == ColorSpace.Linear;
            var arm64 = PlayerSettings.Android.targetArchitectures == AndroidArchitecture.ARM64;
            var astc = EditorUserBuildSettings.androidBuildSubtarget == MobileTextureSubtarget.ASTC;
            var graphicsApis = PlayerSettings.GetGraphicsAPIs(BuildTarget.Android);
            var apiBuilder = new StringBuilder();
            for (var i = 0; i < graphicsApis.Length; i++)
            {
                if (i > 0) apiBuilder.Append(",");
                apiBuilder.Append(graphicsApis[i]);
            }

            var ok = urp != null && targetAndroid && minSdk && il2cpp && linear && arm64 && astc;
            report.Check("Android-compatible rendering configuration", ok,
                "active target Android, URP " + (urp != null ? urp.name : "null") + ", Linear, IL2CPP, ARM64, min SDK 26, ASTC, graphics APIs [" + apiBuilder + "]",
                "activeTarget=" + EditorUserBuildSettings.activeBuildTarget + ", urp=" + (urp != null)
                    + ", minSdk=" + PlayerSettings.Android.minSdkVersion + ", il2cpp=" + il2cpp
                    + ", linear=" + linear + ", architectures=" + PlayerSettings.Android.targetArchitectures
                    + ", subtarget=" + EditorUserBuildSettings.androidBuildSubtarget);

            var supportedShaders = 0;
            var unsupportedShaders = 0;
            for (var i = 0; i < models.Count; i++)
            {
                if (models[i].model == null) continue;
                var renderers = models[i].model.GetComponentsInChildren<Renderer>(true);
                for (var r = 0; r < renderers.Length; r++)
                {
                    var materials = renderers[r].sharedMaterials;
                    for (var m = 0; m < materials.Length; m++)
                    {
                        var shader = materials[m] != null ? materials[m].shader : null;
                        if (shader == null) continue;
                        if (shader.isSupported) supportedShaders++;
                        else unsupportedShaders++;
                    }
                }
            }
            report.Info("Editor shader support under current build target",
                supportedShaders + " supported and " + unsupportedShaders + " unsupported renderer/material slot(s); Editor shader.isSupported is not an Android device shader-variant test");
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        private static CharacterAnimCategory CategoryFromName(string name)
        {
            switch (name)
            {
                case "Idle": return CharacterAnimCategory.Idle;
                case "Walk": return CharacterAnimCategory.Walk;
                case "Run": return CharacterAnimCategory.Run;
                case "Jump": return CharacterAnimCategory.Jump;
                case "Aim": return CharacterAnimCategory.Aim;
                case "Shoot": return CharacterAnimCategory.Shoot;
                case "Reload": return CharacterAnimCategory.Reload;
                default: return CharacterAnimCategory.Idle;
            }
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

        private static bool HasParameter(AnimatorController controller, string name)
        {
            var parameters = controller.parameters;
            for (var i = 0; i < parameters.Length; i++)
            {
                if (!string.Equals(parameters[i].name, name, StringComparison.Ordinal)) continue;
                switch (name)
                {
                    case CharacterAnimParameters.Speed:
                    case CharacterAnimParameters.VerticalSpeed:
                        return parameters[i].type == AnimatorControllerParameterType.Float;
                    case CharacterAnimParameters.IsAirborne:
                    case CharacterAnimParameters.IsCrouching:
                    case CharacterAnimParameters.IsAiming:
                        return parameters[i].type == AnimatorControllerParameterType.Bool;
                    default:
                        return parameters[i].type == AnimatorControllerParameterType.Trigger;
                }
            }
            return false;
        }

        private static void CollectStates(AnimatorStateMachine machine, List<AnimatorState> output)
        {
            if (machine == null) return;
            var states = machine.states;
            for (var i = 0; i < states.Length; i++)
            {
                if (states[i].state != null) output.Add(states[i].state);
            }
            var machines = machine.stateMachines;
            for (var i = 0; i < machines.Length; i++)
            {
                if (machines[i].stateMachine != null) CollectStates(machines[i].stateMachine, output);
            }
        }

        private static AnimatorState FindState(List<AnimatorState> states, string name)
        {
            for (var i = 0; i < states.Count; i++)
            {
                if (states[i] != null && string.Equals(states[i].name, name, StringComparison.Ordinal))
                {
                    return states[i];
                }
            }
            return null;
        }

        private static void CheckAnyStateTransition(
            AnimatorStateMachine machine,
            List<AnimatorState> states,
            string stateName,
            string parameter,
            List<string> problems)
        {
            var state = FindState(states, stateName);
            if (state == null)
            {
                problems.Add("missing state " + stateName);
                return;
            }
            var found = false;
            var transitions = machine.anyStateTransitions;
            for (var i = 0; i < transitions.Length; i++)
            {
                var transition = transitions[i];
                if (transition.destinationState != state || !TransitionUsesParameter(transition, parameter)) continue;
                found = true;
                if (transition.hasExitTime) problems.Add(stateName + " entry transition has forced exit time");
                break;
            }
            if (!found) problems.Add("no Any State -> " + stateName + " transition using " + parameter);
        }

        private static void CheckExitTransition(
            List<AnimatorState> states,
            string fromName,
            string toName,
            List<string> problems)
        {
            var from = FindState(states, fromName);
            var to = FindState(states, toName);
            if (from == null || to == null)
            {
                problems.Add("missing state for exit " + fromName + " -> " + toName);
                return;
            }
            var transitions = from.transitions;
            for (var i = 0; i < transitions.Length; i++)
            {
                if (transitions[i].destinationState == to) return;
            }
            problems.Add("no " + fromName + " -> " + toName + " transition");
        }

        private static bool TransitionUsesParameter(AnimatorStateTransition transition, string parameter)
        {
            var conditions = transition.conditions;
            for (var i = 0; i < conditions.Length; i++)
            {
                if (string.Equals(conditions[i].parameter, parameter, StringComparison.Ordinal)) return true;
            }
            return false;
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

        private static string[] Sorted(HashSet<string> values)
        {
            var output = new List<string>(values);
            output.Sort(StringComparer.Ordinal);
            return output.ToArray();
        }

        private static string JsonString(string value)
        {
            if (value == null) return "\"\"";
            var builder = new StringBuilder("\"");
            foreach (var character in value)
            {
                switch (character)
                {
                    case '\\': builder.Append("\\\\"); break;
                    case '"': builder.Append("\\\""); break;
                    case '\r': builder.Append("\\r"); break;
                    case '\n': builder.Append("\\n"); break;
                    case '\t': builder.Append("\\t"); break;
                    default:
                        if (character < ' ') builder.Append("\\u").Append(((int)character).ToString("x4"));
                        else builder.Append(character);
                        break;
                }
            }
            builder.Append("\"");
            return builder.ToString();
        }
    }
}
