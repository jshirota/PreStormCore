using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

using static PreStormCore.Tools.FieldType;
using static PreStormCore.Tools.GeometryType;

namespace PreStormCore.Tools
{
    public static class Generator
    {
        public static IEnumerable<(string name, string code)> Generate(string url, string token, string tokenUrl, string user, string password, string @namespace, string domain)
        {
            var name = Regex.Match(url, @"(?<=(https?://.*?/))(\w|\-)+(?=(/(MapServer|FeatureServer)))", RegexOptions.IgnoreCase).Value;

            if (name == string.Empty)
                throw new InvalidOperationException($"{url} is not a valid map service url.");

            if (string.IsNullOrEmpty(@namespace))
                @namespace = name.ToSafeName(false, true, null);

            var classes = new List<string>();
            var properties = new List<string>();

            using var http = new HttpClient();

            if (string.IsNullOrEmpty(token) && !string.IsNullOrEmpty(user) && !string.IsNullOrEmpty(password))
            {
                if (string.IsNullOrEmpty(tokenUrl))
                    tokenUrl = "https://www.arcgis.com/sharing/generateToken";

                var data = $"username={WebUtility.UrlEncode(user)}&password={WebUtility.UrlEncode(password)}&clientid=requestip&expiration=60";

                token = http.PostAsync(tokenUrl, new StringContent(data, Encoding.UTF8, "application/x-www-form-urlencoded")).Result.Content.ReadAsStringAsync().Result;
            }

            var json = http.GetAsync($"{url}?f=json{(string.IsNullOrEmpty(token) ? null : $"&token={token}")}").Result.Content.ReadAsStringAsync().Result;
            var serviceInfo = JsonConvert.DeserializeObject<ServiceInfo>(json);

            if (serviceInfo.error != null)
                throw new InvalidOperationException($"ArcGIS responded with an error message while processsing the request against {url}.  {serviceInfo.error.message}");

            var layers = serviceInfo.layers.Concat(serviceInfo.tables)
                .AsParallel()
                .AsOrdered()
                .Select(x => http.GetAsync($"{url}/{x.id}?f=json{(token is null ? null : $"&token={token}")}").Result.Content.ReadAsStringAsync().Result)
                .Select(JsonConvert.DeserializeObject<Layer>)
                .Where(x => x.type == "Feature Layer" || x.type == "Table")
                .Where(x => x.SupportsQuery)
                .ToArray();

            var types = new List<(string @class, string property, int id)>();

            var useDomain = domain == "code" || domain == "name";

            foreach (var layer in layers.Where(x => x.fields != null))
            {
                var fullname = $"{layer.parentLayer?.name}{layer.name}";

                var @class = fullname.ToSafeName(true, true, classes);
                classes.Add(@class);

                var property = fullname.ToSafeName(false, true, properties);
                properties.Add(property);

                types.Add((@class, property, layer.id));

                yield return ($"{@namespace}.{@class}", layer.GetClass(@class, $"{url}/{layer.id}", @namespace, useDomain));
            }

            if (useDomain)
            {
                var domains = layers
                    .Where(x => x.fields != null)
                    .SelectMany(x => x.fields)
                    .Select(x => (field: x, x.domain))
                    .Where(x => x.domain?.type == "codedValue")
                    .GroupBy(x => x.domain.name)
                    .Select(x => x.First())
                    .Select(x => (x.field, x.domain, name: x.domain.name.ToSafeName(true, true, null)))
                    .ToArray();

                foreach (var (f, d, n) in domains)
                {
                    var entries = new List<string> { n };

                    yield return ($"{@namespace}.{n}", $@"namespace {@namespace}
{{
    public enum {n}
    {{
        {string.Join(",\r\n        ", d.codedValues.Select(y => GetDomain(f, y.code, y.name, domain, entries)))}
    }}
}}
");
                }
            }

            var type = serviceInfo switch
            {
                { SupportsCreate: false, SupportsUpdate: false, SupportsDelete: false } => "ILayer",
                { SupportsCreate: false, SupportsUpdate: false, SupportsDelete: true } => "IDelete",
                { SupportsCreate: false, SupportsUpdate: true, SupportsDelete: false } => "IUpdate",
                { SupportsCreate: false, SupportsUpdate: true, SupportsDelete: true } => "IUpdateOrDelete",
                { SupportsCreate: true, SupportsUpdate: false, SupportsDelete: false } => "ICreate",
                { SupportsCreate: true, SupportsUpdate: false, SupportsDelete: true } => "ICreateOrDelete",
                { SupportsCreate: true, SupportsUpdate: true, SupportsDelete: false } => "ICreateOrUpdate",
                { SupportsCreate: true, SupportsUpdate: true, SupportsDelete: true } => "IFeatureLayer",
            };

            yield return ($"{@namespace}.Service", $@"namespace {@namespace}
{{
    public class Service
    {{
        private readonly string defaultUrl = ""{url}"";
{string.Join("\r\n", types.Select(x => $@"        public PreStormCore.{type}<{x.@class}> {x.property} {{ get; }}"))}
        public Service() : this(null, null, null, null, null) {{ }}
        public Service(string token) : this(null, null, null, token, null) {{ }}
        public Service(string userName, string password, string tokenUrl = ""https://www.arcgis.com/sharing/rest/generateToken"") : this(null, userName, password, null, tokenUrl) {{ }}
        public Service(string url, string? userName, string? password, string tokenUrl = ""https://www.arcgis.com/sharing/rest/generateToken"") : this(url, userName, password, null, tokenUrl) {{ }}
        private Service(string? url, string? userName, string? password, string? token, string? tokenUrl)
        {{
            PreStormCore.{type}<T> Create<T>(int id) where T : PreStormCore.Feature => userName is null || password is null
                ? new PreStormCore.FeatureLayer<T>($""{{url ?? defaultUrl}}/{{id}}"", token)
                : new PreStormCore.FeatureLayer<T>($""{{url ?? defaultUrl}}/{{id}}"", userName, password, tokenUrl!);
{string.Join("\r\n", types.Select(x => $@"            {x.property} = Create<{x.@class}>({x.id});"))}
        }}
    }}
}}
");
        }

        private static string GetClass(this Layer layerInfo, string className, string url, string ns, bool domain)
        {
            var entries = new List<string> { className };

            var geometryType = layerInfo.geometryType switch
            {
                esriGeometryPoint => "Point",
                esriGeometryMultipoint => "Multipoint",
                esriGeometryPolyline => "Polyline",
                esriGeometryPolygon => "Polygon",
                _ => null
            };

            var t = DateTime.UtcNow;

            return $@"namespace {ns}
{{
    /// <summary>
    /// <para>This code was generated on {t.ToLongDateString()} at {t.ToLongTimeString()} (UTC) based on the service schema. </para>
    /// <para>Service: {url} </para>
    /// <para>Layer: {layerInfo.name} </para>
    /// </summary>
    public class {className} : PreStormCore.Feature{(geometryType == null ? "" : $"<PreStormCore.{geometryType}>")}
    {{
{string.Join("\r\n\r\n", layerInfo.fields.Select(f => GetProperty(f, domain, entries)).Where(x => x != null))}
    }}
}}
";
        }

        private static string GetProperty(Field field, bool domain, List<string> reserved)
        {
            if (field.type == esriFieldTypeOID)
                return null;

            var csType = domain && field.domain?.type == "codedValue"
                ? field.domain.name.ToSafeName(true, true, null)
                : field.type switch
                {
                    esriFieldTypeInteger => "int",
                    esriFieldTypeSmallInteger => "short",
                    esriFieldTypeDouble => "double",
                    esriFieldTypeSingle => "float",
                    esriFieldTypeString => "string",
                    esriFieldTypeDate => "System.DateTime",
                    esriFieldTypeGUID | esriFieldTypeGlobalID => "System.Guid",
                    _ => null
                };

            if (csType is null)
                return null;

            var entry = field.name.ToSafeName(false, true, reserved);
            reserved.Add(entry);

            return $@"        /// <summary>
        /// <para>Field: {field.name} </para>
        /// <para>Alias: {field.alias} </para>
        /// </summary
        [PreStormCore.Mapped(""{field.name}""){(field.nullable == false ? ", System.ComponentModel.DataAnnotations.Required" : "")}]
        public virtual {csType}{(field.nullable == false ? "" : "?")} {entry} {{ get; set; }} = default!;";
        }

        private static string GetDomain(Field field, object code, string name, string domain, List<string> reserved)
        {
            var entry = (domain == "code" ? code.ToString() : name).ToSafeName(false, false, reserved);
            reserved.Add(entry);

            return $@"[PreStormCore.Domain({(field.type == esriFieldTypeString ? $@"""{code.ToString().Replace(@"""", @"\""")}""" : $"{code}")}, ""{name.Replace(@"""", @"\""")}"")] {entry}";
        }

        private static readonly string[] LanguageKeywords = { "abstract", "add", "alias", "as", "ascending", "async", "await", "base", "bool", "break", "by", "byte", "case", "catch", "char", "checked", "class", "const", "continue", "decimal", "default", "delegate", "descending", "do", "double", "dynamic", "else", "enum", "equals", "event", "explicit", "extern", "false", "finally", "fixed", "float", "for", "foreach", "from", "get", "global", "goto", "group", "if", "implicit", "in", "int", "interface", "internal", "into", "is", "join", "let", "lock", "long", "nameof", "namespace", "new", "notnull", "null", "object", "on", "operator", "orderby", "out", "override", "params", "partial", "private", "protected", "public", "readonly", "ref", "remove", "return", "sbyte", "sealed", "select", "set", "short", "sizeof", "stackalloc", "static", "string", "struct", "switch", "this", "throw", "true", "try", "typeof", "uint", "ulong", "unchecked", "unmanaged", "unsafe", "ushort", "using", "value", "var", "virtual", "void", "volatile", "when", "where", "while", "with", "yield" };
        private static readonly string[] ReservedWords = { "OID", "IsDataBound", "IsDirty", "Geometry" };
        private static readonly string[] SingularWords = { "series", "species" };

        private static string ToSafeName(this string text, bool singular, bool split, List<string> reserved)
        {
            if (split)
                text = text.Split('.').Last(x => !string.IsNullOrEmpty(x));

            if (int.TryParse(text, out var n) && n < 0)
                text = $"Minus{Math.Abs(n)}";

            text = Regex.Replace(text, @"(?<=((\W|_)))[a-z]", x => x.Value.ToUpperInvariant());
            text = Regex.Replace(text, @"(?<=([A-Z]))[A-Z]+", x => x.Value.ToLowerInvariant());
            text = Regex.Replace(text, @"_", "");

            text = Regex.Replace(text, @"\W", "");
            text = Regex.IsMatch(text, @"^\d") ? "_" + text : text;

            if (singular && !SingularWords.Any(x => text.EndsWith(x, StringComparison.InvariantCultureIgnoreCase)))
            {
                if (Regex.IsMatch(text, @"ies$", RegexOptions.IgnoreCase))
                {
                    text = Regex.Replace(text, @"ies$", "y");
                    text = Regex.Replace(text, @"IES$", "Y");
                }
                else if (Regex.IsMatch(text, @"(ch|sh|ss)es$", RegexOptions.IgnoreCase))
                {
                    text = Regex.Replace(text, @"es$", "", RegexOptions.IgnoreCase);
                }
                else if (!Regex.IsMatch(text, @"(i|s|u)s$", RegexOptions.IgnoreCase))
                {
                    text = Regex.Replace(text, @"s$", "", RegexOptions.IgnoreCase);
                }
            }

            text = Regex.Replace(text, @"^\w", x => x.Value.ToUpperInvariant());
            text = LanguageKeywords.Contains(text) ? $"@{text}" : text;
            text = ReservedWords.Contains(text) ? $"{text}_" : text;

            return Enumerable.Range(0, 100)
                .Select(x => text + (x == 0 ? "" : x.ToString()))
                .First(x => reserved == null || reserved.All(y => !string.Equals(x, y)));
        }
    }

    #region Esri

    internal class ServiceInfo
    {
        public Layer[] layers { get; set; }
        public Layer[] tables { get; set; }
        public Error error { get; set; }
        public string capabilities { get; set; }
        public bool SupportsQuery => capabilities?.Contains("Query") == true;
        public bool SupportsCreate => capabilities?.Contains("Create") == true;
        public bool SupportsUpdate => capabilities?.Contains("Update") == true;
        public bool SupportsDelete => capabilities?.Contains("Delete") == true;
    }

    public class Error
    {
        public string message { get; set; }
    }

    internal class Layer
    {
        public int id { get; set; }
        public Layer parentLayer { get; set; }
        public string name { get; set; }
        public string type { get; set; }
        public GeometryType? geometryType { get; set; }
        public Field[] fields { get; set; }
        public string capabilities { get; set; }
        public bool SupportsQuery => capabilities?.Contains("Query") == true;
    }

    internal class Field
    {
        public string name { get; set; }
        public FieldType? type { get; set; }
        public string alias { get; set; }
        public Domain domain { get; set; }
        public bool? nullable { get; set; }
    }

    public class Domain
    {
        public string type { get; set; }
        public string name { get; set; }
        public CodedValue[] codedValues { get; set; }
    }

    public class CodedValue
    {
        public object code { get; set; }
        public string name { get; set; }
    }

    internal enum FieldType
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

    internal enum GeometryType
    {
        esriGeometryNull = 0,
        esriGeometryPoint = 1,
        esriGeometryMultipoint = 2,
        esriGeometryPolyline = 3,
        esriGeometryPolygon = 4,
        esriGeometryEnvelope = 5,
        esriGeometryPath = 6,
        esriGeometryAny = 7,
        esriGeometryMultiPatch = 9,
        esriGeometryRing = 11,
        esriGeometryLine = 13,
        esriGeometryCircularArc = 14,
        esriGeometryBezier3Curve = 15,
        esriGeometryEllipticArc = 16,
        esriGeometryBag = 17,
        esriGeometryTriangleStrip = 18,
        esriGeometryTriangleFan = 19,
        esriGeometryRay = 20,
        esriGeometrySphere = 21,
        esriGeometryTriangles = 22
    }

    #endregion
}
