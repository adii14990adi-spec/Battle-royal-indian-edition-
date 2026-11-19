using System.Collections;
using BattleHorizons.GLTFImportTest;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BattleHorizons.Tests
{
    public sealed class GLTFImportTestPlayModeTests
    {
        [UnityTest]
        public IEnumerator IsolatedRuntimeLoaderLoadsBothCharacters()
        {
            yield return LoadOne(IsolatedGltfTestBootstrap.MaleGltfPath, "Male");
            yield return LoadOne(IsolatedGltfTestBootstrap.FemaleGltfPath, "Female");
        }

        private static IEnumerator LoadOne(string path, string label)
        {
            var gameObject = new GameObject("Isolated" + label + "RuntimeLoad");
            var loader = gameObject.AddComponent<IsolatedGltfTestLoader>();
            loader.GltfRelativePath = path;
            loader.LoadOnStart = false;

            _ = loader.LoadAsync();
            yield return new WaitUntil(() => loader.IsDone);

            Assert.IsTrue(loader.Success, label + " glTFast runtime load failed: " + loader.ErrorMessage);
            var renderers = gameObject.GetComponentsInChildren<Renderer>(true);
            Assert.GreaterOrEqual(renderers.Length, 3, label + " should expose all three body-part renderers");
            for (var i = 0; i < renderers.Length; i++)
            {
                Assert.IsNotNull(renderers[i].sharedMaterial, label + " renderer " + i + " has no material");
                Assert.IsNotNull(renderers[i].sharedMaterial.shader, label + " renderer " + i + " has no shader");
            }

            Object.Destroy(gameObject);
        }
    }
}
