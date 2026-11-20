using System.Collections;
using BattleHorizons.Player;
using BattleHorizons.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BattleHorizons.Tests.PlayMode
{
    /// <summary>
    /// The ground must be SOLID. Regression locks for the "land is gas, the
    /// player sinks into it" report: the road network carries one combined
    /// MeshCollider at exactly its rendered height (asphalt 0.12 m above the
    /// terrain so the step-up stays inside the CharacterController's step
    /// offset), and the TerrainCollider covers the whole island.
    /// </summary>
    public class WorldCollisionTests
    {
        private GameObject _world;
        private GameObject _player;

        [TearDown]
        public void TearDown()
        {
            if (_player != null)
            {
                Object.Destroy(_player);
            }

            if (_world != null)
            {
                Object.Destroy(_world);
            }
        }

        private SiranWorldBuilder BuildWorld()
        {
            _world = new GameObject("CollisionWorld");
            var builder = _world.AddComponent<SiranWorldBuilder>();
            Assert.IsTrue(builder.Build(), "SiranWorldBuilder.Build() failed");
            return builder;
        }

        [UnityTest]
        public IEnumerator RoadsCarryASolidColliderAtRenderedHeight()
        {
            var builder = BuildWorld();
            yield return null;

            var collision = builder.transform.Find("Roads/RoadsCollision");
            Assert.IsNotNull(collision, "the road network must carry a combined collision mesh");
            var meshCollider = collision.GetComponent<MeshCollider>();
            Assert.IsNotNull(meshCollider, "RoadsCollision must carry a MeshCollider");
            Assert.Greater(meshCollider.sharedMesh.triangles.Length / 3, 100,
                "the collision mesh must cover the whole ribbon network");

            // Runway pad: flat 9 m airfield plateau, asphalt hovers 0.12 m.
            Assert.IsTrue(Physics.Raycast(new Vector3(60f, 60f, 740f), Vector3.down, out var runway, 200f),
                "a downward ray on the runway must hit solid road");
            Assert.AreEqual(9f + 0.12f, runway.point.y, 0.05f,
                "the road surface must be solid at exactly its rendered height");

            // Konkan quay lane on the 3.4 m dock plateau.
            Assert.IsTrue(Physics.Raycast(new Vector3(-735f, 40f, 65f), Vector3.down, out var dock, 200f),
                "a downward ray on the dock lane must hit solid road");
            Assert.AreEqual(3.4f + 0.12f, dock.point.y, 0.05f);
        }

        [UnityTest]
        public IEnumerator PlayerRestsOnTopOfTheRoadNotInsideIt()
        {
            BuildWorld();
            _player = new GameObject("RoadPlayer");
            _player.transform.position = new Vector3(60f, 20f, 740f);
            var controller = _player.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.35f;
            controller.center = new Vector3(0f, 0.9f, 0f);
            _player.AddComponent<ThirdPersonMotor>();

            for (var i = 0; i < 180; i++)
            {
                yield return null;
            }

            Assert.AreEqual(9f + 0.12f, _player.transform.position.y, 0.35f,
                "the player must rest ON the asphalt, not sunk into it");
        }

        [UnityTest]
        public IEnumerator TerrainColliderCoversTheWholeIsland()
        {
            var builder = BuildWorld();
            yield return null;

            Assert.IsNotNull(builder.Terrain, "the world must build its collision terrain");
            var collider = builder.Terrain.GetComponent<TerrainCollider>();
            Assert.IsNotNull(collider, "terrain must carry a TerrainCollider");
            Assert.IsNotNull(collider.terrainData, "the TerrainCollider needs terrainData");

            var probes = new[]
            {
                new Vector3(0f, 80f, 0f),
                new Vector3(600f, 80f, -40f),
                new Vector3(-625f, 80f, -650f),
                new Vector3(-735f, 80f, 65f),
                new Vector3(60f, 80f, 740f),
            };
            for (var i = 0; i < probes.Length; i++)
            {
                Assert.IsTrue(Physics.Raycast(probes[i], Vector3.down, 300f),
                    "ground must be solid at " + probes[i]);
            }
        }
    }
}
