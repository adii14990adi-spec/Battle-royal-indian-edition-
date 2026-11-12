using System;
using System.Collections.Generic;
using UnityEngine;

namespace BattleHorizons.World
{
    /// <summary>
    /// Builds a Unity <see cref="Terrain"/> and heightmap collider from the
    /// committed 401 x 401 island data. The collider remains active for gameplay;
    /// the full-map Terrain renderer is always hidden so it cannot bypass the
    /// streamed terrain mesh's strict 50 m visual radius.
    ///
    /// The greybox palette is baked into a compact albedo texture for the
    /// non-rendered Terrain component. Streamed mesh tiles use original colours.
    /// </summary>
    public static class SiranIslandTerrainBuilder
    {
        public const int TerrainLayerIndex = 8; // matches TagManager: "Terrain"

        /// <summary>Unity Terrain height resolution (power of two + 1). 513 is 2 m/cell.</summary>
        public const int HeightmapResolution = 513;

        /// <summary>
        /// Sample stride of the full-island base mesh. 2 = one vertex every 20 m,
        /// i.e. 201 x 201 vertices / 80,000 triangles for the whole 4 km island in
        /// a single static draw call. The 10 m streamed disk adds near detail.
        /// </summary>
        public const int OverviewSampleStride = 2;

        public static TerrainData Build(SiranIslandData data, Material terrainMaterial)
        {
            if (data == null || !data.IsLoaded)
            {
                throw new ArgumentException("island data is not loaded", nameof(data));
            }

            var size = SiranMapConfig.MapSizeM;

            // --- heights ---------------------------------------------------------
            // Unity stores normalised heights in the range [terrainY, terrainY + size.y]
            // with a bottom-left origin, ix fastest. The Unity data uses the same
            // row-major convention (ix fastest, iz grows toward +Z), so only the
            // Y remap is needed.
            var minHeight = Mathf.Min(data.MinHeightM, SiranMapConfig.SeaLevel - 1f);
            var maxHeight = Mathf.Max(data.MaxHeightM, SiranMapConfig.MaxHeightM);
            var heightRange = Mathf.Max(maxHeight - minHeight, 1f);

            var heightmap = new float[HeightmapResolution, HeightmapResolution];
            for (var iz = 0; iz < HeightmapResolution; iz++)
            {
                var worldZ = (float)iz / (HeightmapResolution - 1) * size;
                for (var ix = 0; ix < HeightmapResolution; ix++)
                {
                    var worldX = (float)ix / (HeightmapResolution - 1) * size;
                    // Convert to the island's centred world space to sample the grid.
                    var height = data.SampleHeight(worldX - data.HalfExtentM, worldZ - data.HalfExtentM);
                    heightmap[iz, ix] = Mathf.Clamp01((height - minHeight) / heightRange);
                }
            }

            var terrainData = new TerrainData
            {
                heightmapResolution = HeightmapResolution,
                size = new Vector3(size, heightRange, size),
                baseMapResolution = 1024,
                alphamapResolution = 16,       // one layer: as small as Unity allows
                name = "SiranIslandTerrainData",
            };
            terrainData.SetHeights(0, 0, heightmap);

            // --- greybox albedo --------------------------------------------------
            BuildPaletteTexture(data, out var albedo);
            if (albedo != null)
            {
                var layer = new TerrainLayer
                {
                    diffuseTexture = albedo,
                    tileSize = new Vector2(size, size),
                    // Fully matte: any smoothness here turns the sun into a white
                    // specular sheet across the ground.
                    metallic = 0f,
                    smoothness = 0f,
                };
                terrainData.terrainLayers = new[] { layer };
            }

            return terrainData;
        }

        /// <summary>Wraps a finished TerrainData in a GameObject positioned on the map centre.</summary>
        public static Terrain CreateTerrain(SiranIslandData data, TerrainData terrainData, Material terrainMaterial,
            Transform parent = null)
        {
            var go = new GameObject("SiranIslandTerrain");
            if (parent != null)
            {
                go.transform.SetParent(parent, false);
            }

            go.layer = TerrainLayerIndex;
            go.isStatic = true;

            var terrain = go.AddComponent<Terrain>();
            terrain.terrainData = terrainData;
            terrain.drawHeightmap = true;
            terrain.drawTreesAndFoliage = false;
            terrain.heightmapPixelError = QualityProfileHook.ActiveTerrainPixelError;
            terrain.basemapDistance = QualityProfileHook.ActiveBasemapDistance;
            terrain.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            terrain.allowAutoConnect = true;
            terrain.groupingID = 0;

            var collider = go.AddComponent<TerrainCollider>();
            collider.terrainData = terrainData;

            // Unity TerrainData starts at its local lower-left corner. Offset the
            // origin so its 0..4000 m footprint matches the island's -2000..2000 m
            // world-space grid.
            go.transform.position = new Vector3(-data.HalfExtentM, TerrainOriginY(data), -data.HalfExtentM);

            var renderer = go.GetComponent<Terrain>();
            if (terrainMaterial != null && renderer != null)
            {
                renderer.materialTemplate = terrainMaterial;
            }

            return terrain;
        }

        /// <summary>World Y of TerrainData y == 0.</summary>
        public static float TerrainOriginY(SiranIslandData data) =>
            Mathf.Min(data.MinHeightM, SiranMapConfig.SeaLevel - 1f);

        /// <summary>
        /// Turns the greybox vertex colours into a terrain albedo.
        ///
        /// The grid is 401 x 401 samples on a 4000 m map; the texture is resampled
        /// to 512 with bilinear filtering, which is under one texel per 7.8 m — the
        /// same "large flat colour patches" look the greybox had, at 8 bits per
        /// channel instead of per-vertex floats.
        /// </summary>
        private static void BuildPaletteTexture(SiranIslandData data, out Texture2D albedo)
        {
            const int resolution = 512;
            albedo = new Texture2D(resolution, resolution, TextureFormat.RGBA32, true, false)
            {
                name = "SiranIslandPaletteAlbedo",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 0,
            };

            var pixels = new Color32[resolution * resolution];
            for (var py = 0; py < resolution; py++)
            {
                var v = (py + 0.5f) / resolution;                 // 0..1 along +Z
                var iz = Mathf.RoundToInt(v * (data.Size - 1));
                for (var px = 0; px < resolution; px++)
                {
                    var u = (px + 0.5f) / resolution;             // 0..1 along +X
                    var ix = Mathf.RoundToInt(u * (data.Size - 1));
                    pixels[py * resolution + px] = data.ColorAt(ix, iz);
                }
            }

            albedo.SetPixels32(pixels);
            albedo.Apply(updateMipmaps: true, makeNoLongerReadable: true);
        }

        /// <summary>
        /// Builds the coarse full-island surface that is visible outside the 50 m
        /// streamed detail disk: one vertex every 20 m (201 x 201, 80,000
        /// triangles), the baked grass / sand / dirt palette as vertex colours and
        /// up-facing triangles. This is what makes the map read as an island with
        /// hills, flat zone pads and the inland lake rather than a white plane.
        /// </summary>
        public static Mesh BuildOverviewMesh(SiranIslandData data, int stride = OverviewSampleStride)
        {
            if (data == null || !data.IsLoaded)
            {
                throw new ArgumentException("island data is not loaded", nameof(data));
            }

            stride = Mathf.Clamp(stride, 1, 8);
            var vertsPerSide = (data.Size - 1) / stride + 1;
            var vertexCount = vertsPerSide * vertsPerSide;
            var mapSize = SiranMapConfig.MapSizeM;

            var vertices = new List<Vector3>(vertexCount);
            var normals = new List<Vector3>(vertexCount);
            var colors = new List<Color32>(vertexCount);
            var uvs = new List<Vector2>(vertexCount);

            for (var z = 0; z < vertsPerSide; z++)
            {
                var iz = Mathf.Min(z * stride, data.Size - 1);
                var worldZ = -data.HalfExtentM + iz * data.SpacingM;
                for (var x = 0; x < vertsPerSide; x++)
                {
                    var ix = Mathf.Min(x * stride, data.Size - 1);
                    var worldX = -data.HalfExtentM + ix * data.SpacingM;
                    var index = iz * data.Size + ix;
                    vertices.Add(new Vector3(worldX, data.Heights[index], worldZ));
                    normals.Add(data.SampleNormal(worldX, worldZ));
                    colors.Add(data.Colors[index]);
                    uvs.Add(new Vector2((worldX + data.HalfExtentM) / mapSize,
                        (worldZ + data.HalfExtentM) / mapSize));
                }
            }

            var indices = new List<int>((vertsPerSide - 1) * (vertsPerSide - 1) * 6);
            for (var z = 0; z < vertsPerSide - 1; z++)
            {
                for (var x = 0; x < vertsPerSide - 1; x++)
                {
                    var topLeft = z * vertsPerSide + x;
                    var topRight = topLeft + 1;
                    var bottomLeft = topLeft + vertsPerSide;
                    var bottomRight = bottomLeft + 1;

                    // Clockwise from above, same convention as BuildChunkMesh.
                    indices.Add(topLeft);
                    indices.Add(bottomLeft);
                    indices.Add(topRight);
                    indices.Add(topRight);
                    indices.Add(bottomLeft);
                    indices.Add(bottomRight);
                }
            }

            var mesh = new Mesh { name = "SiranIslandOverview" };
            mesh.indexFormat = vertexCount > 65535
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetColors(colors);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(indices, 0, true);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Wraps the overview mesh in a single static, matte, unshadowed renderer.</summary>
        public static GameObject CreateOverviewMesh(SiranIslandData data, Material material, Transform parent = null,
            int stride = OverviewSampleStride)
        {
            var go = new GameObject("SiranIslandOverview");
            if (parent != null)
            {
                go.transform.SetParent(parent, false);
            }

            go.layer = TerrainLayerIndex;
            go.isStatic = true;
            go.AddComponent<MeshFilter>().sharedMesh = BuildOverviewMesh(data, stride);

            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            return go;
        }

    }

    /// <summary>
    /// Tiny indirection so the world builder can ask for the active profile's
    /// terrain numbers without a hard dependency on <c>BattleHorizons.Core</c>
    /// from every world class (and so tests can run without a GameBootstrap).
    /// </summary>
    public static class QualityProfileHook
    {
        public static bool HighQuality { get; set; }

        public static float ActiveTerrainPixelError => HighQuality ? 5f : 8f;
        public static float ActiveBasemapDistance => HighQuality ? 600f : 300f;
        public static float ActiveViewDistance => HighQuality ? 1600f : 900f;
        public static float ActiveStreamRadius => SiranChunkStreamer.DefaultStreamRadiusM;
        public static float ActiveShadowDistance => HighQuality ? 120f : 60f;
    }
}
