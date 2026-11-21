using System;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEngine;

[assembly: InternalsVisibleTo("BattleHorizons.EditModeTests")]

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

            if (IsProductionCharacterTexture(assetPath))
            {
                var isNormalMap = assetPath.IndexOf("Normal", StringComparison.OrdinalIgnoreCase) >= 0;
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.textureType = isNormalMap
                    ? TextureImporterType.NormalMap
                    : TextureImporterType.Default;
                settings.sRGBTexture = !isNormalMap;
                settings.convertToNormalMap = false;
                settings.mipmapEnabled = true;
                importer.SetTextureSettings(settings);

                // Exact source character maps are 256²–2048². Preserve their
                // authored resolution rather than applying the 512² prop budget.
                importer.maxTextureSize = 2048;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.npotScale = TextureImporterNPOTScale.ToNearest;
                var characterAndroid = importer.GetPlatformTextureSettings("Android");
                characterAndroid.overridden = true;
                characterAndroid.maxTextureSize = 2048;
                characterAndroid.format = TextureImporterFormat.ASTC_6x6;
                importer.SetPlatformTextureSettings(characterAndroid);
                return;
            }

            if (IsGroundPhoto(assetPath))
            {
                // The two authored world-ground photos tile across the whole
                // playable disk. Under the 512 px prop budget their grass
                // detail averages into grey mud on device mips (see the
                // 2026-11-21 grey-grass fix), so they keep their authored
                // 2048 px resolution with ASTC 6x6, aniso 8 and a negative
                // mip bias - the same treatment as the character maps.
                // mip bias / aniso stay in the .meta textureSettings block
                // (Unity 6 exposes no C# setter for mipBias); the importer
                // honours the committed values on every reimport.
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = true;
                importer.mipmapEnabled = true;
                importer.anisoLevel = 8;
                importer.maxTextureSize = 2048;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.npotScale = TextureImporterNPOTScale.ToNearest;
                var groundAndroid = importer.GetPlatformTextureSettings("Android");
                groundAndroid.overridden = true;
                groundAndroid.maxTextureSize = 2048;
                groundAndroid.format = TextureImporterFormat.ASTC_6x6;
                importer.SetPlatformTextureSettings(groundAndroid);
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
                var uiMaxSize = (assetPath.Contains("loading_bg") || assetPath.Contains("home_bg"))
                    ? 2048
                    : MaxTextureSize;
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

            // Derived maps written by UserTextureMaterialBuilder: `*_n.png` are
            // tangent-space normal maps (linear, NormalMap type) and `*_d.png`
            // are decal RGBA maps whose alpha came from a black-background key.
            var derivedNormal = IsDerivedNormalMap(assetPath);
            var derivedAlpha = IsDerivedAlphaMap(assetPath);

            importer.textureType = derivedNormal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = !derivedNormal;
            if (derivedAlpha || assetPath.Contains("/Decals/"))
            {
                importer.alphaIsTransparency = derivedAlpha;
                importer.wrapMode = TextureWrapMode.Clamp;
            }
            else
            {
                importer.wrapMode = TextureWrapMode.Repeat;
            }

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

            if (IsProductionCharacterTexture(assetPath))
            {
                if (texture.width > 2048 || texture.height > 2048)
                {
                    Debug.LogError(string.Format(
                        "[bh][import] {0} is {1}x{2}; exact production character maps are capped at 2048x2048",
                        assetPath, texture.width, texture.height));
                }
                return;
            }

            if (texture.width > MaxTextureSize || texture.height > MaxTextureSize)
            {
                Debug.LogError(string.Format(
                    "[bh][import] {0} is {1}x{2}; docs/ASSET_PIPELINE.md caps textures at {3}x{3}",
                    assetPath, texture.width, texture.height, MaxTextureSize));
            }
        }

        /// <summary>
        /// Exact production Male/Female character texture folders need their
        /// committed NormalMap/Default settings and authored resolution. The
        /// 512² budget still applies to environment/UI textures and to the
        /// isolated GLTF fallback.
        /// </summary>
        /// <summary>True for the builder-derived tangent-space normal maps (`*_n.png`).</summary>
        internal static bool IsDerivedNormalMap(string path) =>
            path != null && path.EndsWith("_n.png", StringComparison.OrdinalIgnoreCase);

        /// <summary>True for the builder-derived decal alpha maps (`*_d.png`).</summary>
        internal static bool IsDerivedAlphaMap(string path) =>
            path != null && path.EndsWith("_d.png", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// True for the authored world-ground albedo photos that
        /// <c>SiranWorldBuilder.ApplyGroundPhotos</c> tiles across the land.
        /// </summary>
        internal static bool IsGroundPhoto(string path)
        {
            return path != null
                && path.StartsWith("Assets/Resources/Art/Textures/ground_",
                    StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsProductionCharacterTexture(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            return path.StartsWith("Assets/Characters/Male/Textures/", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("Assets/Characters/Female/Textures/", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// True for model paths that must import as Humanoid: the generated
        /// <c>Characters/</c> props and the hand-authored UserAssets rigs
        /// (<c>Male/</c>, <c>Female/</c>). Matching is case-insensitive and
        /// uses Unity's forward-slash <c>assetPath</c> form.
        ///
        /// History: the <c>Animations/</c> clip library used to match here too;
        /// every animation FBX was removed from the project on 2026-11-20, so
        /// the rule no longer covers that folder.
        /// </summary>
        internal static bool IsHumanoidAsset(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            return path.Contains("/Characters/", StringComparison.OrdinalIgnoreCase)
                || path.Contains("/UserAssets/Models/Male/", StringComparison.OrdinalIgnoreCase)
                || path.Contains("/UserAssets/Models/Female/", StringComparison.OrdinalIgnoreCase)
                || path.Contains("/UserAssets/Models/Animation/", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Respects an explicit Generic rig choice recorded by
        /// CharacterImportService ("force Generic", listed in
        /// Assets/Generated/GenericRigs.txt). Unity's default animation type
        /// for a fresh FBX is also Generic, so only a *recorded* choice is
        /// honoured — never an accidental default. Unmarked files follow the
        /// pipeline default (Humanoid inside the character folders).
        /// </summary>
        internal static bool RespectsExplicitGeneric(string path, ModelImporter importer)
        {
            if (string.IsNullOrEmpty(path) || importer == null)
            {
                return false;
            }

            if (importer.animationType != ModelImporterAnimationType.Generic)
            {
                return false;
            }

            return CharacterAnimationConfigLibrary.IsExplicitGeneric(path);
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

            if (RespectsExplicitGeneric(assetPath, importer))
            {
                // Manual Generic choice (Character & Animation Manager escape
                // hatch): keep the user's rig decision, only apply pipeline
                // defaults for animation import itself.
                importer.importAnimation = true;
                importer.animationCompression = ModelImporterAnimationCompression.Optimal;
                return;
            }

            if (IsHumanoidAsset(assetPath))
            {
                importer.animationType = ModelImporterAnimationType.Human;
                // Also applies to UserAssets/Models/Animation: clips there
                // retarget onto this model's avatar. "Copy From Other Avatar"
                // stays an optional manual override in the importer for rigs
                // that deliberately reuse an existing avatar.
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
