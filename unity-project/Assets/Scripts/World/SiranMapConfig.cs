// Canonical Unity-side layout constants for Siran Island.
// Keep the companion Resources/Config/siran_island_layout.json in sync when
// changing map dimensions, zone placement, water or road control points.

using System.Collections.Generic;
using UnityEngine;

namespace BattleHorizons.World
{
    /// <summary>Footprint shape of a zone, mirroring IslandZone.Shape.</summary>
    public enum ZoneShape { Ellipse = 0, RoundedRect = 1 }

    /// <summary>What a zone is, mirroring IslandZone.Kind.</summary>
    public enum ZoneKind { Industrial = 0, Dock = 1, Urban = 2, Graveyard = 3, Airfield = 4 }

    public sealed class IslandZone
    {
        public string Id;
        public string DisplayName;
        public ZoneKind Kind;
        /// <summary>World XZ centre in metres (X = east, Z = south).</summary>
        public Vector2 Center;
        public Vector2 Size;
        public float RotationDeg;
        public ZoneShape Shape;
        public float CornerRadiusM;
        public float FlatHeight;
        public float Flatness;
        public float FeatherM;
        public Color Tint;
    }

    public sealed class IslandWaterBody
    {
        public string Id;
        public string DisplayName;
        public Vector2 Center;
        public Vector2 Radius;
        public float SurfaceHeight;
        public float Depth;
        public float ShoreFeatherM;
    }

    public sealed class IslandRiver
    {
        public string Id;
        public string DisplayName;
        public Vector2[] Points;
        public float[] Widths;
        public float SurfaceStart;
        public float SurfaceEnd;
        public float Depth;
        public float BankWidthM;
        public float SourceBlendM;
    }

    public sealed class IslandChannel
    {
        public string Id;
        public string DisplayName;
        public Vector2[] Points;
        public float HalfWidthM;
        public float DepthM;
        public float BankWidthM;
    }

    public sealed class IslandRoad
    {
        public string Id;
        public string DisplayName;
        public Vector2[] Points;
        public float Width = 16f;
        public float ShoulderWidth = 7f;
        public float SurfaceOffset = 0.18f;

        public float TotalHalfWidth => Width * 0.5f + ShoulderWidth;
    }

    /// <summary>
    /// The whole layout of Siran Island in one place. World space: metres,
    /// X = east,
    /// Y = up, Z = south, map centred on the origin.
    /// </summary>
    public static class SiranMapConfig
    {
        public const string DisplayName = "Siran Island";
        public const float MapSizeM = 4000.0f;
        public const float ChunkSizeM = 500.0f;
        public const float VertexSpacingM = 10.0f;
        public const float SeaLevel = 0.0f;
        public const float IslandRadiusM = 1950.0f;
        public const float CoastlineNoiseM = 170.0f;
        public const float MinHeightM = -38.0f;
        public const float MaxHeightM = 132.0f;
        public const int GeneratorVersion = 3;
        public const int NoiseSeed = 20261106;
        /// <summary>Where a match starts, world metres (Y is a guess: use the ground).</summary>
        public static readonly Vector3 SpawnPoint = new Vector3(40.0f, 28.0f, -30.0f);

        public static int ChunksPerSide => Mathf.Max(1, Mathf.RoundToInt(MapSizeM / ChunkSizeM));
        public static int ChunkCount => ChunksPerSide * ChunksPerSide;
        public static int ChunkVerticesPerSide => Mathf.Max(2, Mathf.RoundToInt(ChunkSizeM / VertexSpacingM) + 1);
        public static float HalfExtent => MapSizeM * 0.5f;

        // --- Zones -------------------------------------------------------
        public static readonly IslandZone[] Zones =
        {
            new IslandZone
            {
                Id = "factory_island",
                DisplayName = "Factory Island \u2014 Iron Mill",
                Kind = ZoneKind.Industrial,
                Center = new Vector2(-1250.0f, -1300.0f),
                Size = new Vector2(760.0f, 640.0f),
                RotationDeg = -12.0f,
                Shape = ZoneShape.Ellipse,
                CornerRadiusM = 140.8f,
                FlatHeight = 26.0f,
                Flatness = 1.0f,
                FeatherM = 90.0f,
                Tint = new Color(0.41f, 0.39f, 0.35f, 1f),
            },
            new IslandZone
            {
                Id = "konkan_docks",
                DisplayName = "Dock Island \u2014 Konkan Docks",
                Kind = ZoneKind.Dock,
                Center = new Vector2(-1470.0f, 130.0f),
                Size = new Vector2(780.0f, 540.0f),
                RotationDeg = 6.0f,
                Shape = ZoneShape.Ellipse,
                CornerRadiusM = 118.8f,
                FlatHeight = 4.5f,
                Flatness = 1.0f,
                FeatherM = 110.0f,
                Tint = new Color(0.38f, 0.4f, 0.43f, 1f),
            },
            new IslandZone
            {
                Id = "central_state",
                DisplayName = "Central State \u2014 Urban",
                Kind = ZoneKind.Urban,
                Center = new Vector2(40.0f, -30.0f),
                Size = new Vector2(1150.0f, 1150.0f),
                RotationDeg = 0.0f,
                Shape = ZoneShape.RoundedRect,
                CornerRadiusM = 253.0f,
                FlatHeight = 16.0f,
                Flatness = 0.72f,
                FeatherM = 170.0f,
                Tint = new Color(0.52f, 0.5f, 0.45f, 1f),
            },
            new IslandZone
            {
                Id = "graveyard_center",
                DisplayName = "Graveyard Center \u2014 Foggy Hills",
                Kind = ZoneKind.Graveyard,
                Center = new Vector2(1350.0f, -80.0f),
                Size = new Vector2(820.0f, 900.0f),
                RotationDeg = 12.0f,
                Shape = ZoneShape.Ellipse,
                CornerRadiusM = 180.4f,
                FlatHeight = 42.0f,
                Flatness = 0.55f,
                FeatherM = 190.0f,
                Tint = new Color(0.33f, 0.36f, 0.32f, 1f),
            },
            new IslandZone
            {
                Id = "aircraft_industry",
                DisplayName = "Aircraft Industry \u2014 Hangars & Runway",
                Kind = ZoneKind.Airfield,
                Center = new Vector2(120.0f, 1480.0f),
                Size = new Vector2(1020.0f, 740.0f),
                RotationDeg = 0.0f,
                Shape = ZoneShape.RoundedRect,
                CornerRadiusM = 162.8f,
                FlatHeight = 12.0f,
                Flatness = 1.0f,
                FeatherM = 90.0f,
                Tint = new Color(0.61f, 0.6f, 0.56f, 1f),
            },
        };

        // --- Water -------------------------------------------------------
        public static readonly IslandWaterBody Lake = new IslandWaterBody
        {
            Id = "siran_lake",
            DisplayName = "Siran Lake",
            Center = new Vector2(250.0f, -1150.0f),
            Radius = new Vector2(520.0f, 380.0f),
            SurfaceHeight = 9.0f,
            Depth = 11.0f,
            ShoreFeatherM = 80.0f,
        };

        public static readonly IslandRiver River = new IslandRiver
        {
            Id = "siran_river",
            DisplayName = "Siran River",
            Points = new[]
            {
                new Vector2(250.0f, -790.0f),
                new Vector2(500.0f, -520.0f),
                new Vector2(760.0f, -300.0f),
                new Vector2(880.0f, -40.0f),
                new Vector2(900.0f, 420.0f),
                new Vector2(960.0f, 900.0f),
                new Vector2(1080.0f, 1380.0f),
                new Vector2(1160.0f, 1750.0f),
                new Vector2(1200.0f, 2060.0f),
            },
            Widths = new[] { 70.0f, 72.0f, 76.0f, 80.0f, 86.0f, 92.0f, 100.0f, 110.0f, 110.0f },
            SurfaceStart = 9.0f,
            SurfaceEnd = -0.5f,
            Depth = 2.8f,
            BankWidthM = 24.0f,
            SourceBlendM = 90.0f,
        };

        public static readonly IslandChannel[] Channels =
        {
            new IslandChannel
            {
                Id = "iron_mill_moat",
                DisplayName = "Iron Mill Channel",
                Points = new[]
                {
                    new Vector2(-754.891029f, -1405.238661f),
                    new Vector2(-745.027124f, -1265.444844f),
                    new Vector2(-801.193718f, -1132.013426f),
                    new Vector2(-889.933892f, -1014.840732f),
                    new Vector2(-1016.707523f, -931.216385f),
                    new Vector2(-1165.515084f, -902.529722f),
                    new Vector2(-1308.961989f, -896.689759f),
                    new Vector2(-1462.952034f, -893.041966f),
                    new Vector2(-1613.80227f, -942.234129f),
                    new Vector2(-1697.260829f, -1063.041322f),
                    new Vector2(-1713.475678f, -1201.485204f),
                    new Vector2(-1711.948792f, -1331.61103f),
                    new Vector2(-1681.803375f, -1461.622448f),
                    new Vector2(-1598.03133f, -1575.628162f),
                    new Vector2(-1482.979231f, -1668.288442f),
                    new Vector2(-1344.319139f, -1743.736659f),
                    new Vector2(-1182.795803f, -1759.688378f),
                    new Vector2(-1044.165633f, -1693.35595f),
                    new Vector2(-943.389385f, -1601.52317f),
                    new Vector2(-840.846143f, -1516.769614f),
                },
                HalfWidthM = 34.0f,
                DepthM = 4.5f,
                BankWidthM = 30.0f,
            },
        };

        public static readonly IslandRoad[] Roads =
        {
            new IslandRoad
            {
                Id = "docks_central",
                DisplayName = "Konkan Docks \u2192 Central State",
                Points = new[]
                {
                    new Vector2(-1470.0f, 130.0f),
                    new Vector2(-1180.0f, 110.0f),
                    new Vector2(-820.0f, 70.0f),
                    new Vector2(-430.0f, 20.0f),
                    new Vector2(40.0f, -30.0f),
                },
                Width = 16.0f,
                ShoulderWidth = 7.0f,
            },
            new IslandRoad
            {
                Id = "factory_central",
                DisplayName = "Iron Mill \u2192 Central State",
                Points = new[]
                {
                    new Vector2(-1250.0f, -1300.0f),
                    new Vector2(-1080.0f, -980.0f),
                    new Vector2(-880.0f, -700.0f),
                    new Vector2(-560.0f, -420.0f),
                    new Vector2(-160.0f, -160.0f),
                    new Vector2(40.0f, -30.0f),
                },
                Width = 16.0f,
                ShoulderWidth = 7.0f,
            },
            new IslandRoad
            {
                Id = "central_airbase",
                DisplayName = "Central State \u2192 Aircraft Industry",
                Points = new[]
                {
                    new Vector2(40.0f, -30.0f),
                    new Vector2(60.0f, 320.0f),
                    new Vector2(80.0f, 760.0f),
                    new Vector2(110.0f, 1120.0f),
                    new Vector2(120.0f, 1480.0f),
                },
                Width = 16.0f,
                ShoulderWidth = 7.0f,
            },
            new IslandRoad
            {
                Id = "central_graveyard",
                DisplayName = "Central State \u2192 Graveyard Center",
                Points = new[]
                {
                    new Vector2(40.0f, -30.0f),
                    new Vector2(380.0f, -140.0f),
                    new Vector2(700.0f, -160.0f),
                    new Vector2(1000.0f, -120.0f),
                    new Vector2(1350.0f, -80.0f),
                },
                Width = 16.0f,
                ShoulderWidth = 7.0f,
            },
            new IslandRoad
            {
                Id = "coast_highway",
                DisplayName = "Coast Highway \u2014 Aircraft Industry \u2192 Konkan Docks",
                Points = new[]
                {
                    new Vector2(120.0f, 1480.0f),
                    new Vector2(-350.0f, 1330.0f),
                    new Vector2(-900.0f, 940.0f),
                    new Vector2(-1280.0f, 560.0f),
                    new Vector2(-1470.0f, 130.0f),
                },
                Width = 16.0f,
                ShoulderWidth = 7.0f,
            },
        };

        /// <summary>Innermost zone containing a world XZ point (smallest footprint wins).</summary>
        public static IslandZone ZoneAt(float x, float z)
        {
            IslandZone best = null;
            var bestArea = float.MaxValue;
            for (var i = 0; i < Zones.Length; i++)
            {
                var zone = Zones[i];
                if (IslandShapeMath.SignedDistance(zone, x, z) > 0f) continue;
                var area = zone.Size.x * zone.Size.y;
                if (area >= bestArea) continue;
                bestArea = area;
                best = zone;
            }
            return best;
        }

        public static IslandZone ZoneById(string id)
        {
            for (var i = 0; i < Zones.Length; i++)
            {
                if (Zones[i].Id == id) return Zones[i];
            }
            return null;
        }

        /// <summary>Chunk index containing a world XZ point, clamped to the grid.</summary>
        public static Vector2Int WorldToChunk(float x, float z)
        {
            var side = ChunksPerSide;
            var cx = Mathf.Clamp(Mathf.FloorToInt((x + HalfExtent) / ChunkSizeM), 0, side - 1);
            var cz = Mathf.Clamp(Mathf.FloorToInt((z + HalfExtent) / ChunkSizeM), 0, side - 1);
            return new Vector2Int(cx, cz);
        }

        /// <summary>World XZ origin of a chunk (its north-west corner).</summary>
        public static Vector2 ChunkOrigin(int cx, int cz)
        {
            return new Vector2(-HalfExtent + cx * ChunkSizeM, -HalfExtent + cz * ChunkSizeM);
        }
        public static Vector2 ChunkCenter(int cx, int cz)
        {
            return ChunkOrigin(cx, cz) + new Vector2(ChunkSizeM * 0.5f, ChunkSizeM * 0.5f);
        }

        public static readonly List<string> ZoneIds = new List<string>
        {
            "factory_island",
            "konkan_docks",
            "central_state",
            "graveyard_center",
            "aircraft_industry",
        };
    }
}
