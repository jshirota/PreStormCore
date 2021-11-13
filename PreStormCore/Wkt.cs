using System.Text.RegularExpressions;

namespace PreStormCore;

public static class Wkt
{
    internal static string ToWkt(this Point point)
    {
        return point.z.HasValue
            ? $"POINT Z({point.x} {point.y} {point.z})"
            : $"POINT({point.x} {point.y})";
    }

    internal static string ToWkt(this Multipoint multipoint)
    {
        return multipoint.points?.Length > 0
            ? $"MULTIPOINT{(multipoint.points.Any(p => p.Length > 2) ? " Z" : "")}({string.Join(",", multipoint.points.Select(p => $"({string.Join(" ", p)})"))})"
            : "MULTIPOINT EMPTY";
    }

    internal static string ToWkt(this Polyline polyline)
    {
        return polyline.paths?.Length > 0
            ? $"MULTILINESTRING{(polyline.paths.SelectMany(p => p).Any(c => c.Length > 2) ? " Z" : "")}({string.Join(",", polyline.paths.Select(p => $"({string.Join(",", p.Select(c => string.Join(" ", c)))})"))})"
            : "MULTILINESTRING EMPTY";
    }

    internal static string ToWkt(this Polygon polygon)
    {
        return polygon.rings?.Length > 0
            ? $"MULTIPOLYGON{(polygon.rings.SelectMany(r => r).Any(c => c.Length > 2) ? " Z" : "")}({string.Join(",", polygon.GroupRings().Select(p => $"({string.Join(",", p.Select(r => $"({string.Join(",", r.Reverse().Select(c => string.Join(" ", c)))})"))})"))})"
            : "MULTIPOLYGON EMPTY";
    }

    private static string? ToJson(this string wkt, string type)
    {
        if (Regex.IsMatch(wkt, $@"^\s*{type}\s+EMPTY\s*$", RegexOptions.IgnoreCase))
            return null;

        return Regex.Replace(Regex.Replace(wkt, @"(?<x>\-?\d+(\.\d+)?)\s+(?<y>\-?\d+(\.\d+)?)\s?(?<z>\-?\d+(\.\d+)?)?",
            m =>
            {
                var z = m.Groups["z"].Value;
                return $"[{m.Groups["x"]},{m.Groups["y"]}{(z == "" ? "" : $",{z}")}]";
            }),
            $@"{type}\s?Z?", "", RegexOptions.IgnoreCase)
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
        point.z = coordinates.Length > 2 ? coordinates[2] : null;
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
        var rings = wkt.StartsWith("POLYGON")
            ? wkt.ToJson("POLYGON")?.Deserialize<double[][][]>()
            : wkt.ToJson("MULTIPOLYGON")?.Deserialize<double[][][][]>()?.SelectMany(p => p);

        polygon.rings = rings?.Select(r => r.Reverse().ToArray()).ToArray() ?? Array.Empty<double[][]>();
    }

    public static string ToWkt(this Geometry geometry)
    {
        return geometry switch
        {
            Point point => point.ToWkt(),
            Multipoint multipoint => multipoint.ToWkt(),
            Polyline polyline => polyline.ToWkt(),
            Polygon polygon => polygon.ToWkt(),
            _ => throw new ArgumentException("This geometry type is not supported.", nameof(geometry))
        };
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

        if (s.StartsWith("POLYGON") || s.StartsWith("MULTIPOLYGON"))
            return Polygon.FromWkt(wkt);

        throw new ArgumentException("This geometry type is not supported.", nameof(wkt));
    }
}
