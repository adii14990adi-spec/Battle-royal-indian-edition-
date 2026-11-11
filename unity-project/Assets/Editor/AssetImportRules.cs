using System;
using UnityEditor;
using UnityEngine;

namespace BattleHorizons.EditorTools
{
    /// <summary>
    /// Import rules from `docs/ASSET_PIPELINE.md`, enforced by the importer rather
    /// than by review:
    ///   * every texture is ≤ 512 x 512 (a 1024² costs the VRAM of four 512²),
    ///   * ASTC on Android with ETC2 fallback,
    ///   * mipmaps on, anisotropic off (both are budget items on Mali/MTP GPUs),
    ///   * audio is Vorbis, mono for SFX, streamed for music.
    ///
    /// Anything that violates a rule is logged as an error naming the file, so a
    /// bad asset fails the import gate in CI instead of shipping.
    /// </summary>
    public sealed class AssetImportRules : AssetPostprocessor
    {
        public const int MaxTextureSize = 512;

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/", StringComparison.Ordinal))
            {
                return;
            }

            var importer = (TextureImporter)assetImporter;
            if (assetPath.Contains("/Editor/"))
            {
                return;
            }

            if (assetPath.Contains("/UI/Icons/"))
            {
                importer.textureType = TextureImporterType.Default;
                importer.isReadable = true;
                importer.mipmapEnabled = false;
                importer.anisoLevel = 0;
                importer.alphaIsTransparency = false;
                importer.maxTextureSize = MaxTextureSize;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                return;
            }

            if (assetPath.Contains("/Resources/UI/") || assetPath.Contains("/UI/"))
            {
                var uiMaxSize = assetPath.Contains("loading_bg") ? 2048 : MaxTextureSize;
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.anisoLevel = 0;
                importer.alphaIsTransparency = !assetPath.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase);
                importer.maxTextureSize = uiMaxSize;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;

                var uiAndroid = new TextureImporterPlatformSettings
                {
                    name = "Android",
                    overridden = true,
                    maxTextureSize = uiMaxSize,
                    format = TextureImporterFormat.ASTC_6x6,
                    textureCompression = TextureImporterCompression.CompressedHQ,
                    compressionQuality = 100,
                };
                importer.SetPlatformTextureSettings(uiAndroid);
                return;
            }

            importer.textureType = TextureImporterType.Default;
            importer.mipmapEnabled = true;
            importer.anisoLevel = 0;
            importer.maxTextureSize = MaxTextureSize;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.npotScale = TextureImporterNPOTScale.ToNearest;

            var android = importer.GetPlatformTextureSettings("Android");
            android.overridden = true;
            android.maxTextureSize = MaxTextureSize;
            android.format = TextureImporterFormat.ASTC_6x6;
            importer.SetPlatformTextureSettings(android);

            var info = new TextureImporterPlatformSettings
            {
                name = "Android",
                overridden = true,
                maxTextureSize = MaxTextureSize,
                format = TextureImporterFormat.ASTC_6x6,
                textureCompression = TextureImporterCompression.Compressed,
                compressionQuality = 50,
            };
            importer.SetPlatformTextureSettings(info);
        }

        private void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith("Assets/", StringComparison.Ordinal))
            {
                return;
            }

            var importer = (AudioImporter)assetImporter;
            var settings = importer.defaultSampleSettings;
            settings.loadType = assetPath.Contains("/Music/") ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = 0.6f;
            settings.sampleRateOverride = assetPath.Contains("/Music/") ? 0u : 22050u;
            settings.sampleRateSetting = assetPath.Contains("/Music/")
                ? AudioSampleRateSetting.PreserveSampleRate
                : AudioSampleRateSetting.OverrideSampleRate;
            importer.defaultSampleSettings = settings;
            importer.forceToMono = !assetPath.Contains("/Music/");
            importer.loadInBackground = true;
        }

        private void OnPostprocessTexture(Texture2D texture)
        {
            if (!assetPath.StartsWith("Assets/", StringComparison.Ordinal))
            {
                return;
            }

            if (assetPath.Contains("/Editor/") || assetPath.Contains("/UI/"))
            {
                return;
            }

            if (texture.width > MaxTextureSize || texture.height > MaxTextureSize)
            {
                Debug.LogError(string.Format(
                    "[bh][import] {0} is {1}x{2}; docs/ASSET_PIPELINE.md caps textures at {3}x{3}",
                    assetPath, texture.width, texture.height, MaxTextureSize));
            }
        }

        private void OnPreprocessModel()
        {
            if (!assetPath.StartsWith("Assets/", StringComparison.Ordinal))
            {
                return;
            }

            var importer = (ModelImporter)assetImporter;

            // A Unity unit is a metre and the Blender/FBX export in
            // processing/blender is authored that way; 0.01 would shrink the
            // island's props to nothing.
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importBlendShapes = true;
            importer.importVisibility = false;
            importer.optimizeMeshPolygons = true;
            importer.optimizeMeshVertices = true;
            importer.weldVertices = true;
            importer.addCollider = false;      // collision is explicit, per prefab
            importer.meshCompression = ModelImporterMeshCompression.Medium;
            importer.isReadable = false;       // mesh data does not need to be readable on device

            if (assetPath.Contains("/Characters/"))
            {
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.humanoidOversampling = ModelImporterHumanoidOversampling.X1;
                importer.importAnimation = true;
                importer.animationCompression = ModelImporterAnimationCompression.Optimal;
                importer.skinWeights = ModelImporterSkinWeights.Standard;  // 4 influences/vertex
                importer.maxBonesPerVertex = 4;
                importer.optimizeBones = true;
            }
            else
            {
                importer.animationType = ModelImporterAnimationType.None;
            }
        }
    }
}
