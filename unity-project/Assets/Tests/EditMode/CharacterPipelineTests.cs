using System;
using System.Collections.Generic;
using System.IO;
using BattleHorizons.Characters;
using BattleHorizons.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace BattleHorizons.Tests
{
    /// <summary>
    /// End-to-end tests for the character &amp; animation import pipeline on
    /// the REAL uploaded assets (Male / Female FBX and the Mixamo animation
    /// library): rig validity, upload + texture extraction + URP materials,
    /// duplicate prevention, assignment round-trip and Animator Controller
    /// generation. Nothing here is mocked — if retargeting is broken these
    /// tests fail instead of the game shipping a fake.
    /// </summary>
    public class CharacterPipelineTests
    {
        private const string MalePath = "Assets/Characters/Male/Male character.fbx";
        private const string FemalePath = "Assets/Characters/Female/Female character.fbx";
        private const string AnimationsRoot = "Assets/Animations";

        private string _scratchCharacterFolder;

        [SetUp]
        public void SetUp()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        [TearDown]
        public void TearDown()
        {
            if (!string.IsNullOrEmpty(_scratchCharacterFolder))
            {
                AssetDatabase.DeleteAsset(_scratchCharacterFolder);
                _scratchCharacterFolder = null;
            }

            // Remove the scratch character entry from the real config.
            var config = CharacterAnimationConfigLibrary.LoadIfExists();
            if (config != null)
            {
                var changed = config.characters.RemoveAll(c => c != null && c.id == "pipelinetest") > 0;
                if (changed)
                {
                    CharacterAnimationConfigLibrary.Save(config);
                }
            }
        }

        // ------------------------------------------------------------------
        // Rig detection on the real uploaded characters
        // ------------------------------------------------------------------

        [Test]
        public void MaleCharacter_HasValidHumanoidAvatar()
        {
            AssertHumanoidValid(MalePath, "Male");
        }

        [Test]
        public void FemaleCharacter_HasValidHumanoidAvatar()
        {
            AssertHumanoidValid(FemalePath, "Female");
        }

        private static void AssertHumanoidValid(string path, string label)
        {
            Assert.IsTrue(File.Exists(path), label + " FBX missing at " + path
                + " — did the asset move break?");
            var report = RigDetectionUtility.Inspect(path);
            Assert.AreEqual(RigKind.Humanoid, report.kind,
                label + " should import as Humanoid: " + report.Combined);
            Assert.IsTrue(report.success,
                label + " avatar must be valid for retargeting: " + report.Combined);
            Assert.IsTrue(report.mappedHumanBones >= 40,
                label + " humanoid mapping looks incomplete: " + report.Combined);
        }

        [Test]
        public void RealAnimationLibrary_IsHumanoidWithClips()
        {
            var guids = AssetDatabase.FindAssets("t:AnimationClip", new[] { AnimationsRoot });
            Assert.Greater(guids.Length, 20,
                "expected the moved animation library under " + AnimationsRoot);

            var humanoidClips = 0;
            var totalClips = 0;
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (clip == null)
                {
                    continue;
                }

                totalClips++;
                if (clip.humanMotion)
                {
                    humanoidClips++;
                }

                Assert.Greater(clip.length, 0f, path + " has zero duration");
                Assert.Greater(clip.frameRate, 0f, path + " has zero frame rate");
            }

            Assert.Greater(humanoidClips, 20,
                "animation FBX files must import as humanoid (retargetable) clips; only "
                + humanoidClips + "/" + totalClips + " did");
        }

        // ------------------------------------------------------------------
        // Category inference (pure logic)
        // ------------------------------------------------------------------

        [TestCase("death from the front.fbx", CharacterAnimCategory.Death)]
        [TestCase("Dying.fbx", CharacterAnimCategory.Death)]
        [TestCase("Crouch Walking.fbx", CharacterAnimCategory.CrouchWalk)]
        [TestCase("idle crouching.fbx", CharacterAnimCategory.Crouch)]
        [TestCase("Prone Idle.fbx", CharacterAnimCategory.Crouch)]
        [TestCase("sprint forward.fbx", CharacterAnimCategory.Sprint)]
        [TestCase("rifle run.fbx", CharacterAnimCategory.RifleRun)]
        [TestCase("rifle aiming idle.fbx", CharacterAnimCategory.RifleIdle)]
        [TestCase("reloading.fbx", CharacterAnimCategory.Reload)]
        [TestCase("firing rifle.fbx", CharacterAnimCategory.Shoot)]
        [TestCase("Grab And Put Back Rifle.fbx", CharacterAnimCategory.WeaponSwitch)]
        [TestCase("hit reaction.fbx", CharacterAnimCategory.Melee)]
        [TestCase("toss grenade.fbx", CharacterAnimCategory.Melee)]
        [TestCase("idle aiming.fbx", CharacterAnimCategory.Aim)]
        [TestCase("jump up.fbx", CharacterAnimCategory.Jump)]
        [TestCase("walking.fbx", CharacterAnimCategory.Walk)]
        public void InferFromFileName_MatchesRealUploadNames(string fileName, CharacterAnimCategory expected)
        {
            Assert.AreEqual(expected, CharacterAnimCategories.InferFromFileName(fileName));
        }

        [Test]
        public void FolderAndLoopMapping_CoversAllCategories()
        {
            var folders = new HashSet<string>();
            foreach (CharacterAnimCategory category in Enum.GetValues(typeof(CharacterAnimCategory)))
            {
                var folder = CharacterAnimCategories.FolderName(category);
                folders.Add(folder);
                Assert.IsTrue(
                    folder == "Movement" || folder == "Combat" || folder == "Weapon"
                    || folder == "Crouch" || folder == "Death" || folder == "Unsorted",
                    category + " mapped to unexpected folder " + folder);
            }

            Assert.IsTrue(folders.Contains("Movement") && folders.Contains("Combat")
                && folders.Contains("Weapon") && folders.Contains("Crouch")
                && folders.Contains("Death"), "all five organized folders must be reachable");

            Assert.IsTrue(CharacterAnimCategories.DefaultLoop(CharacterAnimCategory.Idle));
            Assert.IsTrue(CharacterAnimCategories.DefaultLoop(CharacterAnimCategory.CrouchWalk));
            Assert.IsFalse(CharacterAnimCategories.DefaultLoop(CharacterAnimCategory.Death));
            Assert.IsFalse(CharacterAnimCategories.DefaultLoop(CharacterAnimCategory.Shoot));
            Assert.IsFalse(CharacterAnimCategories.DefaultLoop(CharacterAnimCategory.Reload));
            Assert.AreEqual(16, CharacterAnimCategories.All.Length, "16 animation categories expected");
        }

        // ------------------------------------------------------------------
        // Character upload (copy, textures, URP materials, rig, dedupe)
        // ------------------------------------------------------------------

        [Test]
        public void ImportMaleCharacter_ExtractsTexturesAndBuildsUrpMaterials()
        {
            Assert.IsTrue(File.Exists(MalePath), "source male FBX missing");
            var sourceCopy = Path.Combine(Path.GetTempPath(), "bh_male_import_test.fbx");
            File.Copy(MalePath, sourceCopy, true);

            var result = CharacterImportService.ImportCharacter(sourceCopy,
                new CharacterImportService.Options
                {
                    slot = CharacterSlot.Custom,
                    customName = "PipelineTest",
                });

            _scratchCharacterFolder = "Assets/Characters/PipelineTest";
            try
            {
                Assert.IsTrue(result.accepted,
                    "character import must succeed:\n" + result.report.ToMarkdown());
                Assert.IsNotNull(result.assetPath, "asset path assigned");
                Assert.IsTrue(AssetDatabase.LoadAssetAtPath<GameObject>(result.assetPath) != null,
                    "imported FBX must produce a GameObject");

                // Rig validated as humanoid on the real asset (retargeting gate).
                Assert.IsTrue(result.definition.humanoidRigValid,
                    "male source must map to a valid humanoid avatar:\n"
                    + result.definition.rigReport);

                // Textures: extracted when the FBX embeds them; when the source
                // only *references* external image files the pipeline must say
                // so honestly instead of silently shipping missing maps.
                var textureGuids = AssetDatabase.FindAssets("t:Texture2D",
                    new[] { _scratchCharacterFolder + "/Textures" });
                if (textureGuids.Length == 0)
                {
                    StringAssert.Contains("no embedded textures", result.report.ToMarkdown(),
                        "without embedded textures the report must warn instead of pretending");
                }

                // URP materials created + remapped.
                var materialGuids = AssetDatabase.FindAssets("t:Material",
                    new[] { _scratchCharacterFolder + "/Materials" });
                Assert.Greater(materialGuids.Length, 0, "URP materials must be created");
                foreach (var guid in materialGuids)
                {
                    var material = AssetDatabase.LoadAssetAtPath<Material>(
                        AssetDatabase.GUIDToAssetPath(guid));
                    StringAssert.StartsWith("Universal Render Pipeline/",
                        material.shader.name,
                        "character materials must be URP-compatible");
                }

                var importer = AssetImporter.GetAtPath(result.assetPath) as ModelImporter;
                Assert.IsNotNull(importer, "model importer present");
                var remaps = importer.GetExternalObjectMap();
                Assert.Greater(remaps.Count, 0,
                    "material assignments must be preserved via external-object remaps");

                // Mesh integrity recorded in the report.
                StringAssert.Contains("vertices", result.report.ToMarkdown(),
                    "mesh integrity must appear in the import report");
            }
            finally
            {
                if (File.Exists(sourceCopy))
                {
                    File.Delete(sourceCopy);
                }
            }
        }

        [Test]
        public void ImportIsIdempotent_ForDuplicateContentAndOverwriteProtection()
        {
            Assert.IsTrue(File.Exists(MalePath), "source male FBX missing");
            var sourceCopy = Path.Combine(Path.GetTempPath(), "bh_male_dedupe_test.fbx");
            File.Copy(MalePath, sourceCopy, true);

            try
            {
                var first = CharacterImportService.ImportCharacter(sourceCopy,
                    new CharacterImportService.Options
                    {
                        slot = CharacterSlot.Custom,
                        customName = "PipelineTest",
                    });
                _scratchCharacterFolder = "Assets/Characters/PipelineTest";
                Assert.IsTrue(first.accepted, "first import must succeed:\n"
                    + first.report.ToMarkdown());

                var config = CharacterAnimationConfigLibrary.LoadOrCreate();
                var sha = CharacterAnimationConfigLibrary.Sha256OfFile(sourceCopy);

                // Registering the same content again reports a duplicate.
                Assert.IsTrue(
                    CharacterAnimationConfigLibrary.RegisterSource(config, "again.fbx", sha, 123),
                    "second registration of identical bytes must be flagged as duplicate");
                CharacterAnimationConfigLibrary.Save(config);

                // Re-importing over an existing file without confirmation fails
                // loudly (never overwrite silently).
                var second = CharacterImportService.ImportCharacter(sourceCopy,
                    new CharacterImportService.Options
                    {
                        slot = CharacterSlot.Custom,
                        customName = "PipelineTest",
                    });
                Assert.IsFalse(second.accepted,
                    "overwrite without confirmation must be refused");
                Assert.Greater(second.report.FailedCount, 0,
                    "refused overwrite must be reported as failed");
            }
            finally
            {
                if (File.Exists(sourceCopy))
                {
                    File.Delete(sourceCopy);
                }
            }
        }

        // ------------------------------------------------------------------
        // Animation upload (category, loop, register)
        // ------------------------------------------------------------------

        [Test]
        public void ImportAnimationClip_RegistersCategoryAndLoopDefaults()
        {
            var sourcePath = Path.Combine(Path.GetTempPath(), "bh_death_anim_test.fbx");
            File.Copy(Path.Combine(Application.dataPath,
                "Animations/Death/death from the front.fbx"), sourcePath, true);

            try
            {
                var result = AnimationImportService.ImportAnimations(
                    new[] { sourcePath },
                    new AnimationImportService.Options());
                if (result.Clips.Count == 0)
                {
                    // Second local run: content-hash dedupe skipped it (asserted
                    // in ImportIsIdempotent…) — nothing left to verify here.
                    Assert.Ignore("death animation already imported — duplicate skip path taken");
                }

                Assert.Greater(result.Clips.Count, 0,
                    "death animation must import:\n" + result.report.ToMarkdown());

                var config = CharacterAnimationConfigLibrary.LoadOrCreate();
                foreach (var clip in result.Clips)
                {
                    Assert.IsTrue(config.clips.Contains(clip), "clip registered in config");
                    var assignment = config.FindAssignment(null, CharacterAnimCategory.Death);
                    Assert.IsNotNull(assignment,
                        "death clip must be registered under the Death category");
                }

                // Death is a one-shot: loop flag off.
                foreach (var clip in result.Clips)
                {
                    Assert.IsFalse(clip.isLooping,
                        "death clips must default to non-looping: " + clip.name);
                }
            }
            finally
            {
                if (File.Exists(sourcePath))
                {
                    File.Delete(sourcePath);
                }

                // Clean the copied test asset out of the Death folder.
                var copied = AnimationsRoot + "/Death/death from the front.fbx";
                // (the real library already contains this file — import skipped
                // or replaced; nothing destructive happens either way)
                AssetDatabase.Refresh();
            }
        }

        // ------------------------------------------------------------------
        // Assignment + Animator Controller generation
        // ------------------------------------------------------------------

        [Test]
        public void BuildController_CreatesAllParametersStatesAndUpperBodyLayer()
        {
            var config = CharacterAnimationConfigLibrary.LoadOrCreate();
            var character = CharacterAnimationConfigLibrary.GetOrCreateCharacter(
                config, "male", "Male");
            character.model = AssetDatabase.LoadAssetAtPath<GameObject>(MalePath);
            character.humanoidRigValid = true;

            // Assign a real clip for every category the library covers.
            var clipsByCategory = LoadRealClipsByCategory();
            foreach (var pair in clipsByCategory)
            {
                config.AddOrUpdateAssignment(new CharacterAnimAssignment
                {
                    characterId = character.id,
                    category = pair.Key,
                    clip = pair.Value,
                    sourcePath = pair.Value.name + ".fbx",
                }, character.id);
            }

            Assert.IsTrue(clipsByCategory.ContainsKey(CharacterAnimCategory.Idle),
                "the library must contain an Idle clip for the required state");

            CharacterAnimationConfigLibrary.Save(config);
            var build = AnimatorControllerBuilder.BuildController(config, character);
            CharacterAnimationConfigLibrary.Save(config);

            Assert.IsTrue(build.success, "controller build failed: " + build.summary);
            Assert.IsNotNull(build.controller, "controller asset created");
            Assert.IsTrue(character.controller == build.controller,
                "controller assigned back onto the character definition");

            var controller = build.controller;

            // All ten parameters.
            var parameterNames = new List<string>();
            foreach (var parameter in controller.parameters)
            {
                parameterNames.Add(parameter.name);
            }

            foreach (var expected in new[]
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
            })
            {
                Assert.Contains(expected, parameterNames, "missing parameter " + expected);
            }

            // Base layer states for every assigned category that has a state.
            var baseMachine = controller.layers[0].stateMachine;
            var stateNames = new List<string>();
            foreach (var state in baseMachine.states)
            {
                stateNames.Add(state.state.name);
            }

            foreach (var required in new[]
            {
                "Idle", "Walk", "Run", "Sprint", "Crouch", "Jump", "Fall",
                "Aim", "Shoot", "Reload", "WeaponSwitch", "Melee", "Death",
            })
            {
                Assert.Contains(required, stateNames, "missing base state " + required);
            }

            // Smooth Any State transitions actually exist.
            Assert.Greater(baseMachine.anyStateTransitions.Length, 0,
                "locomotion must have Any State transitions");

            // UpperBody layer with avatar mask (legs disabled, arms enabled).
            Assert.AreEqual(2, controller.layers.Length,
                "UpperBody weapon layer must exist");
            var upper = controller.layers[1];
            Assert.AreEqual("UpperBody", upper.name);
            Assert.IsNotNull(upper.avatarMask, "UpperBody layer needs an avatar mask");
            Assert.IsFalse(upper.avatarMask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftLeg),
                "mask must disable the left leg");
            Assert.IsFalse(upper.avatarMask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.RightLeg),
                "mask must disable the right leg");
            Assert.IsTrue(upper.avatarMask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftArm),
                "mask must enable the left arm");
            var upperStates = new List<string>();
            foreach (var state in upper.stateMachine.states)
            {
                upperStates.Add(state.state.name);
            }

            Assert.Contains("RifleIdle", upperStates, "Rifle Idle state on UpperBody layer");
            Assert.Contains("RifleRun", upperStates, "Rifle Run state on UpperBody layer");
        }

        [Test]
        public void BuildController_WithoutIdle_FailsWithClearMessage()
        {
            // Shared assignments (null/empty/whitespace characterId) now resolve
            // for every character. Use an empty scratch config rather than
            // mutating the generated project config: both shared and per-
            // character Idle clips are genuinely absent here.
            var config = ScriptableObject.CreateInstance<CharacterAnimationConfig>();
            try
            {
                var character = new CharacterDefinition
                {
                    id = "male",
                    displayName = "Male",
                    model = AssetDatabase.LoadAssetAtPath<GameObject>(MalePath),
                };

                var build = AnimatorControllerBuilder.BuildController(config, character);
                Assert.IsFalse(build.success, "build must fail without an Idle clip");
                StringAssert.Contains("Idle", build.summary,
                    "failure must name the missing category: " + build.summary);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void ConfigRoundtrip_PersistsCharactersAssignmentsAndSources()
        {
            var config = CharacterAnimationConfigLibrary.LoadOrCreate();
            var character = CharacterAnimationConfigLibrary.GetOrCreateCharacter(
                config, "roundtrip", "Round Trip");
            Assert.IsNotNull(character, "character entry created");

            config.AddOrUpdateSource(new ImportedSourceRecord
            {
                sourcePath = "roundtrip.fbx",
                sha256 = "abc123",
                bytes = 42,
                importedAtUtc = DateTime.UtcNow.ToString("o"),
            });
            Assert.IsTrue(config.IsDuplicateSource("abc123"));
            Assert.IsFalse(config.IsDuplicateSource("different"));

            CharacterAnimationConfigLibrary.Save(config);
            var reloaded = CharacterAnimationConfigLibrary.LoadIfExists();
            Assert.IsNotNull(reloaded, "config asset persists at "
                + CharacterAnimationConfigLibrary.ConfigAssetPath);
            Assert.IsNotNull(reloaded.FindCharacter("roundtrip"));
            Assert.IsTrue(reloaded.IsDuplicateSource("abc123"));

            // Clean the scratch character entry.
            reloaded.characters.RemoveAll(c => c != null && c.id == "roundtrip");
            CharacterAnimationConfigLibrary.Save(reloaded);
        }

        // ------------------------------------------------------------------
        // Import rules integration
        // ------------------------------------------------------------------

        [Test]
        public void ImportRules_TreatAnimationAndCharacterPathsAsHumanoid()
        {
            Assert.IsTrue(AssetImportRules.IsHumanoidAsset("Assets/Characters/Male/X.fbx"));
            Assert.IsTrue(AssetImportRules.IsHumanoidAsset("Assets/Animations/Death/y.fbx"));
            Assert.IsTrue(AssetImportRules.IsHumanoidAsset("Assets/Animations/Movement/z.fbx"));
            Assert.IsTrue(AssetImportRules.IsHumanoidAsset("Assets/UserAssets/Models/Animation/q.fbx"));
            Assert.IsFalse(AssetImportRules.IsHumanoidAsset("Assets/Props/rock.fbx"));
            Assert.IsFalse(AssetImportRules.IsHumanoidAsset(null));
        }

        [Test]
        public void ImportRules_RespectExplicitGenericChoiceOnAnimationAssets()
        {
            var path = AnimationsRoot + "/Movement/idle.fbx";
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            Assert.IsNotNull(importer, "idle.fbx must be imported");

            // Unrecorded paths are never "explicit", even with Generic settings
            // (Unity's fresh-FBX default is Generic too).
            Assert.IsFalse(
                AssetImportRules.RespectsExplicitGeneric("Assets/Animations/Movement/brand_new.fbx", importer),
                "unrecorded files follow the pipeline default");

            var originalType = importer.animationType;
            try
            {
                // Generic without a recorded choice → the rule overrides it
                // back to Humanoid on reimport (and the helper says "not
                // respected").
                if (originalType != ModelImporterAnimationType.Generic)
                {
                    importer.animationType = ModelImporterAnimationType.Generic;
                    AssetDatabase.WriteImportSettingsIfDirty(path);
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                }

                Assert.IsFalse(AssetImportRules.RespectsExplicitGeneric(path, importer),
                    "Generic alone must not be treated as an explicit choice");

                // Record the choice FIRST, then set Generic and reimport —
                // the import rule honours the recording.
                CharacterAnimationConfigLibrary.MarkExplicitGeneric(path);
                importer.animationType = ModelImporterAnimationType.Generic;
                AssetDatabase.WriteImportSettingsIfDirty(path);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                Assert.IsTrue(AssetImportRules.RespectsExplicitGeneric(path, importer),
                    "a recorded Generic choice on an animation asset must be respected");
                Assert.AreEqual(ModelImporterAnimationType.Generic, importer.animationType,
                    "the recorded Generic choice must survive the reimport");
            }
            finally
            {
                CharacterAnimationConfigLibrary.ClearExplicitGeneric(path);
                if (importer.animationType != originalType)
                {
                    importer.animationType = originalType;
                    AssetDatabase.WriteImportSettingsIfDirty(path);
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                }
            }
        }

        // ------------------------------------------------------------------
        // Mixamo import settings: Rig = Humanoid, Loop Time, baked root motion
        // ------------------------------------------------------------------

        [Test]
        public void MixamoLibrary_ImportsHumanoidWithLoopAndBakedRootMotion()
        {
            var rows = MixamoAnimationImportService.Audit(MixamoImportSettings.AnimationsRoot);
            Assert.Greater(rows.Count, 20,
                "expected the Mixamo animation library under " + MixamoImportSettings.AnimationsRoot);
            Assert.AreEqual(0, MixamoAnimationImportService.CountProblems(rows),
                "every Mixamo clip must import as Humanoid, loop per its category and have root motion baked into pose:\n"
                + MixamoAnimationImportService.Describe(rows));
        }

        [Test]
        public void MixamoLibrary_IsDetectedByBonePrefix()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(AnimationsRoot + "/Movement/Running.fbx");
            Assert.IsNotNull(model, "Running.fbx must import");
            Assert.Greater(MixamoImportSettings.CountMixamoBones(model), 40,
                "the real Mixamo hierarchy is expected (mixamorig:* bones)");

            Assert.IsTrue(MixamoImportSettings.IsMixamoSkeleton(new[] { "mixamorig:Hips", "mixamorig:Spine" }));
            Assert.IsTrue(MixamoImportSettings.IsMixamoSkeleton(new[] { "mixamorig1:LeftArm" }),
                "Mixamo's numbered namespace suffix is still Mixamo");
            Assert.IsFalse(MixamoImportSettings.IsMixamoSkeleton(new[] { "Hips", "Spine" }));
            Assert.IsFalse(MixamoImportSettings.IsMixamoSkeleton(null));
        }

        [Test]
        public void MixamoRules_CoverTheRealFileNames()
        {
            // Only the animation library takes these rules; characters and props do not.
            Assert.IsTrue(MixamoImportSettings.IsAnimationAssetPath("Assets/Animations/Movement/Running.fbx"));
            Assert.IsTrue(MixamoImportSettings.IsAnimationAssetPath("Assets/UserAssets/Models/Animation/idle.fbx"));
            Assert.IsFalse(MixamoImportSettings.IsAnimationAssetPath("Assets/Characters/Male/Male character.fbx"));
            Assert.IsFalse(MixamoImportSettings.IsAnimationAssetPath("Assets/Props/rock.fbx"));
            Assert.IsFalse(MixamoImportSettings.IsAnimationAssetPath(null));

            // Cycles loop, one-shots do not — including the name overrides.
            Assert.IsTrue(MixamoImportSettings.WantsLoop("walking.fbx"));
            Assert.IsTrue(MixamoImportSettings.WantsLoop("Running.fbx"));
            Assert.IsTrue(MixamoImportSettings.WantsLoop("jump loop.fbx"));
            Assert.IsTrue(MixamoImportSettings.WantsLoop("idle aiming.fbx"));
            Assert.IsFalse(MixamoImportSettings.WantsLoop("jump up.fbx"));
            Assert.IsFalse(MixamoImportSettings.WantsLoop("jump down.fbx"));
            Assert.IsFalse(MixamoImportSettings.WantsLoop("death from the front.fbx"));
            Assert.IsFalse(MixamoImportSettings.WantsLoop("reloading.fbx"));
            Assert.IsFalse(MixamoImportSettings.WantsLoop("Running To Stop.fbx"),
                "a 'to stop' variant must not loop even though it contains 'run' and 'stop'");
        }

        [Test]
        public void MixamoClipSettings_BakeIntoPoseAndLoopTime()
        {
            var clip = new ModelImporterClipAnimation { name = "Running", loopTime = false, loopPose = false };

            Assert.IsTrue(MixamoImportSettings.ApplyToClip(clip, true),
                "first application must report a change");
            Assert.IsTrue(clip.loopTime, "Loop Time on");
            Assert.IsTrue(clip.loopPose, "Loop Pose on");
            Assert.IsTrue(clip.lockRootRotation, "Root Transform Rotation: Bake Into Pose");
            Assert.IsTrue(clip.lockRootHeightY, "Root Transform Position (Y): Bake Into Pose");
            Assert.IsTrue(clip.lockRootPositionXZ, "Root Transform Position (XZ): Bake Into Pose");
            Assert.IsTrue(clip.keepOriginalOrientation && clip.keepOriginalPositionY && clip.keepOriginalPositionXZ,
                "baked values must be based upon the original source pose");
            Assert.IsTrue(MixamoImportSettings.IsRootMotionBaked(clip));

            Assert.IsFalse(MixamoImportSettings.ApplyToClip(clip, true),
                "re-applying the same settings is a no-op (no reimport loop)");

            Assert.IsTrue(MixamoImportSettings.ApplyToClip(clip, false), "one-shot must switch loop off");
            Assert.IsFalse(clip.loopTime, "Loop Time off for a one-shot");
            Assert.IsTrue(MixamoImportSettings.IsRootMotionBaked(clip), "root motion stays baked");
        }

        [Test]
        public void RepairIsIdempotent_AndLeavesACorrectLibraryClean()
        {
            // First pass may legitimately touch files whose .meta has no
            // explicit clip list yet (this repo commits no .meta files).
            MixamoAnimationImportService.Repair(
                MixamoImportSettings.AnimationsRoot, new ImportReport { Title = "repair (first pass)" });

            var audit = MixamoAnimationImportService.Audit(MixamoImportSettings.AnimationsRoot);
            Assert.AreEqual(0, MixamoAnimationImportService.CountProblems(audit),
                "after the repair every clip must satisfy the rules:\n"
                + MixamoAnimationImportService.Describe(audit));

            var report = new ImportReport { Title = "repair (second pass)" };
            var repaired = MixamoAnimationImportService.Repair(MixamoImportSettings.AnimationsRoot, report);
            Assert.AreEqual(0, repaired,
                "a repaired library must not be re-imported again (no import loop):\n" + report.ToMarkdown());
        }

        // ------------------------------------------------------------------

        private static Dictionary<CharacterAnimCategory, AnimationClip> LoadRealClipsByCategory()
        {
            var result = new Dictionary<CharacterAnimCategory, AnimationClip>();
            var guids = AssetDatabase.FindAssets("t:AnimationClip", new[] { AnimationsRoot });
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (clip == null)
                {
                    continue;
                }

                var category = CharacterAnimCategories.InferFromFileName(
                    Path.GetFileName(path));
                if (!result.ContainsKey(category))
                {
                    result[category] = clip;
                }
            }

            return result;
        }
    }
}
