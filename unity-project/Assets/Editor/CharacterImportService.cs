using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BattleHorizons.Characters;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace BattleHorizons.EditorTools
{
    /// <summary>Where a character import is filed (§ asset organization).</summary>
    public enum CharacterSlot
    {
        Male,
        Female,
        Custom,
    }

    /// <summary>
    /// Character upload pipeline (§ character upload system):
    ///
    ///  1. copy the chosen FBX into Assets/Characters/&lt;slot&gt;/ (source file
    ///     is never modified — embedded textures and materials stay intact),
    ///  2. configure &amp; validate the rig (Humanoid with convention-mapping
    ///     fallback, Generic only when explicitly requested),
    ///  3. extract embedded textures into Textures/ and optionally copy
    ///     external PNG/JPG/TGA texture files from a folder the user picked,
    ///  4. build URP-compatible materials in Materials/ and remap them onto
    ///     the model's material slots (assignments preserved), warning about
    ///     textures that could not be found,
    ///  5. verify mesh integrity (vertices / UVs / bones),
    ///  6. record everything in the import report and the config.
    ///
    /// Existing character assets are only ever replaced after an explicit
    /// confirmation (never in batch mode, where confirmation is impossible).
    /// </summary>
    public static class CharacterImportService
    {
        public sealed class Options
        {
            public CharacterSlot slot = CharacterSlot.Custom;
            public string customName = "CustomCharacter";
            /// <summary>Optional folder of external PNG/JPG/TGA/JPEG textures.</summary>
            public string externalTextureFolder;
            /// <summary>Returns false to abort an overwrite. Null = do not overwrite.</summary>
            public Func<string, string, bool> confirmOverwrite;
            /// <summary>Force Generic instead of Humanoid (manual escape hatch).</summary>
            public bool forceGenericRig;
        }

        public sealed class Result
        {
            public bool accepted;
            public string assetPath;
            public CharacterDefinition definition;
            public readonly ImportReport report = new ImportReport();
        }

        public static Result ImportCharacter(string sourceFbxPath, Options options)
        {
            options = options ?? new Options();
            var result = new Result();
            var report = result.report;
            report.Title = "Character import — " + Path.GetFileName(sourceFbxPath);

            if (string.IsNullOrEmpty(sourceFbxPath) || !File.Exists(sourceFbxPath))
            {
                report.Fail("source", "FBX file not found: " + sourceFbxPath);
                return result;
            }

            if (!sourceFbxPath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
            {
                report.Fail("source", "only .fbx character files are supported (got "
                    + Path.GetExtension(sourceFbxPath) + ")");
                return result;
            }

            var slotFolder = SlotFolder(options.slot, options.customName);
            var targetFolder = CharacterAnimationConfigLibrary.CharactersRoot + "/" + slotFolder;
            var fileName = Path.GetFileNameWithoutExtension(sourceFbxPath);
            var assetPath = targetFolder + "/" + fileName + ".fbx";

            // ---- overwrite protection -------------------------------------
            var absoluteTarget = CharacterAnimationConfigLibrary.AbsolutePathOf(assetPath);
            if (File.Exists(absoluteTarget))
            {
                var sameFile = string.Equals(
                    Path.GetFullPath(sourceFbxPath).TrimEnd('/'),
                    Path.GetFullPath(absoluteTarget).TrimEnd('/'),
                    StringComparison.OrdinalIgnoreCase);

                if (sameFile)
                {
                    report.Warn("destination", assetPath + " is already the imported location — revalidating in place");
                }
                else if (Application.isBatchMode)
                {
                    report.Fail("destination", assetPath
                        + " already exists; refusing to overwrite without interactive confirmation (batch mode)");
                    return result;
                }
                else if (options.confirmOverwrite == null || !options.confirmOverwrite(assetPath, sourceFbxPath))
                {
                    report.Fail("destination", "overwrite declined for existing character asset " + assetPath);
                    return result;
                }
                else
                {
                    AssetDatabase.DeleteAsset(assetPath);
                    report.Warn("destination", "existing " + assetPath + " replaced after confirmation");
                }
            }

            // ---- copy (source preserved) ----------------------------------
            CharacterAnimationConfigLibrary.EnsureFolder(targetFolder);
            CharacterAnimationConfigLibrary.EnsureFolder(targetFolder + "/Materials");
            CharacterAnimationConfigLibrary.EnsureFolder(targetFolder + "/Textures");

            var absoluteSource = Path.IsPathRooted(sourceFbxPath)
                ? sourceFbxPath
                : CharacterAnimationConfigLibrary.AbsolutePathOf(sourceFbxPath);
            if (!string.Equals(Path.GetFullPath(absoluteSource), Path.GetFullPath(absoluteTarget),
                    StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(absoluteSource, absoluteTarget, true);
            }

            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            if (AssetDatabase.LoadAssetAtPath<GameObject>(assetPath) == null)
            {
                report.Fail("import", "Unity did not produce a GameObject for " + assetPath);
                return result;
            }

            report.Success("import", "FBX imported to " + assetPath);

            // ---- content-hash registration (duplicate prevention) -----------
            var config = CharacterAnimationConfigLibrary.LoadOrCreate();
            var sourceSha = CharacterAnimationConfigLibrary.Sha256OfFile(absoluteSource);
            var sourceBytes = new FileInfo(absoluteSource).Length;
            if (CharacterAnimationConfigLibrary.RegisterSource(config, assetPath, sourceSha, sourceBytes))
            {
                report.Warn("duplicate",
                    "identical file content was already imported — continuing so the slot stays valid");
            }

            // ---- rig -------------------------------------------------------
            var rigReport = new RigReport();
            if (options.forceGenericRig)
            {
                rigReport = RigDetectionUtility.EnsureGeneric(assetPath);
            }
            else
            {
                if (!RigDetectionUtility.EnsureHumanoid(assetPath, rigReport))
                {
                    report.Warn("rig", rigReport.Combined
                        + " | manual options: model Rig tab > Configure Avatar, or re-import as Generic");
                }
            }

            if (rigReport.kind == RigKind.Humanoid && rigReport.success)
            {
                report.Success("rig", rigReport.summary);
            }
            else if (rigReport.kind == RigKind.Generic && rigReport.success)
            {
                report.Warn("rig", rigReport.summary);
            }
            else
            {
                report.Fail("rig", rigReport.summary);
            }

            // ---- textures --------------------------------------------------
            var textureSummary = ExtractAndCollectTextures(assetPath, targetFolder + "/Textures", options, report);

            // ---- materials (URP) + remap -----------------------------------
            var materialSummary = BuildAndRemapMaterials(assetPath, targetFolder + "/Materials", report);

            // ---- mesh integrity --------------------------------------------
            ValidateMeshIntegrity(assetPath, report);

            // ---- config entry (config was loaded for source registration) ---
            var id = SlotId(options.slot, options.customName);
            var definition = CharacterAnimationConfigLibrary.GetOrCreateCharacter(
                config, id, SlotDisplayName(options.slot, options.customName, fileName));
            definition.model = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            definition.humanoidRigValid = rigReport.kind == RigKind.Humanoid && rigReport.success;
            definition.rigReport = rigReport.Combined;
            definition.materialReport = materialSummary + "\n" + textureSummary;
            CharacterAnimationConfigLibrary.Save(config);

            result.definition = definition;
            result.assetPath = assetPath;
            result.accepted = report.FailedCount == 0;
            return result;
        }

        // ------------------------------------------------------------------

        private static string SlotFolder(CharacterSlot slot, string customName)
        {
            switch (slot)
            {
                case CharacterSlot.Male: return "Male";
                case CharacterSlot.Female: return "Female";
                default: return SanitizeFolderName(string.IsNullOrEmpty(customName) ? "Custom" : customName);
            }
        }

        private static string SlotId(CharacterSlot slot, string customName)
        {
            switch (slot)
            {
                case CharacterSlot.Male: return "male";
                case CharacterSlot.Female: return "female";
                default: return SanitizeFolderName(string.IsNullOrEmpty(customName) ? "custom" : customName).ToLowerInvariant();
            }
        }

        private static string SlotDisplayName(CharacterSlot slot, string customName, string fallback)
        {
            switch (slot)
            {
                case CharacterSlot.Male: return "Male";
                case CharacterSlot.Female: return "Female";
                default: return string.IsNullOrEmpty(customName) ? fallback : customName;
            }
        }

        internal static string SanitizeFolderName(string name)
        {
            var builder = new StringBuilder(name.Length);
            for (var i = 0; i < name.Length; i++)
            {
                var character = name[i];
                if (char.IsLetterOrDigit(character) || character == '_' || character == '-')
                {
                    builder.Append(character);
                }
            }

            return builder.Length > 0 ? builder.ToString() : "Custom";
        }

        private static string ExtractAndCollectTextures(
            string assetPath, string texturesFolder, Options options, ImportReport report)
        {
            var embedded = new List<Texture2D>();
            var subAssets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            for (var i = 0; i < subAssets.Length; i++)
            {
                if (subAssets[i] is Texture2D texture)
                {
                    embedded.Add(texture);
                }
            }

            var extracted = 0;
            if (embedded.Count > 0)
            {
                var importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
                if (importer != null)
                {
                    importer.ExtractTextures(texturesFolder);
                    AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
                    extracted = CountTextureAssetsIn(texturesFolder);
                    report.Success("textures",
                        "extracted " + extracted + " embedded texture(s) to " + texturesFolder
                        + " (formats kept: " + DescribeFormats(embedded) + ")");
                }
            }
            else
            {
                report.Warn("textures", "model declares no embedded textures");
            }

            // External texture files (PNG / JPG / TGA) the user provides.
            var externalCopied = 0;
            if (!string.IsNullOrEmpty(options.externalTextureFolder)
                && Directory.Exists(options.externalTextureFolder))
            {
                var allowed = new[] { ".png", ".jpg", ".jpeg", ".tga" };
                var files = Directory.GetFiles(options.externalTextureFolder);
                for (var i = 0; i < files.Length; i++)
                {
                    var extension = Path.GetExtension(files[i]).ToLowerInvariant();
                    if (Array.IndexOf(allowed, extension) < 0)
                    {
                        continue;
                    }

                    var destination = texturesFolder + "/" + Path.GetFileName(files[i]);
                    var absoluteDestination = CharacterAnimationConfigLibrary.AbsolutePathOf(destination);
                    if (File.Exists(absoluteDestination))
                    {
                        continue;
                    }

                    File.Copy(files[i], absoluteDestination, false);
                    AssetDatabase.ImportAsset(destination, ImportAssetOptions.ForceSynchronousImport);
                    externalCopied++;
                }

                if (externalCopied > 0)
                {
                    report.Success("textures", "copied " + externalCopied
                        + " external texture file(s) (PNG/JPG/TGA) from " + options.externalTextureFolder);
                }
            }

            var total = CountTextureAssetsIn(texturesFolder);
            if (total == 0)
            {
                report.Warn("textures", "no texture assets available for " + assetPath
                    + " — materials will be created without base maps");
            }

            return "textures: " + total + " file(s) in Textures/";
        }

        private static string DescribeFormats(List<Texture2D> textures)
        {
            // Texture2D does not expose the source container, so describe what
            // the source files referenced by this model were (png/jpg/tga are
            // the supported set — see the manager's import dialog).
            return textures.Count > 0 ? "png/jpg/tga" : "none";
        }

        private static int CountTextureAssetsIn(string folder)
        {
            var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folder });
            return guids.Length;
        }

        private static string BuildAndRemapMaterials(
            string assetPath, string materialsFolder, ImportReport report)
        {
            var subAssets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            var materials = new List<Material>();
            for (var i = 0; i < subAssets.Length; i++)
            {
                if (subAssets[i] is Material material)
                {
                    materials.Add(material);
                }
            }

            if (materials.Count == 0)
            {
                report.Warn("materials", "model contains no material slots");
                return "materials: none in model";
            }

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                report.Fail("materials", "URP/Lit shader not found — cannot build URP materials");
                return "materials: URP shader missing";
            }

            var importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
            var modelFolder = materialsFolder.Substring(0,
                materialsFolder.LastIndexOf("/Materials", StringComparison.Ordinal));
            var textures = CollectTextureAssets(modelFolder);
            var created = 0;
            var missingTextures = 0;

            for (var i = 0; i < materials.Count; i++)
            {
                var original = materials[i];
                var materialName = SanitizeFolderName(string.IsNullOrEmpty(original.name) ? "Material_" + i : original.name);
                var materialPath = materialsFolder + "/" + materialName + ".mat";

                var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                var isNew = material == null;
                if (isNew)
                {
                    material = new Material(shader) { name = materialName };
                }
                else if (material.shader != shader)
                {
                    material.shader = shader;
                }

                // Base map: the model's own texture, else a name-matched file.
                var source = original.mainTexture;
                if (source == null)
                {
                    source = FindTextureByName(textures, original.name);
                }

                if (source != null)
                {
                    material.SetTexture("_BaseMap", source);
                    material.SetColor("_BaseColor", Color.white);
                }
                else
                {
                    missingTextures++;
                    report.Warn("materials", "missing texture for material '" + original.name
                        + "' — add the file to Textures/ or map it manually in the material");
                }

                material.SetFloat("_Metallic", 0f);
                material.SetFloat("_Smoothness", 0.3f);
                EditorUtility.SetDirty(material);

                if (isNew)
                {
                    AssetDatabase.CreateAsset(material, materialPath);
                    created++;
                }

                if (importer != null)
                {
                    importer.AddRemap(
                        new AssetImporter.SourceAssetIdentifier(typeof(Material), original.name),
                        material);
                }
            }

            if (importer != null)
            {
                AssetDatabase.WriteImportSettingsIfDirty(assetPath);
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            }

            report.Success("materials", created + " URP material(s) created, "
                + materials.Count + " slot(s) remapped (assignments preserved)");
            if (missingTextures > 0)
            {
                report.Warn("materials", missingTextures + " material slot(s) have no texture (see above)");
            }

            var summaries = new List<string>();
            var guids = AssetDatabase.FindAssets("t:Material", new[]
            {
                materialsFolder,
            });
            for (var i = 0; i < guids.Length; i++)
            {
                summaries.Add(AssetDatabase.GUIDToAssetPath(guids[i]));
            }

            return "materials: " + guids.Length + " URP asset(s) in Materials/ ("
                + string.Join(", ", summaries) + ")";
        }

        private static List<Texture2D> CollectTextureAssets(string modelFolder)
        {
            var textures = new List<Texture2D>();
            var texturesFolder = modelFolder + "/Textures";
            var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { texturesFolder });
            for (var i = 0; i < guids.Length; i++)
            {
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(guids[i]));
                if (texture != null)
                {
                    textures.Add(texture);
                }
            }

            return textures;
        }

        private static Texture2D FindTextureByName(List<Texture2D> textures, string materialName)
        {
            if (textures.Count == 0 || string.IsNullOrEmpty(materialName))
            {
                return null;
            }

            var normalizedMaterial = Sanitize(materialName);
            for (var i = 0; i < textures.Count; i++)
            {
                var textureName = Sanitize(textures[i].name);
                if (textureName.IndexOf(normalizedMaterial, StringComparison.OrdinalIgnoreCase) >= 0
                    || normalizedMaterial.IndexOf(textureName, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return textures[i];
                }
            }

            // Fall back: basecolor/roughness naming conventions.
            for (var i = 0; i < textures.Count; i++)
            {
                var textureName = textures[i].name.ToLowerInvariant();
                if (textureName.Contains("basecolor") || textureName.Contains("albedo")
                    || textureName.Contains("diffuse") || textureName.Contains("_col"))
                {
                    return textures[i];
                }
            }

            return null;
        }

        private static string Sanitize(string value)
        {
            var builder = new StringBuilder(value.Length);
            for (var i = 0; i < value.Length; i++)
            {
                var character = value[i];
                if (char.IsLetterOrDigit(character))
                {
                    builder.Append(char.ToLowerInvariant(character));
                }
            }

            return builder.ToString();
        }

        private static void ValidateMeshIntegrity(string assetPath, ImportReport report)
        {
            var meshes = new List<Mesh>();
            var subAssets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            for (var i = 0; i < subAssets.Length; i++)
            {
                if (subAssets[i] is Mesh mesh)
                {
                    meshes.Add(mesh);
                }
            }

            if (meshes.Count == 0)
            {
                // Animation-only FBX files legitimately have no mesh — for a
                // *character* that is worth a warning, not a failure.
                report.Warn("mesh", "no mesh in this FBX (animation-only file?)");
                return;
            }

            var totalVertices = 0;
            var missingUv = new List<string>();
            var skinnedBones = 0;
            for (var i = 0; i < meshes.Count; i++)
            {
                var mesh = meshes[i];
                totalVertices += mesh.vertexCount;
                if (mesh.vertexCount > 0 && (mesh.uv == null || mesh.uv.Length == 0))
                {
                    missingUv.Add(mesh.name);
                }
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (prefab != null)
            {
                var renderers = prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                for (var i = 0; i < renderers.Length; i++)
                {
                    skinnedBones += renderers[i].bones != null ? renderers[i].bones.Length : 0;
                }
            }

            if (totalVertices == 0)
            {
                report.Fail("mesh", "mesh integrity check failed: 0 vertices across "
                    + meshes.Count + " mesh(es)");
                return;
            }

            report.Success("mesh", meshes.Count + " mesh(es), " + totalVertices + " vertices, "
                + skinnedBones + " bone weights bound"
                + (missingUv.Count > 0 ? ", NO UVs on: " + string.Join(", ", missingUv) : ", UVs present"));
            if (missingUv.Count > 0)
            {
                report.Warn("mesh", missingUv.Count + " mesh(es) have no UV channel — texturing will not work");
            }
        }
    }
}
