using System.Dynamic;

namespace Ssamc.DB.Api;

public class PostRequest
{
    public PostRequest(string ac)
    {
        this.ac = ac;
    }
    public string au { get; set; } = "ssamc";
    public string ap { get; set; } = "api2018";
    public string ak { get; set; } = string.Empty;
    public string ac { get; set; }

    public ExpandoObject ToExpando()
    {
        object obj = this;
        var expando = new ExpandoObject();
        IDictionary<string, object?> dict = expando;

        foreach (var property in obj.GetType().GetProperties())
        {
            dict[property.Name] = property.GetValue(obj);
        }

        return expando;
    }
}
