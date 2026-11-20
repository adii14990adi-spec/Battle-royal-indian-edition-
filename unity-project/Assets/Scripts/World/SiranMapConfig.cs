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
        public const float MapSizeM = 2000.0f;
        public const float ChunkSizeM = 250.0f;
        public const float VertexSpacingM = 5.0f;
        public const float SeaLevel = 0.0f;
        public const float IslandRadiusM = 975.0f;
        public const float CoastlineNoiseM = 85.0f;
        public const float MinHeightM = -12.0f;
        public const float MaxHeightM = 23.3231f;
        public const int GeneratorVersion = 4;
        public const int NoiseSeed = 20261120;
        /// <summary>Where a match starts, world metres (Y is a guess: use the ground).</summary>
        public static readonly Vector3 SpawnPoint = new Vector3(20.0f, 20.0f, -15.0f);

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
                Center = new Vector2(-625.0f, -650.0f),
                Size = new Vector2(380.0f, 320.0f),
                RotationDeg = -12.0f,
                Shape = ZoneShape.Ellipse,
                CornerRadiusM = 70.4f,
                FlatHeight = 19.5f,
                Flatness = 1.0f,
                FeatherM = 45.0f,
                Tint = new Color(0.41f, 0.39f, 0.35f, 1f),
            },
            new IslandZone
            {
                Id = "konkan_docks",
                DisplayName = "Dock Island \u2014 Konkan Docks",
                Kind = ZoneKind.Dock,
                Center = new Vector2(-735.0f, 65.0f),
                Size = new Vector2(390.0f, 270.0f),
                RotationDeg = 6.0f,
                Shape = ZoneShape.Ellipse,
                CornerRadiusM = 59.4f,
                FlatHeight = 3.4f,
                Flatness = 1.0f,
                FeatherM = 55.0f,
                Tint = new Color(0.38f, 0.4f, 0.43f, 1f),
            },
            new IslandZone
            {
                Id = "central_state",
                DisplayName = "Central State \u2014 Urban",
                Kind = ZoneKind.Urban,
                Center = new Vector2(20.0f, -15.0f),
                Size = new Vector2(575.0f, 575.0f),
                RotationDeg = 0.0f,
                Shape = ZoneShape.RoundedRect,
                CornerRadiusM = 126.5f,
                FlatHeight = 12.0f,
                Flatness = 0.72f,
                FeatherM = 85.0f,
                Tint = new Color(0.52f, 0.5f, 0.45f, 1f),
            },
            new IslandZone
            {
                Id = "graveyard_center",
                DisplayName = "Graveyard Center \u2014 Foggy Hills",
                Kind = ZoneKind.Graveyard,
                Center = new Vector2(675.0f, -40.0f),
                Size = new Vector2(410.0f, 450.0f),
                RotationDeg = 12.0f,
                Shape = ZoneShape.Ellipse,
                CornerRadiusM = 90.2f,
                FlatHeight = 28.0f,
                Flatness = 0.40f,
                FeatherM = 95.0f,
                Tint = new Color(0.33f, 0.36f, 0.32f, 1f),
            },
            new IslandZone
            {
                Id = "aircraft_industry",
                DisplayName = "Aircraft Industry \u2014 Hangars & Runway",
                Kind = ZoneKind.Airfield,
                Center = new Vector2(60.0f, 740.0f),
                Size = new Vector2(510.0f, 370.0f),
                RotationDeg = 0.0f,
                Shape = ZoneShape.RoundedRect,
                CornerRadiusM = 81.4f,
                FlatHeight = 9.0f,
                Flatness = 1.0f,
                FeatherM = 45.0f,
                Tint = new Color(0.61f, 0.6f, 0.56f, 1f),
            },
        };

        // --- Water -------------------------------------------------------
        public static readonly IslandWaterBody Lake = new IslandWaterBody
        {
            Id = "siran_lake",
            DisplayName = "Siran Lake",
            Center = new Vector2(125.0f, -575.0f),
            Radius = new Vector2(260.0f, 190.0f),
            SurfaceHeight = 6.5f,
            Depth = 8.0f,
            ShoreFeatherM = 40.0f,
        };

        public static readonly IslandRiver River = new IslandRiver
    {
        Id = "siran_river",
        DisplayName = "Siran River",
        Points = new[]
        {
            new Vector2(125.0f, -395.0f),
            new Vector2(250.0f, -260.0f),
            new Vector2(380.0f, -150.0f),
            new Vector2(440.0f, -20.0f),
            new Vector2(450.0f, 210.0f),
            new Vector2(480.0f, 450.0f),
            new Vector2(540.0f, 690.0f),
            new Vector2(580.0f, 875.0f),
            new Vector2(600.0f, 985.0f),
        },
        Widths = new[] { 35.0f, 36.0f, 38.0f, 40.0f, 43.0f, 46.0f, 50.0f, 55.0f, 55.0f },
        SurfaceStart = 6.5f,
        SurfaceEnd = -0.4f,
        Depth = 2.1f,
        BankWidthM = 12.0f,
        SourceBlendM = 45.0f,
    };

    public static readonly IslandChannel[] Channels =
        {
            new IslandChannel
            {
                Id = "iron_mill_moat",
                DisplayName = "Iron Mill Channel",
                Points = new[]
                {
                    new Vector2(-377.445515f, -702.619331f),
                    new Vector2(-372.513562f, -632.722422f),
                    new Vector2(-400.596859f, -566.006713f),
                    new Vector2(-444.966946f, -507.420366f),
                    new Vector2(-508.353762f, -465.608192f),
                    new Vector2(-582.757542f, -451.264861f),
                    new Vector2(-654.480994f, -448.344879f),
                    new Vector2(-731.476017f, -446.520983f),
                    new Vector2(-806.901135f, -471.117065f),
                    new Vector2(-848.630415f, -531.520661f),
                    new Vector2(-856.737839f, -600.742602f),
                    new Vector2(-855.974396f, -665.805515f),
                    new Vector2(-840.901687f, -730.811224f),
                    new Vector2(-799.015665f, -787.814081f),
                    new Vector2(-741.489616f, -834.144221f),
                    new Vector2(-672.159569f, -871.868329f),
                    new Vector2(-591.397901f, -879.844189f),
                    new Vector2(-522.082817f, -846.677975f),
                    new Vector2(-471.694692f, -800.761585f),
                    new Vector2(-420.423071f, -758.384807f),
                },
                HalfWidthM = 17.0f,
                DepthM = 3.4f,
                BankWidthM = 15.0f,
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
                    new Vector2(-735.0f, 65.0f),
                    new Vector2(-590.0f, 55.0f),
                    new Vector2(-410.0f, 35.0f),
                    new Vector2(-215.0f, 10.0f),
                    new Vector2(20.0f, -15.0f),
                },
                Width = 8.0f,
                ShoulderWidth = 3.5f,
            },
            new IslandRoad
            {
                Id = "factory_central",
                DisplayName = "Iron Mill \u2192 Central State",
                Points = new[]
                {
                    new Vector2(-625.0f, -650.0f),
                    new Vector2(-540.0f, -490.0f),
                    new Vector2(-440.0f, -350.0f),
                    new Vector2(-280.0f, -210.0f),
                    new Vector2(-80.0f, -80.0f),
                    new Vector2(20.0f, -15.0f),
                },
                Width = 8.0f,
                ShoulderWidth = 3.5f,
            },
            new IslandRoad
            {
                Id = "central_airbase",
                DisplayName = "Central State \u2192 Aircraft Industry",
                Points = new[]
                {
                    new Vector2(20.0f, -15.0f),
                    new Vector2(30.0f, 160.0f),
                    new Vector2(40.0f, 380.0f),
                    new Vector2(55.0f, 560.0f),
                    new Vector2(60.0f, 740.0f),
                },
                Width = 8.0f,
                ShoulderWidth = 3.5f,
            },
            new IslandRoad
            {
                Id = "central_graveyard",
                DisplayName = "Central State \u2192 Graveyard Center",
                Points = new[]
                {
                    new Vector2(20.0f, -15.0f),
                    new Vector2(190.0f, -70.0f),
                    new Vector2(350.0f, -80.0f),
                    new Vector2(500.0f, -60.0f),
                    new Vector2(675.0f, -40.0f),
                },
                Width = 8.0f,
                ShoulderWidth = 3.5f,
            },
            new IslandRoad
            {
                Id = "coast_highway",
                DisplayName = "Coast Highway \u2014 Aircraft Industry \u2192 Konkan Docks",
                Points = new[]
                {
                    new Vector2(60.0f, 740.0f),
                    new Vector2(-175.0f, 665.0f),
                    new Vector2(-450.0f, 470.0f),
                    new Vector2(-640.0f, 280.0f),
                    new Vector2(-735.0f, 65.0f),
                },
                Width = 8.0f,
                ShoulderWidth = 3.5f,
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
