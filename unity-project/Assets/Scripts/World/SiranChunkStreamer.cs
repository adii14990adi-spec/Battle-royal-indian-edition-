using System.Collections.Generic;
using BattleHorizons.Core;
using UnityEngine;

namespace BattleHorizons.World
{
    /// <summary>
    /// Port of `scripts/world/chunk_manager.gd`: streams the island's 500 m chunks
    /// in and out around a focus transform.
    ///
    /// What changed, and why:
    ///   * The Godot streamer loaded one committed PackedScene on a worker thread
    ///     because parsing 9.5 MB of text-encoded geometry cost seconds. In Unity
    ///     the geometry arrives as a flat height grid (~1.4 MB) and each chunk
    ///     mesh is built from it, so there is nothing to parse: a chunk is
    ///     *constructed* from arrays that are already in memory.
    ///   * Chunk meshes are therefore pooled (<see cref="ObjectPool{T}"/>) instead
    ///     of instantiated, and mesh arrays are reused per slot, so a full
    ///     traverse of the island does not allocate once the pool is warm.
    ///   * A chunk is resident when its bounds are within the radius of the focus,
    ///     with the same `keep_ring` floor the Godot version used so the player is
    ///     never standing on the edge of the world at a 50 m radius.
    ///
    /// The chunk indices, the 3x3 keep ring and the 5 Hz bookkeeping tick are the
    /// same numbers as the Godot version (see docs/PHASE3_WORLD.md §5) so the
    /// streaming behaviour can be compared side by side.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SiranChunkStreamer : MonoBehaviour
    {
        [Header("Streaming")]
        [Tooltip("Chunks whose bounds are within this distance of the focus are resident (metres).")]
        [SerializeField] private float streamRadiusM = 1100f;

        [Tooltip("Always keep this many chunk rings around the focus chunk resident.")]
        [Range(0, 3)]
        [SerializeField] private int keepRing = 1;

        [Tooltip("How often the resident set is recomputed, in seconds (Godot used 0.2).")]
        [SerializeField] private float tickIntervalSeconds = 0.2f;

        [Tooltip("Chunks that may be built per tick; smooths the first fill.")]
        [Range(1, 8)]
        [SerializeField] private int maxChunksPerTick = 3;

        [Tooltip("Build every resident chunk on the first tick instead of a few per tick.")]
        [SerializeField] private bool preloadImmediately = true;

        [Header("Mesh")]
        [Tooltip("Material for chunk meshes. Vertex colours carry the greybox palette.")]
        [SerializeField] private Material chunkMaterial;

        [Tooltip("Draw meshes at all. Off = terrain-only rendering (the cheap path).")]
        [SerializeField] private bool drawChunkMeshes = true;

        private SiranIslandData _data;
        private Transform _focus;
        private Vector3 _lastFocusPosition = new Vector3(float.MaxValue, 0f, 0f);
        private float _tickAccumulator;

        private readonly HashSet<Vector2Int> _resident = new HashSet<Vector2Int>();
        private readonly List<Vector2Int> _desired = new List<Vector2Int>(64);
        private readonly Dictionary<Vector2Int, ChunkView> _views = new Dictionary<Vector2Int, ChunkView>();
        private readonly Stack<ChunkView> _idleViews = new Stack<ChunkView>();

        private sealed class ChunkView
        {
            public GameObject Root;
            public MeshFilter Filter;
            public MeshRenderer Renderer;
            public Mesh Mesh;
            public List<Vector3> Vertices = new List<Vector3>(2601);
            public List<Vector3> Normals = new List<Vector3>(2601);
            public List<Color32> Colors = new List<Color32>(2601);
            public List<int> Indices = new List<int>(15000);
        }

        public int ResidentChunkCount => _resident.Count;
        public int BuiltChunkCount => _views.Count;
        public float StreamRadiusM => streamRadiusM;
        public int KeepRing => keepRing;

        public void Initialise(SiranIslandData data, Transform focus, Material material = null)
        {
            _data = data;
            _focus = focus;
            if (material != null)
            {
                chunkMaterial = material;
            }

            if (chunkMaterial == null)
            {
                // A vertex-colour material is required for the greybox look; fall
                // back to Unity's default so the streamer is still diagnosable.
                chunkMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"))
                {
                    name = "ChunkFallback",
                    enableInstancing = true,
                };
                chunkMaterial.SetFloat("_Smoothness", 0.05f);
                GameLog.Warn("stream", "no chunk material supplied; using a generated URP/Lit fallback");
            }

            GameLog.Info("stream", string.Format(
                "map {0}x{0} m, {1} chunks of {2} m, radius {3} m, keep_ring {4}",
                SiranMapConfig.MapSizeM, SiranMapConfig.ChunkCount, SiranMapConfig.ChunkSizeM,
                streamRadiusM, keepRing));

            ApplyQualityBudget();
        }

        public void ApplyQualityBudget()
        {
            streamRadiusM = QualityProfileHook.ActiveStreamRadius;
        }

        public void SetFocus(Transform focus)
        {
            _focus = focus;
            _lastFocusPosition = new Vector3(float.MaxValue, 0f, 0f);
        }

        private void Update()
        {
            if (_data == null || _focus == null)
            {
                return;
            }

            _tickAccumulator += Time.unscaledDeltaTime;
            if (_tickAccumulator < tickIntervalSeconds)
            {
                return;
            }

            _tickAccumulator = 0f;

            // Only recompute when the focus leaves the chunk it was in: the
            // desired set cannot change while the focus chunk is the same.
            var focusChunk = SiranMapConfig.WorldToChunk(_focus.position.x, _focus.position.z);
            var previousChunk = SiranMapConfig.WorldToChunk(_lastFocusPosition.x, _lastFocusPosition.z);
            var first = _lastFocusPosition.x == float.MaxValue;
            _lastFocusPosition = _focus.position;

            if (first || focusChunk != previousChunk || _resident.Count == 0)
            {
                RecomputeDesired(focusChunk);
            }

            var budget = first && preloadImmediately ? int.MaxValue : maxChunksPerTick;
            BuildDesired(budget);
            ReleaseOutsideRadius();
        }

        private void RecomputeDesired(Vector2Int focusChunk)
        {
            _desired.Clear();
            var side = SiranMapConfig.ChunksPerSide;

            // A radius smaller than one chunk means "one chunk" literally; the keep
            // ring is the floor that keeps the world present around the player.
            var radiusChunks = Mathf.CeilToInt(streamRadiusM / SiranMapConfig.ChunkSizeM);
            var ring = Mathf.Max(radiusChunks, keepRing + 1);

            for (var dz = -ring; dz <= ring; dz++)
            {
                for (var dx = -ring; dx <= ring; dx++)
                {
                    var cx = focusChunk.x + dx;
                    var cz = focusChunk.y + dz;
                    if (cx < 0 || cz < 0 || cx >= side || cz >= side)
                    {
                        continue;
                    }

                    var center = SiranMapConfig.ChunkCenter(cx, cz);
                    var inRing = Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dz)) <= keepRing;
                    if (!inRing)
                    {
                        // Distance from the point on the chunk rectangle nearest the focus.
                        var nearestX = Mathf.Clamp(_focus.position.x, center.x - SiranMapConfig.ChunkSizeM * 0.5f,
                            center.x + SiranMapConfig.ChunkSizeM * 0.5f);
                        var nearestZ = Mathf.Clamp(_focus.position.z, center.y - SiranMapConfig.ChunkSizeM * 0.5f,
                            center.y + SiranMapConfig.ChunkSizeM * 0.5f);
                        var dxM = _focus.position.x - nearestX;
                        var dzM = _focus.position.z - nearestZ;
                        if (dxM * dxM + dzM * dzM > streamRadiusM * streamRadiusM)
                        {
                            continue;
                        }
                    }

                    _desired.Add(new Vector2Int(cx, cz));
                }
            }
        }

        private void BuildDesired(int budget)
        {
            var built = 0;
            for (var i = 0; i < _desired.Count && built < budget; i++)
            {
                var chunk = _desired[i];
                if (_resident.Contains(chunk))
                {
                    continue;
                }

                BuildChunk(chunk);
                _resident.Add(chunk);
                built++;
            }
        }

        private void ReleaseOutsideRadius()
        {
            if (_resident.Count == _desired.Count)
            {
                return;
            }

            _toRelease.Clear();
            foreach (var chunk in _resident)
            {
                if (!_desired.Contains(chunk))
                {
                    _toRelease.Add(chunk);
                }
            }

            for (var i = 0; i < _toRelease.Count; i++)
            {
                ReleaseChunk(_toRelease[i]);
            }
        }

        private readonly List<Vector2Int> _toRelease = new List<Vector2Int>(16);

        private void BuildChunk(Vector2Int chunk)
        {
            if (!drawChunkMeshes)
            {
                return;
            }

            if (!_views.TryGetValue(chunk, out var view))
            {
                view = RentView();
                _views[chunk] = view;
            }

            _data.BuildChunkMesh(chunk.x, chunk.y, view.Vertices, view.Normals, view.Colors, view.Indices);

            var mesh = view.Mesh;
            mesh.Clear();
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt16; // 2601 verts
            mesh.SetVertices(view.Vertices);
            mesh.SetNormals(view.Normals);
            mesh.SetColors(view.Colors);
            mesh.SetTriangles(view.Indices, 0, calculateBounds: true);
            mesh.name = string.Format("SiranChunk_{0}_{1}", chunk.x, chunk.y);

            var origin = SiranMapConfig.ChunkCenter(chunk.x, chunk.y);
            view.Root.transform.position = new Vector3(origin.x, 0f, origin.y);
            view.Root.name = mesh.name;
            view.Root.SetActive(true);
        }

        private void ReleaseChunk(Vector2Int chunk)
        {
            _resident.Remove(chunk);
            if (!_views.TryGetValue(chunk, out var view))
            {
                return;
            }

            _views.Remove(chunk);
            view.Root.SetActive(false);
            _idleViews.Push(view);
        }

        private ChunkView RentView()
        {
            if (_idleViews.Count > 0)
            {
                return _idleViews.Pop();
            }

            var go = new GameObject("Chunk");
            go.transform.SetParent(transform, false);
            go.isStatic = true;
            var filter = go.AddComponent<MeshFilter>();
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = chunkMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

            var mesh = new Mesh { name = "ChunkMesh" };
            mesh.MarkDynamic();
            filter.sharedMesh = mesh;
            return new ChunkView { Root = go, Filter = filter, Renderer = renderer, Mesh = mesh };
        }

        /// <summary>Test/telemetry helper: force a full update at a position.</summary>
        public void ForceTickAt(Vector3 position)
        {
            if (_data == null)
            {
                return;
            }

            var focusChunk = SiranMapConfig.WorldToChunk(position.x, position.z);
            RecomputeDesired(focusChunk);
            BuildDesired(int.MaxValue);
            ReleaseOutsideRadius();
        }
    }
}
