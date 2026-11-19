using System;
using System.IO;
using System.Threading.Tasks;
using GLTFast;
using UnityEngine;

namespace BattleHorizons.GLTFImportTest
{
    /// <summary>
    /// Loads one isolated glTF character into a test-only GameObject.
    ///
    /// This component deliberately has no dependency on the gameplay player,
    /// AnimatorController, Avatar or animation FBX system. It is used only by
    /// GLTF_Player_Import_Test.unity and the isolated PlayMode test.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class IsolatedGltfTestLoader : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Path relative to Unity's Assets folder. The test copies use corrected URI names.")]
        private string gltfRelativePath = "Characters/GLTF_Import_Test/Male/UnityTestCopy/Superhero_Male_FullBody.gltf";

        [SerializeField]
        private bool loadOnStart = true;

        private GltfImport importer;

        public string GltfRelativePath
        {
            get => gltfRelativePath;
            set => gltfRelativePath = value;
        }

        public bool LoadOnStart
        {
            get => loadOnStart;
            set => loadOnStart = value;
        }

        public bool IsDone { get; private set; }

        public bool Success { get; private set; }

        public string ErrorMessage { get; private set; } = string.Empty;

        public GameObject RootInstance { get; private set; }

        private async void Start()
        {
            if (loadOnStart && Application.isPlaying)
            {
                await LoadAsync();
            }
        }

        /// <summary>
        /// Loads the glTF using the already-pinned Unity glTFast 6.21.0 package.
        /// The absolute source file URI is passed as the base URI so BIN/PNG
        /// references resolve case-sensitively from the test-copy folder.
        /// </summary>
        public async Task<bool> LoadAsync()
        {
            if (IsDone)
            {
                return Success;
            }

            try
            {
                var localPath = Path.GetFullPath(Path.Combine(Application.dataPath, gltfRelativePath));
                if (!File.Exists(localPath))
                {
                    throw new FileNotFoundException("Isolated GLTF test file not found", localPath);
                }

                importer = new GltfImport();
                var baseUri = new Uri(localPath);
                var loaded = await importer.LoadFile(localPath, baseUri);
                if (!loaded)
                {
                    throw new InvalidOperationException("glTFast reported a critical load error for " + localPath);
                }

                var instantiated = await importer.InstantiateMainSceneAsync(transform);
                if (!instantiated)
                {
                    throw new InvalidOperationException("glTFast could not instantiate the main scene for " + localPath);
                }

                RootInstance = gameObject;
                Success = true;
                return true;
            }
            catch (Exception exception)
            {
                Success = false;
                ErrorMessage = exception.ToString();
                Debug.LogError("[bh][gltf-test] " + ErrorMessage);
                return false;
            }
            finally
            {
                IsDone = true;
            }
        }

        private void OnDestroy()
        {
            // The imported materials/textures are referenced by the instantiated
            // renderers, so the importer is intentionally kept alive with this
            // component rather than disposed while the test object exists.
            importer = null;
        }
    }
}
