using System;
using BattleHorizons.GLTFImportTest;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BattleHorizons.EditorTools
{
    /// <summary>
    /// Generates the isolated GLTF comparison scene from code. This avoids
    /// hand-written scene YAML and guarantees that Unity 6 serializes the
    /// correct script GUIDs and component fields.
    /// </summary>
    public static class GLTFImportTestBuilder
    {
        public const string ScenePath =
            "Assets/Characters/GLTF_Import_Test/GLTF_Player_Import_Test.unity";

        [MenuItem("Battle Horizons/GLTF Test/Build Isolated Scene")]
        public static void BuildScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("GLTF_Import_Test_Root");
            var bootstrap = root.AddComponent<IsolatedGltfTestBootstrap>();
            bootstrap.MalePath = IsolatedGltfTestBootstrap.MaleGltfPath;
            bootstrap.FemalePath = IsolatedGltfTestBootstrap.FemaleGltfPath;
            bootstrap.BuildEnvironment();

            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                throw new InvalidOperationException("Could not save " + ScenePath);
            }

            AssetDatabase.Refresh();
            Debug.Log("[bh][gltf-test] built isolated scene " + ScenePath);
        }

        public static void BuildSceneFromCommandLine()
        {
            try
            {
                BuildScene();
            }
            catch (Exception exception)
            {
                Debug.LogError("[bh][gltf-test] scene build failed: " + exception);
                throw;
            }
        }
    }
}
