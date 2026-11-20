using BattleHorizons.EditorTools;
using NUnit.Framework;
using UnityEngine;

namespace BattleHorizons.Tests.EditMode
{
    /// <summary>
    /// Locks the user-texture import contract: the importer must recognise the
    /// builder's derived maps by suffix (normal `_n.png` → linear NormalMap,
    /// decal `_d.png` → alpha), and the material tiling table must keep the
    /// ProBuilder brief (brick walls at 4×4).
    /// </summary>
    public class UserAssetPipelineTests
    {
        [Test]
        public void DerivedNormalMapsAreDetectedBySuffix()
        {
            Assert.IsTrue(AssetImportRules.IsDerivedNormalMap(
                "Assets/UserAssets/Textures/T_RedBrick_n.png"));
            Assert.IsTrue(AssetImportRules.IsDerivedNormalMap(
                "assets/userassets/textures/t_redbrick_n.PNG"), "case-insensitive");
            Assert.IsFalse(AssetImportRules.IsDerivedNormalMap(
                "Assets/UserAssets/Textures/T_RedBrick.png"), "albedo is not a normal map");
            Assert.IsFalse(AssetImportRules.IsDerivedNormalMap(null));
        }

        [Test]
        public void DerivedDecalAlphaMapsAreDetectedBySuffix()
        {
            Assert.IsTrue(AssetImportRules.IsDerivedAlphaMap(
                "Assets/UserAssets/Textures/Decals/D_BulletHole_d.png"));
            Assert.IsFalse(AssetImportRules.IsDerivedAlphaMap(
                "Assets/UserAssets/Textures/Decals/D_BulletHole.png"));
            Assert.IsFalse(AssetImportRules.IsDerivedAlphaMap(string.Empty));
        }

        [Test]
        public void TilingTableFollowsThePoiBrief()
        {
            // The brief: brick tiles at (4, 4) on ProBuilder walls.
            Assert.AreEqual(new Vector2(4f, 4f), UserTextureMaterialBuilder.TilingFor("T_RedBrick"));
            Assert.AreEqual(new Vector2(4f, 4f), UserTextureMaterialBuilder.TilingFor("T_WhiteBrick"));
            Assert.AreEqual(new Vector2(4f, 4f), UserTextureMaterialBuilder.TilingFor("T_DarkConcrete"));
            Assert.AreEqual(new Vector2(2f, 6f), UserTextureMaterialBuilder.TilingFor("T_AsphaltRoad"));
            Assert.AreEqual(Vector2.one, UserTextureMaterialBuilder.TilingFor("T_LootCrate"));
            Assert.AreEqual(new Vector2(2f, 2f), UserTextureMaterialBuilder.TilingFor("T_UnknownProp"),
                "unknown textures get a sane 2×2 default");
        }

        [Test]
        public void ProductionCharacterScopingIsUnaffectedByDerivedMaps()
        {
            Assert.IsTrue(AssetImportRules.IsProductionCharacterTexture(
                "Assets/Characters/Female/Textures/T_Superhero_Female_Normal.png"));
            Assert.IsFalse(AssetImportRules.IsProductionCharacterTexture(
                "Assets/UserAssets/Textures/T_RedBrick_n.png"),
                "environment derived maps must stay inside the 512² prop budget");
        }
    }
}
