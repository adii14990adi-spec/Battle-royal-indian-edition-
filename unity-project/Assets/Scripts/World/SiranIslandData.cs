using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace BattleHorizons.World
{
    /// <summary>
    /// The converted Siran Island bake: one 401 x 401 height grid, the greybox
    /// vertex colours, and the road ribbons, all loaded from
    /// <c>Assets/Resources/Config/</c>.
    ///
    /// Produced by `processing/unity/convert_godot_island.py` from
    /// `scenes/world/siran_island_baked.tscn`. Loading it is a single synchronous
    /// read of ~1.4 MB of binary plus one hashed JSON — the same order of
    /// magnitude as the Godot build's threaded load of its 9.5 MB baked scene,
    /// and it happens once at match start, not per frame.
    ///
    /// Layout is <c>index = iz * Size + ix</c>, ix grows east (+X), iz grows
    /// south (+Z), sample (0, 0) sits at (-HalfExtent, -HalfExtent). This is the
    /// same ordering the Godot baker used, so a chunk's local vertex (lx, lz) is
    /// still <c>((cz * 50) + lz) * 401 + (cx * 50) + lx</c>.
    /// </summary>
    public sealed class SiranIslandData
    {
        public const string TerrainJsonResource = "Config/siran_island_terrain";
        public const string HeightsResource = "Config/siran_island_heights";
        public const string ColorsResource = "Config/siran_island_colors";
        public const string RoadsResource = "Config/siran_island_roads";
        public const string RiverResource = "Config/siran_island_river";

        [Serializable]
        private sealed class MetaDoc
        {
            public int schema_version;
            public GridDoc grid;
            public HeightsDoc heights;
            public ColorsDoc colors;
            public StatsDoc stats;
            public string[] warnings;
        }

        [Serializable]
        private sealed class GridDoc
        {
            public int size;
            public float spacing_m;
            public float half_extent_m;
            public float chunk_size_m;
            public int chunks_per_side;
        }

        [Serializable]
        private sealed class HeightsDoc
        {
            public float min_m;
            public float max_m;
            public float sea_level_m;
        }

        [Serializable]
        private sealed class ColorsDoc
        {
        }

        [Serializable]
        private sealed class StatsDoc
        {
            public float spawn_ground_height_m;
        }

        public int Size { get; private set; }
        public float SpacingM { get; private set; }
        public float HalfExtentM { get; private set; }
        public float ChunkSizeM { get; private set; }
        public int ChunksPerSide { get; private set; }
        public float MinHeightM { get; private set; }
        public float MaxHeightM { get; private set; }
        public float SeaLevelM { get; private set; }
        public float SpawnGroundHeightM { get; private set; }
        public string[] Warnings { get; private set; } = Array.Empty<string>();

        /// <summary>Terrain height in metres, row-major (iz * Size + ix).</summary>
        public float[] Heights { get; private set; }

        /// <summary>Greybox colour per sample, RGBA8, same indexing as <see cref="Heights"/>.</summary>
        public Color32[] Colors { get; private set; }

        /// <summary>Baked road ribbons, vertices already in world space.</summary>
        public List<RoadRibbon> RoadRibbons { get; private set; } = new List<RoadRibbon>();

        /// <summary>River water surface mesh data, or null when the bake has no river.</summary>
        public RiverMesh River { get; private set; }

        public bool IsLoaded => Heights != null && Heights.Length == Size * Size;

        public sealed class RoadRibbon
        {
            public string Name;
            public Vector3[] Vertices;
            public Color32[] Colors;
            public int[] Indices;
        }

        public sealed class RiverMesh
        {
            public Vector3[] Vertices;
            public int[] Indices;
        }

        /// <summary>Loads everything from Resources. Throws only on corrupt data.</summary>
        public static SiranIslandData Load()
        {
            var data = new SiranIslandData();
            data.LoadMeta();
            data.LoadHeights();
            data.LoadColors();
            data.LoadRoads();
            data.LoadRiver();
            return data;
        }

        /// <summary>Non-throwing variant for boot scenes and tests.</summary>
        public static bool TryLoad(out SiranIslandData data, out string error)
        {
            try
            {
                data = Load();
                error = null;
                return true;
            }
            catch (Exception exception)
            {
                data = null;
                error = exception.Message;
                return false;
            }
        }

        private void LoadMeta()
        {
            var asset = Resources.Load<TextAsset>(TerrainJsonResource);
            if (asset == null)
            {
                throw new InvalidDataException(
                    "missing Resources/" + TerrainJsonResource + ".json — run " +
                    "python3 processing/unity/convert_godot_island.py");
            }

            var doc = JsonUtility.FromJson<MetaDoc>(asset.text);
            if (doc?.grid == null)
            {
                throw new InvalidDataException("siran_island_terrain.json has no grid section");
            }

            Size = doc.grid.size;
            SpacingM = doc.grid.spacing_m;
            HalfExtentM = doc.grid.half_extent_m;
            ChunkSizeM = doc.grid.chunk_size_m;
            ChunksPerSide = doc.grid.chunks_per_side;
            MinHeightM = doc.heights?.min_m ?? 0f;
            MaxHeightM = doc.heights?.max_m ?? 0f;
            SeaLevelM = doc.heights?.sea_level_m ?? 0f;
            SpawnGroundHeightM = doc.stats?.spawn_ground_height_m ?? 0f;
            Warnings = doc.warnings ?? Array.Empty<string>();

            if (Size < 4)
            {
                throw new InvalidDataException("terrain grid is " + Size + " samples across");
            }
        }

        private void LoadHeights()
        {
            var bytes = LoadBinary(HeightsResource);
            var expected = Size * Size * sizeof(float);
            if (bytes.Length != expected)
            {
                throw new InvalidDataException(
                    "heights blob is " + bytes.Length + " bytes, expected " + expected);
            }

            Heights = new float[Size * Size];
            Buffer.BlockCopy(bytes, 0, Heights, 0, expected);
        }

        private void LoadColors()
        {
            var bytes = LoadBinary(ColorsResource);
            var expected = Size * Size * 4;
            if (bytes.Length != expected)
            {
                throw new InvalidDataException(
                    "colour blob is " + bytes.Length + " bytes, expected " + expected);
            }

            Colors = new Color32[Size * Size];
            for (var i = 0; i < Colors.Length; i++)
            {
                var offset = i * 4;
                Colors[i] = new Color32(bytes[offset], bytes[offset + 1], bytes[offset + 2], bytes[offset + 3]);
            }
        }

        private void LoadRoads()
        {
            var bytes = LoadBinary(RoadsResource);
            if (bytes.Length < 12)
            {
                throw new InvalidDataException("road blob is too small");
            }

            const int headerSize = 12;   // magic(8) + ribbon count(4)
            var magic = System.Text.Encoding.ASCII.GetString(bytes, 0, 8);
            if (magic != "BHROAD01")
            {
                throw new InvalidDataException("road blob magic is '" + magic + "'");
            }

            var ribbonCount = BitConverter.ToInt32(bytes, 8);
            var dataStart = headerSize + ribbonCount * 44; // 32 name + 12 numbers
            for (var r = 0; r < ribbonCount; r++)
            {
                var entry = headerSize + r * 44;
                var name = System.Text.Encoding.UTF8.GetString(bytes, entry, 32).TrimEnd('\0');
                var vertexCount = BitConverter.ToInt32(bytes, entry + 32);
                var indexCount = BitConverter.ToInt32(bytes, entry + 36);
                var offset = BitConverter.ToInt32(bytes, entry + 40);

                var ribbon = new RoadRibbon
                {
                    Name = name,
                    Vertices = new Vector3[vertexCount],
                    Colors = new Color32[vertexCount],
                    Indices = new int[indexCount],
                };

                var cursor = dataStart + offset;
                for (var i = 0; i < vertexCount; i++)
                {
                    ribbon.Vertices[i] = new Vector3(
                        BitConverter.ToSingle(bytes, cursor),
                        BitConverter.ToSingle(bytes, cursor + 4),
                        BitConverter.ToSingle(bytes, cursor + 8));
                    ribbon.Colors[i] = new Color32(
                        bytes[cursor + 12], bytes[cursor + 13], bytes[cursor + 14], bytes[cursor + 15]);
                    cursor += 16;
                }

                for (var i = 0; i < indexCount; i++)
                {
                    ribbon.Indices[i] = BitConverter.ToUInt16(bytes, cursor);
                    cursor += 2;
                }

                RoadRibbons.Add(ribbon);
            }
        }

        private void LoadRiver()
        {
            var asset = Resources.Load<TextAsset>(RiverResource);
            if (asset == null)
            {
                return;
            }

            var bytes = asset.bytes;
            if (bytes.Length < 12)
            {
                return;
            }

            var magic = System.Text.Encoding.ASCII.GetString(bytes, 0, 8);
            if (magic != "BHRIVER1")
            {
                throw new InvalidDataException("river blob magic is '" + magic + "'");
            }

            var vertexCount = BitConverter.ToInt32(bytes, 8);
            var indexCount = BitConverter.ToInt32(bytes, 12);
            var vertices = new Vector3[vertexCount];
            var cursor = 16;
            for (var i = 0; i < vertexCount; i++)
            {
                vertices[i] = new Vector3(
                    BitConverter.ToSingle(bytes, cursor),
                    BitConverter.ToSingle(bytes, cursor + 4),
                    BitConverter.ToSingle(bytes, cursor + 8));
                cursor += 12;
            }

            var indices = new int[indexCount];
            for (var i = 0; i < indexCount; i++)
            {
                indices[i] = BitConverter.ToInt32(bytes, cursor);
                cursor += 4;
            }

            River = new RiverMesh { Vertices = vertices, Indices = indices };
        }

        private static byte[] LoadBinary(string resourcePath)
        {
            var asset = Resources.Load<TextAsset>(resourcePath);
            if (asset == null)
            {
                throw new InvalidDataException(
                    "missing Resources/" + resourcePath + ".bytes — run " +
                    "python3 processing/unity/convert_godot_island.py");
            }

            return asset.bytes;
        }

        // --- Queries -----------------------------------------------------------

        public float HeightAt(int ix, int iz)
        {
            ix = Mathf.Clamp(ix, 0, Size - 1);
            iz = Mathf.Clamp(iz, 0, Size - 1);
            return Heights[iz * Size + ix];
        }

        public Color32 ColorAt(int ix, int iz)
        {
            ix = Mathf.Clamp(ix, 0, Size - 1);
            iz = Mathf.Clamp(iz, 0, Size - 1);
            return Colors[iz * Size + ix];
        }

        /// <summary>Bilinear terrain height anywhere in the map (metres).</summary>
        public float SampleHeight(float x, float z)
        {
            var fx = Mathf.Clamp((x + HalfExtentM) / SpacingM, 0f, Size - 1);
            var fz = Mathf.Clamp((z + HalfExtentM) / SpacingM, 0f, Size - 1);
            var x0 = Mathf.FloorToInt(fx);
            var z0 = Mathf.FloorToInt(fz);
            var x1 = Mathf.Min(x0 + 1, Size - 1);
            var z1 = Mathf.Min(z0 + 1, Size - 1);
            var tx = fx - x0;
            var tz = fz - z0;
            var top = Mathf.Lerp(Heights[z0 * Size + x0], Heights[z0 * Size + x1], tx);
            var bottom = Mathf.Lerp(Heights[z1 * Size + x0], Heights[z1 * Size + x1], tx);
            return Mathf.Lerp(top, bottom, tz);
        }

        /// <summary>Terrain normal from central differences, same formula as the baker.</summary>
        public Vector3 SampleNormal(float x, float z)
        {
            var spacing = SpacingM;
            var left = SampleHeight(x - spacing, z);
            var right = SampleHeight(x + spacing, z);
            var back = SampleHeight(x, z - spacing);
            var front = SampleHeight(x, z + spacing);
            return new Vector3(left - right, 2f * spacing, back - front).normalized;
        }

        /// <summary>World-space spawn point on the ground, from the Godot `spawn_point`.</summary>
        public Vector3 SpawnPoint()
        {
            var spawn = SiranMapConfig.SpawnPoint;
            var ground = SampleHeight(spawn.x, spawn.z);
            return new Vector3(spawn.x, ground + 1.2f, spawn.z);
        }

        /// <summary>
        /// Slices one chunk out of the shared grid, in the same winding the Godot
        /// baker emitted (two triangles per cell, clockwise from above). Used by
        /// the chunk-mesh fallback and by the edit-mode geometry test, so the
        /// visual and the verified collar always come from the same code.
        /// </summary>
        public void BuildChunkMesh(int cx, int cz, List<Vector3> vertices, List<Vector3> normals,
            List<Color32> colors, List<int> indices, bool includeNormals = true)
        {
            vertices.Clear();
            colors.Clear();
            indices.Clear();
            if (includeNormals)
            {
                normals.Clear();
            }

            var cells = Mathf.RoundToInt(ChunkSizeM / SpacingM);      // 50
            var vertsPerSide = cells + 1;                              // 51
            var ix0 = cx * cells;
            var iz0 = cz * cells;
            var halfChunk = ChunkSizeM * 0.5f;

            for (var lz = 0; lz < vertsPerSide; lz++)
            {
                var iz = iz0 + lz;
                for (var lx = 0; lx < vertsPerSide; lx++)
                {
                    var ix = ix0 + lx;
                    var height = Heights[iz * Size + ix];
                    vertices.Add(new Vector3(lx * SpacingM - halfChunk, height, lz * SpacingM - halfChunk));
                    colors.Add(Colors[iz * Size + ix]);
                    if (includeNormals)
                    {
                        var worldX = -HalfExtentM + ix * SpacingM;
                        var worldZ = -HalfExtentM + iz * SpacingM;
                        normals.Add(SampleNormal(worldX, worldZ));
                    }
                }
            }

            for (var lz = 0; lz < cells; lz++)
            {
                for (var lx = 0; lx < cells; lx++)
                {
                    var topLeft = lz * vertsPerSide + lx;
                    var topRight = topLeft + 1;
                    var bottomLeft = topLeft + vertsPerSide;
                    var bottomRight = bottomLeft + 1;
                    indices.Add(topLeft);
                    indices.Add(topRight);
                    indices.Add(bottomLeft);
                    indices.Add(topRight);
                    indices.Add(bottomRight);
                    indices.Add(bottomLeft);
                }
            }
        }

        /// <summary>Chunk-local origin of chunk (cx, cz), matching the Godot Chunk_ node transform.</summary>
        public Vector3 ChunkOriginWorld(int cx, int cz)
        {
            var center = SiranMapConfig.ChunkCenter(cx, cz);
            return new Vector3(center.x, 0f, center.y);
        }
    }
}
