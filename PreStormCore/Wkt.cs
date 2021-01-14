using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace PreStormCore
{
    public static class Wkt
    {
        internal static string ToWkt(this Point point)
        {
            return $"POINT({point.x} {point.y})";
        }

        internal static string ToWkt(this Multipoint multipoint)
        {
            return multipoint.points?.Length > 0
                ? $"MULTIPOINT({string.Join(",", multipoint.points.Select(p => $"({p[0]} {p[1]})"))})"
                : "MULTIPOINT EMPTY";
        }

        internal static string ToWkt(this Polyline polyline)
        {
            return polyline.paths?.Length > 0
                ? $"MULTILINESTRING({string.Join(",", polyline.paths.Select(p => $"({string.Join(",", p.Select(c => $"{c[0]} {c[1]}"))})"))})"
                : "MULTILINESTRING EMPTY";
        }

        internal static string ToWkt(this Polygon polygon)
        {
            return polygon.rings?.Length > 0
                ? $"MULTIPOLYGON({string.Join(",", polygon.GroupRings().Select(p => $"({string.Join(",", p.Select(r => $"({string.Join(",", r.Select(c => $"{c[0]} {c[1]}"))})"))})"))})"
                : "MULTIPOLYGON EMPTY";
        }

        private static string? ToJson(this string wkt, string type)
        {
            if (Regex.IsMatch(wkt, $@"^\s*{type}\s+EMPTY\s*$", RegexOptions.IgnoreCase))
                return null;

            return Regex.Replace(Regex.Replace(wkt, @"(?<x>\-?\d+(\.\d+)?)\s+(?<y>\-?\d+(\.\d+)?)",
                m => $"[{m.Groups["x"]},{m.Groups["y"]}]"), type, "", RegexOptions.IgnoreCase)
                .Replace("(", "[")
                .Replace(")", "]");
        }

        internal static void LoadWkt(this Point point, string wkt)
        {
            var json = wkt.ToJson("POINT");

            if (json is null)
                throw new ArgumentException("Empty point is not supported.", nameof(wkt));

            var coordinates = json.Deserialize<double[][]>()![0];
            point.x = coordinates[0];
            point.y = coordinates[1];
        }

        internal static void LoadWkt(this Multipoint multipoint, string wkt)
        {
            multipoint.points = wkt.ToJson("MULTIPOINT")?.Deserialize<double[][]>() ?? Array.Empty<double[]>();
        }

        internal static void LoadWkt(this Polyline polyline, string wkt)
        {
            polyline.paths = wkt.ToJson("MULTILINESTRING")?.Deserialize<double[][][]>() ?? Array.Empty<double[][]>();
        }

        internal static void LoadWkt(this Polygon polygon, string wkt)
        {
            polygon.rings = wkt.ToJson("MULTIPOLYGON")?.Deserialize<double[][][][]>()?.SelectMany(p => p).ToArray() ?? Array.Empty<double[][]>();
        }

        public static string ToWkt(this Geometry geometry)
        {
            if (geometry is Point point)
                return point.ToWkt();

            if (geometry is Multipoint multipoint)
                return multipoint.ToWkt();

            if (geometry is Polyline polyline)
                return polyline.ToWkt();

            if (geometry is Polygon polygon)
                return polygon.ToWkt();

            throw new ArgumentException("This geometry type is not supported.", nameof(geometry));
        }

        public static Geometry ToGeometry(string wkt)
        {
            var s = wkt.ToUpperInvariant().Trim();

            if (s.StartsWith("POINT"))
                return Point.FromWkt(wkt);

            if (s.StartsWith("MULTIPOINT"))
                return Multipoint.FromWkt(wkt);

            if (s.StartsWith("MULTILINESTRING"))
                return Polyline.FromWkt(wkt);

            if (s.StartsWith("MULTIPOLYGON"))
                return Polygon.FromWkt(wkt);

            throw new ArgumentException("This geometry type is not supported.", nameof(wkt));
        }
    }
}
