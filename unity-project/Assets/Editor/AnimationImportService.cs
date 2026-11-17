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
    /// Animation upload pipeline (§ animation upload system):
    ///
    ///  * accepts multiple FBX files at once, classifies each into a category
    ///    (Idle/Walk/Run/Sprint/Jump/Fall/Crouch/…/Melee/Death) from its file
    ///    name with an editable override,
    ///  * imports into Assets/Animations/&lt;category folder&gt;/,
    ///  * inspects every clip (duration, frame rate, loop flag, retargetability)
    ///    and records it in the import report,
    ///  * configures loop flags (clips are non-looping by default —
    ///    Death/Shoot/Reload/WeaponSwitch/Melee one-shots),
    ///  * prevents duplicate imports by SHA-256 content hash,
    ///  * registers the clips into the saved CharacterAnimationConfig so the
    ///    assignment UI and the generated Animator Controllers can use them.
    /// </summary>
    public static class AnimationImportService
    {
        public sealed class Options
        {
            /// <summary>Optional per-file category overrides (key = file name with extension).</summary>
            public readonly Dictionary<string, CharacterAnimCategory> CategoryOverrides =
                new Dictionary<string, CharacterAnimCategory>(StringComparer.OrdinalIgnoreCase);
        }

        public sealed class Result
        {
            public readonly List<AnimationClip> Clips = new List<AnimationClip>();
            public readonly ImportReport report = new ImportReport();
        }

        public sealed class ClipInspection
        {
            public string sourcePath;
            public string category;
            public bool loop;
            public readonly List<AnimationClip> clips = new List<AnimationClip>();
            public readonly List<string> failures = new List<string>();
        }

        /// <summary>
        /// Classify + inspect a set of animation FBX files without importing
        /// (the manager window shows this table so the user can change the
        /// category of a file before committing).
        /// </summary>
        public static List<ClipInspection> InspectSources(string[] sourcePaths)
        {
            var inspections = new List<ClipInspection>();
            if (sourcePaths == null)
            {
                return inspections;
            }

            for (var i = 0; i < sourcePaths.Length; i++)
            {
                var source = sourcePaths[i];
                var inspection = new ClipInspection { sourcePath = source };
                var fileName = Path.GetFileName(source);
                inspection.category = CharacterAnimCategories.InferFromFileName(fileName).ToString();
                inspection.loop = CharacterAnimCategories.DefaultLoop(
                    CharacterAnimCategories.InferFromFileName(fileName));

                AnimationClip[] clips = null;
                try
                {
                    clips = LoadClipsFromFbx(source);
                }
                catch (Exception exception)
                {
                    inspection.failures.Add("cannot load FBX: " + exception.Message);
                }

                if (clips != null && clips.Length > 0)
                {
                    inspection.clips.AddRange(clips);
                }
                else if (inspection.failures.Count == 0)
                {
                    inspection.failures.Add("no AnimationClip in this FBX");
                }

                inspections.Add(inspection);
            }

            return inspections;
        }

        /// <summary>Import animation FBX files and register their clips.</summary>
        public static Result ImportAnimations(string[] sourcePaths, Options options)
        {
            options = options ?? new Options();
            var result = new Result();
            var report = result.report;
            report.Title = "Animation import (" + (sourcePaths?.Length ?? 0) + " file(s))";

            var config = CharacterAnimationConfigLibrary.LoadOrCreate();
            var skippedDuplicates = 0;
            var failedFiles = 0;

            if (sourcePaths == null || sourcePaths.Length == 0)
            {
                report.Fail("source", "no animation FBX files selected");
                return result;
            }

            for (var i = 0; i < sourcePaths.Length; i++)
            {
                var source = sourcePaths[i];
                var fileName = Path.GetFileName(source);

                if (!File.Exists(source))
                {
                    report.Fail("source", fileName + " not found at " + source);
                    failedFiles++;
                    continue;
                }

                if (!source.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                {
                    report.Fail("source", fileName + " is not an .fbx file");
                    failedFiles++;
                    continue;
                }

                // ---- duplicate prevention --------------------------------
                var absoluteSource = Path.IsPathRooted(source)
                    ? source
                    : CharacterAnimationConfigLibrary.AbsolutePathOf(source);
                var sha = CharacterAnimationConfigLibrary.Sha256OfFile(absoluteSource);
                if (CharacterAnimationConfigLibrary.RegisterSource(
                        config, fileName, sha, new FileInfo(absoluteSource).Length))
                {
                    skippedDuplicates++;
                    report.Warn("duplicate",
                        fileName + " has the same content as an already-imported file — skipped");
                    continue;
                }

                // ---- category --------------------------------------------
                options.CategoryOverrides.TryGetValue(fileName, out var overridden);
                var category = options.CategoryOverrides.ContainsKey(fileName)
                    ? overridden
                    : CharacterAnimCategories.InferFromFileName(fileName);

                var folderName = CharacterAnimCategories.FolderName(category);
                var targetFolder = CharacterAnimationConfigLibrary.AnimationsRoot + "/" + folderName;
                var targetPath = targetFolder + "/" + fileName;

                CharacterAnimationConfigLibrary.EnsureFolder(targetFolder);
                CopyIntoProject(absoluteSource, targetPath, report);

                // ---- inspect + import ------------------------------------
                var importer = AssetImporter.GetAtPath(targetPath) as ModelImporter;
                if (importer == null)
                {
                    report.Fail("import", fileName + ": no ModelImporter after copy");
                    failedFiles++;
                    continue;
                }

                // Configure loop flags BEFORE the clip is generated.
                ConfigureLoopFlags(importer, category);

                AnimationClip[] importedClips;
                try
                {
                    importedClips = LoadClipsFromFbx(targetPath);
                }
                catch (Exception exception)
                {
                    report.Fail("import", fileName + ": " + exception.Message);
                    failedFiles++;
                    continue;
                }

                if (importedClips == null || importedClips.Length == 0)
                {
                    report.Fail("import", fileName + ": FBX contains no AnimationClip");
                    failedFiles++;
                    continue;
                }

                var rigKind = RigKind.Missing;
                var rigSummary = "no rig information";
                var rigReport = RigDetectionUtility.Inspect(targetPath);
                rigKind = rigReport.kind;
                rigSummary = rigReport.summary;

                if (rigKind == RigKind.Generic)
                {
                    report.Warn("rig", fileName + ": " + rigSummary
                        + " — clips stay generic (no humanoid retargeting)");
                }
                else if (rigKind != RigKind.Humanoid)
                {
                    report.Warn("rig", fileName + ": " + rigSummary
                        + " — animation import still attempted, retargeting may fail");
                }
                else
                {
                    report.Success("rig", fileName + ": " + rigSummary);
                }

                var model = AssetDatabase.LoadAssetAtPath<GameObject>(targetPath);
                var isMixamo = MixamoImportSettings.CountMixamoBones(model) > 0;
                if (isMixamo)
                {
                    report.Success("rig", fileName + ": " + MixamoImportSettings.DescribeSkeleton(model)
                        + " — clips retarget onto the character's Humanoid avatar");
                }

                // ---- register clips --------------------------------------
                for (var c = 0; c < importedClips.Length; c++)
                {
                    var clip = importedClips[c];
                    if (clip == null)
                    {
                        continue;
                    }

                    config.AddClip(clip);
                    config.AddOrUpdateAssignment(new CharacterAnimAssignment
                    {
                        characterId = null, // shared default — per-character override comes from the Assign tab
                        category = category,
                        clip = clip,
                        sourcePath = fileName,
                        duration = clip.length,
                        frameRate = clip.frameRate,
                        loop = clip.isLooping,
                        retargetable = rigKind == RigKind.Humanoid,
                    }, characterId: null);

                    // A Mixamo clip that still reports generic motion means the
                    // Rig tab conversion did not take — name the cause instead
                    // of letting it look like a normal import.
                    if (isMixamo && !clip.humanMotion)
                    {
                        report.Warn("rig", fileName + ": clip '" + clip.name
                            + "' is still generic motion — set Rig > Animation Type to Humanoid"
                            + " and Apply (this is why the character ignored it)");
                    }

                    result.Clips.Add(clip);
                    report.Success("clip",
                        string.Format("{0} → {1} · category {2} · {3:0.00}s · {4:0} fps · {5} · {6} · {7}",
                        fileName, clip.name, category,
                        clip.length, clip.frameRate,
                        clip.isLooping ? "loop" : "one-shot",
                        clip.humanMotion ? "humanoid" : "generic",
                        rigKind == RigKind.Humanoid ? "retargetable" : "no retarget"));
                }
            }

            CharacterAnimationConfigLibrary.Save(config);
            report.Success("summary", result.Clips.Count + " clip(s) imported, "
                + skippedDuplicates + " duplicate file(s) skipped, "
                + failedFiles + " file(s) failed");

            if (result.Clips.Count == 0 && failedFiles > 0)
            {
                report.Fail("summary", "no animation clips could be imported");
            }

            return result;
        }

        /// <summary>
        /// Scan the existing Assets/Animations tree and register every clip
        /// already present (covers clips imported by the pipeline rules or by
        /// an earlier session).
        /// </summary>
        public static int RegisterExistingAnimationAssets(ImportReport report)
        {
            var config = CharacterAnimationConfigLibrary.LoadOrCreate();
            var guids = AssetDatabase.FindAssets("t:AnimationClip",
                new[] { CharacterAnimationConfigLibrary.AnimationsRoot });
            var added = 0;
            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (clip == null || clip.name.StartsWith("__preview__", StringComparison.Ordinal))
                {
                    continue;
                }

                if (config.clips.Contains(clip))
                {
                    continue;
                }

                config.AddClip(clip);
                var category = CharacterAnimCategories.InferFromFileName(Path.GetFileName(path));
                config.AddOrUpdateAssignment(new CharacterAnimAssignment
                {
                    characterId = null,
                    category = category,
                    clip = clip,
                    sourcePath = Path.GetFileName(path),
                    duration = clip.length,
                    frameRate = clip.frameRate,
                    loop = clip.isLooping,
                    retargetable = clip.humanMotion,
                }, characterId: null);
                added++;
            }

            if (added > 0 || guids.Length > 0)
            {
                report.Success("scan", "registered " + added + " new clip(s) from "
                    + guids.Length + " animation asset(s) under " + CharacterAnimationConfigLibrary.AnimationsRoot);
                CharacterAnimationConfigLibrary.Save(config);
            }

            return added;
        }

        // ------------------------------------------------------------------

        private static void CopyIntoProject(string absoluteSource, string targetPath, ImportReport report)
        {
            var absoluteTarget = CharacterAnimationConfigLibrary.AbsolutePathOf(targetPath);
            if (string.Equals(Path.GetFullPath(absoluteSource), Path.GetFullPath(absoluteTarget),
                    StringComparison.OrdinalIgnoreCase))
            {
                return; // already in place
            }

            File.Copy(absoluteSource, absoluteTarget, true);
            AssetDatabase.ImportAsset(targetPath, ImportAssetOptions.ForceSynchronousImport);
            report.Success("import", "copied to " + targetPath);
        }

        /// <summary>
        /// Configure the clip settings that make a Mixamo FBX usable on a
        /// Humanoid character (Animation tab): Loop Time / Loop Pose from the
        /// category (with a file-name override, e.g. "jump loop.fbx"), and Root
        /// Transform Rotation / Position (Y, XZ) baked into pose so the clip
        /// can never slide or spin the character. Written through the
        /// importer's clip list — the documented way to persist these flags.
        /// </summary>
        private static void ConfigureLoopFlags(ModelImporter importer, CharacterAnimCategory category)
        {
            var fileName = Path.GetFileName(importer.assetPath);
            var desiredLoop = MixamoImportSettings.WantsLoop(category, fileName);
            var clipAnimations = importer.clipAnimations;
            var changed = false;

            if (clipAnimations == null || clipAnimations.Length == 0)
            {
                // No explicit clip list yet: generate one from the imported
                // clips so the flags can persist.
                var clips = LoadClipsFromFbx(importer.assetPath);
                if (clips == null || clips.Length == 0)
                {
                    return;
                }

                clipAnimations = new ModelImporterClipAnimation[clips.Length];
                for (var i = 0; i < clips.Length; i++)
                {
                    clipAnimations[i] = new ModelImporterClipAnimation
                    {
                        name = clips[i].name,
                        firstFrame = 0f,
                        lastFrame = Mathf.Max(1f, clips[i].length * clips[i].frameRate),
                        takeName = clips[i].name,
                    };
                    changed = true;
                }
            }

            for (var i = 0; i < clipAnimations.Length; i++)
            {
                // Also bakes in the root-motion settings (Bake Into Pose).
                if (MixamoImportSettings.ApplyToClip(clipAnimations[i], desiredLoop))
                {
                    changed = true;
                }
            }

            if (changed)
            {
                importer.clipAnimations = clipAnimations;
                AssetDatabase.WriteImportSettingsIfDirty(importer.assetPath);
                AssetDatabase.ImportAsset(importer.assetPath, ImportAssetOptions.ForceSynchronousImport);
            }
        }

        /// <summary>Load the animation clips of an FBX asset (sub-objects).</summary>
        public static AnimationClip[] LoadClipsFromFbx(string assetPath)
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            if (assets == null)
            {
                return Array.Empty<AnimationClip>();
            }

            var clips = new List<AnimationClip>(assets.Length);
            for (var i = 0; i < assets.Length; i++)
            {
                if (assets[i] is AnimationClip clip
                    && !clip.name.StartsWith("__preview__", StringComparison.Ordinal))
                {
                    clips.Add(clip);
                }
            }

            return clips.ToArray();
        }

        /// <summary>Human-readable duration/frame-rate/loop table for a clip set.</summary>
        public static string DescribeClips(IList<AnimationClip> clips)
        {
            if (clips == null || clips.Count == 0)
            {
                return "no clips";
            }

            var builder = new StringBuilder();
            for (var i = 0; i < clips.Count; i++)
            {
                var clip = clips[i];
                if (i > 0)
                {
                    builder.Append(" | ");
                }

                builder.Append(clip.name)
                    .Append(" ").Append(clip.length.ToString("0.00")).Append("s")
                    .Append(" ").Append(clip.frameRate.ToString("0")).Append("fps")
                    .Append(clip.isLooping ? " loop" : " once")
                    .Append(clip.humanMotion ? " humanoid" : " generic");
            }

            return builder.ToString();
        }
    }
}
