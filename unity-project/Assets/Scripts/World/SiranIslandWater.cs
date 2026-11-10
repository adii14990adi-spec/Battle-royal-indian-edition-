using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BattleHorizons.World
{
    /// <summary>
    /// Builds the sea, lake and river surfaces from the committed Unity map data.
    /// The opaque material uses the streamed-radius shader and a small scrolling
    /// normal map, keeping the water inside the same 50 m visual disk as terrain.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SiranIslandWater : MonoBehaviour
    {
        [Header("Geometry (metres)")]
        [SerializeField] private float seaPlaneSize = 4600f;
        [SerializeField] private Vector2 lakePlaneSize = new Vector2(1040f, 760f);

        [Header("Ripple scroll")]
        [SerializeField] private float scrollSpeedA = 0.03f;
        [SerializeField] private float scrollSpeedB = 0.019f;
        [SerializeField] private Vector2 oceanUvScale = new Vector2(0.022f, 0.022f);
        [SerializeField] private Vector2 riverUvScale = new Vector2(0.06f, 0.06f);

        private readonly List<Renderer> _waterRenderers = new List<Renderer>(3);
        private MaterialPropertyBlock _block;
        private SiranIslandData _data;

        public int SurfaceCount => _waterRenderers.Count;

        public void Build(SiranIslandData data, Material waterMaterial)
        {
            _data = data;
            _block ??= new MaterialPropertyBlock();

            if (waterMaterial == null)
            {
                Debug.LogWarning("[bh][world] no water material assigned; water will not be drawn");
                return;
            }

            var sea = CreateSurface("Ocean", new Vector3(0f, SiranMapConfig.SeaLevel, 0f),
                Quaternion.identity, new Vector2(seaPlaneSize, seaPlaneSize), waterMaterial, "Ocean");
            sea.transform.SetParent(transform, false);

            var lake = SiranMapConfig.Lake;
            if (lake != null)
            {
                var lakeObject = CreateSurface("Lake", new Vector3(lake.Center.x, lake.SurfaceHeight, lake.Center.y),
                    Quaternion.identity, lakePlaneSize, waterMaterial, "Lake");
                lakeObject.transform.SetParent(transform, false);
            }

            if (data != null && data.River != null)
            {
                var riverObject = CreateRiver(data.River, waterMaterial);
                riverObject.transform.SetParent(transform, false);
            }
        }

        private GameObject CreateSurface(string name, Vector3 position, Quaternion rotation, Vector2 size,
            Material material, string materialName)
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
