using UnityEngine;
using UnityEngine.Rendering;

namespace BattleHorizons.GLTFImportTest
{
    /// <summary>
    /// Builds an isolated comparison environment. It intentionally creates no
    /// gameplay player, movement, camera controller, networking, shooting or
    /// production Animator references.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class IsolatedGltfTestBootstrap : MonoBehaviour
    {
        public const string MaleGltfPath =
            "Characters/GLTF_Import_Test/Male/UnityTestCopy/Superhero_Male_FullBody.gltf";

        public const string FemaleGltfPath =
            "Characters/GLTF_Import_Test/Female/UnityTestCopy/Superhero_Female_FullBody.gltf";

        [SerializeField]
        private string maleGltfPath = MaleGltfPath;

        [SerializeField]
        private string femaleGltfPath = FemaleGltfPath;

        [SerializeField]
        private float characterSpacing = 2.4f;

        public string MalePath
        {
            get => maleGltfPath;
            set => maleGltfPath = value;
        }

        public string FemalePath
        {
            get => femaleGltfPath;
            set => femaleGltfPath = value;
        }

        private void Awake()
        {
            EnsureEnvironment();
        }

        public GameObject EnsureEnvironment()
        {
            var existing = transform.Find("Environment");
            if (existing != null)
            {
                return existing.gameObject;
            }

            return BuildEnvironment();
        }

        public GameObject BuildEnvironment()
        {
            var environment = new GameObject("Environment");
            environment.transform.SetParent(transform, false);

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.42f, 0.45f, 0.50f, 1f);

            var cameras = new GameObject("Cameras");
            cameras.transform.SetParent(environment.transform, false);
            var comparisonCamera = CreateCamera(
                "ComparisonCamera", cameras.transform, new Vector3(0f, 1.55f, -6.2f),
                new Vector3(0f, 1.0f, 0f), true, 46f);
            comparisonCamera.tag = "MainCamera";
            CreateCamera(
                "MaleCamera", cameras.transform, new Vector3(-characterSpacing * 0.5f, 1.55f, -4.4f),
                new Vector3(-characterSpacing * 0.5f, 1.0f, 0f), false, 38f);
            CreateCamera(
                "FemaleCamera", cameras.transform, new Vector3(characterSpacing * 0.5f, 1.55f, -4.4f),
                new Vector3(characterSpacing * 0.5f, 1.0f, 0f), false, 38f);

            var sun = new GameObject("DirectionalLight");
            sun.transform.SetParent(environment.transform, false);
            sun.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
            var sunLight = sun.AddComponent<Light>();
            sunLight.type = LightType.Directional;
            sunLight.intensity = 1.25f;
            sunLight.color = new Color(1f, 0.97f, 0.92f, 1f);

            var fill = new GameObject("FillLight");
            fill.transform.SetParent(environment.transform, false);
            fill.transform.position = new Vector3(0f, 2.8f, -2.4f);
            var fillLight = fill.AddComponent<Light>();
            fillLight.type = LightType.Point;
            fillLight.range = 9f;
            fillLight.intensity = 0.65f;
            fillLight.color = new Color(0.78f, 0.86f, 1f, 1f);

            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Floor";
            floor.transform.SetParent(environment.transform, false);
            floor.transform.localPosition = new Vector3(0f, -0.06f, 0f);
            floor.transform.localScale = new Vector3(8f, 0.12f, 8f);
            var floorRenderer = floor.GetComponent<Renderer>();
            if (floorRenderer != null)
            {
                var block = new MaterialPropertyBlock();
                block.SetColor("_BaseColor", new Color(0.30f, 0.32f, 0.35f, 1f));
                block.SetColor("_Color", new Color(0.30f, 0.32f, 0.35f, 1f));
                floorRenderer.SetPropertyBlock(block);
            }

            CreateLoader("MaleGltfLoader", environment.transform,
                new Vector3(-characterSpacing * 0.5f, 0f, 0f), maleGltfPath);
            CreateLoader("FemaleGltfLoader", environment.transform,
                new Vector3(characterSpacing * 0.5f, 0f, 0f), femaleGltfPath);

            return environment;
        }

        private static Camera CreateCamera(
            string name, Transform parent, Vector3 position, Vector3 lookAt,
            bool enabled, float fieldOfView)
        {
            var gameObject = new GameObject(name);
            gameObject.transform.SetParent(parent, false);
            gameObject.transform.position = position;
            gameObject.transform.LookAt(lookAt);
            var camera = gameObject.AddComponent<Camera>();
            camera.enabled = enabled;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.11f, 0.13f, 0.16f, 1f);
            camera.fieldOfView = fieldOfView;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 80f;
            return camera;
        }

        private static IsolatedGltfTestLoader CreateLoader(
            string name, Transform parent, Vector3 position, string gltfPath)
        {
            var gameObject = new GameObject(name);
            gameObject.transform.SetParent(parent, false);
            gameObject.transform.localPosition = position;
            var loader = gameObject.AddComponent<IsolatedGltfTestLoader>();
            loader.GltfRelativePath = gltfPath;
            loader.LoadOnStart = true;
            return loader;
        }
    }
}
