using Microsoft.CodeAnalysis;
using PreStormCore.CodeGeneration;
using System.Text;

namespace PreStormCore.CSharp;

public class Generator : GeneratorBase
{
    public Generator()
    {
    }

    public Generator(string url, string? token = null, string? tokenUrl = null, string? user = null, string? password = null, string? @namespace = null, string? domain = null, string? exclude = null)
        : base(url, token, tokenUrl, user, password, @namespace, domain, exclude)
    {
    }

    protected override string GetNamespace()
    {
        return $"namespace {Namespace};";
    }

    protected override string GetService(string @interface, List<(string @class, string property, int id)> types)
    {
        return $@"public class Service
{{
    private readonly string defaultUrl = ""{Service.Url}"";

{string.Join("\r\n\r\n", types.Select(x => $@"    public PreStormCore.{@interface}<{x.@class}> {x.property} {{ get; }}"))}

    public Service(string? url = null, string? user = null, string? password = null, string? tokenUrl = ""https://www.arcgis.com/sharing/rest/generateToken"", string? token = null)
    {{
        PreStormCore.{@interface}<T> Create<T>(int id) where T : PreStormCore.Feature => string.IsNullOrEmpty(user) || string.IsNullOrEmpty(password)
            ? new PreStormCore.FeatureLayer<T>($""{{url ?? defaultUrl}}/{{id}}"", token)
            : new PreStormCore.FeatureLayer<T>($""{{url ?? defaultUrl}}/{{id}}"", user, password, tokenUrl!);

{string.Join("\r\n", types.Select(x => $@"        {x.property} = Create<{x.@class}>({x.id});"))}
    }}
}}";
    }

    protected override string GetClass(string @class, string? geometryType, Field[] fields, bool useDomain)
    {
        var reserved = new List<string> { @class };

        var builder = new StringBuilder();

        builder.AppendLine($"public class {@class} : PreStormCore.Feature{(geometryType is null ? "" : $"<PreStormCore.{geometryType}>")}");
        builder.Append('{');

        foreach (var field in fields)
        {
            var csType = useDomain && field.domain?.type == "codedValue"
                ? field.domain.name.ToSafeName(true, true, null)
                : field.type switch
                {
                    "esriFieldTypeInteger" => "int",
                    "esriFieldTypeSmallInteger" => "short",
                    "esriFieldTypeDouble" => "double",
                    "esriFieldTypeSingle" => "float",
                    "esriFieldTypeString" => "string",
                    "esriFieldTypeDate" => "System.DateTime",
                    "esriFieldTypeGUID" => "System.Guid",
                    "esriFieldTypeGlobalID" => "System.Guid",
                    _ => null
                };

            if (csType is null)
                continue;

            var propertyName = field.name.ToSafeName(false, true, reserved);
            reserved.Add(propertyName);

            builder.AppendLine();
            builder.AppendLine($"    [PreStormCore.Mapped(\"{field.name}\"){(field.nullable == false ? ", System.ComponentModel.DataAnnotations.Required" : "")}]");
            builder.AppendLine($"    public virtual {csType}{(field.nullable == false ? "" : "?")} {propertyName} {{ get;{(field.editable == true ? "" : " protected")} set; }} = default!;");
        }

        builder.Append('}');

        return builder.ToString();
    }

    protected override string GetEnum(Field field, string @enum, bool useCode)
    {
        var reserved = new List<string> { @enum };

        return $@"public enum {@enum}
{{
    {string.Join(",\r\n\r\n    ", field.domain.codedValues.Select(y =>
        {
            var entry = (useCode ? y.code.ToString() : y.name).ToSafeName(false, false, reserved);
            reserved.Add(entry);
            return $@"[PreStormCore.Domain({(field.type == "esriFieldTypeString" ? $@"""{y.code.ToString().Replace(@"""", @"\""")}""" : $"{y.code}")}, ""{@enum.Replace(@"""", @"\""")}"")]
    {entry}";
        }))}
}}";
    }
}

[Generator(LanguageNames.CSharp)]
public class SourceGenerator : SourceGenerator<Generator>, IIncrementalGenerator
{
}
