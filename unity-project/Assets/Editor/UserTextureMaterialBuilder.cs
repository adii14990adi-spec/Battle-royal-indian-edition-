using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace BattleHorizons.EditorTools
{
    /// <summary>
    /// Turns the AI-generated environment textures in
    /// <c>Assets/UserAssets/Textures/</c> into ready URP materials:
    ///
    ///   * derives a tangent-space normal map (`*_n.png`) from each albedo via a
    ///     Sobel pass — no external baking tool needed;
    ///   * keys decals (`Textures/Decals/*.png`, painted on black) into RGBA
    ///     (`*_d.png`) with alpha = luminance, and builds URP **Decal** materials;
    ///   * builds URP **Lit** materials (matte house style: metallic 0,
    ///     smoothness 0.08, like the committed terrain materials) with sensible
    ///     per-texture tiling (red/white brick at 4×4 per the ProBuilder brief);
    ///   * builds a **Skybox/Panoramic** material for the morning panorama.
    ///
    /// Import settings come free from <see cref="AssetImportRules"/>: 512² cap,
    /// mipmaps on, ASTC 6×6 on Android, `_n.png` imported as NormalMap/linear
    /// and decal maps as Clamp — the committed UNISOC budget forbids 1024².
    /// </summary>
    public static class UserTextureMaterialBuilder
    {
        private const string TextureRoot = "Assets/UserAssets/Textures";
        private const string MaterialRoot = "Assets/UserAssets/Materials";
        private const string NormalSuffix = "_n.png";
        private const string DecalSuffix = "_d.png";
        private const float NormalStrength = 2.0f;

        /// <summary>World-unit tiling per texture (file name without extension).</summary>
        internal static Vector2 TilingFor(string textureName)
        {
            switch (textureName)
            {
                case "T_RedBrick": return new Vector2(4f, 4f);
                case "T_WhiteBrick": return new Vector2(4f, 4f);
                case "T_DarkConcrete": return new Vector2(4f, 4f);
                case "T_GrassDirt": return new Vector2(6f, 6f);
                case "T_WoodPlanks": return new Vector2(3f, 3f);
                case "T_RoofTiles": return new Vector2(4f, 4f);
                case "T_AsphaltRoad": return new Vector2(2f, 6f);
                case "T_LootCrate": return new Vector2(1f, 1f);
                default: return new Vector2(2f, 2f);
            }
        }

        [MenuItem("Battle Horizons/Assets/Build Materials From User Textures")]
        public static void BuildAll()
        {
            if (!AssetDatabase.IsValidFolder(TextureRoot))
            {
                Debug.LogError($"[bh][editor] {TextureRoot} does not exist — nothing to build.");
                return;
            }

            EnsureFolder(MaterialRoot);
            var built = 0;

            foreach (var path in EnumeratePngs(TextureRoot))
            {
                var name = Path.GetFileNameWithoutExtension(path);
                if (name.EndsWith("_n", StringComparison.OrdinalIgnoreCase) ||
                    name.EndsWith("_d", StringComparison.OrdinalIgnoreCase))
                {
                    continue; // derived maps are inputs, not sources
                }

                if (path.Contains("/Decals/"))
                {
                    BuildDecalMaterial(path);
                    built++;
                    continue;
                }

                if (name.Contains("Skybox"))
                {
                    BuildSkyboxMaterial(path);
                    built++;
                    continue;
                }

                BuildLitMaterial(path);
                built++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[bh][editor] {built} material(s) built under {MaterialRoot} " +
                      "(URP Lit + derived normal maps, Decal, Panoramic skybox)");
        }

        [MenuItem("Battle Horizons/Assets/Apply Morning Skybox")]
        public static void ApplyMorningSkybox()
        {
            var skybox = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialRoot}/M_SkyboxMorning.mat");
            if (skybox == null)
            {
                Debug.LogError("[bh][editor] build the skybox material first " +
                               "(Battle Horizons > Assets > Build Materials From User Textures).");
                return;
            }

            RenderSettings.skybox = skybox;
            EditorUtility.SetDirty(RenderSettings.instance);
            Debug.Log("[bh][editor] scene skybox set to the morning panorama; save the scene to keep it. " +
                      "ThirdPersonCameraController switches its clear flags to Skybox automatically.");
        }

        /// <summary>
        /// ProBuilder workflow helper: duplicates the selected material with a
        /// custom tiling (menu prompts via the inspector of the copy). Select a
        /// material in the Project window first.
        /// </summary>
        [MenuItem("Battle Horizons/Assets/Create Tiled Variant Of Selected Material")]
        public static void CreateTiledVariant()
        {
            var source = Selection.activeObject as Material;
            if (source == null)
            {
                Debug.LogError("[bh][editor] select a material in the Project window first.");
                return;
            }

            var copy = new Material(source) { name = source.name + "_Tiled" };
            AssetDatabase.CreateAsset(copy, $"{MaterialRoot}/{copy.name}.mat");
            Debug.Log($"[bh][editor] created {copy.name}; set Main Map tiling X/Y in the inspector " +
                      "(4×4 is the brick standard from the POI brief).");
        }

        // --- Builders -------------------------------------------------------

        private static void BuildLitMaterial(string texturePath)
        {
            var name = Path.GetFileNameWithoutExtension(texturePath);
            var albedo = Load<Texture2D>(texturePath);
            var normalPath = EnsureDerivedMap(texturePath, NormalSuffix, WriteNormalMapPng);
            var normal = normalPath != null ? Load<Texture2D>(normalPath) : null;

            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"))
            {
                name = "M_" + name,
            };
            material.SetTexture("_BaseMap", albedo);
            material.SetTextureScale("_BaseMap", TilingFor(name));
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Metallic", 0f);          // matte house style
            material.SetFloat("_Smoothness", 0.08f);
            if (normal != null)
            {
                material.SetTexture("_BumpMap", normal);
                material.SetFloat("_BumpScale", 0.7f);
                material.EnableKeyword("_NORMALMAP");
            }

            Save(material, $"{MaterialRoot}/M_{name}.mat");
        }

        private static void BuildDecalMaterial(string texturePath)
        {
            var name = Path.GetFileNameWithoutExtension(texturePath);
            var alphaPath = EnsureDerivedMap(texturePath, DecalSuffix, WriteDecalAlphaPng);
            var map = Load<Texture2D>(alphaPath ?? texturePath);

            var shader = Shader.Find("Universal Render Pipeline/Decal");
            if (shader == null)
            {
                Debug.LogWarning("[bh][editor] URP Decal shader unavailable — falling back to Lit for " + name);
                shader = Shader.Find("Universal Render Pipeline/Lit");
            }

            var material = new Material(shader) { name = "M_" + name };
            material.SetTexture("_BaseMap", map);
            material.SetColor("_BaseColor", Color.white);
            Save(material, $"{MaterialRoot}/M_{name}.mat");
        }

        private static void BuildSkyboxMaterial(string texturePath)
        {
            var name = Path.GetFileNameWithoutExtension(texturePath);
            var panorama = Load<Texture2D>(texturePath);

            var material = new Material(Shader.Find("Skybox/Panoramic")) { name = "M_" + name };
            material.SetTexture("_MainTex", panorama);
            material.SetFloat("_Exposure", 1f);
            material.SetFloat("_Rotation", 0f);
            // 1 = Latitude-Longitude layout (equirectangular).
            material.SetFloat("_Mapping", 1f);
            Save(material, $"{MaterialRoot}/M_{name}.mat");
        }

        // --- Derived maps ---------------------------------------------------

        private static string EnsureDerivedMap(string sourcePath, string suffix, Func<Texture2D, byte[]> writer)
        {
            var directory = Path.GetDirectoryName(sourcePath)?.Replace('\\', '/');
            var stem = Path.GetFileNameWithoutExtension(sourcePath);
            var derivedPath = $"{directory}/{stem}{suffix}";

            if (File.Exists(derivedPath))
            {
                return derivedPath;
            }

            var source = ReadSourcePixels(sourcePath, out var wasReadable);
            if (source == null)
            {
                return null;
            }

            try
            {
                File.WriteAllBytes(derivedPath, writer(source));
            }
            finally
            {
                RestoreReadability(sourcePath, wasReadable);
            }

            AssetDatabase.ImportAsset(derivedPath);
            return derivedPath;
        }

        private static byte[] WriteNormalMapPng(Texture2D source)
        {
            var width = source.width;
            var height = source.height;
            var pixels = source.GetPixels32();
            var heights = new float[width * height];
            for (var i = 0; i < pixels.Length; i++)
            {
                heights[i] = (0.299f * pixels[i].r + 0.587f * pixels[i].g + 0.114f * pixels[i].b) / 255f;
            }

            var normals = new Color32[width * height];
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var left = heights[y * width + Wrap(x - 1, width)];
                    var right = heights[y * width + Wrap(x + 1, width)];
                    var down = heights[Wrap(y - 1, height) * width + x];
                    var up = heights[Wrap(y + 1, height) * width + x];

                    var dx = (right - left) * NormalStrength;
                    var dy = (up - down) * NormalStrength;
                    var n = new Vector3(-dx, -dy, 1f).normalized;
                    normals[y * width + x] = new Color32(
                        (byte)((n.x * 0.5f + 0.5f) * 255f),
                        (byte)((n.y * 0.5f + 0.5f) * 255f),
                        (byte)((n.z * 0.5f + 0.5f) * 255f),
                        255);
                }
            }

            return EncodePng(width, height, normals);
        }

        private static byte[] WriteDecalAlphaPng(Texture2D source)
        {
            var pixels = source.GetPixels32();
            var keyed = new Color32[pixels.Length];
            for (var i = 0; i < pixels.Length; i++)
            {
                // Decals are painted on pure black: alpha = max channel, so the
                // background keys out and the artwork keeps its own colour.
                var alpha = Math.Max(pixels[i].r, Math.Max(pixels[i].g, pixels[i].b));
                keyed[i] = new Color32(pixels[i].r, pixels[i].g, pixels[i].b, alpha);
            }

            return EncodePng(source.width, source.height, keyed);
        }

        private static byte[] EncodePng(int width, int height, Color32[] pixels)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels);
            var bytes = texture.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(texture);
            return bytes;
        }

        private static Texture2D ReadSourcePixels(string path, out bool wasReadable)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            wasReadable = importer != null && importer.isReadable;
            if (importer != null && !importer.isReadable)
            {
                importer.isReadable = true;
                importer.SaveAndReimport();
            }

            var texture = Load<Texture2D>(path);
            if (texture == null)
            {
                Debug.LogError($"[bh][editor] cannot read pixels from {path}");
            }

            return texture;
        }

        private static void RestoreReadability(string path, bool wasReadable)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null && importer.isReadable != wasReadable)
            {
                importer.isReadable = wasReadable;
                importer.SaveAndReimport();
            }
        }

        // --- Helpers ----------------------------------------------------------

        private static int Wrap(int index, int size) => (index + size) % size;

        private static IEnumerable<string> EnumeratePngs(string folder)
        {
            var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folder });
            var paths = new List<string>(guids.Length);
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                {
                    paths.Add(path);
                }
            }

            paths.Sort(StringComparer.Ordinal);
            return paths;
        }

        private static T Load<T>(string path) where T : UnityEngine.Object =>
            AssetDatabase.LoadAssetAtPath<T>(path);

        private static void Save(Material material, string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(material, existing);
                UnityEngine.Object.DestroyImmediate(material);
                return;
            }

            AssetDatabase.CreateAsset(material, path);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            var leaf = Path.GetFileName(path);
            if (string.IsNullOrEmpty(parent))
            {
                return;
            }

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
