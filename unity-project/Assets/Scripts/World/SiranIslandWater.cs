using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BattleHorizons.World
{
    /// <summary>
    /// Builds the sea, lake and river surfaces from the committed Unity map data.
    ///
    /// The sea is a single large plane at sea level, the lake is a mesh generated
    /// from the height grid so its edge follows the real basin instead of a
    /// rectangle, and the river is the baked ribbon. All three use the shared
    /// world shader with a soft, low-gloss water material and a scrolling normal
    /// map. Water is drawn to the horizon (no streaming clip); only the 10 m
    /// detail terrain is clipped to the 50 m disk.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SiranIslandWater : MonoBehaviour
    {
        [Header("Geometry (metres)")]
        [SerializeField] private float seaPlaneSize = 4600f;

        [Header("Lake")]
        [Tooltip("Grid step in height samples used for the lake surface mesh (2 = 20 m).")]
        [SerializeField] private int lakeSampleStride = 2;

        [Tooltip("Lake footprint as a multiple of the authored ellipse; the basin contour trims it further.")]
        [SerializeField] private float lakeFootprintScale = 1.35f;

        [Header("Ripple scroll")]
        [SerializeField] private float scrollSpeedA = 0.03f;
        [SerializeField] private float scrollSpeedB = 0.019f;
        [SerializeField] private Vector2 oceanUvScale = new Vector2(0.022f, 0.022f);
        [SerializeField] private Vector2 riverUvScale = new Vector2(0.06f, 0.06f);

        private readonly List<Renderer> _waterRenderers = new List<Renderer>(3);
        private MaterialPropertyBlock _block;

        public int SurfaceCount => _waterRenderers.Count;

        public void Build(SiranIslandData data, Material waterMaterial)
        {
            _block ??= new MaterialPropertyBlock();

            if (waterMaterial == null)
            {
                Debug.LogWarning("[bh][world] no water material assigned; water will not be drawn");
                return;
            }

            var sea = CreateSurface("Ocean", new Vector3(0f, SiranMapConfig.SeaLevel, 0f),
                Quaternion.identity, new Vector2(seaPlaneSize, seaPlaneSize), waterMaterial);
            sea.transform.SetParent(transform, false);

            var lake = CreateLakeSurface(data, waterMaterial);
            if (lake != null)
            {
                lake.transform.SetParent(transform, false);
            }

            if (data != null && data.River != null)
            {
                var riverObject = CreateRiver(data.River, waterMaterial);
                riverObject.transform.SetParent(transform, false);
            }
        }

        private GameObject CreateSurface(string name, Vector3 position, Quaternion rotation, Vector2 size,
            Material material)
        {
            var plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            plane.name = name;
            Destroy(plane.GetComponent<Collider>()); // water is not walkable geometry
            plane.transform.SetPositionAndRotation(position, rotation);
            // Unity's Plane primitive is 10x10 units at scale 1.
            plane.transform.localScale = new Vector3(size.x / 10f, 1f, size.y / 10f);
            plane.isStatic = true;

            var renderer = plane.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            _waterRenderers.Add(renderer);
            return plane;
        }

        /// <summary>
        /// Builds the lake from the height grid: every 20 m cell whose corners dip
        /// below the lake surface gets a quad at that surface. The visible edge is
        /// therefore the real shoreline of the basin, not the authored rectangle
        /// that used to float over (and be cut by) the surrounding hills.
        /// </summary>
        private GameObject CreateLakeSurface(SiranIslandData data, Material material)
        {
            var lake = SiranMapConfig.Lake;
            if (lake == null || data == null || !data.IsLoaded)
            {
                return null;
            }

            var stride = Mathf.Clamp(lakeSampleStride, 1, 8);
            var footprint = Mathf.Max(lakeFootprintScale, 1f);
            var surfaceHeight = lake.SurfaceHeight;
            var spacing = data.SpacingM * stride;

            var minX = lake.Center.x - lake.Radius.x * footprint;
            var maxX = lake.Center.x + lake.Radius.x * footprint;
            var minZ = lake.Center.y - lake.Radius.y * footprint;
            var maxZ = lake.Center.y + lake.Radius.y * footprint;
            var nx = Mathf.Max(1, Mathf.FloorToInt((maxX - minX) / spacing));
            var nz = Mathf.Max(1, Mathf.FloorToInt((maxZ - minZ) / spacing));
            var vertsPerSide = nx + 1;

            var vertices = new List<Vector3>((nx + 1) * (nz + 1));
            var normals = new List<Vector3>((nx + 1) * (nz + 1));
            var uvs = new List<Vector2>((nx + 1) * (nz + 1));
            var submerged = new bool[(nx + 1) * (nz + 1)];

            for (var iz = 0; iz <= nz; iz++)
            {
                var z = minZ + iz * spacing;
                for (var ix = 0; ix <= nx; ix++)
                {
                    var x = minX + ix * spacing;
                    vertices.Add(new Vector3(x, surfaceHeight, z));
                    normals.Add(Vector3.up);
                    uvs.Add(new Vector2((x - minX) / (maxX - minX), (z - minZ) / (maxZ - minZ)));
                    submerged[iz * vertsPerSide + ix] = data.SampleHeight(x, z) < surfaceHeight - 0.02f;
                }
            }

            var indices = new List<int>(nx * nz * 6);
            for (var iz = 0; iz < nz; iz++)
            {
                var cellZ = minZ + (iz + 0.5f) * spacing;
                for (var ix = 0; ix < nx; ix++)
                {
                    var cellX = minX + (ix + 0.5f) * spacing;
                    var ex = (cellX - lake.Center.x) / Mathf.Max(lake.Radius.x, 0.001f);
                    var ez = (cellZ - lake.Center.y) / Mathf.Max(lake.Radius.y, 0.001f);
                    if (ex * ex + ez * ez > footprint * footprint)
                    {
                        continue;
                    }

                    var topLeft = iz * vertsPerSide + ix;
                    var topRight = topLeft + 1;
                    var bottomLeft = topLeft + vertsPerSide;
                    var bottomRight = bottomLeft + 1;
                    if (!(submerged[topLeft] || submerged[topRight] ||
                          submerged[bottomLeft] || submerged[bottomRight]))
                    {
                        continue;
                    }

                    // Clockwise from above: the same convention as the terrain.
                    indices.Add(topLeft);
                    indices.Add(bottomLeft);
                    indices.Add(topRight);
                    indices.Add(topRight);
                    indices.Add(bottomLeft);
                    indices.Add(bottomRight);
                }
            }

            if (indices.Count == 0)
            {
                Debug.LogWarning("[bh][world] the lake basin held no submerged cells; lake surface skipped");
                return null;
            }

            var mesh = new Mesh { name = "SiranLakeSurface" };
            mesh.indexFormat = vertices.Count > 65535
                ? IndexFormat.UInt32
                : IndexFormat.UInt16;
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(indices, 0, true);
            mesh.RecalculateBounds();

            var go = new GameObject("LakeSurface");
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            go.isStatic = true;

            _waterRenderers.Add(renderer);
            return go;
        }

        private GameObject CreateRiver(SiranIslandData.RiverMesh river, Material material)
        {
            var go = new GameObject("RiverSurface");
            var filter = go.AddComponent<MeshFilter>();
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            var mesh = new Mesh { name = "SiranRiverSurface" };
            mesh.SetVertices(river.Vertices);
            mesh.SetTriangles(river.Indices, 0, true);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            filter.sharedMesh = mesh;
            go.isStatic = true;

            _waterRenderers.Add(renderer);
            return go;
        }

        private void Update()
        {
            if (_waterRenderers.Count == 0)
            {
                return;
            }

            // Keep independent UV offsets for the surface and normal map; the
            // normal-map offset drives the visible ripple motion.
            var time = Time.time;
            var offsetA = new Vector4(
                time * scrollSpeedA,
                time * scrollSpeedA * 0.7f,
                time * scrollSpeedB,
                time * scrollSpeedB * 0.55f);

            for (var i = 0; i < _waterRenderers.Count; i++)
            {
                var renderer = _waterRenderers[i];
                if (renderer == null)
                {
                    continue;
                }

                var scale = renderer.gameObject.name == "RiverSurface" ? riverUvScale : oceanUvScale;
                _block.Clear();
                _block.SetVector("_BaseMap_ST", new Vector4(scale.x, scale.y, offsetA.x, offsetA.y));
                _block.SetVector("_BumpMap_ST", new Vector4(scale.x, scale.y, offsetA.z, offsetA.w));
                renderer.SetPropertyBlock(_block);
            }
        }
    }
}
