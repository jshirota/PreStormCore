namespace PreStormCore
{
    internal class Vector
    {
        public double X { get; }
        public double Y { get; }

        public Vector(double x, double y)
            => (X, Y) = (x, y);

        public static implicit operator Vector(Point point)
            => new(point.x, point.y);

        public static implicit operator Point(Vector vector)
            => new() { x = vector.X, y = vector.Y };

        public static Vector operator +(Vector v1, Vector v2)
            => new(v1.X + v2.X, v1.Y + v2.Y);

        public static Vector operator -(Vector v1, Vector v2)
            => new(v1.X - v2.X, v1.Y - v2.Y);

        public static Vector operator *(Vector v, double n)
            => new(v.X * n, v.Y * n);

        public static Vector operator *(double n, Vector v)
            => v * n;

        public static Vector operator /(Vector v, double n)
            => new(v.X / n, v.Y / n);

        public static double CrossProduct(Vector v1, Vector v2)
            => v1.X * v2.Y - v1.Y * v2.X;

        public static double DotProduct(Vector v1, Vector v2)
            => v1.X * v2.X + v1.Y * v2.Y;
    }
}
