using System;
using System.Linq;

namespace PreStormCore
{
    public abstract class GeometryBase
    {
        public SpatialReference? spatialReference { get; set; }
    }

    public abstract class Geometry : GeometryBase
    {
    }

    public class SpatialReference
    {
        public int wkid { get; set; }

        public SpatialReference() { }

        public SpatialReference(int wkid)
        {
            this.wkid = wkid;
        }

        public static implicit operator SpatialReference(int wkid)
            => new SpatialReference { wkid = wkid };
    }

    public sealed class Point : Geometry
    {
        public double x { get; set; }
        public double y { get; set; }
        public double? z { get; set; }

        public Point() { }

        public Point(double x, double y, double? z, SpatialReference? spatialReference = null)
        {
            this.x = x;
            this.y = y;
            this.z = z;
            this.spatialReference = spatialReference;
        }

        public Point(double x, double y, SpatialReference? spatialReference = null)
        {
            this.x = x;
            this.y = y;
            this.spatialReference = spatialReference;
        }

        public static Point? FromJson(string json, SpatialReference? spatialReference = null)
        {
            var point = json.Deserialize<Point>();
            point.spatialReference = spatialReference;
            return point;
        }

        public static Point FromWkt(string wkt, SpatialReference? spatialReference = null)
        {
            var point = new Point();
            point.LoadWkt(wkt);
            point.spatialReference = spatialReference;
            return point;
        }

        public static implicit operator Point((double x, double y) point)
        {
            return new Point(point.x, point.y);
        }

        public static implicit operator Point((double x, double y, double? z) point)
        {
            return new Point(point.x, point.y, point.z);
        }

        public static implicit operator (double x, double y)(Point point)
        {
            return (point.x, point.y);
        }

        public static implicit operator (double x, double y, double? z)(Point point)
        {
            return (point.x, point.y, point.z);
        }

        public void Deconstruct(out double x, out double y)
        {
            x = this.x;
            y = this.y;
        }

        public void Deconstruct(out double x, out double y, out double? z)
        {
            x = this.x;
            y = this.y;
            z = this.z;
        }

        public static bool operator ==(Point point1, Point point2) => point1.Equals(point2);
        public static bool operator !=(Point point1, Point point2) => !point1.Equals(point2);

        public override bool Equals(object? obj)
            => ReferenceEquals(obj, this) || obj is Point point
            && (point.x, point.y, point.z, point.spatialReference?.wkid) == (x, y, z, spatialReference?.wkid);

        public override int GetHashCode() => base.GetHashCode();
    }

    public sealed class Multipoint : Geometry
    {
        public double[][]? points { get; set; }

        public Multipoint() { }

        public Multipoint(params Point[] points)
        {
            this.points = points
                .Select(p => new[] { p.x, p.y })
                .ToArray();
        }

        public static Multipoint? FromJson(string json, SpatialReference? spatialReference = null)
        {
            var multipoint = json.Deserialize<Multipoint>();
            multipoint.spatialReference = spatialReference;
            return multipoint;
        }

        public static Multipoint FromWkt(string wkt, SpatialReference? spatialReference = null)
        {
            var multipoint = new Multipoint();
            multipoint.LoadWkt(wkt);
            multipoint.spatialReference = spatialReference;
            return multipoint;
        }

        public static bool operator ==(Multipoint multipoint1, Multipoint multipoint2) => multipoint1.Equals(multipoint2);

        public static bool operator !=(Multipoint multipoint1, Multipoint multipoint2) => !multipoint1.Equals(multipoint2);

        public override bool Equals(object? obj)
            => ReferenceEquals(obj, this) || obj is Multipoint multipoint
            && multipoint.spatialReference?.wkid == spatialReference?.wkid
            && multipoint.points is not null
            && points is not null
            && multipoint.points.SelectMany(c => c).SequenceEqual(points.SelectMany(c => c));

        public override int GetHashCode() => base.GetHashCode();
    }

    public sealed class Polyline : Geometry
    {
        public double[][][]? paths { get; set; }

        public object[][]? curvePaths { get; set; }

        public Polyline() { }

        public Polyline(params Point[] points)
        {
            var path = points
                .Select(p => new[] { p.x, p.y })
                .ToArray();

            paths = new[] { path };
        }

        public static Polyline? FromJson(string json, SpatialReference? spatialReference = null)
        {
            var polyline = json.Deserialize<Polyline>();
            polyline.spatialReference = spatialReference;
            return polyline;
        }

        public static Polyline FromWkt(string wkt, SpatialReference? spatialReference = null)
        {
            var polyline = new Polyline();
            polyline.LoadWkt(wkt);
            polyline.spatialReference = spatialReference;
            return polyline;
        }

        public static bool operator ==(Polyline polyline1, Polyline polyline2) => polyline1.Equals(polyline2);

        public static bool operator !=(Polyline polyline1, Polyline polyline2) => !polyline1.Equals(polyline2);

        public override bool Equals(object? obj)
            => ReferenceEquals(obj, this) || obj is Polyline polyline
            && polyline.spatialReference?.wkid == spatialReference?.wkid
            && polyline.paths is not null
            && paths is not null
            && polyline.paths.SelectMany(p => p).SelectMany(c => c).SequenceEqual(paths.SelectMany(p => p).SelectMany(c => c));

        public override int GetHashCode() => base.GetHashCode();
    }

    public sealed class Polygon : Geometry
    {
        public double[][][]? rings { get; set; }
        public object[][]? curveRings { get; set; }

        public Polygon() { }

        public Polygon(params Point[] points)
        {
            var ring = points
                .Concat(points.First() == points.Last() ? Array.Empty<Point>() : points.Take(1))
                .Select(p => new[] { p.x, p.y })
                .ToArray();

            rings = new[] { ring };
        }

        public static Polygon? FromJson(string json, SpatialReference? spatialReference = null)
        {
            var polygon = json.Deserialize<Polygon>();
            polygon.spatialReference = spatialReference;
            return polygon;
        }

        public static Polygon FromWkt(string wkt, SpatialReference? spatialReference = null)
        {
            var polygon = new Polygon();
            polygon.LoadWkt(wkt);
            polygon.spatialReference = spatialReference;
            return polygon;
        }

        public static bool operator ==(Polygon polygon1, Polygon polygon2) => polygon1.Equals(polygon2);
        public static bool operator !=(Polygon polygon1, Polygon polygon2) => !polygon1.Equals(polygon2);

        public override bool Equals(object? obj)
            => ReferenceEquals(obj, this) || obj is Polygon polygon
            && polygon.spatialReference?.wkid == spatialReference?.wkid
            && polygon.rings is not null
            && rings is not null
            && polygon.rings.SelectMany(r => r).SelectMany(c => c).SequenceEqual(rings.SelectMany(r => r).SelectMany(c => c));

        public override int GetHashCode() => base.GetHashCode();
    }

    public class Envelope : GeometryBase
    {
        public double xmin { get; set; }

        public double ymin { get; set; }

        public double xmax { get; set; }

        public double ymax { get; set; }

        public Envelope() { }

        public Envelope(double xmin, double ymin, double xmax, double ymax)
        {
            this.xmin = xmin;
            this.ymin = ymin;
            this.xmax = xmax;
            this.ymax = ymax;
        }

        public static Envelope? FromJson(string json, SpatialReference? spatialReference = null)
        {
            var envelope = json.Deserialize<Envelope>();
            envelope.spatialReference = spatialReference;
            return envelope;
        }

        public static implicit operator Envelope((double xmin, double ymin, double xmax, double ymax) envelope)
        {
            return new Envelope(envelope.xmin, envelope.ymin, envelope.xmax, envelope.ymax);
        }

        public static implicit operator (double xmin, double ymin, double xmax, double ymax)(Envelope envelope)
        {
            return (envelope.xmin, envelope.ymin, envelope.xmax, envelope.ymax);
        }

        public void Deconstruct(out double xmin, out double ymin, out double xmax, out double ymax)
        {
            xmin = this.xmin;
            ymin = this.ymin;
            xmax = this.xmax;
            ymax = this.ymax;
        }

        public static bool operator ==(Envelope envelope1, Envelope envelope2) => envelope1.Equals(envelope2);
        public static bool operator !=(Envelope envelope1, Envelope envelope2) => !envelope1.Equals(envelope2);

        public override bool Equals(object? obj)
            => ReferenceEquals(obj, this) || obj is Envelope envelope
            && (envelope.xmin, envelope.ymin, envelope.xmax, envelope.ymax, envelope.spatialReference?.wkid) == (xmin, ymin, xmax, ymax, envelope.spatialReference?.wkid);

        public override int GetHashCode() => base.GetHashCode();
    }
}
