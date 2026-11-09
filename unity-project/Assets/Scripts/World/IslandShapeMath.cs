using UnityEngine;

namespace BattleHorizons.World
{
    /// <summary>
    /// Shared geometric queries for Siran Island zones, paths and water bodies.
    ///
    /// Conventions are kept identical so the ported map data means the same thing:
    ///   * a point is <c>(x, z)</c> in world metres, the map is centred on the origin,
    ///   * X = east, Z = south, Y = up,
    ///   * signed distances are negative inside a shape,
    ///   * "weight" is 1.0 inside and 0.0 past the feather band.
    /// </summary>
    public static class IslandShapeMath
    {
        public static float Smooth01(float t)
        {
            var x = Mathf.Clamp01(t);
            return x * x * (3f - 2f * x);
        }

        /// <summary>Rotates a world XZ vector. Positive degrees = clockwise seen from above.</summary>
        public static Vector2 RotateXZ(Vector2 point, float degrees)
        {
            if (Mathf.Approximately(degrees, 0f))
            {
                return point;
            }

            var radians = degrees * Mathf.Deg2Rad;
            var c = Mathf.Cos(radians);
            var s = Mathf.Sin(radians);
            return new Vector2(point.x * c - point.y * s, point.x * s + point.y * c);
        }

        /// <summary>Approximate signed distance (metres) to an ellipse. Positive outside.</summary>
        public static float EllipseSignedDistance(float x, float z, Vector2 center, Vector2 size, float rotationDeg)
        {
            var local = RotateXZ(new Vector2(x, z) - center, -rotationDeg);
            var rx = Mathf.Max(size.x * 0.5f, 0.001f);
            var rz = Mathf.Max(size.y * 0.5f, 0.001f);
            var normalised = Mathf.Sqrt(Mathf.Pow(local.x / rx, 2f) + Mathf.Pow(local.y / rz, 2f));
            return (normalised - 1f) * Mathf.Min(rx, rz);
        }

        /// <summary>Exact signed distance (metres) to a rounded rectangle. Positive outside.</summary>
        public static float RoundedRectSignedDistance(
            float x, float z, Vector2 center, Vector2 size, float rotationDeg, float cornerRadius)
        {
            var local = RotateXZ(new Vector2(x, z) - center, -rotationDeg);
            var half = new Vector2(Mathf.Max(size.x * 0.5f, 0f), Mathf.Max(size.y * 0.5f, 0f));
            var radius = Mathf.Clamp(cornerRadius, 0f, Mathf.Min(half.x, half.y));
            var q = new Vector2(Mathf.Abs(local.x), Mathf.Abs(local.y)) - (half - new Vector2(radius, radius));
            var outside = new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f));
            return outside.magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - radius;
        }

        /// <summary>1.0 inside the shape, fading to 0.0 `featherM` outside it.</summary>
        public static float FeatherWeight(float signedDistance, float featherM)
        {
            if (featherM <= 0f)
            {
                return signedDistance <= 0f ? 1f : 0f;
            }

            return 1f - Smooth01(signedDistance / featherM);
        }

        public static float SignedDistance(IslandZone zone, float x, float z) =>
            zone.Shape == ZoneShape.RoundedRect
                ? RoundedRectSignedDistance(x, z, zone.Center, zone.Size, zone.RotationDeg, zone.CornerRadiusM)
                : EllipseSignedDistance(x, z, zone.Center, zone.Size, zone.RotationDeg);

        public static float ZoneWeight(IslandZone zone, float x, float z) =>
            FeatherWeight(SignedDistance(zone, x, z), zone.FeatherM);

        public static bool ZoneContains(IslandZone zone, float x, float z) => SignedDistance(zone, x, z) <= 0f;

        // --- Polylines (roads, rivers, channels) --------------------------------

        public static float PolylineLength(Vector2[] points)
        {
            var total = 0f;
            for (var i = 1; i < points.Length; i++)
            {
                total += Vector2.Distance(points[i - 1], points[i]);
            }

            return total;
        }

        /// <summary>Walks a polyline and returns the point `distanceM` along it.</summary>
        public static Vector2 PointAtDistance(Vector2[] points, float distanceM, bool closed = false)
        {
            if (points == null || points.Length == 0)
            {
                return Vector2.zero;
            }

            if (points.Length == 1)
            {
                return points[0];
            }

            var total = 0f;
            var count = closed ? points.Length : points.Length - 1;
            for (var i = 0; i < count; i++)
            {
                total += Vector2.Distance(points[i], points[(i + 1) % points.Length]);
            }

            var remaining = Mathf.Clamp(distanceM, 0f, total);
            for (var i = 0; i < count; i++)
            {
                var a = points[i];
                var b = points[(i + 1) % points.Length];
                var segment = Vector2.Distance(a, b);
                if (remaining <= segment && segment > 0f)
                {
                    return Vector2.Lerp(a, b, remaining / segment);
                }

                remaining -= segment;
            }

            return points[points.Length - 1];
        }

        /// <summary>
        /// Nearest-point query against a polyline, ported verbatim from
        /// `IslandShapeMath.query_polyline`.
        /// <c>x</c> = distance in metres, <c>y</c> = normalised position 0..1,
        /// <c>z</c> = <c>segmentIndex + fraction</c> (use it with
        /// <see cref="SamplePackedFloat"/>).
        /// </summary>
        public static Vector3 QueryPolyline(Vector2[] points, float x, float z, bool closed = false)
        {
            if (points == null || points.Length == 0)
            {
                return new Vector3(float.PositiveInfinity, 0f, 0f);
            }

            if (points.Length == 1)
            {
                return new Vector3(Vector2.Distance(new Vector2(x, z), points[0]), 0f, 0f);
            }

            var point = new Vector2(x, z);
            var count = closed ? points.Length : points.Length - 1;
            var segmentLengths = new float[count];
            var total = 0f;
            for (var i = 0; i < count; i++)
            {
                segmentLengths[i] = Vector2.Distance(points[i], points[(i + 1) % points.Length]);
                total += segmentLengths[i];
            }

            var bestDistanceSq = float.PositiveInfinity;
            var bestSegment = 0;
            var bestFraction = 0f;
            for (var i = 0; i < count; i++)
            {
                var a = points[i];
                var ab = points[(i + 1) % points.Length] - a;
                var lengthSq = ab.sqrMagnitude;
                var fraction = 0f;
                if (lengthSq > 0.0001f)
                {
                    fraction = Mathf.Clamp01(Vector2.Dot(point - a, ab) / lengthSq);
                }

                var offset = point - (a + ab * fraction);
                var distanceSq = offset.sqrMagnitude;
                if (distanceSq >= bestDistanceSq)
                {
                    continue;
                }

                bestDistanceSq = distanceSq;
                bestSegment = i;
                bestFraction = fraction;
            }

            var walked = 0f;
            for (var i = 0; i < bestSegment; i++)
            {
                walked += segmentLengths[i];
            }

            walked += segmentLengths[bestSegment] * bestFraction;
            var normalised = total <= 0.0001f ? 0f : walked / total;
            return new Vector3(Mathf.Sqrt(bestDistanceSq), normalised, bestSegment + bestFraction);
        }

        /// <summary>Linearly samples a per-point attribute array, clamping at both ends.</summary>
        public static float SamplePackedFloat(float[] values, float position)
        {
            if (values == null || values.Length == 0)
            {
                return 0f;
            }

            if (values.Length == 1)
            {
                return values[0];
            }

            var clamped = Mathf.Clamp(position, 0f, values.Length - 1);
            var low = Mathf.FloorToInt(clamped);
            var high = Mathf.Min(low + 1, values.Length - 1);
            return Mathf.Lerp(values[low], values[high], clamped - low);
        }

        // --- Island-level queries (ports of the SiranMapConfig helpers) ---------

        public static float RiverSurfaceAt(float normalised)
        {
            var river = SiranMapConfig.River;
            var clamped = Mathf.Clamp01(normalised);
            return Mathf.Lerp(river.SurfaceStart, river.SurfaceEnd, Smooth01(clamped));
        }

        public static float RiverHalfWidthAt(float polylinePosition) =>
            SamplePackedFloat(SiranMapConfig.River.Widths, polylinePosition) * 0.5f;

        /// <summary>Highest water surface at a world point: sea, lake or river.</summary>
        public static float WaterSurfaceAt(float x, float z, bool riverVisible = true)
        {
            var surface = SiranMapConfig.SeaLevel;
            var lake = SiranMapConfig.Lake;
            if (lake != null)
            {
                var dx = (x - lake.Center.x) / Mathf.Max(lake.Radius.x, 0.001f);
                var dz = (z - lake.Center.y) / Mathf.Max(lake.Radius.y, 0.001f);
                if (Mathf.Sqrt(dx * dx + dz * dz) <= 1f)
                {
                    surface = Mathf.Max(surface, lake.SurfaceHeight);
                }
            }

            var river = SiranMapConfig.River;
            if (riverVisible && river != null && river.Points != null && river.Points.Length >= 2)
            {
                var query = QueryPolyline(river.Points, x, z);
                if (query.x <= RiverHalfWidthAt(query.z))
                {
                    surface = Mathf.Max(surface, RiverSurfaceAt(query.y));
                }
            }

            return surface;
        }
    }
}
