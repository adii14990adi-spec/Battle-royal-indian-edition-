using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace BattleHorizons.EditorTools
{
    /// <summary>What kind of rig an imported model ended up with.</summary>
    public enum RigKind
    {
        Missing,
        Humanoid,
        Generic,
        None,
    }

    /// <summary>Human-readable outcome of a rig inspection / configuration run.</summary>
    public sealed class RigReport
    {
        public bool success;
        public RigKind kind = RigKind.Missing;
        public string summary = string.Empty;
        public readonly List<string> errors = new List<string>();
        public readonly List<string> warnings = new List<string>();
        public int mappedHumanBones;
        public int skeletonBones;

        public string Combined
        {
            get
            {
                var builder = new StringBuilder();
                builder.Append(summary);
                for (var i = 0; i < errors.Count; i++)
                {
                    builder.Append("\nERROR: ").Append(errors[i]);
                }

                for (var i = 0; i < warnings.Count; i++)
                {
                    builder.Append("\nWARN: ").Append(warnings[i]);
                }

                return builder.ToString();
            }
        }
    }

    /// <summary>
    /// Automatic rig detection (§ automatic rig detection &amp; retargeting):
    ///
    ///  * inspects an imported model and reports whether it has a valid
    ///    humanoid skeleton (Avatar.isValid / isHuman, mapped bone count),
    ///  * configures the ModelImporter (Humanoid when compatible, Generic
    ///    when humanoid mapping is unsuitable) and re-imports,
    ///  * when Unity's built-in auto-mapper leaves bones unmapped, builds a
    ///    HumanDescription from known rig naming conventions (Mixamo,
    ///    Unreal-style pelvis/spine_01/upperarm_l, common Biped names) and
    ///    re-imports with that mapping,
    ///  * validates retargeting compatibility between animation clips and
    ///    character avatars,
    ///  * never forces an incompatible animation onto a character — it
    ///    reports exactly what is missing so the user can fix it manually.
    /// </summary>
    public static class RigDetectionUtility
    {
        /// <summary>
        /// Inspect an already-imported model and describe its rig.
        /// </summary>
        public static RigReport Inspect(string assetPath)
        {
            var report = new RigReport();
            var importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
            if (importer == null)
            {
                report.errors.Add(assetPath + " is not a model (FBX/OBJ) asset");
                report.summary = "not a model importer";
                return report;
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (prefab == null)
            {
                report.errors.Add("import produced no root GameObject for " + assetPath);
                report.summary = "import failed";
                return report;
            }

            var animator = prefab.GetComponentInChildren<Animator>(true);
            var avatar = animator != null ? animator.avatar : null;
            report.skeletonBones = CountSkeleton(prefab.transform);

            if (importer.animationType != ModelImporterAnimationType.Human
                && importer.animationType != ModelImporterAnimationType.Generic)
            {
                report.kind = RigKind.None;
                report.summary = "Animation Type is None (skeleton imported without a Mecanim rig)";
                report.warnings.Add("set Animation Type to Humanoid (compatible) or Generic in the model importer");
                return report;
            }

            if (avatar == null)
            {
                report.kind = RigKind.Missing;
                report.errors.Add("no Avatar was generated (" + report.skeletonBones + " skeleton bones found)");
                report.summary = "no avatar";
                return report;
            }

            if (importer.animationType == ModelImporterAnimationType.Human)
            {
                report.kind = avatar.isHuman ? RigKind.Humanoid : RigKind.Generic;
                report.mappedHumanBones = CountMappedHumanBones(avatar);

                if (avatar.isValid && avatar.isHuman)
                {
                    report.success = true;
                    report.summary = string.Format(
                        "Humanoid avatar valid ({0} human bones mapped, {1} skeleton bones)",
                        report.mappedHumanBones, report.skeletonBones);
                }
                else
                {
                    report.summary = avatar.isValid
                        ? "avatar exists but is NOT humanoid — required bones are missing or mis-mapped ("
                          + report.mappedHumanBones + "/" + HumanTrait.BoneName.Length + " mapped)"
                        : "avatar is INVALID (mecanim rejected the skeleton — missing required bones)";
                    report.errors.Add(report.summary);
                    report.warnings.Add(
                        "manual options: open the model's Rig tab > Configure Avatar, or re-import as Generic rig");
                }

                return report;
            }

            report.kind = RigKind.Generic;
            report.success = true;
            report.summary = string.Format(
                "Generic avatar valid ({0} skeleton bones) — humanoid retargeting not available for this rig",
                report.skeletonBones);
            return report;
        }

        /// <summary>
        /// Configure the rig for a model: Humanoid when it maps, with one
        /// automatic HumanDescription fallback before giving up. Returns
        /// false when only Generic is viable (caller decides whether to apply
        /// it — characters are reported to the user instead of silently
        /// downgraded unless the service asked for the Generic path).
        /// </summary>
        public static bool EnsureHumanoid(string assetPath, RigReport report)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
            if (importer == null)
            {
                report.errors.Add(assetPath + ": no ModelImporter");
                return false;
            }

            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;
            AssetDatabase.WriteImportSettingsIfDirty(assetPath);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);

            var firstPass = Inspect(assetPath);
            if (firstPass.success && firstPass.kind == RigKind.Humanoid)
            {
                CharacterAnimationConfigLibrary.ClearExplicitGeneric(assetPath);
                report.success = true;
                report.kind = RigKind.Humanoid;
                report.mappedHumanBones = firstPass.mappedHumanBones;
                report.skeletonBones = firstPass.skeletonBones;
                report.summary = "humanoid auto-map: " + firstPass.summary;
                report.warnings.AddRange(firstPass.warnings);
                return true;
            }

            // Fallback: augment Unity's (partial) auto-mapping with our own
            // convention tables and re-import.
            report.warnings.Add("built-in humanoid auto-map failed — retrying with convention-based bone mapping");
            if (TryApplyConventionMapping(assetPath, importer, report))
            {
                var secondPass = Inspect(assetPath);
                if (secondPass.success && secondPass.kind == RigKind.Humanoid)
                {
                    CharacterAnimationConfigLibrary.ClearExplicitGeneric(assetPath);
                    report.success = true;
                    report.kind = RigKind.Humanoid;
                    report.mappedHumanBones = secondPass.mappedHumanBones;
                    report.skeletonBones = secondPass.skeletonBones;
                    report.summary = "humanoid convention-map: " + secondPass.summary;
                    return true;
                }

                report.errors.Add("convention mapping still did not produce a valid humanoid avatar: "
                    + secondPass.summary);
            }

            report.success = false;
            report.kind = firstPass.kind;
            return false;
        }

        /// <summary>Explicitly (re)configure a model as a Generic rig.</summary>
        public static RigReport EnsureGeneric(string assetPath)
        {
            var report = new RigReport();
            var importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
            if (importer == null)
            {
                report.errors.Add(assetPath + ": no ModelImporter");
                return report;
            }

            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;
            AssetDatabase.WriteImportSettingsIfDirty(assetPath);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            // Record the choice so the import rule never overrides it back
            // to Humanoid on a later reimport.
            CharacterAnimationConfigLibrary.MarkExplicitGeneric(assetPath);

            var inspection = Inspect(assetPath);
            report.kind = inspection.kind;
            report.success = inspection.kind != RigKind.Missing && inspection.kind != RigKind.None;
            report.summary = "configured Generic — " + inspection.summary;
            report.errors.AddRange(inspection.errors);
            report.warnings.AddRange(inspection.warnings);
            return report;
        }

        /// <summary>
        /// Retargeting compatibility (§ never force incompatible animations):
        /// a clip can be retargeted onto a character when the clip carries
        /// humanoid motion AND the character's avatar is a valid human avatar.
        /// </summary>
        public static bool IsRetargetCompatible(AnimationClip clip, Avatar targetAvatar, out string reason)
        {
            if (clip == null)
            {
                reason = "no clip";
                return false;
            }

            if (!clip.humanMotion)
            {
                reason = "clip '" + clip.name + "' is generic motion (source model was not imported as Humanoid)";
                return false;
            }

            if (targetAvatar == null)
            {
                reason = "target character has no avatar";
                return false;
            }

            if (!targetAvatar.isValid || !targetAvatar.isHuman)
            {
                reason = "target character avatar is not a valid humanoid — fix its rig first";
                return false;
            }

            reason = "ok";
            return true;
        }

        /// <summary>Avatar of a model asset (null when the model has none).</summary>
        public static Avatar FindAvatar(string assetPath)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (prefab == null)
            {
                return null;
            }

            var animator = prefab.GetComponentInChildren<Animator>(true);
            return animator != null ? animator.avatar : null;
        }

        /// <summary>Number of skeleton transforms below the model root.</summary>
        public static int CountSkeleton(Transform root)
        {
            if (root == null)
            {
                return 0;
            }

            var count = 0;
            for (var i = 0; i < root.childCount; i++)
            {
                count += 1 + CountSkeleton(root.GetChild(i));
            }

            return count;
        }

        /// <summary>Mapped entries in a humanoid avatar's human description.</summary>
        public static int CountMappedHumanBones(Avatar avatar)
        {
            if (avatar == null || !avatar.isHuman)
            {
                return 0;
            }

            var human = avatar.humanDescription.human;
            if (human == null)
            {
                return 0;
            }

            var mapped = 0;
            for (var i = 0; i < human.Length; i++)
            {
                if (!string.IsNullOrEmpty(human[i].boneName))
                {
                    mapped++;
                }
            }

            return mapped;
        }

        // ------------------------------------------------------------------
        // Convention-based HumanDescription fallback
        // ------------------------------------------------------------------

        private static bool TryApplyConventionMapping(string assetPath, ModelImporter importer, RigReport report)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (prefab == null)
            {
                report.errors.Add(assetPath + ": root GameObject missing for convention mapping");
                return false;
            }

            HumanDescription description = importer.humanDescription;
            var existing = description.human != null
                ? new List<HumanBone>(description.human)
                : new List<HumanBone>();
            var mappedHumanNames = new HashSet<string>();
            var mappedBoneNames = new HashSet<string>();
            for (var i = 0; i < existing.Count; i++)
            {
                if (!string.IsNullOrEmpty(existing[i].boneName))
                {
                    mappedHumanNames.Add(existing[i].humanName);
                    mappedBoneNames.Add(existing[i].boneName);
                }
            }

            // Collect transforms in breadth-first order (parents first).
            var transforms = new List<Transform>();
            CollectBreadthFirst(prefab.transform, transforms);

            var byNormalized = new Dictionary<string, Transform>(StringComparer.Ordinal);
            for (var i = 0; i < transforms.Count; i++)
            {
                var normalized = NormalizeBoneName(transforms[i].name);
                if (normalized.Length > 0 && !byNormalized.ContainsKey(normalized))
                {
                    byNormalized[normalized] = transforms[i];
                }
            }

            var added = 0;
            var bones = (HumanBodyBones[])Enum.GetValues(typeof(HumanBodyBones));
            for (var i = 0; i < bones.Length; i++)
            {
                var bone = bones[i];
                if (bone == HumanBodyBones.LastBone)
                {
                    continue;
                }

                var canonical = CamelToSpacedName(bone.ToString());
                if (mappedHumanNames.Contains(canonical))
                {
                    continue;
                }

                var aliasList = BoneAliases(bone);
                for (var a = 0; a < aliasList.Length; a++)
                {
                    var normalizedAlias = NormalizeBoneName(aliasList[a]);
                    if (normalizedAlias.Length > 0
                        && byNormalized.TryGetValue(normalizedAlias, out var hit)
                        && !mappedBoneNames.Contains(hit.name))
                    {
                        existing.Add(new HumanBone
                        {
                            boneName = hit.name,
                            humanName = canonical,
                            limit = new HumanLimit { useDefaultValues = true },
                        });
                        mappedHumanNames.Add(canonical);
                        mappedBoneNames.Add(hit.name);
                        added++;
                        break;
                    }
                }
            }

            // Core bones must exist or the avatar cannot be built.
            var required = new[] { "Hips", "Spine", "Head" };
            var missingCore = new List<string>();
            for (var i = 0; i < required.Length; i++)
            {
                if (!mappedHumanNames.Contains(required[i]))
                {
                    missingCore.Add(required[i]);
                }
            }

            if (missingCore.Count > 0)
            {
                report.errors.Add("skeleton is missing core humanoid bones: " + string.Join(", ", missingCore)
                    + " — this rig cannot be humanoid (Generic import is the supported option)");
                return false;
            }

            if (added == 0 && existing.Count > 0)
            {
                // Unity already mapped everything our tables would; nothing to fix.
                return true;
            }

            description.human = existing.ToArray();
            if (description.skeleton == null || description.skeleton.Length == 0)
            {
                description.skeleton = BuildSkeleton(transforms);
            }

            importer.humanDescription = description;
            AssetDatabase.WriteImportSettingsIfDirty(assetPath);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            report.warnings.Add("applied convention-based mapping for " + added + " additional human bones");
            return true;
        }

        private static SkeletonBone[] BuildSkeleton(List<Transform> transforms)
        {
            var skeleton = new SkeletonBone[transforms.Count];
            for (var i = 0; i < transforms.Count; i++)
            {
                var transform = transforms[i];
                skeleton[i] = new SkeletonBone
                {
                    name = transform.name,
                    position = transform.localPosition,
                    rotation = transform.localRotation,
                    scale = transform.localScale,
                };
            }

            return skeleton;
        }

        private static void CollectBreadthFirst(Transform root, List<Transform> output)
        {
            var queue = new Queue<Transform>();
            queue.Enqueue(root);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                output.Add(current);
                for (var i = 0; i < current.childCount; i++)
                {
                    queue.Enqueue(current.GetChild(i));
                }
            }
        }

        private static string NormalizeBoneName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(name.Length);
            for (var i = 0; i < name.Length; i++)
            {
                var character = char.ToLowerInvariant(name[i]);
                if (character == '_' || character == ':' || character == '-' || character == ' ')
                {
                    continue;
                }

                builder.Append(character);
            }

            return builder.ToString();
        }

        /// <summary>"LeftUpperLeg" → "Left Upper Leg" (Unity's canonical human names).</summary>
        public static string CamelToSpacedName(string enumName)
        {
            var builder = new StringBuilder(enumName.Length + 6);
            for (var i = 0; i < enumName.Length; i++)
            {
                var character = enumName[i];
                if (i > 0 && char.IsUpper(character) && !char.IsUpper(enumName[i - 1]))
                {
                    builder.Append(' ');
                }

                builder.Append(character);
            }

            return builder.ToString();
        }

        /// <summary>
        /// Alias table (normalized: lowercase, no separators) per humanoid
        /// bone, covering Mixamo, Unreal-style (pelvis / spine_01 /
        /// upperarm_l) and common Biped/Blender conventions. Left/right are
        /// explicit in every alias so sides can never swap.
        /// </summary>
        private static string[] BoneAliases(HumanBodyBones bone)
        {
            switch (bone)
            {
                case HumanBodyBones.Hips:
                    return new[] { "hips", "pelvis", "bip001pelvis", "cog" };
                case HumanBodyBones.Spine:
                    return new[] { "spine", "spine01", "bip001spine", "torsostart" };
                case HumanBodyBones.Chest:
                    return new[] { "spine02", "spine1", "chest", "bip001spine1", "torsomiddle" };
                case HumanBodyBones.UpperChest:
                    return new[] { "spine03", "spine2", "upperchest", "bip001spine2", "torsostop" };
                case HumanBodyBones.Neck:
                    return new[] { "neck", "neck01", "bip001neck" };
                case HumanBodyBones.Head:
                    return new[] { "head", "bip001head", "headtop" };

                case HumanBodyBones.LeftShoulder:
                    return new[] { "claviclel", "leftshoulder", "shoulderl", "lclavicle", "bip001lclavicle" };
                case HumanBodyBones.LeftUpperArm:
                    return new[] { "upperarml", "leftarm", "armleft", "lupperarm", "bip001lupperarm" };
                case HumanBodyBones.LeftLowerArm:
                    return new[] { "lowerarml", "leftforearm", "forearml", "lforearm", "bip001lforearm" };
                case HumanBodyBones.LeftHand:
                    return new[] { "handl", "lefthand", "lhand", "bip001lhand" };
                case HumanBodyBones.RightShoulder:
                    return new[] { "clavicleR", "rightshoulder", "shoulderR", "rclavicle", "bip001rclavicle" };
                case HumanBodyBones.RightUpperArm:
                    return new[] { "upperarmR", "rightarm", "armright", "rupperarm", "bip001rupperarm" };
                case HumanBodyBones.RightLowerArm:
                    return new[] { "lowerarmR", "rightforearm", "forearmR", "rforearm", "bip001rforearm" };
                case HumanBodyBones.RightHand:
                    return new[] { "handR", "righthand", "rhand", "bip001rhand" };

                case HumanBodyBones.LeftUpperLeg:
                    return new[] { "thighl", "leftupleg", "uplegl", "upperlegl", "bip001lthigh" };
                case HumanBodyBones.LeftLowerLeg:
                    return new[] { "calfl", "leftleg", "lowerlegl", "shinl", "bip001lcalf" };
                case HumanBodyBones.LeftFoot:
                    return new[] { "footl", "leftfoot", "bip001lfoot" };
                case HumanBodyBones.LeftToes:
                    return new[] { "balll", "toel", "lefttoes", "toesl", "bip001ltoe0" };
                case HumanBodyBones.RightUpperLeg:
                    return new[] { "thighR", "rightupleg", "uplegR", "upperlegR", "bip001rthigh" };
                case HumanBodyBones.RightLowerLeg:
                    return new[] { "calfR", "rightleg", "lowerlegR", "shinR", "bip001rcalf" };
                case HumanBodyBones.RightFoot:
                    return new[] { "footR", "rightfoot", "bip001rfoot" };
                case HumanBodyBones.RightToes:
                    return new[] { "ballR", "toeR", "righttoes", "toesR", "bip001rtoe0" };
            }

            // Fingers: (finger, segment) generic patterns for both sides.
            var fingerName = bone.ToString(); // e.g. "LeftThumbProximal"
            if (fingerName.Length < 8)
            {
                return Array.Empty<string>();
            }

            var side = fingerName.StartsWith("Left", StringComparison.Ordinal) ? "l" : "r";
            var sideWord = side == "l" ? "left" : "right";
            var rest = fingerName.Substring(4); // e.g. "ThumbProximal" / "IndexIntermediate"...
            string finger;
            string segment;
            if (rest.StartsWith("Thumb", StringComparison.Ordinal))
            {
                finger = "thumb";
                segment = rest.Substring(5); // Proximal/Intermediate/Distal
            }
            else if (rest.StartsWith("Index", StringComparison.Ordinal))
            {
                finger = "index";
                segment = rest.Substring(5);
            }
            else if (rest.StartsWith("Middle", StringComparison.Ordinal))
            {
                finger = "middle";
                segment = rest.Substring(6);
            }
            else if (rest.StartsWith("Ring", StringComparison.Ordinal))
            {
                finger = "ring";
                segment = rest.Substring(4);
            }
            else if (rest.StartsWith("Little", StringComparison.Ordinal))
            {
                finger = "pinky";
                segment = rest.Substring(6);
            }
            else
            {
                return Array.Empty<string>();
            }

            int indexNumber;
            switch (segment)
            {
                case "Proximal": indexNumber = 1; break;
                case "Intermediate": indexNumber = 2; break;
                case "Distal": indexNumber = 3; break;
                default: return Array.Empty<string>();
            }

            var sideTag = side;
            return new[]
            {
                // Unreal-style: index_01_l
                finger + "0" + indexNumber + sideTag,
                // Mixamo: mixamorig:LeftHandIndex1 → left hand index1
                sideWord + "hand" + finger + indexNumber,
                // Biped-ish: bip001 l finger1 / finger1 l
                "bip001" + sideTag + finger + indexNumber,
                sideTag + finger + "finger" + indexNumber,
                finger + indexNumber + sideTag,
                finger + "l" + indexNumber + (side == "r" ? "r" : ""),
            };
        }
    }
}
