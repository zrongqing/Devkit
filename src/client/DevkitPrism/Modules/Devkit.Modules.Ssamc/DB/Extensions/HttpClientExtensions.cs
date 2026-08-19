using System.Net.Http;
using System.Text;
using Newtonsoft.Json;
using Ssamc.DB.Api;

namespace Ssamc.DB.Extensions;

public static class HttpClientExtensions
{
    public static async Task<HttpResponseMessage> PostAsJsonAsync(this HttpClient client, string url, object data)
    {
        var json = JsonConvert.SerializeObject(data);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await client.PostAsync(url, content);
        return response;
    }

    public static Response? GetJson(this HttpResponseMessage response)
    {
        var jsonContent = response.Content.ReadAsStringAsync().Result;
        return JsonConvert.DeserializeObject<Response>(jsonContent);
    }

    public static async Task<Response?> PostAsBarcodeAsync(this HttpClient client, string url, object data)
    {
        var response = await client.PostAsJsonAsync(url, data);
        return response.GetJson();
    }
}
