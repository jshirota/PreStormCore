using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PreStormCore
{
    public static class Json
    {
        private static readonly JsonSerializerOptions options = new JsonSerializerOptions();

        static Json()
        {
            options.IgnoreNullValues = true;
            options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        }

        internal static T? Deserialize<T>(this string json)
        {
            return JsonSerializer.Deserialize<T>(json, options);
        }

        internal static string Serialize(this object obj)
        {
            return JsonSerializer.Serialize(obj, options);
        }

        public static string? ToJson(this GeometryBase geometry)
        {
            return geometry?.Serialize();
        }

        public static Geometry? ToGeometry(string json)
        {
            if (json is null)
                return null;

            if (json.Contains("x") && json.Contains("y"))
                return Point.FromJson(json);

            if (json.Contains("points"))
                return Multipoint.FromJson(json);

            if (json.Contains("paths"))
                return Polyline.FromJson(json);

            if (json.Contains("rings"))
                return Polygon.FromJson(json);

            throw new ArgumentException("This geometry type is not supported.", nameof(json));
        }
    }
}
