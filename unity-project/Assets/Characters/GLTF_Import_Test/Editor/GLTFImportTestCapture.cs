using System;
using System.IO;
using System.Text;
using BattleHorizons.GLTFImportTest;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace BattleHorizons.EditorTools
{
    /// <summary>
    /// Genuine Unity Editor camera renders of the imported glTF characters.
    /// This is intentionally a separate, graphics-enabled batch-mode step: a
    /// successful file import or APK build is not visual verification.
    /// </summary>
    public static class GLTFImportTestCapture
    {
        private const int Width = 1024;
        private const int Height = 768;

        public static void CaptureFromCommandLine()
        {
            try
            {
                if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                {
                    throw new InvalidOperationException(
                        "No graphics device is available; run this step under Xvfb with software GL and without -nographics.");
                }

                var outputDirectory = Path.GetFullPath(Path.Combine(
                    Application.dataPath, "..", "..", "docs", "gltf_import_test", "screenshots"));
                Directory.CreateDirectory(outputDirectory);

                AssetDatabase.ImportAsset(GLTFImportTestValidator.MaleGltfPath,
                    ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                AssetDatabase.ImportAsset(GLTFImportTestValidator.FemaleGltfPath,
                    ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

                var malePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GLTFImportTestValidator.MaleGltfPath);
                var femalePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GLTFImportTestValidator.FemaleGltfPath);
                if (malePrefab == null || femalePrefab == null)
                {
                    throw new InvalidOperationException("glTFast did not produce both imported prefabs.");
                }

                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var root = new GameObject("GLTF_Visual_Capture");
                var bootstrap = root.AddComponent<IsolatedGltfTestBootstrap>();
                bootstrap.MalePath = IsolatedGltfTestBootstrap.MaleGltfPath;
                bootstrap.FemalePath = IsolatedGltfTestBootstrap.FemaleGltfPath;
                var environment = bootstrap.BuildEnvironment();

                var maleLoader = FindLoader(environment.transform, "MaleGltfLoader");
                var femaleLoader = FindLoader(environment.transform, "FemaleGltfLoader");
                maleLoader.LoadOnStart = false;
                femaleLoader.LoadOnStart = false;

                var maleInstance = PrefabUtility.InstantiatePrefab(malePrefab) as GameObject;
                var femaleInstance = PrefabUtility.InstantiatePrefab(femalePrefab) as GameObject;
                if (maleInstance == null || femaleInstance == null)
                {
                    throw new InvalidOperationException("Could not instantiate the imported glTF prefabs.");
                }

                maleInstance.name = "Male_ImportedGLTF";
                femaleInstance.name = "Female_ImportedGLTF";
                maleInstance.transform.SetParent(maleLoader.transform, false);
                femaleInstance.transform.SetParent(femaleLoader.transform, false);

                var maleBounds = AlignToFloor(maleInstance);
                var femaleBounds = AlignToFloor(femaleInstance);
                var combined = maleBounds;
                combined.Encapsulate(femaleBounds);

                var cameras = environment.GetComponentsInChildren<Camera>(true);
                var comparison = FindCamera(cameras, "ComparisonCamera");
                var maleCamera = FindCamera(cameras, "MaleCamera");
                var femaleCamera = FindCamera(cameras, "FemaleCamera");
                if (comparison == null || maleCamera == null || femaleCamera == null)
                {
                    throw new InvalidOperationException("Comparison environment cameras are missing.");
                }

                ConfigureComparisonCamera(comparison, combined);
                ConfigureIndividualCamera(maleCamera, maleBounds, -1.2f);
                ConfigureIndividualCamera(femaleCamera, femaleBounds, 1.2f);

                var sideBySide = Path.Combine(outputDirectory, "male_female_side_by_side.png");
                var maleShot = Path.Combine(outputDirectory, "male_character.png");
                var femaleShot = Path.Combine(outputDirectory, "female_character.png");
                RenderCamera(comparison, sideBySide);
                RenderCamera(maleCamera, maleShot);
                RenderCamera(femaleCamera, femaleShot);

                var report = new StringBuilder();
                report.AppendLine("# Unity Editor visual capture");
                report.AppendLine();
                report.AppendLine("Generated by `GLTFImportTestCapture.CaptureFromCommandLine` using Unity `Camera.Render` to a `RenderTexture` under a graphics-enabled Xvfb session.");
                report.AppendLine();
                report.AppendLine("- Graphics device: `" + SystemInfo.graphicsDeviceType + " / " + SystemInfo.graphicsDeviceName + "`");
                report.AppendLine("- Male bounds: size `" + maleBounds.size.ToString("F3") + "`, minY `" + maleBounds.min.y.ToString("F3") + "`");
                report.AppendLine("- Female bounds: size `" + femaleBounds.size.ToString("F3") + "`, minY `" + femaleBounds.min.y.ToString("F3") + "`");
                report.AppendLine("- Screenshots: `male_female_side_by_side.png`, `male_character.png`, `female_character.png`");
                report.AppendLine();
                report.AppendLine("These are real Unity Editor camera renders, not import success messages. Human visual inspection is still recorded separately in `docs/GLTF_PLAYER_IMPORT_TEST_REPORT.md`.");
                File.WriteAllText(Path.Combine(outputDirectory, "CAPTURE_REPORT.md"), report.ToString(), Encoding.UTF8);

                UnityEngine.Object.DestroyImmediate(femaleInstance);
                UnityEngine.Object.DestroyImmediate(maleInstance);
                UnityEngine.Object.DestroyImmediate(root);
                EditorSceneManager.CloseScene(scene, true);

                Debug.Log("[bh][gltf-test] wrote 3 Unity Editor screenshots to " + outputDirectory);
            }
            catch (Exception exception)
            {
                Debug.LogError("[bh][gltf-test] visual capture failed: " + exception);
                throw;
            }
        }

        private static IsolatedGltfTestLoader FindLoader(Transform root, string name)
        {
            var loaders = root.GetComponentsInChildren<IsolatedGltfTestLoader>(true);
            for (var i = 0; i < loaders.Length; i++)
            {
                if (loaders[i].gameObject.name == name)
                {
                    return loaders[i];
                }
            }

            throw new InvalidOperationException("Missing loader " + name);
        }

        private static Camera FindCamera(Camera[] cameras, string name)
        {
            for (var i = 0; i < cameras.Length; i++)
            {
                if (cameras[i].name == name)
                {
                    return cameras[i];
                }
            }

            return null;
        }

        private static Bounds AlignToFloor(GameObject instance)
        {
            var renderers = instance.GetComponentsInChildren<Renderer>(true);
            var bounds = CalculateBounds(renderers);
            instance.transform.localPosition = new Vector3(0f, -bounds.min.y, 0f);
            bounds = CalculateBounds(renderers);
            return bounds;
        }

        private static Bounds CalculateBounds(Renderer[] renderers)
        {
            var bounds = new Bounds(Vector3.zero, Vector3.zero);
            var has = false;
            for (var i = 0; i < renderers.Length; i++)
            {
                if (!has)
                {
                    bounds = renderers[i].bounds;
                    has = true;
                }
                else
                {
                    bounds.Encapsulate(renderers[i].bounds);
                }
            }

            return bounds;
        }

        private static void ConfigureComparisonCamera(Camera camera, Bounds combined)
        {
            var height = Mathf.Max(1.5f, combined.size.y);
            camera.transform.position = new Vector3(0f, height * 0.58f, -Mathf.Max(5.6f, height * 3.6f));
            camera.transform.LookAt(new Vector3(0f, height * 0.52f, 0f));
            camera.enabled = true;
        }

        private static void ConfigureIndividualCamera(Camera camera, Bounds bounds, float x)
        {
            var height = Mathf.Max(1.5f, bounds.size.y);
            camera.transform.position = new Vector3(x, height * 0.58f, -Mathf.Max(3.8f, height * 2.5f));
            camera.transform.LookAt(new Vector3(x, height * 0.52f, 0f));
            camera.enabled = true;
        }

        private static void RenderCamera(Camera camera, string path)
        {
            var oldTarget = camera.targetTexture;
            var oldActive = RenderTexture.active;
            var texture = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            var output = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = texture;
                camera.Render();
                RenderTexture.active = texture;
                output.ReadPixels(new Rect(0f, 0f, Width, Height), 0, 0);
                output.Apply();
                File.WriteAllBytes(path, output.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = oldTarget;
                RenderTexture.active = oldActive;
                UnityEngine.Object.DestroyImmediate(output);
                texture.Release();
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }
    }
}
