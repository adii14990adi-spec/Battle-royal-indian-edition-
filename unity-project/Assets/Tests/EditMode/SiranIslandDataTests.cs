using System.Linq;
using BattleHorizons.World;
using NUnit.Framework;
using UnityEngine;

namespace BattleHorizons.Tests.EditMode
{
    /// <summary>
    /// Checks the committed island dataset: grid, extents, zone pads and mesh winding.
    ///
    /// These run in edit mode (no scene, no device) so the map can be checked in
    /// CI on a machine with no Unity licence for a player build.
    /// </summary>
    public class SiranIslandDataTests
    {
        private static SiranIslandData _data;

        [OneTimeSetUp]
        public void Load()
        {
            Assert.IsTrue(SiranIslandData.TryLoad(out _data, out var error), error);
        }

        [Test]
        public void GridMatchesTheCommittedIslandMetadata()
        {
            Assert.AreEqual(401, _data.Size, "401 samples across 400 cells at 10 m");
            Assert.AreEqual(10f, _data.SpacingM, 0.001f);
            Assert.AreEqual(2000f, _data.HalfExtentM, 0.001f);
            Assert.AreEqual(4000f, SiranMapConfig.MapSizeM, 0.001f);
            Assert.AreEqual(8, _data.ChunksPerSide);
            Assert.AreEqual(160801, _data.Heights.Length);
            Assert.AreEqual(160801, _data.Colors.Length);
        }

        [Test]
        public void HeightsAreInTheBakedRange()
        {
            // siran_island_terrain.json: min -38.0, max 64.8359, sea level 0
            Assert.AreEqual(-38f, _data.MinHeightM, 0.01f);
            Assert.AreEqual(64.8359f, _data.MaxHeightM, 0.01f);
            Assert.AreEqual(0f, _data.SeaLevelM, 0.001f);
            Assert.AreEqual(-38f, _data.Heights.Min(), 0.01f);
            Assert.AreEqual(64.8359f, _data.Heights.Max(), 0.01f);
        }

        [Test]
        public void SpawnPointSitsOnTheBakedGround()
        {
            // summary: spawn_ground_height_m = 17.5249 at (40, -30)
            var spawn = _data.SpawnPoint();
            Assert.AreEqual(40f, spawn.x, 0.01f);
            Assert.AreEqual(-30f, spawn.z, 0.01f);
            Assert.AreEqual(_data.SpawnGroundHeightM + 1.2f, spawn.y, 0.05f);
            Assert.AreEqual(17.5249f, _data.SpawnGroundHeightM, 0.05f);
        }

        [Test]
        public void EveryZonePadIsOnLand()
        {
            foreach (var zone in SiranMapConfig.Zones)
            {
                var height = _data.SampleHeight(zone.Center.x, zone.Center.y);
                var floor = SiranMapConfig.SeaLevel + (zone.Kind == ZoneKind.Dock ? 0.5f : 1.5f);
                Assert.Greater(height, floor, zone.Id + " is underwater (" + height + " m)");
            }
        }

        [Test]
        public void AirfieldIsFlatBecauseTheBakeSaidFlatnessOne()
        {
            var airfield = SiranMapConfig.ZoneById("aircraft_industry");
            Assert.IsNotNull(airfield);
            var worst = 0f;
            for (var i = 0; i <= 20; i++)
            {
                for (var j = 0; j <= 20; j++)
                {
                    var x = airfield.Center.x - airfield.Size.x * 0.4f + airfield.Size.x * 0.8f * i / 20f;
                    var z = airfield.Center.y - airfield.Size.y * 0.4f + airfield.Size.y * 0.8f * j / 20f;
                    worst = Mathf.Max(worst, Mathf.Abs(_data.SampleHeight(x, z) - airfield.FlatHeight));
                }
            }

            Assert.Less(worst, 0.15f, "runway deviates by " + worst + " m");
        }

        [Test]
        public void LakeHoldsWaterAndTheRiverDescends()
        {
            var lake = SiranMapConfig.Lake;
            var centreHeight = _data.SampleHeight(lake.Center.x, lake.Center.y);
            Assert.Less(centreHeight, lake.SurfaceHeight, "lake bed must be below its surface");

            var river = SiranMapConfig.River;
            Assert.Greater(river.SurfaceStart, river.SurfaceEnd, "the river must not flow uphill");
            Assert.AreEqual(river.Points.Length, river.Widths.Length);
            Assert.AreEqual(river.SurfaceStart, IslandShapeMath.RiverSurfaceAt(0f), 0.001f);
            Assert.AreEqual(river.SurfaceEnd, IslandShapeMath.RiverSurfaceAt(1f), 0.001f);
        }

        [Test]
        public void RoadRibbonsWereConverted()
        {
            // The committed road data contains 18 ribbons and 4,212 triangles.
            Assert.AreEqual(18, _data.RoadRibbons.Count);
            var triangles = _data.RoadRibbons.Sum(ribbon => ribbon.Indices.Length / 3);
            Assert.AreEqual(4212, triangles);
            foreach (var ribbon in _data.RoadRibbons)
            {
                Assert.Greater(ribbon.Vertices.Length, 0, ribbon.Name);
                Assert.AreEqual(ribbon.Vertices.Length, ribbon.Colors.Length, ribbon.Name);
                foreach (var index in ribbon.Indices)
                {
                    Assert.Less(index, ribbon.Vertices.Length, ribbon.Name + " index out of range");
                }
            }
        }

        [Test]
        public void RiverRibbonWasRebuilt()
        {
            Assert.IsNotNull(_data.River, "river surface missing (rebuilt from the authored centreline)");
            Assert.AreEqual(348, _data.River.Indices.Length / 3);
            foreach (var vertex in _data.River.Vertices)
            {
                Assert.Less(Mathf.Abs(vertex.y), 20f, "river surface should sit between the lake and the sea");
            }
        }

        [Test]
        public void ChunkMeshWindingFacesUp()
        {
            // The mesh winding is clockwise from above. A quad wound the other
            // way is invisible from above - the classic
            // "island disappeared" bug.
            var vertices = new System.Collections.Generic.List<Vector3>(2601);
            var normals = new System.Collections.Generic.List<Vector3>(2601);
            var colors = new System.Collections.Generic.List<Color32>(2601);
            var indices = new System.Collections.Generic.List<int>(15000);
            _data.BuildChunkMesh(4, 4, vertices, normals, colors, indices);

            Assert.AreEqual(2601, vertices.Count);
            Assert.AreEqual(15000, indices.Count);
            var wrongWay = 0;
            for (var t = 0; t < indices.Count; t += 3)
            {
                var a = vertices[indices[t]];
                var b = vertices[indices[t + 1]];
                var c = vertices[indices[t + 2]];
                if (Vector3.Cross(b - a, c - a).y > 0f)
                {
                    wrongWay++;
                }
            }

            Assert.AreEqual(0, wrongWay, wrongWay + " triangles face down");
        }

        [Test]
        public void ChunkMeshHeightsAreTheGridHeights()
        {
            var vertices = new System.Collections.Generic.List<Vector3>(2601);
            var normals = new System.Collections.Generic.List<Vector3>(2601);
            var colors = new System.Collections.Generic.List<Color32>(2601);
            var indices = new System.Collections.Generic.List<int>(15000);
            _data.BuildChunkMesh(2, 5, vertices, normals, colors, indices);

            var center = SiranMapConfig.ChunkCenter(2, 5);
            for (var i = 0; i < vertices.Count; i += 53)
            {
                var worldX = center.x + vertices[i].x;
                var worldZ = center.y + vertices[i].z;
                Assert.AreEqual(_data.SampleHeight(worldX, worldZ), vertices[i].y, 0.01f,
                    "vertex " + i + " disagrees with the height grid");
            }
        }

        [Test]
        public void ZoneLookupPrefersTheInnermostFootprint()
        {
            // The runway sits inside the urban feather band; the smaller footprint
            // must win over the broader urban feather band.
            var zone = SiranMapConfig.ZoneAt(120f, 1480f);
            Assert.IsNotNull(zone);
            Assert.AreEqual("aircraft_industry", zone.Id);
            Assert.IsNull(SiranMapConfig.ZoneAt(1900f, 1900f), "the map corner is open sea");
        }
    }
}
