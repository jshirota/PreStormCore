using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace PreStormCore
{
    public static class Text
    {
        private static string Join(this IEnumerable<object> values, string delimiter, char? qualifier)
        {
            var d = delimiter ?? "";
            var q = qualifier.ToString()!;

            if (qualifier is not null && d.Contains(q))
                throw new ArgumentException("The qualifier is not valid.", nameof(qualifier));

            return string.Join(d, values.Select(o => q == "" ? o : q + (o ?? "").ToString()!.Replace(q, q + q) + q));
        }

        private static string ToDelimitedText(this Feature feature, string delimiter, char? qualifier, Func<Geometry, object>? geometrySelector, Func<DateTime, string>? dateSelector)
        {
            var values = feature.GetFieldNames().Select(n => feature[n]).ToList();

            values.Insert(0, feature.OID);

            if (geometrySelector is not null)
            {
                var o = geometrySelector(((dynamic)feature).Geometry);

                if (o is string) values.Add(o);
                else values.AddRange((o as IEnumerable ?? new[] { o }).Cast<object>());
            }

            dateSelector ??= (d => d.ToString("o"));

            return values.Select(o => o is DateTime t ? dateSelector(t) : o)!.Join(delimiter, qualifier);
        }

        public static string ToText(this Feature feature, string delimiter = ",", char? qualifier = '"', Func<DateTime, string>? dateSelector = null)
        {
            return feature.ToDelimitedText(delimiter, qualifier, null, dateSelector);
        }

        public static string ToText<T>(this Feature<T> feature, string delimiter = ",", char? qualifier = '"', Func<T, object>? geometrySelector = null, Func<DateTime, string>? dateSelector = null) where T : Geometry
        {
            return feature.ToDelimitedText(delimiter, qualifier, geometrySelector is null ? null : g => geometrySelector((T)g), dateSelector);
        }
    }
}
