using System.ComponentModel;
using System.Linq.Expressions;
using System.Text.Json.Serialization;

namespace PreStormCore;

public abstract class Feature : INotifyPropertyChanged
{
    private readonly Dictionary<string, string> propertyToField;
    private readonly Dictionary<string, string> fieldToProperty;
    internal readonly Dictionary<string, object?> UnmappedFields = new();
    internal readonly List<string> ChangedFields = new();
    internal bool GeometryChanged;

    protected Feature()
    {
        OID = -1;
        propertyToField = GetType().GetMappings().ToDictionary(m => m.Property!.Name, m => m.FieldName);
        fieldToProperty = GetType().GetMappings().ToDictionary(m => m.FieldName, m => m.Property!.Name);
    }

    public static T Create<T>() where T : Feature
    {
        return Proxy.Create<T>();
    }

    public static Mapped[] GetMappings(Type type)
    {
        return type.GetMappings();
    }

    public int OID { get; internal set; }

    [JsonIgnore]
    public bool IsDataBound => OID > -1;

    public string[] GetFieldNames()
    {
        return fieldToProperty.Keys.Concat(UnmappedFields.Keys).ToArray();
    }

    private object? GetValue(string fieldName)
    {
        if (fieldToProperty.ContainsKey(fieldName))
        {
            return GetType().GetProperty(fieldToProperty[fieldName])!.GetValue(this, null);
        }

        if (UnmappedFields.ContainsKey(fieldName))
        {
            var value = UnmappedFields[fieldName];

            if (value is long)
                return Convert.ToInt32(value);

            return value;
        }

        throw new MissingFieldException($"Field '{fieldName}' does not exist.");
    }

    private void SetValue(string fieldName, object? value)
    {
        if (fieldToProperty.ContainsKey(fieldName))
        {
            var p = GetType().GetProperty(fieldToProperty[fieldName])!;

            if (!Equals(p.GetValue(this, null), value))
            {
                p.SetValue(this, value, null);
                IsDirty = true;
                ChangedFields.Add(fieldName);
            }

            return;
        }

        if (UnmappedFields.ContainsKey(fieldName))
        {
            if (!Equals(UnmappedFields[fieldName], value))
            {
                UnmappedFields[fieldName] = value;
                IsDirty = true;
                ChangedFields.Add(fieldName);
            }

            return;
        }

        UnmappedFields.Add(fieldName, value);
        IsDirty = true;
        ChangedFields.Add(fieldName);
    }

    public object? this[string fieldName]
    {
        get { return GetValue(fieldName); }
        set { SetValue(fieldName, value); }
    }

    public T Cast<T>() where T : Feature
    {
        var f = Create<T>();
        f.OID = OID;

        foreach (var fieldName in GetFieldNames())
        {
            var value = this[fieldName];

            if (value is null && f.fieldToProperty.ContainsKey(fieldName))
            {
                var t = typeof(T).GetProperty(f.fieldToProperty[fieldName])?.PropertyType.GetUnderlyingType();
                f[fieldName] = t is null ? null : Convert.ChangeType(value, t);
            }
            else
            {
                f[fieldName] = value;
            }
        }

        if (typeof(T).HasGeometry() && GetType().HasGeometry())
            ((dynamic)f).Geometry = ((dynamic)this).Geometry;

        f.IsDirty = false;

        return f;
    }

    private bool isDirty;

    [JsonIgnore]
    public bool IsDirty
    {
        get { return isDirty; }
        internal set
        {
            if (isDirty == value)
                return;

            isDirty = value;

            if (!isDirty)
            {
                ChangedFields.Clear();
                GeometryChanged = false;
            }

            RaisePropertyChanged(() => IsDirty);
        }
    }

    public bool Changed(string propertyName)
    {
        return propertyName is "Geometry" ? GeometryChanged : ChangedFields.Contains(propertyToField[propertyName]);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void RaisePropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        if (propertyName == "IsDirty")
            return;

        if (propertyToField.ContainsKey(propertyName))
        {
            ChangedFields.Add(propertyToField[propertyName]);
            IsDirty = true;
        }
        else if (propertyName == "Geometry")
        {
            GeometryChanged = true;
            IsDirty = true;
        }
    }

    public void RaisePropertyChanged<T>(Expression<Func<T>> propertySelector)
    {
        if (propertySelector.Body is MemberExpression memberExpression)
            RaisePropertyChanged(memberExpression.Member.Name);
    }
}

public abstract class Feature<T> : Feature where T : Geometry
{
    private T? geometry;

    public T? Geometry
    {
        get { return geometry; }
        set
        {
            geometry = value;
            RaisePropertyChanged(nameof(Geometry));
        }
    }
}

public class DynamicFeature : Feature<Geometry>
{
}
