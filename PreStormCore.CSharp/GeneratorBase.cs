using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Newtonsoft.Json;

namespace PreStormCore.CodeGeneration;

internal record struct Service(
    string Url,
    string? Token,
    string? TokenUrl,
    string? User,
    string? Password,
    string? Namespace,
    string? Domain,
    string? Exclude);

public abstract class GeneratorBase
{
    internal Service Service { get; set; }

    internal GeneratorBase()
    {
    }

    public GeneratorBase(string url, string? token, string? tokenUrl, string? user, string? password, string? @namespace, string? domain, string? exclude)
    {
        Service = new Service(url, token, tokenUrl, user, password, @namespace, domain, exclude);
    }

    public string Namespace
    {
        get
        {
            if (Service.Namespace is not null)
                return Service.Namespace;

            var name = Regex.Match(Service.Url, @"(?<=(https?://.*?/))(\w|\-)+(?=(/(MapServer|FeatureServer)))", RegexOptions.IgnoreCase).Value;

            if (name == string.Empty)
                throw new InvalidOperationException($"{Service.Url} is not a valid map service url.");

            return name.ToSafeName(false, true, null);
        }
    }

    public IEnumerable<(string name, string @namespace, string code)> Generate()
    {
        var (url, token, tokenUrl, user, password, domain, exclude)
            = (Service.Url, Service.Token ?? "", Service.TokenUrl ?? "", Service.User ?? "", Service.Password ?? "", Service.Domain ?? "", Service.Exclude ?? "");

        var classes = new List<string>();
        var serviceProperties = new List<string>();

        using var http = new HttpClient();

        if (string.IsNullOrEmpty(token) && !string.IsNullOrEmpty(user) && !string.IsNullOrEmpty(password))
        {
            if (string.IsNullOrEmpty(tokenUrl))
                tokenUrl = "https://www.arcgis.com/sharing/rest/generateToken";

            var data = $"username={WebUtility.UrlEncode(user)}&password={WebUtility.UrlEncode(password)}&clientid=requestip&expiration=60";

            token = http.PostAsync(tokenUrl, new StringContent(data, Encoding.UTF8, "application/x-www-form-urlencoded")).Result.Content.ReadAsStringAsync().Result;
        }

        var json = http.GetAsync($"{url}?f=json{(string.IsNullOrEmpty(token) ? null : $"&token={token}")}").Result.Content.ReadAsStringAsync().Result;
        var serviceInfo = JsonConvert.DeserializeObject<ServiceInfo>(json)!;

        if (serviceInfo.error is not null)
            throw new InvalidOperationException($"ArcGIS responded with an error message while processsing the request against {url}.  {serviceInfo.error.message}");

        var layers = serviceInfo.layers.Concat(serviceInfo.tables)
            .AsParallel()
            .AsOrdered()
            .Select(x => http.GetAsync($"{url}/{x.id}?f=json{(token is null ? null : $"&token={token}")}").Result.Content.ReadAsStringAsync().Result)
            .Select(x => JsonConvert.DeserializeObject<Layer>(x)!)
            .Where(x => x.type == "Feature Layer" || x.type == "Table")
            .Where(x => x.SupportsQuery)
            .ToArray();

        var types = new List<(string @class, string property, int id)>();

        var useDomain = domain == "code" || domain == "name";

        foreach (var layer in layers.Where(x => x.fields is not null))
        {
            var fullname = $"{layer.parentLayer?.name}{layer.name}";

            var @class = fullname.ToSafeName(true, true, classes);
            classes.Add(@class);

            var serviceProperty = fullname.ToSafeName(false, true, serviceProperties);
            serviceProperties.Add(serviceProperty);

            types.Add((@class, serviceProperty, layer.id));

            var geometryType = layer.geometryType switch
            {
                "esriGeometryPoint" => "Point",
                "esriGeometryMultipoint" => "Multipoint",
                "esriGeometryPolyline" => "Polyline",
                "esriGeometryPolygon" => "Polygon",
                _ => null
            };

            var entries = new List<string> { @class };
            var fieldsToExclude = exclude.Split(',').Select(x => x.Trim()).ToArray();
            var fields = layer.fields
                .Where(x => x.type != "esriFieldTypeOID")
                .Where(x => !fieldsToExclude.Contains(x.name))
                .ToArray();

            yield return ($"{Namespace}.{@class}", GetNamespace(), GetClass(@class, layer.name, layer.description, geometryType, fields, useDomain));
        }

        if (useDomain)
        {
            var domains = layers
                .Where(x => x.fields is not null)
                .SelectMany(x => x.fields)
                .Select(x => (field: x, x.domain))
                .Where(x => x.domain?.type == "codedValue")
                .GroupBy(x => x.domain.name)
                .Select(x => x.First())
                .Select(x => (x.field, name: x.domain.name.ToSafeName(true, true, null)))
                .ToArray();

            foreach (var (f, n) in domains)
            {
                yield return ($"{Namespace}.{n}", GetNamespace(), GetEnum(f, n, domain == "code"));
            }
        }

        var @interface = serviceInfo switch
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

        yield return ($"{Namespace}.Service", GetNamespace(), GetService(@interface, types));
    }

    protected abstract string GetNamespace();
    protected abstract string GetService(string @interface, List<(string @class, string property, int id)> types);
    protected abstract string GetClass(string @class, string layerName, string? description, string? geometryType, Field[] fields, bool useDomain);
    protected abstract string GetEnum(Field field, string @enum, bool useCode);
}

internal static class Helpers
{
    private static readonly string[] LanguageKeywords = { "abstract", "add", "alias", "as", "ascending", "async", "await", "base", "bool", "break", "by", "byte", "case", "catch", "char", "checked", "class", "const", "continue", "decimal", "default", "delegate", "descending", "do", "double", "dynamic", "else", "enum", "equals", "event", "explicit", "extern", "false", "finally", "fixed", "float", "for", "foreach", "from", "get", "global", "goto", "group", "if", "implicit", "in", "int", "interface", "internal", "into", "is", "join", "let", "lock", "long", "nameof", "namespace", "new", "notnull", "null", "object", "on", "operator", "orderby", "out", "override", "params", "partial", "private", "protected", "public", "readonly", "ref", "remove", "return", "sbyte", "sealed", "select", "set", "short", "sizeof", "stackalloc", "static", "string", "struct", "switch", "this", "throw", "true", "try", "typeof", "uint", "ulong", "unchecked", "unmanaged", "unsafe", "ushort", "using", "value", "var", "virtual", "void", "volatile", "when", "where", "while", "with", "yield" };
    private static readonly string[] ReservedWords = { "OID", "IsDataBound", "IsDirty", "Geometry" };
    private static readonly string[] SingularWords = { "series", "species" };

    public static string ToSafeName(this string text, bool singular, bool split, List<string>? reserved)
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
            .First(x => reserved is null || reserved.All(y => !string.Equals(x, y)));
    }
}

#region Esri
#pragma warning disable IDE1006 // Naming Styles

public class ServiceInfo
{
    public Layer[] layers { get; set; } = default!;
    public Layer[] tables { get; set; } = default!;
    public Error? error { get; set; }
    public string capabilities { get; set; } = default!;
    public bool SupportsQuery => capabilities?.Contains("Query") == true;
    public bool SupportsCreate => capabilities?.Contains("Create") == true;
    public bool SupportsUpdate => capabilities?.Contains("Update") == true;
    public bool SupportsDelete => capabilities?.Contains("Delete") == true;
}

public class Error
{
    public string message { get; set; } = default!;
}

public class Layer
{
    public int id { get; set; }
    public Layer parentLayer { get; set; } = default!;
    public string name { get; set; } = default!;
    public string type { get; set; } = default!;
    public string? description { get; set; } = default!;
    public string? geometryType { get; set; }
    public Field[] fields { get; set; } = default!;
    public string capabilities { get; set; } = default!;
    public bool SupportsQuery => capabilities?.Contains("Query") == true;
}

public class Field
{
    public string name { get; set; } = default!;
    public string type { get; set; } = default!;
    public string alias { get; set; } = default!;
    public Domain domain { get; set; } = default!;
    public bool? editable { get; set; }
    public bool? nullable { get; set; }
}

public class Domain
{
    public string type { get; set; } = default!;
    public string name { get; set; } = default!;
    public CodedValue[] codedValues { get; set; } = default!;
}

public class CodedValue
{
    public object code { get; set; } = default!;
    public string name { get; set; } = default!;
}

#pragma warning restore IDE1006 // Naming Styles
#endregion

public class SourceGenerator<T> : IIncrementalGenerator
    where T : GeneratorBase, new()
{
    private static readonly ConcurrentDictionary<Service, (string name, string @namespace, string code)[]> cache = new();

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var additionalTexts = context.AdditionalTextsProvider
            .Where(x => x.Path.EndsWith(".json"))
            .Select((x, y) => x.GetText(y)?.ToString());

        context.RegisterSourceOutput(additionalTexts, (source, json) =>
        {
            if (string.IsNullOrWhiteSpace(json))
                return;

            try
            {
                var services = JsonConvert.DeserializeAnonymousType(json!, new { services = Array.Empty<Service>() })!.services;

                foreach (var s in services)
                {
                    foreach (var (name, @namespace, code) in cache.GetOrAdd(s, x => new T { Service = s }.Generate().ToArray()))
                        source.AddSource($"{Guid.NewGuid()}", SourceText.From($"{@namespace}\r\n\r\n{code}", Encoding.UTF8));
                }
            }
            catch
            {
            }
        });
    }
}
