using System;
using System.Reflection;

namespace PreStormCore
{
    [AttributeUsage(AttributeTargets.Property)]
    public class Mapped : Attribute
    {
        public string FieldName { get; }
        public PropertyInfo? Property { get; internal set; }

        public Mapped(string fieldName)
        {
            FieldName = fieldName;
        }
    }
}
