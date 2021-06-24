using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PreStormCore
{
    internal static class Esri
    {
        public static readonly DateTime BaseTime = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        private static async Task<T> GetResponse<T>(string url, string? data, string? token = null) where T : IResponse
        {
            var parameters = new Dictionary<string, object?> { { "token", token }, { "f", "json" } };
            var queryString = string.Join("&", parameters.Where(p => p.Value is not null).Select(p => $"{p.Key}={WebUtility.UrlEncode(p.Value!.ToString())}"));

            try
            {
                var response = data is null
                    ? await Http.Get<T>(url + "?" + queryString)
                    : await Http.Post<T>(url, data + "&" + queryString);

                if (response.error?.message is not null)
                    throw new InvalidOperationException(response.error?.message);

                if (response is EditResultSet resultSet && new[] { resultSet.addResults, resultSet.updateResults, resultSet.deleteResults }.Any(results => results?.Any(r => !r.success) == true))
                    throw new InvalidOperationException("ArcGIS Server returned an error response.");

                return response;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"An error occurred while processing a request against '{url}'.", ex);
            }
        }

        private static string CleanWhereClause(string? whereClause)
        {
            return WebUtility.UrlEncode(string.IsNullOrWhiteSpace(whereClause) ? "1=1" : whereClause);
        }

        private static string CleanExtraParameters(string? extraParameters)
        {
            return string.IsNullOrWhiteSpace(extraParameters) ? "" : ("&" + extraParameters);
        }

        private static string CleanObjectIds(IEnumerable<int>? objectIds)
        {
            return objectIds is null ? "" : WebUtility.UrlEncode(string.Join(",", objectIds));
        }

        public static async Task<LayerInfo> GetLayer(string url, string? token)
        {
            return await GetResponse<LayerInfo>(url, null, token);
        }

        public static async Task<FeatureSet> GetFeatureSet(string url, string? token, bool returnGeometry, bool returnZ, string? whereClause, string? extraParameters, IEnumerable<int>? objectIds)
        {
            var data = $"where={CleanWhereClause(whereClause)}{CleanExtraParameters(extraParameters)}&objectIds={CleanObjectIds(objectIds)}&returnGeometry={(returnGeometry ? "true" : "false")}&returnZ={(returnZ ? "true" : "false")}&outFields=*";
            return await GetResponse<FeatureSet>($"{url}/query", data, token);
        }

        public static async Task<OIDSet> GetOIDSet(string url, string? token, string? whereClause, string? extraParameters)
        {
            var data = $"where={CleanWhereClause(whereClause)}{CleanExtraParameters(extraParameters)}&returnIdsOnly=true";
            return await GetResponse<OIDSet>($"{url}/query", data, token);
        }

        public static bool IsArcGISOnline(string url)
        {
            return Regex.IsMatch(url, @"\.arcgis\.com/", RegexOptions.IgnoreCase);
        }

        public static async Task<TokenInfo> GetTokenInfo(string tokenUrl, string username, string password, int expiration)
        {
            var data = $"username={WebUtility.UrlEncode(username)}&password={WebUtility.UrlEncode(password)}&clientid=requestip&expiration={expiration}";
            return await GetResponse<TokenInfo>(tokenUrl, data);
        }

        public static async Task<EditResultSet> ApplyEdits(string url, string? token, object? adds = null, object? updates = null, object? deletes = null)
        {
            var ops = new[] { (op: "adds", data: RemoveNullZ(adds)), (op: "updates", data: RemoveNullZ(updates)), (op: "deletes", data: deletes) }
                .Where(x => x.data is not null);

            var data = string.Join("&", ops.Select(x => $"{x.op}={WebUtility.UrlEncode(x.data!.Serialize())}"));
            return await GetResponse<EditResultSet>($"{url}/applyEdits", data, token);
        }

        public static async Task<EditResultSet> Delete(string url, string? token, string whereClause)
        {
            var data = $"where={WebUtility.UrlEncode(whereClause)}";
            return await GetResponse<EditResultSet>($"{url}/deleteFeatures", data, token);
        }

        private static object? RemoveNullZ(object? obj)
        {
            if (obj is not object[] array)
                return obj;

            foreach (var d in array.OfType<Dictionary<string, object>>())
            {
                if (d.ContainsKey("geometry"))
                {
                    if (d["geometry"] is Dictionary<string, object> g && g.ContainsKey("z") && g["z"] is null && g["x"] is not null && g["y"] is not null)
                        g.Remove("z");
                }
            }

            return array;
        }

        public static string GetObjectIdFieldName(this LayerInfo layer)
        {
            var objectIdField = layer.fields.FirstOrDefault(f => f.type == FieldType.esriFieldTypeOID)
                ?? layer.fields.FirstOrDefault(f => f.name.Equals("OBJECTID", StringComparison.OrdinalIgnoreCase));

            if (objectIdField is null)
                throw new InvalidOperationException($"'{layer.name}' does not have any field of type esriFieldTypeOID (or a field named 'OBJECTID').");

            return objectIdField.name;
        }
    }

    public enum FieldType
    {
        esriFieldTypeSmallInteger = 0,
        esriFieldTypeInteger = 1,
        esriFieldTypeSingle = 2,
        esriFieldTypeDouble = 3,
        esriFieldTypeString = 4,
        esriFieldTypeDate = 5,
        esriFieldTypeOID = 6,
        esriFieldTypeGeometry = 7,
        esriFieldTypeBlob = 8,
        esriFieldTypeRaster = 9,
        esriFieldTypeGUID = 10,
        esriFieldTypeGlobalID = 11,
        esriFieldTypeXML = 12
    }

    public enum GeometryType
    {
        esriGeometryPoint = 1,
        esriGeometryMultipoint = 2,
        esriGeometryPolyline = 3,
        esriGeometryPolygon = 4,
        esriGeometryEnvelope = 5
    }

    public enum SpatialRel
    {
        Intersects = 1,
        EnvelopeIntersects = 2,
        IndexIntersects = 3,
        Touches = 4,
        Overlaps = 5,
        Crosses = 6,
        Within = 7,
        Contains = 8,
        Relation = 9
    }

    public record LayerInfo(int id, string name, string type, GeometryType? geometryType, Field[] fields, bool hasZ, int? maxRecordCount, Error? error) : IResponse;
    public record Field(string name, string? alias, FieldType? type, bool? nullable, bool? editable, int? length);
    public record Error(string? message);
    public record Result(int? objectId, bool success);
    public record EditResultSet(Result[] addResults, Result[] updateResults, Result[] deleteResults, Error? error) : IResponse;

    internal interface IResponse { Error? error { get; } }
    internal record TokenInfo(string token, long expires, Error? error) : IResponse;
    internal record OIDSet(int[] objectIds, Error? error) : IResponse;
    internal record FeatureSet(Graphic[] features, Error? error) : IResponse;
    internal record Graphic(Dictionary<string, JsonElement> attributes, CatchAllGeometry geometry);

    internal class CatchAllGeometry
    {
        public JsonElement x { get; set; }
        public double? ActualX => x.ValueKind == JsonValueKind.Number ? x.GetDouble() : null;
        public JsonElement y { get; set; }
        public double? ActualY => y.ValueKind == JsonValueKind.Number ? y.GetDouble() : null;
        public JsonElement z { get; set; }
        public double? ActualZ => z.ValueKind == JsonValueKind.Number ? z.GetDouble() : null;
        public double[][]? points { get; set; }
        public double[][][]? paths { get; set; }
        public object[][]? curvePaths { get; set; }
        public double[][][]? rings { get; set; }
        public object[][]? curveRings { get; set; }
    }
}
