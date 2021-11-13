using System.ComponentModel;
using System.Reflection;

namespace PreStormCore;

internal static class TypeExt
{
    private static readonly Func<Type, bool> HasGeometryMemoized = Memoization.Memoize<Type, bool>(t =>
        typeof(Feature<Point>).IsAssignableFrom(t) ||
        typeof(Feature<Multipoint>).IsAssignableFrom(t) ||
        typeof(Feature<Polyline>).IsAssignableFrom(t) ||
        typeof(Feature<Polygon>).IsAssignableFrom(t) ||
        typeof(Feature<Geometry>).IsAssignableFrom(t));

    public static bool HasGeometry(this Type type)
    {
        return HasGeometryMemoized(type);
    }

    private static readonly Func<Type, Mapped[]> GetMappingsMemoized = Memoization.Memoize<Type, Mapped[]>(t => t.GetProperties()
        .SelectMany(p =>
        {
            var mapped = p.GetCustomAttribute<Mapped>();

            if (mapped is null)
                return Enumerable.Empty<Mapped>();

            mapped.Property = p;

            return new[] { mapped };
        })
        .ToArray()!);

    public static Mapped[] GetMappings(this Type type)
    {
        return GetMappingsMemoized(type);
    }

    public static Type GetUnderlyingType(this Type type)
    {
        if (type.GetTypeInfo().IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
            return new NullableConverter(type).UnderlyingType;

        return type;
    }
}
