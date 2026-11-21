using System;
using System.IO;
using BattleHorizons.Characters;
using BattleHorizons.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BattleHorizons.Tests
{
    /// <summary>
    /// End-to-end tests for the character import pipeline on the REAL uploaded
    /// assets (Male / Female FBX): rig validity, upload + texture extraction +
    /// URP materials, duplicate prevention, roster round-trip and the headless
    /// integration builder. Nothing here is mocked — if the character import is
    /// broken these tests fail instead of the game shipping a fake.
    ///
    /// History: this file also proved the 74-clip Mixamo animation library
    /// (Humanoid clip import, category inference, loop defaults and generated
    /// Animator Controllers). All animation clips were removed from the project
    /// on 2026-11-20 by owner decision, so those tests were deleted and
    /// replaced by a regression test that proves the clip library stays gone.
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

            // Remove scratch character entries from the real config.
            var config = CharacterAnimationConfigLibrary.LoadIfExists();
            if (config != null)
            {
                var changed = config.characters.RemoveAll(
                    c => c != null && (c.id == "pipelinetest" || c.id == "roundtrip")) > 0;
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
                label + " avatar must be valid: " + report.Combined);
            Assert.IsTrue(report.mappedHumanBones >= 40,
                label + " humanoid mapping looks incomplete: " + report.Combined);
        }

        // ------------------------------------------------------------------
        // Animation removal regression
        // ------------------------------------------------------------------

        [Test]
        public void AnimationLibrary_IsRemovedAndStaysRemoved()
        {
            var absolute = CharacterAnimationConfigLibrary.AbsolutePathOf(AnimationsRoot);
            if (Directory.Exists(absolute))
            {
                var files = Directory.GetFiles(absolute, "*.fbx", SearchOption.AllDirectories);
                Assert.AreEqual(0, files.Length,
                    "the animation FBX library was removed on 2026-11-20 — "
                    + files.Length + " clip(s) reappeared under " + AnimationsRoot);
            }

            var clipGuids = AssetDatabase.FindAssets("t:AnimationClip",
                new[] { "Assets/Characters", "Assets/Generated" });
            Assert.AreEqual(0, clipGuids.Length,
                "no generated AnimationClip may be wired into the character roster");
        }

        [Test]
        public void IntegrationBuilder_ProducesRosterWithoutClips()
        {
            string report;
            var failures = CharacterAnimationIntegration.Build(out report);
            Assert.AreEqual(0, failures,
                "headless character integration must pass:\n" + report);

            var config = CharacterAnimationConfigLibrary.LoadIfExists();
            Assert.IsNotNull(config, "config asset exists at "
                + CharacterAnimationConfigLibrary.ConfigAssetPath);

            var male = config.FindCharacter("male");
            var female = config.FindCharacter("female");
            Assert.IsNotNull(male, "male roster entry present");
            Assert.IsNotNull(female, "female roster entry present");
            Assert.IsNotNull(male.model, "male model reference wired");
            Assert.IsNotNull(female.model, "female model reference wired");
            Assert.IsTrue(male.humanoidRigValid, "male rig report: " + male.rigReport);
            Assert.IsTrue(female.humanoidRigValid, "female rig report: " + female.rigReport);

            var selected = config.SelectedCharacter();
            Assert.IsNotNull(selected, "a selected character is always resolvable");
        }

        // ------------------------------------------------------------------
        // Character upload (textures, URP materials, mesh integrity)
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

                // Rig validated as humanoid on the real asset.
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
        // Config persistence
        // ------------------------------------------------------------------

        [Test]
        public void ConfigRoundtrip_PersistsCharactersAndSources()
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
        public void ImportRules_TreatCharacterPathsAsHumanoid()
        {
            Assert.IsTrue(AssetImportRules.IsHumanoidAsset("Assets/Characters/Male/X.fbx"));
            Assert.IsTrue(AssetImportRules.IsHumanoidAsset("Assets/UserAssets/Models/Animation/q.fbx"));
            Assert.IsFalse(AssetImportRules.IsHumanoidAsset("Assets/Props/rock.fbx"));
            Assert.IsFalse(AssetImportRules.IsHumanoidAsset(null));

            // The animation clip library is gone, so Assets/Animations is no
            // longer a Humanoid import folder.
            Assert.IsFalse(AssetImportRules.IsHumanoidAsset("Assets/Animations/Movement/z.fbx"),
                "Assets/Animations must not be treated as a Humanoid clip folder any more");
        }

        [Test]
        public void ImportRules_RespectExplicitGenericChoiceOnCharacterAssets()
        {
            var path = MalePath;
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            Assert.IsNotNull(importer, "the male character FBX must be imported");

            // Unrecorded paths are never "explicit", even with Generic settings
            // (Unity's fresh-FBX default is Generic too).
            Assert.IsFalse(
                AssetImportRules.RespectsExplicitGeneric("Assets/Characters/Male/brand_new.fbx", importer),
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
                    "a recorded Generic choice on a character asset must be respected");
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
    }
}
