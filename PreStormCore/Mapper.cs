using System.Text.Json;

using static PreStormCore.FieldType;
using static PreStormCore.GeometryType;

namespace PreStormCore;

internal static class Mapper
{
    public static T ToFeature<T>(this Graphic graphic, LayerInfo layer) where T : Feature
    {
        var feature = Proxy.Create<T>();
        var element = graphic.attributes[layer.GetObjectIdFieldName()];
        feature.OID = element.GetInt32();

        var mappings = typeof(T).GetMappings();

        foreach (var m in mappings)
        {
            if (!graphic.attributes.ContainsKey(m.FieldName))
                throw new MissingFieldException($"Field '{m.FieldName}' does not exist in '{layer.name}'.");

            var value = graphic.attributes[m.FieldName];

            if (value.ValueKind is JsonValueKind.Null)
                continue;

            var t = m.Property!.PropertyType.GetUnderlyingType();

            object? obj = null;

            if (t == typeof(DateTime))
            {
                obj = Esri.BaseTime.AddMilliseconds(value.GetDouble());
            }
            else if (t == typeof(Guid))
            {
                obj = Guid.Parse(value.GetString()!);
            }
            else if (t == typeof(string))
            {
                obj = value.GetString();
            }
            else
            {
                var domain = t.Domains().FirstOrDefault(x => x.domain.Code.ToString() == value.ToString());

                if (domain.field is not null)
                {
                    obj = Enum.Parse(t, domain.field);
                }
                else
                {
                    try
                    {
                        obj = Convert.ChangeType(value.GetDouble(), t);
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException($"'{typeof(T)}.{m.Property.Name}' is not defined with the correct type.  Error trying to convert '{value}' to {t}.", ex);
                    }
                }
            }

            m.Property.SetValue(feature, obj, null);
        }

        foreach (var a in graphic.attributes)
            if (a.Key != layer.GetObjectIdFieldName() && mappings.All(m => m.FieldName != a.Key))
                feature.UnmappedFields.Add(a.Key, a.Value.ToDotNetValue(layer.fields.FirstOrDefault(f => f.name == a.Key)?.type));

        var g = graphic.geometry;

        if (g is not null)
        {
            dynamic f = feature;

            if (g.ActualX.HasValue && g.ActualY.HasValue)
                f.Geometry = new Point { x = g.ActualX.Value, y = g.ActualY.Value, z = g.ActualZ };

            else if (g.points?.Length > 0)
                f.Geometry = new Multipoint { points = g.points };

            else if (g.paths?.Length > 0 || g.curvePaths is not null)
                f.Geometry = new Polyline { paths = g.paths, curvePaths = g.curvePaths };

            else if (g.rings?.Length > 0 || g.curveRings is not null)
                f.Geometry = new Polygon { rings = g.rings, curveRings = g.curveRings };
        }

        feature.IsDirty = false;

        return feature;
    }

    public static object? ToGraphic(this Feature feature, LayerInfo layer, bool changesOnly)
    {
        if (changesOnly && feature.ChangedFields.Count == 0 && !feature.GeometryChanged)
            return null;

        var t = feature.GetType();

        var attributes = new Dictionary<string, object?>();

        if (changesOnly)
            attributes.Add(layer.GetObjectIdFieldName(), feature.OID);

        var mappings = t.GetMappings().ToList();

        foreach (var m in mappings)
        {
            if (changesOnly && !feature.ChangedFields.Contains(m.FieldName))
                continue;

            if (m.Property!.GetSetMethod() is null)
                continue;

            attributes.Add(m.FieldName, m.Property.GetValue(feature, null)?.ToEsriValue());
        }

        foreach (var a in feature.UnmappedFields)
            if (!changesOnly || feature.ChangedFields.Contains(a.Key))
                attributes.Add(a.Key, a.Value.ToEsriValue());

        return (!changesOnly || feature.GeometryChanged) && t.HasGeometry()
            ? new { attributes, geometry = GetGeometry(feature, layer) }
            : new { attributes } as object;
    }

    private static object? ToEsriValue(this object? value)
    {
        return value switch
        {
            DateTime time => Convert.ToInt64(time.ToUniversalTime().Subtract(Esri.BaseTime).TotalMilliseconds),
            Guid guid => guid.ToString("B").ToUpper(),
            Enum @enum => @enum.Domain()?.Code,
            _ => value
        };
    }

    private static object? ToDotNetValue(this JsonElement value, FieldType? type)
    {
        if (value.ValueKind is JsonValueKind.Null)
            return null;

        return type switch
        {
            esriFieldTypeString => value.GetString(),
            esriFieldTypeDate => Esri.BaseTime.AddMilliseconds(value.GetInt64()),
            esriFieldTypeGlobalID or esriFieldTypeGUID => Guid.Parse(value.GetString()!),
            esriFieldTypeInteger => value.GetInt32(),
            esriFieldTypeSmallInteger => value.GetInt16(),
            esriFieldTypeDouble => value.GetDouble(),
            esriFieldTypeSingle => value.GetSingle(),
            _ => value.GetString()
        };
    }

    private static object? GetGeometry(Feature feature, LayerInfo layer)
    {
        var geometry = ((dynamic)feature).Geometry;

        if (geometry is not null)
            return geometry;

        return layer.geometryType switch
        {
            esriGeometryPoint => layer.hasZ ? new { x = "NaN", y = "NaN", z = "NaN" } : new { x = "NaN", y = "NaN" },
            esriGeometryMultipoint => new { points = Array.Empty<double[]>() },
            esriGeometryPolyline => new { paths = Array.Empty<double[][]>() },
            esriGeometryPolygon => new { rings = Array.Empty<double[][]>() },
            _ => null
        };
    }
}
