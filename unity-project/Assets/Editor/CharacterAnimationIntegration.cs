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
    /// Headless, deterministic integration of the committed animation library
    /// with the committed Male / Female base characters.
    ///
    /// The Character &amp; Animation Manager window is an interactive authoring
    /// tool; this class is the reproducible equivalent for CI and for anyone
    /// without a desktop Unity Editor. It:
    ///
    ///  * discovers every FBX under Assets/Animations and the upload folder by
    ///    asking AssetDatabase for its imported AnimationClips (nothing is
    ///    invented — a clip that does not import is reported, not fabricated);
    ///  * registers each clip once (reference dedupe) and each source file once
    ///    by SHA-256 (content dedupe);
    ///  * assigns exactly one deterministic clip per category as the shared
    ///    default, preferring the canonical file name for that category;
    ///  * verifies both characters' Humanoid Avatars and skinned meshes, never
    ///    forcing an incompatible rig — an explicit Generic rig is reported
    ///    and left alone;
    ///  * rebuilds each character's Animator Controller from the config so the
    ///    clip references and transitions survive an asset reimport;
    ///  * preserves every original FBX file byte-for-byte.
    ///
    /// Entry points:
    ///   Battle Horizons &gt; Asset Management &gt; Build Character &amp; Animation Integration
    ///   -executeMethod BattleHorizons.EditorTools.CharacterAnimationIntegration.BuildFromCommandLine
    ///
    /// This is Editor code. It does not prove visual playback, an Avatar import
    /// on a device, or an Android build; those remain Editor/device tests.
    /// </summary>
    public static class CharacterAnimationIntegration
    {
        public const string MenuPath =
            "Battle Horizons/Asset Management/Build Character & Animation Integration";

        private static readonly string[] AnimationRoots =
        {
            CharacterAnimationConfigLibrary.AnimationsRoot,
            MixamoImportSettings.UserAnimationRoot,
        };

        /// <summary>
        /// Canonical file-name preference per category. Every stem listed here
        /// exists in this repository; no clip name is invented. A lower rank
        /// wins when several clips classify into the same category, so a scan
        /// never depends on AssetDatabase's GUID enumeration order.
        /// </summary>
        private static readonly (CharacterAnimCategory category, string stem)[] PreferredClipNames =
        {
            (CharacterAnimCategory.Idle, "idle"),
            (CharacterAnimCategory.Walk, "walking"),
            (CharacterAnimCategory.Run, "Running"),
            (CharacterAnimCategory.Sprint, "sprint forward"),
            (CharacterAnimCategory.Jump, "jump up"),
            (CharacterAnimCategory.Fall, "jump down"),
            (CharacterAnimCategory.Crouch, "idle crouching"),
            (CharacterAnimCategory.CrouchWalk, "Crouch Walking"),
            (CharacterAnimCategory.Aim, "idle aiming"),
            (CharacterAnimCategory.RifleRun, "rifle run"),
            (CharacterAnimCategory.RifleIdle, "rifle aiming idle"),
            (CharacterAnimCategory.Shoot, "firing rifle"),
            (CharacterAnimCategory.Reload, "reloading"),
            (CharacterAnimCategory.WeaponSwitch, "Grab And Put Back Rifle"),
            (CharacterAnimCategory.Melee, "hit reaction"),
            (CharacterAnimCategory.Death, "Dying"),
        };

        private sealed class ClipCandidate
        {
            public string assetPath;
            public AnimationClip clip;
            public CharacterAnimCategory category;
            public int rank;
        }

        /// <summary>Deterministic preference: lower is better.</summary>
        public static int ClipPreferenceRank(string fileName, CharacterAnimCategory category)
        {
            var stem = Path.GetFileNameWithoutExtension(fileName ?? string.Empty) ?? string.Empty;
            for (var i = 0; i < PreferredClipNames.Length; i++)
            {
                if (PreferredClipNames[i].category == category
                    && string.Equals(PreferredClipNames[i].stem, stem,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            // Non-canonical variants (strafe left, run backward left, ...) are
            // still registered in config.clips; they just do not win the shared
            // default. Length + ordinal path is a stable tie-break.
            return 1000 + stem.Length;
        }

        /// <summary>Build the whole integration. Returns the number of failures.</summary>
        public static int Build(out string report)
        {
            var log = new StringBuilder();
            var importReport = new ImportReport
            {
                Title = "Character & Animation Integration — headless build",
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

            // ---- discover animation clips ---------------------------------
            var animationPaths = CollectFbxPaths(AnimationRoots, "t:AnimationClip");
            log.AppendLine("Animation FBX files discovered: " + animationPaths.Count);
            if (animationPaths.Count == 0)
            {
                importReport.Fail("clips", "no animation FBX with an imported "
                    + "AnimationClip was found under " + string.Join(", ", AnimationRoots));
                failures++;
            }

            var candidates = new List<ClipCandidate>();
            var newClips = 0;
            var duplicateSources = 0;
            for (var i = 0; i < animationPaths.Count; i++)
            {
                var path = animationPaths[i];
                var clips = AnimationImportService.LoadClipsFromFbx(path);
                if (clips.Length == 0)
                {
                    importReport.Warn("clips", path + ": no AnimationClip imported "
                        + "(Generic rig or no animation take) — skipped");
                    warnings++;
                    continue;
                }

                RegisterSourceOnce(config, path, ref duplicateSources);
                var category = CharacterAnimCategories.InferFromFileName(
                    Path.GetFileName(path));

                for (var c = 0; c < clips.Length; c++)
                {
                    var clip = clips[c];
                    if (clip == null)
                    {
                        continue;
                    }

                    var before = config.clips.Count;
                    config.AddClip(clip);
                    if (config.clips.Count > before)
                    {
                        newClips++;
                    }

                    candidates.Add(new ClipCandidate
                    {
                        assetPath = path,
                        clip = clip,
                        category = category,
                        rank = ClipPreferenceRank(Path.GetFileName(path), category),
                    });
                }
            }

            candidates.Sort((a, b) =>
            {
                var byRank = a.rank.CompareTo(b.rank);
                return byRank != 0
                    ? byRank
                    : string.Compare(a.assetPath, b.assetPath, StringComparison.Ordinal);
            });

            // ---- one assignment per category, deterministic ---------------
            var assigned = new HashSet<CharacterAnimCategory>();
            for (var i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                if (!assigned.Add(candidate.category))
                {
                    continue;
                }

                // A hand-made assignment from the Manager window always wins;
                // a dangling clip (deleted asset) is replaced.
                var existing = config.FindAssignment(null, candidate.category);
                if (existing != null && existing.clip != null)
                {
                    continue;
                }

                config.AddOrUpdateAssignment(new CharacterAnimAssignment
                {
                    characterId = null,
                    category = candidate.category,
                    clip = candidate.clip,
                    sourcePath = Path.GetFileName(candidate.assetPath),
                    duration = candidate.clip.length,
                    frameRate = candidate.clip.frameRate,
                    loop = candidate.clip.isLooping,
                    retargetable = candidate.clip.humanMotion,
                }, characterId: null);
            }

            var duplicateAssignments = CountDuplicateAssignments(config);
            if (duplicateAssignments > 0)
            {
                importReport.Fail("assignments", duplicateAssignments
                    + " duplicate (characterId, category) entries");
                failures++;
            }

            log.AppendLine("Clips registered: " + config.clips.Count
                + " (" + newClips + " new, " + duplicateSources
                + " duplicate source file(s) skipped)");
            log.AppendLine("Category assignments: " + config.assignments.Count
                + ", duplicate entries: " + duplicateAssignments);
            importReport.Success("clips", config.clips.Count
                + " clip(s) registered from " + animationPaths.Count
                + " FBX; " + newClips + " new, " + duplicateSources
                + " duplicate source(s) skipped; " + config.assignments.Count
                + " category assignment(s)");

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

            for (var i = 0; i < characterPaths.Count; i++)
            {
                var path = characterPaths[i];
                var id = CharacterIdFromPath(path);
                var displayName = Path.GetFileNameWithoutExtension(path);
                var definition = CharacterAnimationConfigLibrary.GetOrCreateCharacter(
                    config, id, displayName);

                // Explicit Generic escape hatch: never force it to Humanoid.
                if (CharacterAnimationConfigLibrary.IsExplicitGeneric(path))
                {
                    definition.humanoidRigValid = false;
                    definition.controller = null;
                    definition.rigReport = "explicit Generic rig recorded in "
                        + CharacterAnimationConfigLibrary.GenericRigListPath
                        + "; Humanoid retargeting is not available for this character. "
                        + "Use a Generic Animator Controller or fix the skeleton.";
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
                        definition.controller = null;
                        definition.rigReport = rigReport.Combined
                            + " | this rig cannot be Humanoid — import it as Generic "
                            + "(the Manager window's Generic option) instead of "
                            + "forcing retargeting onto an incompatible skeleton";
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
                    definition.controller = null;
                    definition.rigReport = "no valid Humanoid Avatar — Mixamo clips "
                        + "cannot play on this model; import as Generic or configure "
                        + "the Avatar mapping in the Rig tab";
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

                importReport.Success("rig", displayName + ": "
                    + inspection.mappedHumanBones + " mapped humanoid bones, "
                    + skinned.Length + " skinned mesh(es), avatar is human");
            }

            // ---- controllers ----------------------------------------------
            var built = 0;
            for (var i = 0; i < config.characters.Count; i++)
            {
                var character = config.characters[i];
                if (character == null || !character.humanoidRigValid
                    || character.model == null)
                {
                    continue;
                }

                var build = AnimatorControllerBuilder.BuildController(config, character);
                if (!build.success)
                {
                    character.controller = null;
                    importReport.Warn("controller", build.summary);
                    warnings++;
                    continue;
                }

                character.controller = build.controller;
                built++;
                importReport.Success("controller", build.summary);
                for (var w = 0; w < build.warnings.Count; w++)
                {
                    importReport.Warn("controller", build.warnings[w]);
                    warnings++;
                }
            }

            log.AppendLine("Animator Controllers built: " + built + "/"
                + config.characters.Count);
            if (built == 0)
            {
                importReport.Fail("controller", "no Animator Controller could be "
                    + "built — Idle is a required category");
                failures++;
            }

            int missingReferences;
            var referenceReport = config.ValidateReferences(out missingReferences);
            log.AppendLine("Reference check: " + referenceReport);
            if (missingReferences > 0)
            {
                importReport.Fail("references", referenceReport);
                failures += missingReferences;
            }
            else
            {
                importReport.Success("references", referenceReport);
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
                Debug.Log("[bh][integration] character & animation integration complete");
            }

            EditorUtility.DisplayDialog("Character & Animation Integration",
                report, "OK");
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

            Debug.Log("[bh][integration] all character & animation checks passed");
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

        private static int CountDuplicateAssignments(CharacterAnimationConfig config)
        {
            var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var duplicates = 0;
            for (var i = 0; i < config.assignments.Count; i++)
            {
                var assignment = config.assignments[i];
                if (assignment == null)
                {
                    continue;
                }

                var key = (assignment.characterId ?? "<shared>") + "/"
                          + assignment.category;
                if (!keys.Add(key))
                {
                    duplicates++;
                }
            }

            return duplicates;
        }
    }
}
