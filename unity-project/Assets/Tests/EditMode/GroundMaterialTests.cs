using BattleHorizons.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BattleHorizons.Tests.EditMode
{
    /// <summary>
    /// Regression locks for the 2026-11-21 "grey grass" device screenshot:
    /// the world material must bind BOTH authored ground photos, tile them at
    /// 4 m, and the photo importers must keep mip streaming off with a negative
    /// mip bias and full 2048 px resolution so device mips stay green and sharp.
    /// The numeric albedo probe lives in
    /// <c>processing/unity/validate_unity_map.py</c>; these tests lock the
    /// Unity-side half of the same contract.
    /// </summary>
    public class GroundMaterialTests
    {
        private const string SparsePath = "Assets/Resources/Art/Textures/ground_grass_dirt.jpg";
        private const string DensePath = "Assets/Resources/Art/Textures/ground_leaves_dense.jpg";

        [Test]
        public void WorldMaterial_BindsBothGroundPhotosAtFourMetreTile()
        {
            var shader = Shader.Find(SiranRadiusClip.ShaderName);
            Assert.IsNotNull(shader, "radius-clip shader must exist");

            var material = new Material(shader);
            try
            {
                SiranWorldBuilder.ApplyGroundPhotos(material);

                Assert.IsNotNull(material.GetTexture("_BaseMap"),
                    "sparse grass/dirt photo must be bound to _BaseMap");
                Assert.IsNotNull(material.GetTexture("_GroundMapB"),
                    "dense green leaf carpet must be bound to _GroundMapB");
                Assert.AreEqual(4f, material.GetFloat("_GroundTile"), 0.001f,
                    "ground photos must tile at 4 m so device mips keep grass detail");
            }
            finally
            {
                Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void GroundPhotoImporters_KeepFullResolutionAndSharpMips()
        {
            foreach (var path in new[] { SparsePath, DensePath })
            {
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                Assert.IsNotNull(importer, path + " must be imported as a texture");
                Assert.IsFalse(importer.streamingMipmaps,
                    path + ": mip streaming starves the ground photos on device");
                Assert.LessOrEqual(importer.mipBias, -0.4f,
                    path + ": negative mip bias keeps grass tufts crisp at range");
                Assert.GreaterOrEqual(importer.maxTextureSize, 2048,
                    path + ": photos must keep their 2048 px resolution");

                var android = importer.GetPlatformTextureSettings("Android");
                Assert.GreaterOrEqual(android.maxTextureSize, 2048,
                    path + ": Android override must not downscale the ground photos");
            }
        }

        [Test]
        public void RadiusClipShader_SamplesShadowsAndUsesDenseGreenBase()
        {
            var shaderSource = System.IO.File.ReadAllText(
                "Assets/Shaders/SiranRadiusClip.shader");
            StringAssert.Contains("mainLight.shadowAttenuation", shaderSource,
                "world shader must show player/prop contact shadows");
            StringAssert.Contains("lerp(dense, sparse, dirt)", shaderSource,
                "dense green leaf carpet must be the base with dirt as noise patches");
        }
    }
}
