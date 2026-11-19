using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace BattleHorizons.EditorTools
{
    /// <summary>
    /// Deterministic material import for the two production character FBXs.
    /// Source FBX binaries and GUIDs are immutable, so Unity's ImportStandard
    /// mode is used with explicit external URP/Lit material remaps. The
    /// externalObjects maps in the committed .fbx.meta files are the source of
    /// truth; this scoped postprocessor enforces the importer settings and
    /// returns the exact material for each FBX material name during import.
    /// </summary>
    public sealed class ProductionFBXMaterialImportRules : AssetPostprocessor
    {
        public const string MaleModelPath = "Assets/Characters/Male/Male character.fbx";
        public const string FemaleModelPath = "Assets/Characters/Female/Female character.fbx";

        internal static bool IsProductionCharacterModel(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            return string.Equals(path, MaleModelPath, StringComparison.OrdinalIgnoreCase)
                || string.Equals(path, FemaleModelPath, StringComparison.OrdinalIgnoreCase);
        }

        private void OnPreprocessModel()
        {
            if (!IsProductionCharacterModel(assetPath)) return;
            var importer = assetImporter as ModelImporter;
            if (importer == null) return;

            // ImportStandard=1 is the only mode that generates FBX material
            // sub-assets; materialName=BasedOnMaterialName preserves MI_* names.
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.materialName = ModelImporterMaterialName.BasedOnMaterialName;
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
            importer.materialSearch = ModelImporterMaterialSearch.Local;
        }

        private Material OnAssignMaterialModel(Material material, Renderer renderer)
        {
            if (!IsProductionCharacterModel(assetPath) || material == null) return null;
            var replacement = ResolveExternalMaterial(assetPath, material.name);
            if (replacement == null)
            {
                Debug.LogError("[bh][fbx-repair] no production URP/Lit remap for material '"
                    + material.name + "' in " + assetPath);
                return null;
            }
            return replacement;
        }

        internal static Material ResolveExternalMaterial(string modelPath, string materialName)
        {
            var name = NormalizeMaterialName(materialName);
            var folder = Path.GetDirectoryName(modelPath.Replace('\\', '/')) + "/Materials";
            foreach (var candidate in MaterialNames)
            {
                if (string.Equals(candidate, name, StringComparison.OrdinalIgnoreCase))
                {
                    return AssetDatabase.LoadAssetAtPath<Material>(folder + "/" + candidate + ".mat");
                }
            }
            return null;
        }

        private static readonly string[] MaterialNames =
        {
            "MI_Eyes", "MI_Hair_1", "MI_Hair_2", "MI_Superhero_Male", "MI_Superhero_Female",
        };

        private static string NormalizeMaterialName(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            var marker = value.IndexOf(" (Instance)", StringComparison.Ordinal);
            return (marker >= 0 ? value.Substring(0, marker) : value).Trim();
        }

        private void OnPostprocessModel(GameObject root)
        {
            if (!IsProductionCharacterModel(assetPath) || root == null) return;
            var expectedFolder = Path.GetDirectoryName(assetPath.Replace('\\', '/')) + "/Materials/";
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                var shared = renderer.sharedMaterials;
                for (var i = 0; i < shared.Length; i++)
                {
                    var material = shared[i];
                    if (material == null || material.shader == null)
                    {
                        Debug.LogError("[bh][fbx-repair] missing production material on " + renderer.name);
                        continue;
                    }
                    var materialPath = AssetDatabase.GetAssetPath(material);
                    if (!materialPath.StartsWith(expectedFolder, StringComparison.OrdinalIgnoreCase)
                        || material.shader.name != "Universal Render Pipeline/Lit")
                    {
                        Debug.LogError("[bh][fbx-repair] renderer " + renderer.name
                            + " did not receive a production URP/Lit material: " + materialPath
                            + " shader=" + material.shader.name);
                    }
                }
            }
        }
    }
}
