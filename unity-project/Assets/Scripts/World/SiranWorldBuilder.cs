using System.Collections.Generic;
using BattleHorizons.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace BattleHorizons.World
{
    /// <summary>
    /// Assembles Siran Island at scene start from committed Unity resources:
    /// heightmap collision, circularly clipped terrain tiles, roads and water.
    /// No editor-time map bake is required to build the project.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SiranWorldBuilder : MonoBehaviour
    {
        [Header("Materials")]
        [Tooltip("URP terrain material (Terrain/Lit with the greybox albedo). Optional.")]
        [SerializeField] private Material terrainMaterial;

        [Tooltip("URP material for chunk-mesh rendering. Optional.")]
        [SerializeField] private Material chunkMaterial;

        [Tooltip("URP water material (Lit with the exported water normal map). Optional.")]
        [SerializeField] private Material waterMaterial;

        [Tooltip("URP unlit material for the baked road ribbons. Optional.")]
        [SerializeField] private Material roadMaterial;

        [Header("World")]
        [Tooltip("Transform the streamer follows. Defaults to the main camera, then the origin.")]
        [SerializeField] private Transform focusOverride;

        [Tooltip("Render the island with Unity Terrain (cheap collision + LOD).")]
        [SerializeField] private bool useTerrain = true;

        [Tooltip("Draw the full 4000x4000m greybox terrain base out to the camera far plane.")]
        [SerializeField] private bool showFullTerrainBase;

        [Header("Lighting")]
        [Tooltip("Create the URP sun, sky and fog if the scene has none.")]
        [SerializeField] private bool createLighting = true;

        public SiranIslandData Data { get; private set; }
        public Terrain Terrain { get; private set; }
        public SiranChunkStreamer Streamer { get; private set; }
        public SiranIslandWater Water { get; private set; }
        public bool TerrainUsed => useTerrain;

        private void Awake()
        {
            Build();
        }

        private void Start()
        {
            Build();
        }

        /// <summary>Explicit entry point so tests and the editor tooling can build without a scene.</summary>
        public bool Build()
        {
            if (Data != null && Data.IsLoaded)
            {
                return true;
            }

            if (!SiranIslandData.TryLoad(out var data, out var error))
            {
                GameLog.Error("world", "could not load the committed Siran Island data: " + error);
                return false;
            }

            Data = data;
            GameLog.Info("world", string.Format(
                "Siran Island loaded: {0} x {0} m grid, {1} chunks, {2} road ribbons, {3} {4}",
                data.Size, SiranMapConfig.ChunkCount, data.RoadRibbons.Count,
                data.River != null ? "river ribbon" : "no river", data.Warnings.Length > 0 ? "(warnings)" : ""));
            for (var i = 0; i < data.Warnings.Length; i++)
            {
                GameLog.Warn("world", data.Warnings[i]);
            }

            ApplyQuality();
            BuildLighting();

            if (useTerrain)
            {
                var terrainData = SiranIslandTerrainBuilder.Build(data, terrainMaterial);
                Terrain = SiranIslandTerrainBuilder.CreateTerrain(data, terrainData, terrainMaterial, transform);
                // Keep the heightmap collider for gameplay, but do not draw the
                // full-map Terrain. The streamed terrain meshes, roads and water
                // are all clipped by the shared shader to the same 50 m disk.
                Terrain.drawHeightmap = false;
                if (showFullTerrainBase)
                {
                    Terrain.drawHeightmap = true;
                    Terrain.transform.position += new Vector3(0f, -0.05f, 0f);
                }
                GameLog.Info("world", string.Format(
                    "terrain collision built: {0}x{0} heightmap, heights {1:F1}..{2:F1} m, full-map renderer hidden",
                    SiranIslandTerrainBuilder.HeightmapResolution, data.MinHeightM, data.MaxHeightM));
            }

            BuildRoads();
            BuildWater();

            var focus = ResolveFocus();
            Streamer = gameObject.GetComponent<SiranChunkStreamer>();
            if (Streamer == null)
            {
                Streamer = gameObject.AddComponent<SiranChunkStreamer>();
            }

            Streamer.Initialise(data, focus, chunkMaterial);
            // The streamer updates the shared radial shader focus every frame.
            Streamer.enabled = true;

            return true;
        }

        private void ApplyQuality()
        {
            QualityProfileHook.HighQuality = QualityProfiles.Active == QualityProfile.High;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 250f;
            RenderSettings.fogEndDistance = 1000f;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.63f, 0.73f, 0.84f);
            RenderSettings.ambientEquatorColor = new Color(0.52f, 0.60f, 0.68f);
            RenderSettings.ambientGroundColor = new Color(0.24f, 0.25f, 0.23f);

            var camera = Camera.main;
            if (camera != null)
            {
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 1000f;
                camera.clearFlags = CameraClearFlags.SolidColor;
            }
        }

        private void BuildLighting()
        {
            if (!createLighting || FindSun() != null)
            {
                return;
            }

            var sunObject = new GameObject("Sun");
            sunObject.transform.SetParent(transform, false);
            var sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.96f, 0.89f);
            sun.intensity = 1.15f;
            sun.shadows = QualityProfiles.Active == QualityProfile.High
                ? LightShadows.Soft
                : LightShadows.None;

            // A stable warm daylight direction for the low-poly island materials.
            var direction = new Vector3(0.45f, 0.78f, 0.44f).normalized;
            sunObject.transform.rotation = Quaternion.LookRotation(-direction);
            RenderSettings.sun = sun;
        }

        private static Light FindSun()
        {
            var lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
            for (var i = 0; i < lights.Length; i++)
            {
                if (lights[i].type == LightType.Directional)
                {
                    return lights[i];
                }
            }

            return null;
        }

        private void BuildRoads()
        {
            if (Data.RoadRibbons.Count == 0)
            {
                return;
            }

            var root = new GameObject("Roads");
            root.transform.SetParent(transform, false);
            root.isStatic = true;

            var material = roadMaterial != null
                ? roadMaterial
                : CreateVertexColorMaterial("GeneratedRoadMaterial", 0.08f);

            var triangles = 0;
            for (var i = 0; i < Data.RoadRibbons.Count; i++)
            {
                var ribbon = Data.RoadRibbons[i];
                var mesh = new Mesh { name = ribbon.Name };
                mesh.SetVertices(ribbon.Vertices);
                mesh.SetColors(ribbon.Colors);
                // The bake emits flat-up normals for every road vertex.
                var normals = new Vector3[ribbon.Vertices.Length];
                for (var v = 0; v < normals.Length; v++)
                {
                    normals[v] = Vector3.up;
                }

                mesh.SetNormals(normals);
                mesh.SetTriangles(ribbon.Indices, 0, true);
                triangles += ribbon.Indices.Length / 3;

                var go = new GameObject(ribbon.Name);
                go.transform.SetParent(root.transform, false);
                go.isStatic = true;
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            }

            GameLog.Info("world", string.Format("road ribbons built: {0} meshes, {1} triangles",
                Data.RoadRibbons.Count, triangles));
        }

        private void BuildWater()
        {
            Water = gameObject.GetComponent<SiranIslandWater>();
            if (Water == null)
            {
                Water = gameObject.AddComponent<SiranIslandWater>();
            }

            Water.Build(Data, waterMaterial != null ? waterMaterial : CreateWaterFallback());
        }

        private Transform ResolveFocus()
        {
            if (focusOverride != null)
            {
                return focusOverride;
            }

            var camera = Camera.main;
            return camera != null ? camera.transform : transform;
        }

        private static Material CreateVertexColorMaterial(string name, float smoothness)
        {
            var shader = Shader.Find(SiranRadiusClip.ShaderName);
            if (shader == null)
            {
                GameLog.Error("world", "missing " + SiranRadiusClip.ShaderName + " shader; run Project Setup > Configure");
                return null;
            }

            var material = new Material(shader) { name = name, enableInstancing = true };
            material.SetFloat("_UseVertexColor", 1f);
            material.SetFloat("_Smoothness", smoothness);
            return material;
        }

        private static Material CreateWaterFallback()
        {
            var shader = Shader.Find(SiranRadiusClip.ShaderName);
            if (shader == null)
            {
                GameLog.Error("world", "missing " + SiranRadiusClip.ShaderName + " shader; run Project Setup > Configure");
                return null;
            }

            var material = new Material(shader) { name = "GeneratedWaterMaterial" };
            material.SetColor("_BaseColor", new Color(0.20f, 0.44f, 0.50f, 1f));
            material.SetFloat("_Smoothness", 0.92f);
            material.SetFloat("_Metallic", 0.0f);
            var normal = Resources.Load<Texture2D>("Art/Textures/water_normal_256");
            if (normal != null)
            {
                material.SetTexture("_BumpMap", normal);
                material.SetFloat("_BumpScale", 0.45f);
                material.EnableKeyword("_NORMALMAP");
            }

            return material;
        }
    }
}
