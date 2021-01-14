using System;
using System.Linq;

namespace PreStormCore
{
    [AttributeUsage(AttributeTargets.Field)]
    public class Domain : Attribute
    {
        public object Code { get; }

        public string Name { get; }

        public Domain(object code, string name)
        {
            this.Code = code;
            this.Name = name;
        }
    }

    public static class DomainExt
    {
        private static readonly Func<Type, (string field, Domain domain)[]> DomainsMemoized = Memoization.Memoize<Type, (string field, Domain domain)[]>(t =>
            t.GetFields()
                .Select(x => new
                {
                    field = x.Name,
                    domain = x.GetCustomAttributes(typeof(Domain), false).FirstOrDefault()
                })
                .Where(x => x.domain is not null)
                .Select(x => (x.field, (Domain)x.domain!))
                .ToArray());

        internal static (string field, Domain domain)[] Domains(this Type type)
            => DomainsMemoized(type);

        internal static Domain? Domain(this Type type, string name)
            => type.Domains().FirstOrDefault(x => x.field == name).domain;

        public static Domain? Domain(this Enum value)
            => value.GetType().Domain(value.ToString());
    }
}
