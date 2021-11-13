using System.Xml.Linq;

namespace PreStormCore;

public static class Kml
{
    public static XNamespace ns { get; } = "http://www.opengis.net/kml/2.2";

    private static string ToCoordinates(this Point geometry, double? z)
    {
        return geometry.x + "," + geometry.y + "," + (z ?? (geometry.z ?? 0));
    }

    private static string ToCoordinates(this double[] coordinates, double? z)
    {
        return coordinates[0] + "," + coordinates[1] + "," + (z ?? coordinates.ElementAtOrDefault(2));
    }

    private static string ToCoordinates(this double[][] coordinates, double? z)
    {
        return string.Join(" ", coordinates.Select(c => c.ToCoordinates(z)));
    }

    private static XElement ToKmlPoint(this Point geometry, double? z, XElement[] extraElements)
    {
        return new XElement(ns + "Point", extraElements,
            new XElement(ns + "coordinates", geometry.ToCoordinates(z)));
    }

    private static XElement ToKmlMultipoint(this Multipoint geometry, double? z, XElement[] extraElements)
    {
        return new XElement(ns + "MultiGeometry",
            geometry.points!.Select(p => new Point { x = p[0], y = p[1], z = p.Length > 2 ? p[2] : (double?)null }.ToKmlPoint(z, extraElements)));
    }

    private static XElement ToKmlPolyline(this Polyline geometry, double? z, XElement[] extraElements)
    {
        return new XElement(ns + "MultiGeometry",
            geometry.paths?.Select(p =>
                new XElement(ns + "LineString", extraElements,
                    new XElement(ns + "coordinates", p.ToCoordinates(z)))));
    }

    private static XElement ToKmlPolygon(this Polygon geometry, double? z, XElement[] extraElements)
    {
        return new XElement(ns + "MultiGeometry",
            geometry.GroupRings().Select(p => new XElement(ns + "Polygon", extraElements,
                p.Select(r => new XElement(ns + (r.IsClockwise() ? "innerBoundaryIs" : "outerBoundaryIs"),
                    new XElement(ns + "LinearRing",
                        new XElement(ns + "coordinates", r.ToCoordinates(z))))))));
    }

    public static XElement ToKml(this KmlStyle style)
    {
        return new XElement(ns + "Style", new XAttribute("id", style.GetHashCode()),
                   new XElement(ns + "IconStyle",
                       new XElement(ns + "color", style.IconColour),
                       new XElement(ns + "scale", style.IconScale),
                       new XElement(ns + "Icon", style.IconUrl)),
                   new XElement(ns + "LineStyle",
                       new XElement(ns + "color", style.LineColour),
                       new XElement(ns + "width", style.LineWidth)),
                   new XElement(ns + "PolyStyle",
                       new XElement(ns + "color", style.PolygonColour)));
    }

    public static XElement? ToKml(this Geometry geometry, double? z = null, params XElement[] geometryElements)
    {
        if (geometry is null)
            return null;

        if (geometry is Point point)
            return point.ToKmlPoint(z, geometryElements);

        if (geometry is Multipoint multipoint)
            return multipoint.ToKmlMultipoint(z, geometryElements);

        if (geometry is Polyline polyline)
            return polyline.ToKmlPolyline(z, geometryElements);

        if (geometry is Polygon polygon)
            return polygon.ToKmlPolygon(z, geometryElements);

        throw new ArgumentException("This geometry type is not supported.", nameof(geometry));
    }

    public static XElement ToKml(this Feature feature, string? name = null, double? z = null, XElement[]? geometryElements = null, params XElement?[]? placemarkElements)
    {
        return new XElement(ns + "Placemark", new XAttribute("id", feature.OID),
                   new XElement(ns + "name", name), placemarkElements,
                   new XElement(ns + "ExtendedData",
                       from n in feature.GetFieldNames()
                       select new XElement(ns + "Data", new XAttribute("name", n),
                                  new XElement(ns + "value", feature[n]))),
                                      feature.GetType().HasGeometry() ? ToKml(((dynamic)feature).Geometry, z, geometryElements) : null);
    }

    public static XElement ToKml(this Feature feature, string? name = null, KmlStyle? style = null)
    {
        return feature.ToKml(name, 0, null, style?.ToKml());
    }

    public static XElement ToKml<T>(this IEnumerable<T> features, Func<T, string>? name, Func<T, double?>? z, Func<T, XElement[]>? placemarkElements, params XElement[] documentElements) where T : Feature
    {
        return new XElement(ns + "kml",
                   new XElement(ns + "Document", documentElements,
                       features.Select(f => f.ToKml(name?.Invoke(f), z?.Invoke(f), null, placemarkElements?.Invoke(f)))));
    }

    public static XElement ToKml<T>(this IEnumerable<T> features, Func<T, string>? name = null, Func<T, double?>? z = null, Func<T, KmlStyle>? style = null, Func<T, XElement[]>? placemarkElements = null, params XElement[] documentElements) where T : Feature
    {
        if (style is null)
            return features.ToKml(name, z, placemarkElements, documentElements);

        var dictionary = features.Distinct().ToDictionary(f => f, style);

        return dictionary.Keys.ToKml(name, z,
            f => new[] { new XElement(ns + "styleUrl", "#" + dictionary[f].GetHashCode()) }.Concat(placemarkElements == null ? Array.Empty<XElement>() : placemarkElements(f)).ToArray(),
            dictionary.Values.Distinct().Select(ToKml).Concat(documentElements ?? Array.Empty<XElement>()).ToArray());
    }
}
