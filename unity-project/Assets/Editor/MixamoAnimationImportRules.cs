using System;
using System.Collections.Generic;
using System.IO;
using BattleHorizons.Characters;
using UnityEditor;
using UnityEngine;

namespace BattleHorizons.EditorTools
{
    /// <summary>
    /// The three Mixamo import settings that decide whether a downloaded
    /// animation plays on a Battle Horizons character at all:
    ///
    ///  1. <b>Rig tab</b> — Animation Type "Generic" → "Humanoid", so the clip
    ///     is retargeted onto the character's avatar instead of staying bound
    ///     to Mixamo's own skeleton (a Generic clip can never drive a Humanoid
    ///     character, it is simply ignored by the animator);
    ///  2. <b>Animation tab</b> — Loop Time on for cycles (Idle/Walk/Run/…),
    ///     off for one-shots (Death/Shoot/Reload/…), and Root Transform
    ///     Rotation / Position (Y, XZ) baked into pose so the clip cannot
    ///     slide or spin the character while ThirdPersonMotor owns the
    ///     transform (the Animator runs with applyRootMotion = false);
    ///  3. <b>Animator Controller</b> — the clip is referenced by a state; the
    ///     generated controllers do this from CharacterAnimationConfig, so the
    ///     Assign tab (or the Save button) is the equivalent of dragging the
    ///     clip into the controller.
    ///
    /// These rules live in code, not in the Inspector, on purpose: this
    /// repository commits no .meta files, so anything ticked by hand is gone
    /// after the next clone or CI import and the whole Mixamo library would
    /// come back as Generic again.
    /// </summary>
    public static class MixamoImportSettings
    {
        public const string AnimationsRoot = "Assets/Animations";
        public const string UserAnimationRoot = "Assets/UserAssets/Models/Animation";

        /// <summary>Every Mixamo export names its bones "mixamorig:…".</summary>
        public const string MixamoBonePrefix = "mixamorig";

        // File names that override the category's loop default. Mixamo's own
        // export names are exactly this literal, e.g. "jump loop.fbx" and
        // "Running To Stop.fbx".
        private static readonly string[] ForceLoopTokens = { "loop", "cycle" };
        private static readonly string[] ForceOneShotTokens =
        {
            "to stop", "to idle", "stop", "one shot", "one-shot", "land",
        };

        /// <summary>
        /// True for the Mixamo animation library (and the legacy UserAssets
        /// animation folder). Character FBXs are deliberately excluded: their
        /// rig comes from CharacterImportService, not from these clip rules.
        /// </summary>
        public static bool IsAnimationAssetPath(string path)
        {
            if (string.IsNullOrEmpty(path) || !path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var normalized = path.Replace('\\', '/');
            if (normalized.StartsWith(AnimationsRoot + "/", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, AnimationsRoot, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return normalized.StartsWith(UserAnimationRoot + "/", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// "mixamorig:Hips" / "mixamorig1:LeftArm" → "mixamorighips" /
        /// "mixamorig1leftarm": lowercase, separators removed, so the check is
        /// stable across the FBX namespace suffix Mixamo adds for multi-model
        /// exports.
        /// </summary>
        public static string NormalizeBoneName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return string.Empty;
            }

            var builder = new System.Text.StringBuilder(name.Length);
            for (var i = 0; i < name.Length; i++)
            {
                var character = char.ToLowerInvariant(name[i]);
                if (character == ':' || character == '_' || character == '-' || character == ' ')
                {
                    continue;
                }

                builder.Append(character);
            }

            return builder.ToString();
        }

        /// <summary>True when at least one bone is named "mixamorig*".</summary>
        public static bool IsMixamoSkeleton(IEnumerable<string> boneNames)
        {
            if (boneNames == null)
            {
                return false;
            }

            foreach (var name in boneNames)
            {
                if (NormalizeBoneName(name).StartsWith(MixamoBonePrefix, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Number of "mixamorig*" transforms in an imported model.</summary>
        public static int CountMixamoBones(GameObject model)
        {
            if (model == null)
            {
                return 0;
            }

            var transforms = model.GetComponentsInChildren<Transform>(true);
            var count = 0;
            for (var i = 0; i < transforms.Length; i++)
            {
                if (NormalizeBoneName(transforms[i].name).StartsWith(MixamoBonePrefix, StringComparison.Ordinal))
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>Short label for the import report ("Mixamo (mixamorig:*)").</summary>
        public static string DescribeSkeleton(GameObject model)
        {
            var mixamoBones = CountMixamoBones(model);
            if (mixamoBones > 0)
            {
                return "Mixamo skeleton (" + mixamoBones + " mixamorig bone(s))";
            }

            return "non-Mixamo skeleton";
        }

        public static CharacterAnimCategory CategoryFor(string fileName)
        {
            return CharacterAnimCategories.InferFromFileName(fileName);
        }

        /// <summary>
        /// Explicit file-name hint (true = loop, false = one-shot, null = use
        /// the category default). Ordered: a "loop" in the name wins over a
        /// "stop"/"to idle" token because Mixamo names repeats literally.
        /// </summary>
        public static bool? LoopHint(string fileName)
        {
            var name = (fileName ?? string.Empty).ToLowerInvariant();
            for (var i = 0; i < ForceLoopTokens.Length; i++)
            {
                if (name.Contains(ForceLoopTokens[i]))
                {
                    return true;
                }
            }

            for (var i = 0; i < ForceOneShotTokens.Length; i++)
            {
                if (name.Contains(ForceOneShotTokens[i]))
                {
                    return false;
                }
            }

            return null;
        }

        /// <summary>Loop flag a clip of this category/file must end up with.</summary>
        public static bool WantsLoop(CharacterAnimCategory category, string fileName)
        {
            var hint = LoopHint(fileName);
            return hint ?? CharacterAnimCategories.DefaultLoop(category);
        }

        public static bool WantsLoop(string fileName)
        {
            return WantsLoop(CategoryFor(fileName), fileName);
        }

        /// <summary>
        /// Apply the Animation-tab settings to one clip. Returns true when any
        /// value actually changed (so callers only re-import when needed).
        ///
        /// Loop Time / Loop Pose follow the category; Root Transform Rotation,
        /// Position Y and Position XZ are baked into pose and based upon the
        /// original source values — the exact combination Unity's own humanoid
        /// default uses, made explicit so a stray unchecked box cannot survive.
        /// </summary>
        public static bool ApplyToClip(ModelImporterClipAnimation clip, bool loop)
        {
            if (clip == null)
            {
                return false;
            }

            var changed = false;

            if (clip.loopTime != loop)
            {
                clip.loopTime = loop;
                changed = true;
            }

            if (clip.loopPose != loop)
            {
                clip.loopPose = loop;
                changed = true;
            }

            if (!clip.keepOriginalOrientation)
            {
                clip.keepOriginalOrientation = true;
                changed = true;
            }

            if (!clip.keepOriginalPositionY)
            {
                clip.keepOriginalPositionY = true;
                changed = true;
            }

            if (!clip.keepOriginalPositionXZ)
            {
                clip.keepOriginalPositionXZ = true;
                changed = true;
            }

            if (!clip.lockRootRotation)
            {
                clip.lockRootRotation = true;
                changed = true;
            }

            if (!clip.lockRootHeightY)
            {
                clip.lockRootHeightY = true;
                changed = true;
            }

            if (!clip.lockRootPositionXZ)
            {
                clip.lockRootPositionXZ = true;
                changed = true;
            }

            return changed;
        }

        /// <summary>True when the clip's root motion is baked into the pose.</summary>
        public static bool IsRootMotionBaked(ModelImporterClipAnimation clip)
        {
            return clip != null
                && clip.lockRootRotation
                && clip.lockRootHeightY
                && clip.lockRootPositionXZ;
        }

        /// <summary>
        /// Read the clip settings Unity will use for this importer. When the
        /// .meta carries no explicit clip list, Unity applies its own defaults
        /// (humanoid: loop off, root motion baked), which is what
        /// <c>defaultClipAnimations</c> returns.
        /// </summary>
        public static ModelImporterClipAnimation[] ReadClipSettings(
            ModelImporter importer, out bool areUnityDefaults)
        {
            areUnityDefaults = true;
            if (importer == null)
            {
                return Array.Empty<ModelImporterClipAnimation>();
            }

            var configured = importer.clipAnimations;
            if (configured != null && configured.Length > 0)
            {
                areUnityDefaults = false;
                return configured;
            }

            return importer.defaultClipAnimations ?? Array.Empty<ModelImporterClipAnimation>();
        }

        /// <summary>
        /// The settings entry for a clip. Mixamo exports one take per file, so
        /// a single-entry list matches even when Unity renamed the clip.
        /// </summary>
        public static ModelImporterClipAnimation FindClipSettings(
            ModelImporterClipAnimation[] clips, string clipName)
        {
            if (clips == null || clips.Length == 0)
            {
                return null;
            }

            for (var i = 0; i < clips.Length; i++)
            {
                if (clips[i] != null
                    && string.Equals(clips[i].name, clipName, StringComparison.OrdinalIgnoreCase))
                {
                    return clips[i];
                }
            }

            return clips.Length == 1 ? clips[0] : null;
        }

        /// <summary>
        /// Write the rig + clip settings onto an importer without re-importing.
        /// Returns true when the caller should persist them
        /// (WriteImportSettingsIfDirty + ImportAsset). Idempotent: a second
        /// call on a configured importer returns false.
        /// </summary>
        public static bool ApplyToImporter(ModelImporter importer, string fileName)
        {
            if (importer == null)
            {
                return false;
            }

            var clips = importer.clipAnimations;
            var fromDefaults = clips == null || clips.Length == 0;
            if (fromDefaults)
            {
                clips = importer.defaultClipAnimations;
            }

            if (clips == null || clips.Length == 0)
            {
                return false;
            }

            var loop = WantsLoop(fileName);
            var changed = false;
            for (var i = 0; i < clips.Length; i++)
            {
                if (ApplyToClip(clips[i], loop))
                {
                    changed = true;
                }
            }

            if (changed || fromDefaults)
            {
                // Assigning the array back is what persists the flags into the
                // FBX's .meta — mutating the elements alone does nothing.
                importer.clipAnimations = clips;
                return true;
            }

            return false;
        }

        /// <summary>One-line description of a clip for reports.</summary>
        public static string DescribeClip(ModelImporterClipAnimation clip, bool loop)
        {
            if (clip == null)
            {
                return "no clip settings";
            }

            return string.Format("{0} · {1} · {2} · root motion {3}",
                clip.name,
                loop ? "loop" : "one-shot",
                clip.loopTime ? "LoopTime on" : "LoopTime off",
                IsRootMotionBaked(clip) ? "baked into pose" : "NOT baked");
        }
    }

    /// <summary>One audited clip: what it is and what is still wrong with it.</summary>
    public sealed class MixamoClipAudit
    {
        public string assetPath;
        public string clipName;
        public string category;
        public bool humanMotion;
        public bool looping;
        public bool expectedLoop;
        public bool rootMotionBaked;
        public bool explicitGeneric;
        public readonly List<string> problems = new List<string>();

        public bool Clean
        {
            get { return problems.Count == 0; }
        }
    }

    /// <summary>
    /// Audits and repairs the whole Mixamo animation library against
    /// <see cref="MixamoImportSettings"/>:
    ///
    ///  * every animation FBX imports as Humanoid (Rig tab),
    ///  * every cycle loops and every one-shot does not (Animation tab),
    ///  * root motion is baked into pose so the clips cannot slide the
    ///    character (Animation tab),
    ///  * files recorded as an explicit Generic rig are reported and left
    ///    untouched — the manual escape hatch always wins.
    ///
    /// The same audit is the CI gate (<c>ProjectValidator</c>) and the EditMode
    /// test, so "settings are right" is a checked fact, not a claim.
    /// </summary>
    public static class MixamoAnimationImportService
    {
        /// <summary>Every FBX path under <paramref name="root"/> that holds clips.</summary>
        public static List<string> CollectAnimationModelPaths(string root)
        {
            var paths = new List<string>();
            if (string.IsNullOrEmpty(root) || !AssetDatabase.IsValidFolder(root))
            {
                return paths;
            }

            var guids = AssetDatabase.FindAssets("t:AnimationClip", new[] { root });
            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (!MixamoImportSettings.IsAnimationAssetPath(path) || paths.Contains(path))
                {
                    continue;
                }

                paths.Add(path);
            }

            paths.Sort(StringComparer.OrdinalIgnoreCase);
            return paths;
        }

        /// <summary>Inspect every clip without changing anything.</summary>
        public static List<MixamoClipAudit> Audit(string root)
        {
            var rows = new List<MixamoClipAudit>();
            var paths = CollectAnimationModelPaths(root);
            for (var i = 0; i < paths.Count; i++)
            {
                var path = paths[i];
                var fileName = Path.GetFileName(path);
                var category = MixamoImportSettings.CategoryFor(fileName);
                var expectedLoop = MixamoImportSettings.WantsLoop(category, fileName);
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;

                if (importer == null)
                {
                    var unimportable = new MixamoClipAudit
                    {
                        assetPath = path,
                        clipName = fileName,
                        category = category.ToString(),
                        expectedLoop = expectedLoop,
                    };
                    unimportable.problems.Add("no ModelImporter — the FBX did not import");
                    rows.Add(unimportable);
                    continue;
                }

                var clips = AnimationImportService.LoadClipsFromFbx(path);
                if (clips.Length == 0)
                {
                    var empty = new MixamoClipAudit
                    {
                        assetPath = path,
                        clipName = fileName,
                        category = category.ToString(),
                        expectedLoop = expectedLoop,
                    };
                    empty.problems.Add("the FBX contains no AnimationClip");
                    rows.Add(empty);
                    continue;
                }

                var explicitGeneric = CharacterAnimationConfigLibrary.IsExplicitGeneric(path);
                var humanoidImporter = importer.animationType == ModelImporterAnimationType.Human;
                var settings = MixamoImportSettings.ReadClipSettings(importer, out var settingsAreDefaults);

                for (var c = 0; c < clips.Length; c++)
                {
                    var clip = clips[c];
                    var entry = MixamoImportSettings.FindClipSettings(settings, clip.name);
                    var row = new MixamoClipAudit
                    {
                        assetPath = path,
                        clipName = clip.name,
                        category = category.ToString(),
                        expectedLoop = expectedLoop,
                        humanMotion = clip.humanMotion,
                        looping = clip.isLooping,
                        explicitGeneric = explicitGeneric,
                        rootMotionBaked = entry != null
                            ? MixamoImportSettings.IsRootMotionBaked(entry)
                            : settingsAreDefaults && humanoidImporter,
                    };

                    if (explicitGeneric)
                    {
                        // Manual escape hatch: reported, never forced.
                        rows.Add(row);
                        continue;
                    }

                    if (!humanoidImporter)
                    {
                        row.problems.Add("Rig tab: Animation Type is " + importer.animationType
                            + " — must be Humanoid");
                    }
                    else if (!clip.humanMotion)
                    {
                        row.problems.Add("Rig tab: clip imported as generic motion — must be Humanoid");
                    }

                    if (clip.isLooping != expectedLoop)
                    {
                        row.problems.Add(string.Format(
                            "Animation tab: Loop Time must be {0} for {1}",
                            expectedLoop ? "on" : "off", category));
                    }

                    if (!row.rootMotionBaked)
                    {
                        row.problems.Add(
                            "Animation tab: Root Transform Rotation/Position (Y, XZ) are not baked into pose");
                    }

                    rows.Add(row);
                }
            }

            return rows;
        }

        public static int CountProblems(IList<MixamoClipAudit> rows)
        {
            if (rows == null)
            {
                return 0;
            }

            var count = 0;
            for (var i = 0; i < rows.Count; i++)
            {
                count += rows[i].problems.Count;
            }

            return count;
        }

        /// <summary>Human-readable list of offenders (report / assertion message).</summary>
        public static string Describe(IList<MixamoClipAudit> rows)
        {
            if (rows == null || rows.Count == 0)
            {
                return "no animation clips audited";
            }

            var builder = new System.Text.StringBuilder();
            var clean = 0;
            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (row.Clean)
                {
                    clean++;
                    continue;
                }

                builder.Append(Path.GetFileName(row.assetPath))
                    .Append(" · ").Append(row.clipName);
                for (var p = 0; p < row.problems.Count; p++)
                {
                    builder.Append("\n    - ").Append(row.problems[p]);
                }

                builder.Append('\n');
            }

            builder.Append(string.Format("{0}/{1} clip(s) clean", clean, rows.Count));
            return builder.ToString();
        }

        /// <summary>
        /// Repair every animation FBX under <paramref name="root"/>: Humanoid
        /// rig where the importer is not humanoid, loop flags per category and
        /// file name, root motion baked into pose. Returns the number of files
        /// re-imported (0 on an already-correct library).
        /// </summary>
        public static int Repair(string root, ImportReport report)
        {
            var repaired = 0;
            var keptGeneric = 0;
            var paths = CollectAnimationModelPaths(root);

            for (var i = 0; i < paths.Count; i++)
            {
                var path = paths[i];
                var fileName = Path.GetFileName(path);

                if (CharacterAnimationConfigLibrary.IsExplicitGeneric(path))
                {
                    keptGeneric++;
                    if (report != null)
                    {
                        report.Warn("repair", fileName
                            + ": recorded as an explicit Generic rig — left untouched");
                    }

                    continue;
                }

                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null)
                {
                    if (report != null)
                    {
                        report.Fail("repair", fileName + ": no ModelImporter");
                    }

                    continue;
                }

                var rigChanged = false;
                if (importer.animationType != ModelImporterAnimationType.Human)
                {
                    importer.animationType = ModelImporterAnimationType.Human;
                    importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                    importer.importAnimation = true;
                    rigChanged = true;
                }

                var clipsChanged = MixamoImportSettings.ApplyToImporter(importer, fileName);
                if (!rigChanged && !clipsChanged)
                {
                    continue;
                }

                AssetDatabase.WriteImportSettingsIfDirty(path);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                repaired++;

                if (report != null)
                {
                    var loop = MixamoImportSettings.WantsLoop(fileName);
                    report.Success("repair", fileName + ": Humanoid rig · "
                        + (loop ? "Loop Time on" : "one-shot") + " · " + RootMotionBakedMessage());
                }
            }

            if (report != null)
            {
                report.Success("repair", string.Format(
                    "{0} of {1} animation file(s) repaired under {2}; {3} explicit Generic rig(s) kept",
                    repaired, paths.Count, root, keptGeneric));
            }

            return repaired;
        }

        private static string RootMotionBakedMessage()
        {
            return "root motion baked into pose (Rotation, Position Y, Position XZ)";
        }
    }

    /// <summary>
    /// Import-time enforcement of the rules above, so a fresh clone (no
    /// committed .meta files) never comes back with Generic Mixamo clips.
    /// </summary>
    public sealed class MixamoAnimationImportRules : AssetPostprocessor
    {
        internal void OnPreprocessModel()
        {
            var importer = assetImporter as ModelImporter;
            if (importer == null || !MixamoImportSettings.IsAnimationAssetPath(assetPath))
            {
                return;
            }

            if (CharacterAnimationConfigLibrary.IsExplicitGeneric(assetPath))
            {
                return; // recorded Generic choice — never overridden
            }

            // Rig tab equivalent: Generic (Unity's default for a downloaded
            // FBX) → Humanoid, with an avatar generated from this model so the
            // clips carry humanoid motion and can retarget onto the character.
            if (importer.animationType != ModelImporterAnimationType.Human)
            {
                importer.animationType = ModelImporterAnimationType.Human;
            }

            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;
        }

        internal void OnPreprocessAnimation()
        {
            var importer = assetImporter as ModelImporter;
            if (importer == null || !MixamoImportSettings.IsAnimationAssetPath(assetPath))
            {
                return;
            }

            if (CharacterAnimationConfigLibrary.IsExplicitGeneric(assetPath))
            {
                return;
            }

            MixamoImportSettings.ApplyToImporter(importer, Path.GetFileName(assetPath));
        }
    }

    /// <summary>
    /// One-click equivalent of the manual Mixamo fix-up loop: Rig → Humanoid,
    /// Animation → Loop Time + Bake Into Pose, then rebuild the generated
    /// Animator Controllers so the repaired clips are actually referenced by a
    /// state (step 3 of the same loop).
    /// </summary>
    public static class MixamoAnimationFixMenu
    {
        public const string MenuPath = "Battle Horizons/Asset Management/Repair Mixamo Import Settings";

        [MenuItem(MenuPath, priority = 12)]
        public static void RepairAll()
        {
            var report = new ImportReport
            {
                Title = "Mixamo import repair — Humanoid rig, loop time, baked root motion",
            };

            var before = MixamoAnimationImportService.CountProblems(
                MixamoAnimationImportService.Audit(MixamoImportSettings.AnimationsRoot));
            var repaired = MixamoAnimationImportService.Repair(MixamoImportSettings.AnimationsRoot, report);
            var rowsAfter = MixamoAnimationImportService.Audit(MixamoImportSettings.AnimationsRoot);
            var after = MixamoAnimationImportService.CountProblems(rowsAfter);

            RebuildControllers(report);

            CharacterAnimationConfigLibrary.WriteReport(report);
            EditorUtility.DisplayDialog(
                "Mixamo import settings",
                string.Format(
                    "{0} animation file(s) repaired.\nSettings problems: {1} → {2}.\n\nReport: {3}\n\n"
                    + "Step 3 (Animator Controller) was rebuilt for every configured character.",
                    repaired, before, after, CharacterAnimationConfigLibrary.ReportPath),
                "OK");
        }

        /// <summary>Rebuild the generated controllers so the clips are wired to states.</summary>
        public static int RebuildControllers(ImportReport report)
        {
            var config = CharacterAnimationConfigLibrary.LoadIfExists();
            if (config == null || config.characters == null || config.characters.Count == 0)
            {
                if (report != null)
                {
                    report.Warn("controller", "no CharacterAnimationConfig yet — import a character, then assign clips");
                }

                return 0;
            }

            var rebuilt = 0;
            for (var i = 0; i < config.characters.Count; i++)
            {
                var character = config.characters[i];
                if (character == null)
                {
                    continue;
                }

                var build = AnimatorControllerBuilder.BuildController(config, character);
                if (!build.success)
                {
                    if (report != null)
                    {
                        report.Warn("controller", build.summary);
                    }

                    continue;
                }

                rebuilt++;
                if (report != null)
                {
                    report.Success("controller", build.summary);
                    for (var w = 0; w < build.warnings.Count; w++)
                    {
                        report.Warn("controller", build.warnings[w]);
                    }
                }
            }

            CharacterAnimationConfigLibrary.Save(config);
            return rebuilt;
        }
    }
}
