using System.Dynamic;

namespace Ssamc.Data.Extensions;

public static class ExpandoObjectExtensions
{
    public static ExpandoObject ToExpando(this object obj)
    {
        var expando = new ExpandoObject();
        IDictionary<string, object?> dict = expando;
        foreach (var property in obj.GetType().GetProperties())
        {
            dict[property.Name] = property.GetValue(obj);
        }
        return expando;
    }
}
