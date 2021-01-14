using System;
using System.Collections.Generic;
using System.Linq;

namespace PreStormCore
{
    public static class GeometryExt
    {
        #region Helpers

        private static bool Null(params object?[] geometries)
        {
            return geometries is not null && geometries.Any(g => g is null);
        }

        private static void AssertNotNull(params object?[] geometries)
        {
            if (Null(geometries))
                throw new ArgumentException("Input geometries cannot be null.", nameof(geometries));
        }

        private static double Length(this double[] p1, double[] p2)
        {
            return Math.Sqrt(Math.Pow(p1[0] - p2[0], 2) + Math.Pow(p1[1] - p2[1], 2));
        }

        private static double Area(double[] p1, double[] p2)
        {
            return (-p1[0] + p2[0]) * (p1[1] + p2[1]) / 2;
        }

        private static double? Distance(this Point point, double[][][] paths)
        {
            return paths.SelectMany(p => p.Zip(p.Skip(1), (p1, p2) => Distance(new Vector(p1[0], p1[1]), new Vector(p2[0], p2[1]), point))).Min();
        }

        private static double? Distance(this double[][][] paths1, double[][][] paths2)
        {
            return paths1.SelectMany(path => path.Select(p => new Point(p[0], p[1]).Distance(paths2))).Min();
        }

        private static double? Distance(Vector p1, Vector p2, Vector p)
        {
            var d = Math.Pow(Distance(p1, p2)!.Value, 2);

            if (d == 0)
                return Distance(p, p1);

            var dot = Vector.DotProduct(p - p1, p2 - p1) / d;

            if (dot < 0)
                return Distance(p, p1);

            if (dot > 1)
                return Distance(p, p2);

            return Distance(p, p1 + ((p2 - p1) * dot));
        }

        private static Envelope Extent(this double[][] points)
        {
            return new Envelope(points.Min(p => p[0]), points.Min(p => p[1]), points.Max(p => p[0]), points.Max(p => p[1]));
        }

        private static Envelope Extent(this double[][][] paths)
        {
            return paths.SelectMany(p => p).ToArray().Extent();
        }

        private static Point? Intersect(double[][] l1, double[][] l2)
        {
            if (l1 is null || l2 is null || l1.Equals(l2))
                return null;

            var p1 = new Vector(l1[0][0], l1[0][1]);
            var p2 = new Vector(l2[0][0], l2[0][1]);
            var d1 = new Vector(l1[1][0], l1[1][1]) - p1;
            var d2 = new Vector(l2[1][0], l2[1][1]) - p2;

            var d1xd2 = Vector.CrossProduct(d1, d2);

            if (d1xd2 == 0)
                return null;

            var d = p2 - p1;

            var cross1 = Vector.CrossProduct(d, d1 / d1xd2);

            if (cross1 < 0 || cross1 > 1)
                return null;

            var cross2 = Vector.CrossProduct(d, d2 / d1xd2);

            if (cross2 < 0 || cross2 > 1)
                return null;

            return p1 + cross2 * d1;
        }

        private static Point[] Intersect(double[][][] path1, double[][][] path2)
        {
            var lines1 = path1.SelectMany(p => p.Zip(p.Skip(1), (p1, p2) => new[] { p1, p2 })).ToArray();
            var lines2 = path2.SelectMany(p => p.Zip(p.Skip(1), (p1, p2) => new[] { p1, p2 })).ToArray();

            var points = from l1 in lines1
                         from l2 in lines2
                         let p = Intersect(l1, l2)
                         where p is not null
                         select p;

            return points.ToArray();
        }

        private static bool Intersects(this Envelope extent1, Envelope extent2)
        {
            return extent1.xmin <= extent2.xmax
                && extent1.ymin <= extent2.ymax
                && extent1.xmax >= extent2.xmin
                && extent1.ymax >= extent2.ymin;
        }

        private static bool Contains(double[][] ring, Point point)
        {
            return ring
                .Zip(ring.Skip(1), (p1, p2) => new { p1, p2 })
                .Where(o => o.p1[1] > point.y != o.p2[1] > point.y && point.x < (o.p2[0] - o.p1[0]) * (point.y - o.p1[1]) / (o.p2[1] - o.p1[1]) + o.p1[0])
                .Aggregate(false, (isWithin, _) => !isWithin);
        }

        private static bool Contains(this Polygon polygon, double[][] points)
        {
            return points.All(p => polygon.Contains(new Point(p[0], p[1])));
        }

        internal static bool IsClockwise(this double[][] ring)
        {
            return Enumerable.Range(0, ring.Length - 1)
                .Sum(i => ring[i][0] * ring[i + 1][1] - ring[i + 1][0] * ring[i][1]) > 0;
        }

        internal static List<List<double[][]>> GroupRings(this Polygon polygon)
        {
            var polygons = new List<List<double[][]>>();

            if (polygon?.rings?.FirstOrDefault() is null)
                return polygons;

            var isClockwise = polygon.rings.First().IsClockwise();

            bool isOuterRing(double[][] r) => r.IsClockwise() == isClockwise;

            foreach (var ring in polygon.rings)
            {
                if (isOuterRing(ring))
                    polygons.Add(new List<double[][]>());

                if (polygons.Count == 0)
                    throw new InvalidOperationException("The first ring of a polygon must be an outer ring.");

                polygons.Last().Add(ring);
            }

            return polygons;
        }

        #endregion

        #region Length / Area

        public static double Length(this Polyline polyline)
        {
            if (polyline?.paths is null)
                return 0;

            return polyline.paths.SelectMany(p => p.Zip(p.Skip(1), Length)).Sum();
        }

        public static double Perimeter(this Polygon polygon)
        {
            if (polygon?.rings is null)
                return 0;

            return polygon.rings.SelectMany(r => r.Zip(r.Skip(1), Length)).Sum();
        }

        public static double Area(this Polygon polygon)
        {
            if (polygon?.rings is null)
                return 0;

            return polygon.rings.SelectMany(r => r.Zip(r.Skip(1), Area)).Sum();
        }

        #endregion

        #region Distance

        public static double? Distance(this Point point1, Point point2)
        {
            if (Null(point1, point2))
                return null;

            if (point1.Equals(point2))
                return 0;

            return Length(new[] { point1.x, point1.y }, new[] { point2.x, point2.y });
        }

        public static double? Distance(this Point point, Multipoint multipoint)
        {
            if (Null(point, multipoint, multipoint.points))
                return null;

            return multipoint.points!.Min(p => Length(new[] { point.x, point.y }, p));
        }

        public static double? Distance(this Point point, Polyline polyline)
        {
            if (Null(point, polyline, polyline.paths))
                return null;

            return point.Distance(polyline.paths!);
        }

        public static double? Distance(this Point point, Polygon polygon)
        {
            if (Null(point, polygon, polygon.rings))
                return null;

            if (polygon.Contains(point))
                return 0;

            return point.Distance(polygon.rings!);
        }

        public static double? Distance(this Multipoint multipoint, Point point)
        {
            if (Null(multipoint, point))
                return null;

            return point.Distance(multipoint);
        }

        public static double? Distance(this Multipoint multipoint1, Multipoint multipoint2)
        {
            if (Null(multipoint1, multipoint1.points, multipoint2, multipoint2.points))
                return null;

            if (multipoint1.Equals(multipoint2))
                return 0;

            return multipoint1.points!.SelectMany(p1 => multipoint2.points!.Select(p2 => Length(p1, p2))).Min();
        }

        public static double? Distance(this Multipoint multipoint, Polyline polyline)
        {
            if (Null(multipoint, multipoint.points, polyline))
                return null;

            return multipoint.points!.Min(p => new Point(p[0], p[1]).Distance(polyline));
        }

        public static double? Distance(this Multipoint multipoint, Polygon polygon)
        {
            if (Null(multipoint, multipoint.points, polygon))
                return null;

            return multipoint.points!.Min(p => new Point(p[0], p[1]).Distance(polygon));
        }

        public static double? Distance(this Polyline polyline, Point point)
        {
            if (Null(polyline, point))
                return null;

            return point.Distance(polyline);
        }

        public static double? Distance(this Polyline polyline, Multipoint multipoint)
        {
            if (Null(polyline, multipoint))
                return null;

            return multipoint.Distance(polyline);
        }

        public static double? Distance(this Polyline polyline1, Polyline polyline2)
        {
            if (Null(polyline1, polyline1.paths, polyline2, polyline2.paths))
                return null;

            if (polyline1.Equals(polyline2))
                return 0;

            if (polyline1.Intersects(polyline2))
                return 0;

            return Math.Min(polyline1.paths!.Distance(polyline2.paths!)!.Value, polyline2.paths!.Distance(polyline1.paths!)!.Value);
        }

        public static double? Distance(this Polyline polyline, Polygon polygon)
        {
            if (Null(polyline, polyline.paths, polygon, polygon.rings))
                return null;

            if (polyline.Intersects(polygon))
                return 0;

            if (polyline.Within(polygon))
                return 0;

            return Math.Min(polyline.paths!.Distance(polygon.rings!)!.Value, polygon.rings!.Distance(polyline.paths!)!.Value);
        }

        public static double? Distance(this Polygon polygon, Point point)
        {
            if (Null(polygon, point))
                return null;

            return point.Distance(polygon);
        }

        public static double? Distance(this Polygon polygon, Multipoint multipoint)
        {
            if (Null(polygon, multipoint))
                return null;

            return multipoint.Distance(polygon);
        }

        public static double? Distance(this Polygon polygon, Polyline polyline)
        {
            if (Null(polygon, polyline))
                return null;

            return polyline.Distance(polygon);
        }

        public static double? Distance(this Polygon polygon1, Polygon polygon2)
        {
            if (Null(polygon1, polygon1.rings, polygon2, polygon2.rings))
                return null;

            if (polygon1.Equals(polygon2))
                return 0;

            if (polygon1.Intersects(polygon2))
                return 0;

            if (polygon1.Within(polygon2))
                return 0;

            if (polygon2.Within(polygon1))
                return 0;

            return Math.Min(polygon1.rings!.Distance(polygon2.rings!)!.Value, polygon2.rings!.Distance(polygon1.rings!)!.Value);
        }

        public static double? Distance(this Feature<Point> feature1, Feature<Point> feature2)
        {
            return feature1.Geometry!.Distance(feature2.Geometry!);
        }

        public static double? Distance(this Feature<Point> feature1, Feature<Multipoint> feature2)
        {
            return feature1.Geometry!.Distance(feature2.Geometry!);
        }

        public static double? Distance(this Feature<Point> feature1, Feature<Polyline> feature2)
        {
            return feature1.Geometry!.Distance(feature2.Geometry!);
        }

        public static double? Distance(this Feature<Point> feature1, Feature<Polygon> feature2)
        {
            return feature1.Geometry!.Distance(feature2.Geometry!);
        }

        public static double? Distance(this Feature<Multipoint> feature1, Feature<Point> feature2)
        {
            return feature1.Geometry!.Distance(feature2.Geometry!);
        }

        public static double? Distance(this Feature<Multipoint> feature1, Feature<Multipoint> feature2)
        {
            return feature1.Geometry!.Distance(feature2.Geometry!);
        }

        public static double? Distance(this Feature<Multipoint> feature1, Feature<Polyline> feature2)
        {
            return feature1.Geometry!.Distance(feature2.Geometry!);
        }

        public static double? Distance(this Feature<Multipoint> feature1, Feature<Polygon> feature2)
        {
            return feature1.Geometry!.Distance(feature2.Geometry!);
        }

        public static double? Distance(this Feature<Polyline> feature1, Feature<Point> feature2)
        {
            return feature1.Geometry!.Distance(feature2.Geometry!);
        }

        public static double? Distance(this Feature<Polyline> feature1, Feature<Multipoint> feature2)
        {
            return feature1.Geometry!.Distance(feature2.Geometry!);
        }

        public static double? Distance(this Feature<Polyline> feature1, Feature<Polyline> feature2)
        {
            return feature1.Geometry!.Distance(feature2.Geometry!);
        }

        public static double? Distance(this Feature<Polyline> feature1, Feature<Polygon> feature2)
        {
            return feature1.Geometry!.Distance(feature2.Geometry!);
        }

        public static double? Distance(this Feature<Polygon> feature1, Feature<Point> feature2)
        {
            return feature1.Geometry!.Distance(feature2.Geometry!);
        }

        public static double? Distance(this Feature<Polygon> feature1, Feature<Multipoint> feature2)
        {
            return feature1.Geometry!.Distance(feature2.Geometry!);
        }

        public static double? Distance(this Feature<Polygon> feature1, Feature<Polyline> feature2)
        {
            return feature1.Geometry!.Distance(feature2.Geometry!);
        }

        public static double? Distance(this Feature<Polygon> feature1, Feature<Polygon> feature2)
        {
            return feature1.Geometry!.Distance(feature2.Geometry!);
        }

        #endregion

        #region Extent

        public static Envelope Extent(this Point point)
        {
            AssertNotNull(point);

            var extent = new Envelope(point.x, point.y, point.x, point.y)
            {
                spatialReference = point.spatialReference
            };

            return extent;
        }

        public static Envelope Extent(this Multipoint multipoint)
        {
            AssertNotNull(multipoint, multipoint.points);

            var extent = multipoint.points!.Extent();
            extent.spatialReference = multipoint.spatialReference;

            return extent;
        }

        public static Envelope Extent(this Polyline polyline)
        {
            AssertNotNull(polyline, polyline.paths);

            var extent = polyline.paths!.Extent();
            extent.spatialReference = polyline.spatialReference;

            return extent;
        }

        public static Envelope Extent(this Polygon polygon)
        {
            AssertNotNull(polygon, polygon.rings);

            var extent = polygon.rings!.Extent();
            extent.spatialReference = polygon.spatialReference;

            return extent;
        }

        public static Envelope Buffer(this Envelope extent, double distance)
        {
            AssertNotNull(extent);

            var xmin = extent.xmin - distance;
            var ymin = extent.ymin - distance;
            var xmax = extent.xmax + distance;
            var ymax = extent.ymax + distance;

            if (xmin > xmax)
                xmin = xmax = (xmin + xmax) / 2;

            if (ymin > ymax)
                ymin = ymax = (ymin + ymax) / 2;

            return new Envelope(xmin, ymin, xmax, ymax)
            {
                spatialReference = extent.spatialReference
            };
        }

        public static Point Centre(this Envelope extent)
        {
            AssertNotNull(extent);

            return new Point
            {
                x = (extent.xmin + extent.xmax) / 2,
                y = (extent.ymin + extent.ymax) / 2,
                spatialReference = extent.spatialReference
            };
        }

        #endregion

        #region Intersect / Intersects

        public static Point[] Intersect(this Polyline polyline1, Polyline polyline2)
        {
            if (Null(polyline1, polyline1.paths, polyline2, polyline2.paths))
                return Array.Empty<Point>();

            return !polyline1.Extent().Intersects(polyline2.Extent())
                ? Array.Empty<Point>()
                : Intersect(polyline1.paths!, polyline2.paths!);
        }

        public static bool Intersects(this Polyline polyline1, Polyline polyline2)
        {
            return polyline1.Intersect(polyline2).Any();
        }

        public static bool Intersects(this Polyline polyline, Polygon polygon)
        {
            return polyline.Intersects(new Polyline { paths = polygon.rings });
        }

        public static bool Intersects(this Polygon polygon, Polyline polyline)
        {
            return new Polyline { paths = polygon.rings }.Intersects(polyline);
        }

        public static bool Intersects(this Polygon polygon1, Polygon polygon2)
        {
            return new Polyline { paths = polygon1.rings }.Intersects(new Polyline { paths = polygon2.rings });
        }

        public static Point[] Intersect(this Feature<Polyline> feature1, Feature<Polyline> feature2)
        {
            return feature1.Geometry!.Intersect(feature2.Geometry!);
        }

        public static bool Intersects(this Feature<Polyline> feature1, Feature<Polyline> feature2)
        {
            return feature1.Geometry!.Intersects(feature2.Geometry!);
        }

        public static bool Intersects(this Feature<Polyline> feature1, Feature<Polygon> feature2)
        {
            return feature1.Geometry!.Intersects(feature2.Geometry!);
        }

        public static bool Intersects(this Feature<Polygon> feature1, Feature<Polyline> feature2)
        {
            return feature1.Geometry!.Intersects(feature2.Geometry!);
        }

        public static bool Intersects(this Feature<Polygon> feature1, Feature<Polygon> feature2)
        {
            return feature1.Geometry!.Intersects(feature2.Geometry!);
        }

        #endregion

        #region Contains / Within

        public static bool Contains(this Polygon polygon, Point point)
        {
            if (Null(polygon, polygon.rings, point))
                return false;

            return polygon.rings!.Where(r => Contains(r, point)).Sum(r => r.IsClockwise() ? -1 : 1) > 0;
        }

        public static bool Contains(this Polygon polygon, Multipoint multipoint)
        {
            if (Null(polygon, multipoint, multipoint.points))
                return false;

            return polygon.Contains(multipoint.points!);
        }

        public static bool Contains(this Polygon polygon, Polyline polyline)
        {
            if (Null(polygon, polyline, polyline.paths))
                return false;

            return !polygon.Intersects(polyline) && polyline.paths!.All(polygon.Contains);
        }

        public static bool Contains(this Polygon polygon1, Polygon polygon2)
        {
            if (Null(polygon1, polygon2, polygon2.rings))
                return false;

            return !polygon1.Intersects(polygon2) && polygon2.rings!.All(polygon1.Contains);
        }

        public static bool Contains(this Feature<Polygon> feature1, Feature<Point> feature2)
        {
            if (Null(feature1.Geometry, feature2.Geometry))
                return false;

            return feature1.Geometry!.Contains(feature2.Geometry!);
        }

        public static bool Contains(this Feature<Polygon> feature1, Feature<Multipoint> feature2)
        {
            if (Null(feature1.Geometry, feature2.Geometry))
                return false;

            return feature1.Geometry!.Contains(feature2.Geometry!);
        }

        public static bool Contains(this Feature<Polygon> feature1, Feature<Polyline> feature2)
        {
            if (Null(feature1.Geometry, feature2.Geometry))
                return false;

            return feature1.Geometry!.Contains(feature2.Geometry!);
        }

        public static bool Contains(this Feature<Polygon> feature1, Feature<Polygon> feature2)
        {
            if (Null(feature1.Geometry, feature2.Geometry))
                return false;

            return feature1.Geometry!.Contains(feature2.Geometry!);
        }

        public static bool Within(this Point point, Polygon polygon)
        {
            if (Null(point, polygon))
                return false;

            return polygon.Contains(point);
        }

        public static bool Within(this Multipoint multipoint, Polygon polygon)
        {
            if (Null(multipoint, polygon))
                return false;

            return polygon.Contains(multipoint);
        }

        public static bool Within(this Polyline polyline, Polygon polygon)
        {
            if (Null(polyline, polygon))
                return false;

            return polygon.Contains(polyline);
        }

        public static bool Within(this Polygon polygon1, Polygon polygon2)
        {
            if (Null(polygon1, polygon2))
                return false;

            return polygon2.Contains(polygon1);
        }

        public static bool Within(this Feature<Point> feature1, Feature<Polygon> feature2)
        {
            if (Null(feature1.Geometry, feature2.Geometry))
                return false;

            return feature1.Geometry!.Within(feature2.Geometry!);
        }

        public static bool Within(this Feature<Multipoint> feature1, Feature<Polygon> feature2)
        {
            if (Null(feature1.Geometry, feature2.Geometry))
                return false;

            return feature1.Geometry!.Within(feature2.Geometry!);
        }

        public static bool Within(this Feature<Polyline> feature1, Feature<Polygon> feature2)
        {
            if (Null(feature1.Geometry, feature2.Geometry))
                return false;

            return feature1.Geometry!.Within(feature2.Geometry!);
        }

        public static bool Within(this Feature<Polygon> feature1, Feature<Polygon> feature2)
        {
            if (Null(feature1.Geometry, feature2.Geometry))
                return false;

            return feature1.Geometry!.Within(feature2.Geometry!);
        }

        #endregion

        #region WithinDistance

        public static bool WithinDistance(this Point point1, Point point2, double distance)
        {
            if (Null(point1, point2))
                return false;

            return point1.Distance(point2) < distance;
        }

        public static bool WithinDistance(this Point point, Multipoint multipoint, double distance)
        {
            if (Null(point, multipoint))
                return false;

            return point.Extent().Buffer(distance).Intersects(multipoint.Extent()) && point.Distance(multipoint) < distance;
        }

        public static bool WithinDistance(this Point point, Polyline polyline, double distance)
        {
            if (Null(point, polyline))
                return false;

            return point.Extent().Buffer(distance).Intersects(polyline.Extent()) && point.Distance(polyline) < distance;
        }

        public static bool WithinDistance(this Point point, Polygon polygon, double distance)
        {
            if (Null(point, polygon))
                return false;

            return point.Extent().Buffer(distance).Intersects(polygon.Extent()) && point.Distance(polygon) < distance;
        }

        public static bool WithinDistance(this Multipoint multipoint, Point point, double distance)
        {
            if (Null(multipoint, point))
                return false;

            return point.Distance(multipoint) < distance;
        }

        public static bool WithinDistance(this Multipoint multipoint1, Multipoint multipoint2, double distance)
        {
            if (Null(multipoint1, multipoint2))
                return false;

            return multipoint1.Extent().Buffer(distance).Intersects(multipoint2.Extent()) && multipoint1.Distance(multipoint2) < distance;
        }

        public static bool WithinDistance(this Multipoint multipoint, Polyline polyline, double distance)
        {
            if (Null(multipoint, polyline))
                return false;

            return multipoint.Extent().Buffer(distance).Intersects(polyline.Extent()) && multipoint.Distance(polyline) < distance;
        }

        public static bool WithinDistance(this Multipoint multipoint, Polygon polygon, double distance)
        {
            if (Null(multipoint, polygon))
                return false;

            return multipoint.Extent().Buffer(distance).Intersects(polygon.Extent()) && multipoint.Distance(polygon) < distance;
        }

        public static bool WithinDistance(this Polyline polyline, Point point, double distance)
        {
            if (Null(polyline, point))
                return false;

            return point.Distance(polyline) < distance;
        }

        public static bool WithinDistance(this Polyline polyline, Multipoint multipoint, double distance)
        {
            if (Null(polyline, multipoint))
                return false;

            return multipoint.Distance(polyline) < distance;
        }

        public static bool WithinDistance(this Polyline polyline1, Polyline polyline2, double distance)
        {
            if (Null(polyline1, polyline2))
                return false;

            return polyline1.Extent().Buffer(distance).Intersects(polyline2.Extent()) && polyline1.Distance(polyline2) < distance;
        }

        public static bool WithinDistance(this Polyline polyline, Polygon polygon, double distance)
        {
            if (Null(polyline, polygon))
                return false;

            return polyline.Extent().Buffer(distance).Intersects(polygon.Extent()) && polyline.Distance(polygon) < distance;
        }

        public static bool WithinDistance(this Polygon polygon, Point point, double distance)
        {
            if (Null(polygon, point))
                return false;

            return point.Distance(polygon) < distance;
        }

        public static bool WithinDistance(this Polygon polygon, Multipoint multipoint, double distance)
        {
            if (Null(polygon, multipoint))
                return false;

            return multipoint.Distance(polygon) < distance;
        }

        public static bool WithinDistance(this Polygon polygon, Polyline polyline, double distance)
        {
            if (Null(polygon, polyline))
                return false;

            return polyline.Distance(polygon) < distance;
        }

        public static bool WithinDistance(this Polygon polygon1, Polygon polygon2, double distance)
        {
            if (Null(polygon1, polygon2))
                return false;

            return polygon1.Extent().Buffer(distance).Intersects(polygon2.Extent()) && polygon1.Distance(polygon2) < distance;
        }

        public static bool WithinDistance(this Feature<Point> feature1, Feature<Point> feature2, double distance)
        {
            if (Null(feature1.Geometry, feature2.Geometry))
                return false;

            return feature1.Geometry!.WithinDistance(feature2.Geometry!, distance);
        }

        public static bool WithinDistance(this Feature<Point> feature1, Feature<Multipoint> feature2, double distance)
        {
            if (Null(feature1.Geometry, feature2.Geometry))
                return false;

            return feature1.Geometry!.WithinDistance(feature2.Geometry!, distance);
        }

        public static bool WithinDistance(this Feature<Point> feature1, Feature<Polyline> feature2, double distance)
        {
            if (Null(feature1.Geometry, feature2.Geometry))
                return false;

            return feature1.Geometry!.WithinDistance(feature2.Geometry!, distance);
        }

        public static bool WithinDistance(this Feature<Point> feature1, Feature<Polygon> feature2, double distance)
        {
            if (Null(feature1.Geometry, feature2.Geometry))
                return false;

            return feature1.Geometry!.WithinDistance(feature2.Geometry!, distance);
        }

        public static bool WithinDistance(this Feature<Multipoint> feature1, Feature<Point> feature2, double distance)
        {
            if (Null(feature1.Geometry, feature2.Geometry))
                return false;

            return feature1.Geometry!.WithinDistance(feature2.Geometry!, distance);
        }

        public static bool WithinDistance(this Feature<Multipoint> feature1, Feature<Multipoint> feature2, double distance)
        {
            if (Null(feature1.Geometry, feature2.Geometry))
                return false;

            return feature1.Geometry!.WithinDistance(feature2.Geometry!, distance);
        }

        public static bool WithinDistance(this Feature<Multipoint> feature1, Feature<Polyline> feature2, double distance)
        {
            if (Null(feature1.Geometry, feature2.Geometry))
                return false;

            return feature1.Geometry!.WithinDistance(feature2.Geometry!, distance);
        }

        public static bool WithinDistance(this Feature<Multipoint> feature1, Feature<Polygon> feature2, double distance)
        {
            if (Null(feature1.Geometry, feature2.Geometry))
                return false;

            return feature1.Geometry!.WithinDistance(feature2.Geometry!, distance);
        }

        public static bool WithinDistance(this Feature<Polyline> feature1, Feature<Point> feature2, double distance)
        {
            if (Null(feature1.Geometry, feature2.Geometry))
                return false;

            return feature1.Geometry!.WithinDistance(feature2.Geometry!, distance);
        }

        public static bool WithinDistance(this Feature<Polyline> feature1, Feature<Multipoint> feature2, double distance)
        {
            if (Null(feature1.Geometry, feature2.Geometry))
                return false;

            return feature1.Geometry!.WithinDistance(feature2.Geometry!, distance);
        }

        public static bool WithinDistance(this Feature<Polyline> feature1, Feature<Polyline> feature2, double distance)
        {
            if (Null(feature1.Geometry, feature2.Geometry))
                return false;

            return feature1.Geometry!.WithinDistance(feature2.Geometry!, distance);
        }

        public static bool WithinDistance(this Feature<Polyline> feature1, Feature<Polygon> feature2, double distance)
        {
            if (Null(feature1.Geometry, feature2.Geometry))
                return false;

            return feature1.Geometry!.WithinDistance(feature2.Geometry!, distance);
        }

        public static bool WithinDistance(this Feature<Polygon> feature1, Feature<Point> feature2, double distance)
        {
            if (Null(feature1.Geometry, feature2.Geometry))
                return false;

            return feature1.Geometry!.WithinDistance(feature2.Geometry!, distance);
        }

        public static bool WithinDistance(this Feature<Polygon> feature1, Feature<Multipoint> feature2, double distance)
        {
            if (Null(feature1.Geometry, feature2.Geometry))
                return false;

            return feature1.Geometry!.WithinDistance(feature2.Geometry!, distance);
        }

        public static bool WithinDistance(this Feature<Polygon> feature1, Feature<Polyline> feature2, double distance)
        {
            if (Null(feature1.Geometry, feature2.Geometry))
                return false;

            return feature1.Geometry!.WithinDistance(feature2.Geometry!, distance);
        }

        public static bool WithinDistance(this Feature<Polygon> feature1, Feature<Polygon> feature2, double distance)
        {
            if (Null(feature1.Geometry, feature2.Geometry))
                return false;

            return feature1.Geometry!.WithinDistance(feature2.Geometry!, distance);
        }

        #endregion
    }
}
