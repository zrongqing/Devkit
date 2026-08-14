using System.Dynamic;

namespace Ssamc.Data.Api;

public class PostRequest
{
    public string au { get; set; } = "ssamc";
    public string ap { get; set; } = "api2018";
    public string ak { get; set; } = string.Empty;
    public string ac { get; set; } = string.Empty;

    public PostRequest(string ac)
    {
        this.ac = ac;
    }

    public ExpandoObject ToExpando()
    {
        object obj = this;
        var expando = new ExpandoObject();
        var dict = (IDictionary<string, object>)expando;

        foreach (var property in obj.GetType().GetProperties())
        {
            dict[property.Name] = property.GetValue(obj);
        }

        return expando;
    }
}
