using System.IO;
using System.Text.RegularExpressions;
using BattleHorizons.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BattleHorizons.Tests
{
    public sealed class GLTFImportTestEditModeTests
    {
        private const string MaleGltf =
            "Assets/Characters/GLTF_Import_Test/Male/UnityTestCopy/Superhero_Male_FullBody.gltf";
        private const string FemaleGltf =
            "Assets/Characters/GLTF_Import_Test/Female/UnityTestCopy/Superhero_Female_FullBody.gltf";

        [OneTimeSetUp]
        public void BuildGeneratedScene()
        {
            // The committed scene is a placeholder until the licensed Editor
            // runs this deterministic builder. Tests always generate it first.
            GLTFImportTestBuilder.BuildScene();
        }

        [Test]
        public void UnityGltfImporterIsAlreadyPinned()
        {
            var manifest = File.ReadAllText(Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Packages", "manifest.json")));
            StringAssert.Contains("\"com.unity.cloud.gltfast\": \"6.21.0\"", manifest);
            StringAssert.Contains("\"com.unity.modules.unitywebrequesttexture\": \"1.0.0\"", manifest);
        }

        [Test]
        public void CorrectedTestCopiesResolveAllKnownUriMismatches()
        {
            var male = File.ReadAllText(Path.GetFullPath(Path.Combine(Application.dataPath, "..", MaleGltf)));
            var female = File.ReadAllText(Path.GetFullPath(Path.Combine(Application.dataPath, "..", FemaleGltf)));
            Assert.IsFalse(Regex.IsMatch(male, "T_Hair_1_Normal_png\\.png|T_Eye_Normal_png\\.png"),
                "Male test copy still references a missing PNG URI");
            Assert.IsFalse(Regex.IsMatch(female, "T_Eye_Normal_png\\.png"),
                "Female test copy still references a missing PNG URI");
            StringAssert.Contains("T_Hair_1_Normal.png", male);
            StringAssert.Contains("T_Eye_Normal.png", male);
            StringAssert.Contains("T_Eye_Normal.png", female);
        }

        [Test]
        public void EditorValidationHasNoFailures()
        {
            string report;
            var ok = GLTFImportTestValidator.ValidateAll(out report);
            Assert.IsTrue(ok, report);
        }
    }
}
