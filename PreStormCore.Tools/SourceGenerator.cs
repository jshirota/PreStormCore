using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Newtonsoft.Json;

namespace PreStormCore.Tools
{
    [Generator]
    public class SourceGenerator : ISourceGenerator
    {
        private static readonly ConcurrentDictionary<(string url, string token, string tokenUrl, string user, string password, string @namespace, string domain), (string name, string code)[]> cache
            = new ConcurrentDictionary<(string url, string token, string tokenUrl, string user, string password, string @namespace, string domain), (string name, string code)[]>();

        public void Execute(GeneratorExecutionContext context)
        {
            var file = context.AdditionalFiles.SingleOrDefault(x => x.Path.EndsWith("prestorm.json", StringComparison.InvariantCultureIgnoreCase));

            if (file is null)
                return;

            var type = new { url = "", token = "", tokenUrl = "", user = "", password = "", @namespace = "", domain = "" };

            var services = JsonConvert.DeserializeAnonymousType(file.GetText().ToString(), new { services = new[] { type } }).services;

            foreach (var service in services)
            {
                foreach (var (name, code) in cache.GetOrAdd((service.url, service.token, service.tokenUrl, service.user, service.password, service.@namespace, service.domain),
                    x => Generator.Generate(x.url, x.token, x.tokenUrl, x.user, x.password, x.@namespace, x.domain).ToArray()))
                {
                    context.AddSource($"{Guid.NewGuid()}", SourceText.From(code, Encoding.UTF8));
                }
            }
        }

        public void Initialize(GeneratorInitializationContext context)
        {
        }
    }
}
