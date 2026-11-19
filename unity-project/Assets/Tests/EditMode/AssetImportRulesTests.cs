using BattleHorizons.EditorTools;
using NUnit.Framework;

namespace BattleHorizons.Tests.EditMode
{
    /// <summary>
    /// Guards the folder routing in <see cref="AssetImportRules"/>: the
    /// UserAssets character folders and the generated Characters folder must
    /// import as Humanoid, ordinary environment props must not.
    /// </summary>
    public class AssetImportRulesTests
    {
        [Test]
        public void UserAssetsModelFoldersAreHumanoid()
        {
            Assert.IsTrue(AssetImportRules.IsHumanoidAsset(
                "Assets/UserAssets/Models/Male/Male character.fbx"), "Male folder");
            Assert.IsTrue(AssetImportRules.IsHumanoidAsset(
                "Assets/UserAssets/Models/Female/Female character.fbx"), "Female folder");
            Assert.IsTrue(AssetImportRules.IsHumanoidAsset(
                "Assets/UserAssets/Models/Animation/Crouch Walking.fbx"), "Animation folder");
        }

        [Test]
        public void GeneratedCharactersFolderIsHumanoid()
        {
            Assert.IsTrue(AssetImportRules.IsHumanoidAsset(
                "Assets/Art/Characters/Guard.fbx"), "/Characters/ folder");
        }

        [Test]
        public void MatchingIsCaseInsensitive()
        {
            Assert.IsTrue(AssetImportRules.IsHumanoidAsset(
                "assets/userassets/models/male/idle.fbx"), "lower-case path");
            Assert.IsTrue(AssetImportRules.IsHumanoidAsset(
                "ASSETS/ART/CHARACTERS/Guard.fbx"), "upper-case path");
        }

        [Test]
        public void ProductionCharacterTexturesAreScopedOutsideThePropBudget()
        {
            Assert.IsTrue(AssetImportRules.IsProductionCharacterTexture(
                "Assets/Characters/Male/Textures/T_Superhero_Male_Normal.png"), "Male normal map");
            Assert.IsTrue(AssetImportRules.IsProductionCharacterTexture(
                "Assets/Characters/Female/Textures/T_Superhero_Female_Normal.png"), "Female normal map");
            Assert.IsFalse(AssetImportRules.IsProductionCharacterTexture(
                "Assets/Characters/GLTF_Import_Test/Female/Original/T_Superhero_Female_Normal.png"), "GLTF fallback");
            Assert.IsFalse(AssetImportRules.IsProductionCharacterTexture(
                "Assets/Resources/Art/Textures/water_normal_256.png"), "environment texture");
            Assert.IsFalse(AssetImportRules.IsProductionCharacterTexture(null), "null path");
        }

        [Test]
        public void EnvironmentPropsAreNotHumanoid()
        {
            Assert.IsFalse(AssetImportRules.IsHumanoidAsset(
                "Assets/Art/Environment/crate.fbx"), "environment prop");
            Assert.IsFalse(AssetImportRules.IsHumanoidAsset(null), "null path");
            Assert.IsFalse(AssetImportRules.IsHumanoidAsset(string.Empty), "empty path");
        }
    }
}
