using System;
using UnityEngine;

namespace BattleHorizons.World
{
    /// <summary>
    /// Builds a Unity <see cref="Terrain"/> from the converted Godot height grid.
    ///
    /// Why Terrain and not 64 mesh chunks: URP's mobile path plus Unity's Terrain
    /// gives height-field collision, camera-relative LOD and basemap culling for
    /// free, and the collision is the cheapest thing Unity has for ground (the
    /// Godot side's `HeightMapShape3D` choice, for the same reason — see
    /// docs/PHASE3_WORLD.md). The chunk *meshes* still exist as
    /// <see cref="SiranChunkStreamer"/>'s visual ring when a mesh is wanted.
    ///
    /// The greybox look is preserved by baking the Godot vertex colours into a
    /// 1:1 albedo texture on a URP Terrain/Lit material: 401 x 401 RGBA32 is
    /// 643 KB before compression, and it means no terrain layers, no splat maps
    /// and no detail textures — the same "one material, zero textures" budget the
    /// Godot build kept.
    /// </summary>
    public static class SiranIslandTerrainBuilder
    {
        public const int TerrainLayerIndex = 8; // matches TagManager: "Terrain"

        /// <summary>Unity Terrain height resolution (power of two + 1). 513 is 2 m/cell.</summary>
        public const int HeightmapResolution = 513;

        public static TerrainData Build(SiranIslandData data, Material terrainMaterial)
        {
            if (data == null || !data.IsLoaded)
            {
                throw new ArgumentException("island data is not loaded", nameof(data));
            }

            var size = SiranMapConfig.MapSizeM;

            // --- heights ---------------------------------------------------------
            // Unity stores normalised heights in the range [terrainY, terrainY + size.y]
            // with a bottom-left origin, ix fastest. Godot's grid is already
            // bottom-left origin (ix fastest, iz grows toward +Z) so only the
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
                    // Convert to the Godot-centred space to sample the grid.
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
            variableOcclusionMask(data, out var albedo);
            if (albedo != null)
            {
                var layer = new TerrainLayer
                {
                    diffuseTexture = albedo,
                    tileSize = new Vector2(size, size),
                    metallic = 0f,
                    smoothness = 0.06f,
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

            // Terrain mesh is centred on its own pivot; the map is centred on the
            // origin so the pivot only needs the vertical offset.
            go.transform.position = new Vector3(0f, TerrainOriginY(data), 0f);

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
        private static void variableOcclusionMask(SiranIslandData data, out Texture2D albedo)
        {
            const int resolution = 512;
            albedo = new Texture2D(resolution, resolution, TextureFormat.RGBA32, true, false)
            {
                name = "SiranIslandGreyboxAlbedo",
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
        /// Fallback for when Unity's Terrain is not wanted (e.g. a device that
        /// dislikes terrain basemaps): one Mesh for the whole island.
        /// 160 801 vertices / 320 000 triangles is under the 16-bit index limit at
        /// 51 x 51 per chunk but not island-wide, so this uses 32-bit indices and is
        /// meant for tests and low-detail fallback only.
        /// </summary>
        public static Mesh BuildWholeIslandMesh(SiranIslandData data, int stride = 1)
        {
            var size = data.Size;
            var step = Mathf.Max(1, stride);
            var vertsPerSide = (size - 1) / step + 1;
            var vertices = new Vector3[vertsPerSide * vertsPerSide];
            var colors = new Color32[vertices.Length];
            var indices = new int[(vertsPerSide - 1) * (vertsPerSide - 1) * 6];

            for (var iz = 0; iz < vertsPerSide; iz++)
            {
                for (var ix = 0; ix < vertsPerSide; ix++)
                {
                    var gx = Mathf.Min(ix * step, size - 1);
                    var gz = Mathf.Min(iz * step, size - 1);
                    var index = iz * vertsPerSide + ix;
                    vertices[index] = new Vector3(
                        -data.HalfExtentM + gx * data.SpacingM,
                        data.HeightAt(gx, gz),
                        -data.HalfExtentM + gz * data.SpacingM);
                    colors[index] = data.ColorAt(gx, gz);
                }
            }

            var cursor = 0;
            for (var iz = 0; iz < vertsPerSide - 1; iz++)
            {
                for (var ix = 0; ix < vertsPerSide - 1; ix++)
                {
                    var topLeft = iz * vertsPerSide + ix;
                    var topRight = topLeft + 1;
                    var bottomLeft = topLeft + vertsPerSide;
                    var bottomRight = bottomLeft + 1;
                    indices[cursor++] = topLeft;
                    indices[cursor++] = topRight;
                    indices[cursor++] = bottomLeft;
                    indices[cursor++] = topRight;
                    indices[cursor++] = bottomRight;
                    indices[cursor++] = bottomLeft;
                }
            }

            var mesh = new Mesh { name = "SiranIslandWholeMesh", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.vertices = vertices;
            mesh.colors32 = colors;
            mesh.triangles = indices;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
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
        public static float ActiveStreamRadius => HighQuality ? 1600f : 1100f;
        public static float ActiveShadowDistance => HighQuality ? 120f : 60f;
    }
}
