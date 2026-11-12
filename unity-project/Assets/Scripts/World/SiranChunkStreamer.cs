using System.Collections.Generic;
using BattleHorizons.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace BattleHorizons.World
{
    /// <summary>
    /// Streams the Siran Island's 500 m data tiles around a focus point. The
    /// resident set is the set of tile rectangles intersected by a 50 m circle;
    /// a shader clip keeps the rendered terrain, roads and water inside that same
    /// circle even though the source meshes are much larger tiles.
    /// </summary>
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    public sealed class SiranChunkStreamer : MonoBehaviour
    {
        public const float DefaultStreamRadiusM = 50f;

        private float streamRadiusM = DefaultStreamRadiusM;

        [Header("Streaming")]
        [Tooltip("How often the set of intersecting 500 m tiles is recomputed.")]
        [SerializeField] private float tickIntervalSeconds = 0.2f;

        [Tooltip("New tiles that may be built per update tick after the initial fill.")]
        [Range(1, 8)]
        [SerializeField] private int maxChunksPerTick = 3;

        [Header("Mesh")]
        [Tooltip("Material for chunk meshes. Vertex colours carry the greybox palette.")]
        [SerializeField] private Material chunkMaterial;

        private SiranIslandData _data;
        private Transform _focus;
        private float _tickAccumulator;

        private readonly HashSet<Vector2Int> _resident = new HashSet<Vector2Int>();
        private readonly List<Vector2Int> _desired = new List<Vector2Int>(4);
        private readonly Dictionary<Vector2Int, ChunkView> _views = new Dictionary<Vector2Int, ChunkView>();
        private readonly Stack<ChunkView> _idleViews = new Stack<ChunkView>();
        private readonly List<Vector2Int> _toRelease = new List<Vector2Int>(4);

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
        public bool IsChunkResident(Vector2Int chunk) => _resident.Contains(chunk);

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
                var shader = Shader.Find(SiranRadiusClip.ShaderName);
                if (shader != null)
                {
                    chunkMaterial = new Material(shader)
                    {
                        name = "GeneratedChunkMaterial",
                        enableInstancing = true,
                    };
                    chunkMaterial.SetFloat("_UseVertexColor", 1f);
                    chunkMaterial.SetFloat("_Metallic", 0f);
                    chunkMaterial.SetFloat("_Smoothness", 0f);
                    chunkMaterial.SetFloat("_StreamClipMode", 1f);
                }
                else
                {
                    GameLog.Error("stream", "missing " + SiranRadiusClip.ShaderName + " shader; streamed terrain cannot be rendered");
                }
            }

            ApplyQualityBudget();
            var initialPosition = FocusPosition;
            SiranRadiusClip.SetFocus(initialPosition, streamRadiusM);

            if (_data != null)
            {
                RecomputeDesired(initialPosition);
                BuildDesired(int.MaxValue);
                ReleaseOutsideRadius();
            }

            GameLog.Info("stream", string.Format(
                "map {0}x{0} m, {1} tiles of {2} m, circular radius {3} m",
                SiranMapConfig.MapSizeM, SiranMapConfig.ChunkCount, SiranMapConfig.ChunkSizeM,
                streamRadiusM));
        }

        public void ApplyQualityBudget()
        {
            // Streaming radius is a geometry contract, not a quality knob.
            streamRadiusM = QualityProfiles.StreamRadius(QualityProfiles.Active);
        }

        public void SetFocus(Transform focus)
        {
            _focus = focus;
        }

        private Vector3 FocusPosition => _focus != null ? _focus.position : transform.position;

        private void LateUpdate()
        {
            // The camera rig moves in LateUpdate; run after the default-order camera
            // so the rendered disk is centered on this frame's final focus position.
            SiranRadiusClip.SetFocus(FocusPosition, streamRadiusM);
        }

        private void Update()
        {
            var focusPosition = FocusPosition;
            if (_data == null)
            {
                return;
            }

            _tickAccumulator += Mathf.Max(Time.deltaTime, Time.unscaledDeltaTime);
            if (_tickAccumulator < tickIntervalSeconds)
            {
                return;
            }

            _tickAccumulator = 0f;
            // Chunk membership can change while the focus remains inside one tile:
            // as the 50 m disk crosses a 500 m tile edge, adjacent tiles must be
            // added/released without waiting for a grid-cell transition.
            RecomputeDesired(focusPosition);
            BuildDesired(maxChunksPerTick);
            ReleaseOutsideRadius();
        }

        private void RecomputeDesired(Vector3 focusPosition)
        {
            _desired.Clear();
            var chunkSize = SiranMapConfig.ChunkSizeM;
            var halfExtent = SiranMapConfig.HalfExtent;
            var radiusSquared = streamRadiusM * streamRadiusM;
            var side = SiranMapConfig.ChunksPerSide;

            var minX = Mathf.Clamp(Mathf.FloorToInt((focusPosition.x - streamRadiusM + halfExtent) / chunkSize), 0, side - 1);
            var maxX = Mathf.Clamp(Mathf.FloorToInt((focusPosition.x + streamRadiusM + halfExtent) / chunkSize), 0, side - 1);
            var minZ = Mathf.Clamp(Mathf.FloorToInt((focusPosition.z - streamRadiusM + halfExtent) / chunkSize), 0, side - 1);
            var maxZ = Mathf.Clamp(Mathf.FloorToInt((focusPosition.z + streamRadiusM + halfExtent) / chunkSize), 0, side - 1);

            for (var cz = minZ; cz <= maxZ; cz++)
            {
                for (var cx = minX; cx <= maxX; cx++)
                {
                    var origin = SiranMapConfig.ChunkOrigin(cx, cz);
                    var nearestX = Mathf.Clamp(focusPosition.x, origin.x, origin.x + chunkSize);
                    var nearestZ = Mathf.Clamp(focusPosition.z, origin.y, origin.y + chunkSize);
                    var dx = focusPosition.x - nearestX;
                    var dz = focusPosition.z - nearestZ;
                    if (dx * dx + dz * dz <= radiusSquared)
                    {
                        _desired.Add(new Vector2Int(cx, cz));
                    }
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

        private void BuildChunk(Vector2Int chunk)
        {
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

            var center = SiranMapConfig.ChunkCenter(chunk.x, chunk.y);
            view.Root.transform.position = new Vector3(center.x, 0f, center.y);
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
            var filter = go.AddComponent<MeshFilter>();
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = chunkMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

            var mesh = new Mesh { name = "ChunkMesh" };
            mesh.MarkDynamic();
            filter.sharedMesh = mesh;
            return new ChunkView { Root = go, Filter = filter, Renderer = renderer, Mesh = mesh };
        }

        /// <summary>Test/telemetry helper: synchronously stream the disk at a position.</summary>
        public void ForceTickAt(Vector3 position)
        {
            if (_data == null)
            {
                return;
            }

            SiranRadiusClip.SetFocus(position, streamRadiusM);
            RecomputeDesired(position);
            BuildDesired(int.MaxValue);
            ReleaseOutsideRadius();
        }
    }

    /// <summary>Updates the shader's world-space radial clip shared by terrain, roads and water.</summary>
    public static class SiranRadiusClip
    {
        public const string ShaderName = "BattleHorizons/Radius Clipped Lit";

        private static readonly int FocusProperty = Shader.PropertyToID("_BHStreamFocusXZ");
        private static readonly int RadiusProperty = Shader.PropertyToID("_BHStreamRadius");

        public static void SetFocus(Vector3 position, float radiusM)
        {
            Shader.SetGlobalVector(FocusProperty, new Vector4(position.x, position.z, 0f, 0f));
            Shader.SetGlobalFloat(RadiusProperty, Mathf.Max(0f, radiusM));
        }
    }
}
