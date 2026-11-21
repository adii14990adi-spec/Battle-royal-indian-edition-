using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BattleHorizons.Characters;
using UnityEditor;
using UnityEngine;

namespace BattleHorizons.EditorTools
{
    /// <summary>
    /// Headless, deterministic integration of the committed Male / Female base
    /// characters into the generated roster asset that the runtime reads.
    ///
    /// It:
    ///  * discovers every FBX under Assets/Characters (filesystem enumeration
    ///    plus AssetDatabase, so a freshly uploaded file is found either way);
    ///  * registers each source file once by SHA-256 (content dedupe);
    ///  * verifies each character's Humanoid Avatar and skinned meshes, never
    ///    forcing an incompatible rig — an explicit Generic rig is reported
    ///    and left alone;
    ///  * proves the animation library stays removed: a single FBX under
    ///    Assets/Animations fails the build instead of being silently wired up;
    ///  * preserves every original FBX file byte-for-byte.
    ///
    /// Entry points:
    ///   Battle Horizons &gt; Asset Management &gt; Build Character Integration
    ///   -executeMethod BattleHorizons.EditorTools.CharacterAnimationIntegration.BuildFromCommandLine
    ///
    /// History: this class used to import the 74 Mixamo animation clips, assign
    /// one deterministic clip per category and rebuild the generated Animator
    /// Controllers. All animation clips were removed from the project on
    /// 2026-11-20 by owner decision ("remove all animations"), so only the
    /// character roster is built now. The class keeps its historical name so CI
    /// entry points and the protected-asset lineage stay traceable.
    ///
    /// This is Editor code. It does not prove an Avatar import on a device or an
    /// Android build; those remain Editor/device tests.
    /// </summary>
    public static class CharacterAnimationIntegration
    {
        public const string MenuPath =
            "Battle Horizons/Asset Management/Build Character Integration";

        /// <summary>Build the roster integration. Returns the number of failures.</summary>
        public static int Build(out string report)
        {
            var log = new StringBuilder();
            var importReport = new ImportReport
            {
                Title = "Character Integration — headless build",
            };
            var failures = 0;
            var warnings = 0;

            var config = CharacterAnimationConfigLibrary.LoadOrCreate();
            if (config == null)
            {
                report = "could not load or create "
                         + CharacterAnimationConfigLibrary.ConfigAssetPath;
                return 1;
            }

            // ---- animation library must stay removed ----------------------
            var animationRoot = CharacterAnimationConfigLibrary.AnimationsRoot;
            var animationAbsolute = CharacterAnimationConfigLibrary.AbsolutePathOf(animationRoot);
            var strayClips = Directory.Exists(animationAbsolute)
                ? Directory.GetFiles(animationAbsolute, "*.fbx", SearchOption.AllDirectories)
                : new string[0];
            log.AppendLine("Animation FBX files found: " + strayClips.Length);
            if (strayClips.Length > 0)
            {
                importReport.Fail("clips", strayClips.Length
                    + " animation FBX file(s) reappeared under " + animationRoot
                    + "; the animation library was removed on 2026-11-20 and must stay empty");
                failures++;
            }
            else
            {
                importReport.Success("clips", "no animation FBX under " + animationRoot
                    + " (animation library removed 2026-11-20)");
            }

            // ---- characters, avatars and skinning -------------------------
            var characterPaths = CollectFbxPaths(
                new[] { CharacterAnimationConfigLibrary.CharactersRoot }, "t:Model");
            log.AppendLine("Character FBX files discovered: " + characterPaths.Count);
            if (characterPaths.Count == 0)
            {
                importReport.Fail("characters", "no character FBX found under "
                    + CharacterAnimationConfigLibrary.CharactersRoot);
                failures++;
            }

            var duplicateSources = 0;
            var imported = 0;
            for (var i = 0; i < characterPaths.Count; i++)
            {
                var path = characterPaths[i];
                var id = CharacterIdFromPath(path);
                var displayName = Path.GetFileNameWithoutExtension(path);
                var definition = CharacterAnimationConfigLibrary.GetOrCreateCharacter(
                    config, id, displayName);

                RegisterSourceOnce(config, path, ref duplicateSources);

                // Explicit Generic escape hatch: never force it to Humanoid.
                if (CharacterAnimationConfigLibrary.IsExplicitGeneric(path))
                {
                    definition.humanoidRigValid = false;
                    definition.rigReport = "explicit Generic rig recorded in "
                        + CharacterAnimationConfigLibrary.GenericRigListPath
                        + "; Humanoid retargeting is not available for this character.";
                    importReport.Warn("rig", displayName + ": " + definition.rigReport);
                    warnings++;
                    continue;
                }

                var inspection = RigDetectionUtility.Inspect(path);
                if (!inspection.success || inspection.kind != RigKind.Humanoid)
                {
                    var rigReport = new RigReport();
                    if (!RigDetectionUtility.EnsureHumanoid(path, rigReport))
                    {
                        definition.humanoidRigValid = false;
                        definition.rigReport = rigReport.Combined
                            + " | this rig cannot be Humanoid — import it as Generic "
                            + "(recorded through GenericRigs.txt) instead of forcing "
                            + "retargeting onto an incompatible skeleton";
                        importReport.Warn("rig", displayName + ": " + definition.rigReport);
                        warnings++;
                        continue;
                    }

                    inspection = RigDetectionUtility.Inspect(path);
                }

                var avatar = RigDetectionUtility.FindAvatar(path);
                if (avatar == null || !avatar.isValid || !avatar.isHuman)
                {
                    definition.humanoidRigValid = false;
                    definition.rigReport = "no valid Humanoid Avatar — configure the "
                        + "Avatar mapping in the import settings for this model";
                    importReport.Warn("rig", displayName + ": " + definition.rigReport);
                    warnings++;
                    continue;
                }

                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (model == null)
                {
                    importReport.Fail("character", path + ": no GameObject imported");
                    definition.humanoidRigValid = false;
                    failures++;
                    continue;
                }

                var skinned = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                var boneCount = 0;
                for (var s = 0; s < skinned.Length; s++)
                {
                    boneCount = Math.Max(boneCount,
                        skinned[s].sharedMesh != null
                            ? skinned[s].sharedMesh.bindposes.Length
                            : skinned[s].bones.Length);
                }

                definition.model = model;
                definition.humanoidRigValid = true;
                definition.rigReport = inspection.Combined
                    + " | skinned meshes: " + skinned.Length
                    + ", bones per mesh <= " + boneCount;
                if (skinned.Length == 0)
                {
                    definition.rigReport += " | WARNING: no SkinnedMeshRenderer found";
                    importReport.Warn("skin", displayName
                        + ": no SkinnedMeshRenderer on the imported model");
                    warnings++;
                }

                imported++;
                importReport.Success("rig", displayName + ": "
                    + inspection.mappedHumanBones + " mapped humanoid bones, "
                    + skinned.Length + " skinned mesh(es), avatar is human");
            }

            log.AppendLine("Characters imported: " + imported + "/" + characterPaths.Count
                + " (" + duplicateSources + " duplicate source file(s) skipped)");
            if (imported == 0 && characterPaths.Count > 0)
            {
                importReport.Fail("characters", "no character model could be imported");
                failures++;
            }

            config.generatedAtUtc = DateTime.UtcNow.ToString("o");
            CharacterAnimationConfigLibrary.Save(config);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            CharacterAnimationConfigLibrary.WriteReport(importReport);

            log.AppendLine("Failures: " + failures + ", warnings: " + warnings);
            log.AppendLine("Report: " + CharacterAnimationConfigLibrary.ReportPath);
            report = log.ToString();
            return failures;
        }

        [MenuItem(MenuPath, priority = 5)]
        public static void BuildFromMenu()
        {
            string report;
            var failures = Build(out report);
            Debug.Log("[bh][integration]\n" + report);
            if (failures > 0)
            {
                Debug.LogError("[bh][integration] " + failures + " failure(s)");
            }
            else
            {
                Debug.Log("[bh][integration] character integration complete");
            }

            EditorUtility.DisplayDialog("Character Integration", report, "OK");
        }

        /// <summary>Headless entry point for GitHub Actions / -executeMethod.</summary>
        public static void BuildFromCommandLine()
        {
            string report;
            var failures = Build(out report);
            Debug.Log("[bh][integration]\n" + report);
            if (failures > 0)
            {
                Debug.LogError(string.Format(
                    "[bh][integration] {0} failure(s) — see {1}",
                    failures, CharacterAnimationConfigLibrary.ReportPath));
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }

                return;
            }

            Debug.Log("[bh][integration] all character checks passed");
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(0);
            }
        }

        // ------------------------------------------------------------------

        private static List<string> CollectFbxPaths(string[] roots, string filter)
        {
            var paths = new List<string>();
            for (var i = 0; i < roots.Length; i++)
            {
                var root = roots[i];
                if (string.IsNullOrEmpty(root))
                {
                    continue;
                }

                var absolute = CharacterAnimationConfigLibrary.AbsolutePathOf(root);
                if (!Directory.Exists(absolute))
                {
                    continue;
                }

                // Filesystem enumeration covers FBXs that have not produced a
                // sub-asset of `filter` yet (e.g. a newly uploaded file).
                var files = Directory.GetFiles(absolute, "*.fbx",
                    SearchOption.AllDirectories);
                for (var f = 0; f < files.Length; f++)
                {
                    var relative = ToProjectRelative(files[f]);
                    if (relative != null && !paths.Contains(relative))
                    {
                        paths.Add(relative);
                    }
                }
            }

            for (var i = 0; i < roots.Length; i++)
            {
                if (!AssetDatabase.IsValidFolder(roots[i]))
                {
                    continue;
                }

                var guids = AssetDatabase.FindAssets(filter, new[] { roots[i] });
                for (var g = 0; g < guids.Length; g++)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guids[g]);
                    if (path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)
                        && !paths.Contains(path))
                    {
                        paths.Add(path);
                    }
                }
            }

            paths.Sort(StringComparer.OrdinalIgnoreCase);
            return paths;
        }

        private static string ToProjectRelative(string absolutePath)
        {
            var dataPath = Application.dataPath.Replace('\\', '/').TrimEnd('/');
            var normalized = absolutePath.Replace('\\', '/');
            if (!normalized.StartsWith(dataPath, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return "Assets" + normalized.Substring(dataPath.Length);
        }

        private static void RegisterSourceOnce(
            CharacterAnimationConfig config, string path, ref int duplicates)
        {
            var absolute = CharacterAnimationConfigLibrary.AbsolutePathOf(path);
            if (!File.Exists(absolute))
            {
                return;
            }

            var hash = CharacterAnimationConfigLibrary.Sha256OfFile(absolute);
            var bytes = new FileInfo(absolute).Length;
            if (CharacterAnimationConfigLibrary.RegisterSource(config, path, hash, bytes))
            {
                duplicates++;
            }
        }

        private static string CharacterIdFromPath(string assetPath)
        {
            var folder = Path.GetFileName(Path.GetDirectoryName(assetPath) ?? string.Empty);
            if (string.IsNullOrEmpty(folder))
            {
                return Path.GetFileNameWithoutExtension(assetPath).ToLowerInvariant();
            }

            var builder = new StringBuilder(folder.Length);
            for (var i = 0; i < folder.Length; i++)
            {
                var character = char.ToLowerInvariant(folder[i]);
                if (char.IsLetterOrDigit(character))
                {
                    builder.Append(character);
                }
            }

            return builder.Length > 0
                ? builder.ToString()
                : Path.GetFileNameWithoutExtension(assetPath).ToLowerInvariant();
        }
    }
}
