using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Newtonsoft.Json;

namespace PreStormCore.Tools
{
    [Generator(LanguageNames.CSharp)]
    public class SourceGenerator : IIncrementalGenerator
    {
        private static readonly ConcurrentDictionary<string, (string name, string code)[]> cache
            = new ConcurrentDictionary<string, (string name, string code)[]>();

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var additionalTexts = context.AdditionalTextsProvider
                .Where(x => x.Path.EndsWith("prestorm.json"))
                .Select((x, y) => x.GetText(y)?.ToString());

            context.RegisterSourceOutput(additionalTexts, (source, json) =>
            {
                if (string.IsNullOrWhiteSpace(json))
                    return;

                var services = JsonConvert.DeserializeAnonymousType(json, new { services = Array.Empty<ServiceDefinition>() }).services;

                foreach (var s in services)
                {
                    foreach (var (name, code) in cache.GetOrAdd(s, x
                        => Generator.Generate(s.Url, s.Token, s.TokenUrl, s.Url, s.Password, s.Namespace, s.Domain, s.Exclude).ToArray()))
                    {
                        source.AddSource($"{Guid.NewGuid()}", SourceText.From(code, Encoding.UTF8));
                    }
                }
            });
        }
    }

    public class ServiceDefinition
    {
        public string Url { get; set; }
        public string Token { get; set; }
        public string TokenUrl { get; set; }
        public string User { get; set; }
        public string Password { get; set; }
        public string Namespace { get; set; }
        public string Domain { get; set; }
        public string Exclude { get; set; }

        public static implicit operator string(ServiceDefinition serviceDefinition)
            => JsonConvert.SerializeObject(serviceDefinition);
    }
}
