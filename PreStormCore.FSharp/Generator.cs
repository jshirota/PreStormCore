using Microsoft.CodeAnalysis;
using PreStormCore.CodeGeneration;
using System.Text;

namespace PreStormCore.FSharp;

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
        return $"namespace {Namespace}";
    }

    protected override string GetService(string @interface, List<(string @class, string property, int id)> types)
    {
        return $@"type Service(url : string, user : string, password : string, tokenUrl : string) =
  let _url = if System.String.IsNullOrEmpty(url) then ""{Service.Url}"" else url
{string.Join("\r\n", types.Select(x => $@"  let _{x.property} = if System.String.IsNullOrEmpty(user) then PreStormCore.FeatureLayer<{x.@class}>($""{{_url}}/{x.id}"") else PreStormCore.FeatureLayer<{x.@class}>($""{{_url}}/{x.id}"", user, password, tokenUrl)"))}

{string.Join("\r\n", types.Select(x => $@"  member this.{x.property} with get() = _{x.property}"))}

  new() = Service(null, null, null, null)
  new(url : string) = Service(url, null, null)
  new(user : string, password : string) = Service(null, user, password)
  new(url : string, user : string, password : string) = Service(url, user, password, null)";
    }

    protected override string GetClass(string @class, string? geometryType, Field[] fields, bool useDomain)
    {
        var reserved = new List<string> { @class };

        var builder = new StringBuilder();

        builder.AppendLine($"type {@class}() =");
        builder.AppendLine($"  inherit PreStormCore.Feature{(geometryType is null ? "" : $"<PreStormCore.{geometryType}>")}()");
        builder.AppendLine();

        var fieldList = new List<string>();
        var propertyList = new List<string>();

        foreach (var field in fields)
        {
            var fsType = useDomain && field.domain?.type == "codedValue"
                ? field.domain.name.ToSafeName(true, true, null)
                : field.type switch
                {
                    "esriFieldTypeInteger" => "int",
                    "esriFieldTypeSmallInteger" => "int16",
                    "esriFieldTypeDouble" => "double",
                    "esriFieldTypeSingle" => "single",
                    "esriFieldTypeString" => "string",
                    "esriFieldTypeDate" => "System.DateTime",
                    "esriFieldTypeGUID" => "System.Guid",
                    "esriFieldTypeGlobalID" => "System.Guid",
                    _ => null
                };

            if (fsType is null)
                continue;

            if (field.nullable != false && fsType != "string")
                fsType = $"System.Nullable<{fsType}>";

            var propertyName = field.name.ToSafeName(false, true, reserved);
            reserved.Add(propertyName);

            fieldList.Add($"  let mutable _{propertyName} : {fsType} = Unchecked.defaultof<{fsType}>");

            propertyList.Add($@"
  abstract {propertyName} : {fsType} with get, set
  [<PreStormCore.Mapped(""{ field.name}""){(field.nullable == false ? ", System.ComponentModel.DataAnnotations.Required" : "")}>]
  default this.{propertyName} with get() = _{propertyName} and set(v : {fsType}) = _{propertyName} <- v");
        }

        fieldList.ForEach(x => builder.AppendLine(x));
        propertyList.ForEach(x => builder.AppendLine(x));

        return builder.ToString().Trim();
    }

    protected override string GetEnum(Field field, string @enum)
    {
        return "";
    }
}

[Generator(LanguageNames.FSharp)]
public class SourceGenerator : SourceGenerator<Generator>, IIncrementalGenerator
{
}
