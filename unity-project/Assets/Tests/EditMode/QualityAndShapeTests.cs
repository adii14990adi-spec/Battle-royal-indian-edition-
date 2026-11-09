using BattleHorizons.Core;
using BattleHorizons.World;
using NUnit.Framework;
using UnityEngine;

namespace BattleHorizons.Tests.EditMode
{
    public class QualityProfileTests
    {
        [Test]
        public void BaselineDeviceGetsLowProfile()
        {
            // docs/UNISOC_BUDGET.md: Unisoc T606, 4 GB, 8 cores
            Assert.AreEqual(QualityProfile.Low, QualityProfiles.Detect(4000, 8, 0));
            // A 3 GB device is definitively Low.
            Assert.AreEqual(QualityProfile.Low, QualityProfiles.Detect(3000, 4, 0));
        }

        [Test]
        public void EightGigabyteTargetGetsHighProfile()
        {
            // Target baseline: Snapdragon 6s Gen 3, 8 GB, 8 cores, Adreno 710
            Assert.AreEqual(QualityProfile.High, QualityProfiles.Detect(7680, 8, 2048));
            Assert.AreEqual(QualityProfile.High, QualityProfiles.Detect(7000, 8, 0));
        }

        [Test]
        public void CoreCountGatesTheHighProfile()
        {
            Assert.AreEqual(QualityProfile.Low, QualityProfiles.Detect(8000, 4, 4096),
                "8 GB on 4 cores is still a low-end SoC with big RAM");
        }

        [Test]
        public void ProfilesScaleRenderAndViewDistance()
        {
            Assert.Less(QualityProfiles.RenderScale(QualityProfile.Low),
                QualityProfiles.RenderScale(QualityProfile.High));
            Assert.Less(QualityProfiles.ViewDistance(QualityProfile.Low),
                QualityProfiles.ViewDistance(QualityProfile.High));
            Assert.Less(QualityProfiles.ShadowDistance(QualityProfile.Low),
                QualityProfiles.ShadowDistance(QualityProfile.High));
            Assert.AreEqual(50f, QualityProfiles.StreamRadius(QualityProfile.Low));
            Assert.AreEqual(50f, QualityProfiles.StreamRadius(QualityProfile.High),
                "the circular streaming radius is a world-visibility contract, not a quality setting");
            Assert.AreEqual(50f, SiranChunkStreamer.DefaultStreamRadiusM);
            Assert.AreEqual(50f, QualityProfileHook.ActiveStreamRadius);
        }
    }

    public class IslandShapeMathTests
    {
        [Test]
        public void Smooth01IsHermite()
        {
            Assert.AreEqual(0f, IslandShapeMath.Smooth01(0f), 1e-6f);
            Assert.AreEqual(0.5f, IslandShapeMath.Smooth01(0.5f), 1e-6f);
            Assert.AreEqual(1f, IslandShapeMath.Smooth01(1f), 1e-6f);
            Assert.AreEqual(0f, IslandShapeMath.Smooth01(-3f), 1e-6f);
            Assert.AreEqual(1f, IslandShapeMath.Smooth01(9f), 1e-6f);
        }

        [Test]
        public void EllipseSignedDistanceIsZeroOnTheEdge()
        {
            var centre = new Vector2(100f, -50f);
            var size = new Vector2(200f, 100f);
            Assert.AreEqual(0f, IslandShapeMath.EllipseSignedDistance(centre.x + 100f, centre.y, centre, size, 0f), 0.01f);
            Assert.Less(IslandShapeMath.EllipseSignedDistance(centre.x, centre.y, centre, size, 0f), 0f);
            Assert.Greater(IslandShapeMath.EllipseSignedDistance(centre.x + 400f, centre.y, centre, size, 0f), 0f);
        }

        [Test]
        public void RoundedRectSignedDistanceMatchesAStraightEdge()
        {
            var centre = Vector2.zero;
            var size = new Vector2(100f, 100f);
            // 10 m outside the +X edge, no rotation, corner radius 0.
            Assert.AreEqual(10f, IslandShapeMath.RoundedRectSignedDistance(60f, 0f, centre, size, 0f, 0f), 0.01f);
            Assert.AreEqual(-50f, IslandShapeMath.RoundedRectSignedDistance(0f, 0f, centre, size, 0f, 0f), 0.01f);
        }

        [Test]
        public void RotationIsClockwiseFromAbove()
        {
            var rotated = IslandShapeMath.RotateXZ(new Vector2(1f, 0f), 90f);
            Assert.AreEqual(0f, rotated.x, 1e-4f);
            Assert.AreEqual(1f, rotated.y, 1e-4f);
        }

        [Test]
        public void PolylineQueryReportsDistanceAndProgress()
        {
            var points = new[] { new Vector2(0f, 0f), new Vector2(100f, 0f) };
            var mid = IslandShapeMath.QueryPolyline(points, 50f, 10f);
            Assert.AreEqual(10f, mid.x, 0.01f);
            Assert.AreEqual(0.5f, mid.y, 0.01f);
            Assert.AreEqual(IslandShapeMath.PolylineLength(points), 100f, 0.01f);

            var point = IslandShapeMath.PointAtDistance(points, 25f);
            Assert.AreEqual(25f, point.x, 0.01f);
        }

        [Test]
        public void WaterSurfaceIsTheHighestBodyAtAPoint()
        {
            var lake = SiranMapConfig.Lake;
            Assert.AreEqual(lake.SurfaceHeight,
                IslandShapeMath.WaterSurfaceAt(lake.Center.x, lake.Center.y), 0.001f);
            Assert.AreEqual(SiranMapConfig.SeaLevel,
                IslandShapeMath.WaterSurfaceAt(-1900f, 1900f), 0.001f);
        }

        [Test]
        public void WorldToChunkMatchesTheChunkGrid()
        {
            Assert.AreEqual(new Vector2Int(0, 0), SiranMapConfig.WorldToChunk(-1999f, -1999f));
            Assert.AreEqual(new Vector2Int(7, 7), SiranMapConfig.WorldToChunk(1999f, 1999f));
            Assert.AreEqual(new Vector2Int(4, 3), SiranMapConfig.WorldToChunk(40f, -30f));
            // Clamped, not wrapped.
            Assert.AreEqual(new Vector2Int(0, 0), SiranMapConfig.WorldToChunk(-9999f, -9999f));
            Assert.AreEqual(new Vector2Int(3, 2), SiranMapConfig.WorldToChunk(-250f, -750f));
        }
    }

    public class ObjectPoolTests
    {
        [Test]
        public void RentAndReturnReuseInstances()
        {
            var root = new GameObject("pool-root");
            var created = 0;
            var pool = new ObjectPool<Transform>(() =>
            {
                created++;
                return new GameObject("pooled").transform;
            }, root.transform, prewarm: 2);

            Assert.AreEqual(2, created);
            var first = pool.Rent();
            var second = pool.Rent();
            var third = pool.Rent();
            Assert.AreEqual(3, created);
            Assert.IsTrue(first.gameObject.activeSelf);

            pool.Return(first);
            Assert.IsFalse(first.gameObject.activeSelf);
            Assert.AreEqual(1, pool.IdleCount);

            var reused = pool.Rent();
            Assert.AreSame(first, reused, "the pool must recycle, not recreate");
            Assert.AreEqual(3, created);

            pool.Return(second);
            pool.Return(third);
            pool.Return(reused);
            Object.DestroyImmediate(root);
        }
    }
}
