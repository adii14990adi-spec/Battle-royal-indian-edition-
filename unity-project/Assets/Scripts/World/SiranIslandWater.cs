using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BattleHorizons.World
{
    /// <summary>
    /// Sea, lake and river surfaces, ported from `scenes/world/siran_island.tscn`
    /// + `assets/shaders/water_surface.gdshader`.
    ///
    /// Opacity and geometry are a 1:1 port: one 4600 m sea plane at sea level, one
    /// 1040 x 760 lake plane at 9 m, and the river ribbon rebuilt from the same
    /// descending surface profile. The material is URP Lit with the ported tiling
    /// normal map rather than a bespoke shader, deliberately:
    ///
    ///   * the shader was `unshaded` in Godot because the Compatibility renderer
    ///     re-shades per light; URP's forward renderer with one directional light
    ///     costs the same and is a shader URP can batch/strip properly,
    ///   * it keeps water on the same SRP batcher as terrain, so the draw-call
    ///     budget does not grow.
    ///
    /// The ripple *motion* is preserved by scrolling the normal map's UVs in
    /// <see cref="Update"/>, which is what `TIME` did in the original shader.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SiranIslandWater : MonoBehaviour
    {
        [Header("Geometry (metres, ported from the Godot world scene)")]
        [SerializeField] private float seaPlaneSize = 4600f;
        [SerializeField] private Vector2 lakePlaneSize = new Vector2(1040f, 760f);

        [Header("Ripple scroll (port of the water_surface shader uniforms)")]
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

            // Two layers scrolling at different rates over the same tiling normal
            // map, exactly like the original shader's A/B samples.
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
                renderer.SetPropertyBlock(_block);
            }
        }
    }
}
