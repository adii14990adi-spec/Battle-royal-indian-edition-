using System.Collections.Generic;
using System.IO;
using BattleHorizons.Characters;
using BattleHorizons.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BattleHorizons.Tests
{
    /// <summary>
    /// EditMode tests for the headless integration. These run only when a real
    /// Unity Editor is available (CI's unity job). The static Python validator
    /// covers the same contracts from disk without Unity.
    /// </summary>
    public sealed class CharacterAnimationIntegrationTests
    {
        [Test]
        public void PreferredClipNames_AllExistInTheCommittedLibrary()
        {
            // "Do not invent animation clips": every canonical preference used
            // by the headless builder must resolve to a real FBX file.
            var preferences = new Dictionary<CharacterAnimCategory, string>
            {
                { CharacterAnimCategory.Idle, "idle" },
                { CharacterAnimCategory.Walk, "walking" },
                { CharacterAnimCategory.Run, "Running" },
                { CharacterAnimCategory.Sprint, "sprint forward" },
                { CharacterAnimCategory.Jump, "jump up" },
                { CharacterAnimCategory.Fall, "jump down" },
                { CharacterAnimCategory.Crouch, "idle crouching" },
                { CharacterAnimCategory.CrouchWalk, "Crouch Walking" },
                { CharacterAnimCategory.Aim, "idle aiming" },
                { CharacterAnimCategory.RifleRun, "rifle run" },
                { CharacterAnimCategory.RifleIdle, "rifle aiming idle" },
                { CharacterAnimCategory.Shoot, "firing rifle" },
                { CharacterAnimCategory.Reload, "reloading" },
                { CharacterAnimCategory.WeaponSwitch, "Grab And Put Back Rifle" },
                { CharacterAnimCategory.Melee, "hit reaction" },
                { CharacterAnimCategory.Death, "Dying" },
            };

            foreach (var pair in preferences)
            {
                var path = MixamoImportSettings.AnimationsRoot + "/"
                           + CharacterAnimCategories.FolderName(pair.Key)
                           + "/" + pair.Value + ".fbx";
                Assert.IsTrue(File.Exists(
                        Path.Combine(Directory.GetParent(Application.dataPath).FullName, path)),
                    "preferred clip is missing: " + path);
            }
        }

        [Test]
        public void ClipPreferenceRank_IsDeterministicAndPrefersCanonicalNames()
        {
            var canonical = CharacterAnimationIntegration.ClipPreferenceRank(
                "walking.fbx", CharacterAnimCategory.Walk);
            var variant = CharacterAnimationIntegration.ClipPreferenceRank(
                "strafe (2).fbx", CharacterAnimCategory.Walk);
            Assert.Less(canonical, variant,
                "the canonical walk clip must win over a strafe variant");
            Assert.AreEqual(canonical, CharacterAnimationIntegration.ClipPreferenceRank(
                "walking.fbx", CharacterAnimCategory.Walk),
                "the same name must always produce the same rank");
        }

        [Test]
        public void TurnClips_AreOneShotsInBothTheCodeAndTheCommittedMetadata()
        {
            // PR #25 committed Loop Time off for all six turn clips. The C#
            // import hook must agree, or it silently turns one-shots into loops.
            string[] turns =
            {
                "turn 90 left.fbx", "turn 90 right.fbx", "turn left.fbx",
                "turning right 45 degrees.fbx",
                "crouching turn 90 left.fbx", "crouching turn 90 right.fbx",
            };

            for (var i = 0; i < turns.Length; i++)
            {
                Assert.IsFalse(MixamoImportSettings.WantsLoop(turns[i]),
                    turns[i] + " must not loop");
            }

            Assert.IsTrue(MixamoImportSettings.WantsLoop("walking.fbx"));
            Assert.IsTrue(MixamoImportSettings.WantsLoop("jump loop.fbx"));
            Assert.IsFalse(MixamoImportSettings.WantsLoop("Running To Stop.fbx"));
        }

        [Test]
        public void CharacterFbxFiles_HaveStableCommittedMetadata()
        {
            string[] characters =
            {
                "Assets/Characters/Male/Male character.fbx",
                "Assets/Characters/Female/Female character.fbx",
            };

            for (var i = 0; i < characters.Length; i++)
            {
                var path = characters[i];
                Assert.IsTrue(File.Exists(
                        Path.Combine(Directory.GetParent(Application.dataPath).FullName, path)),
                    path + " is missing");
                Assert.IsTrue(File.Exists(
                        Path.Combine(Directory.GetParent(Application.dataPath).FullName,
                            path + ".meta")),
                    path + ".meta is missing — GUIDs would be regenerated on reimport");
                Assert.IsFalse(string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(path)),
                    path + " must have a stable GUID");

                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                Assert.IsNotNull(importer, path + " did not import as a model");
                Assert.AreEqual(ModelImporterAnimationType.Human,
                    importer.animationType,
                    path + " must import as Humanoid for Mixamo retargeting");
            }
        }
    }
}
