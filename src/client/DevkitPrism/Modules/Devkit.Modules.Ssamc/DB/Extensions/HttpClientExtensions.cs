using System.Text;
using System.Net.Http;
using Newtonsoft.Json;
using Ssamc.Data.Api;

namespace Ssamc.Data.Extensions;

public static class HttpClientExtensions
{
    public async static Task<HttpResponseMessage> PostAsJsonAsync(this HttpClient client, string url, object data)
    {
        var json = JsonConvert.SerializeObject(data);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await client.PostAsync(url, content);
        return response;
    }

    public static Response GetJson(this HttpResponseMessage response)
    {
        var jsonContent = response.Content.ReadAsStringAsync().Result;
        return JsonConvert.DeserializeObject<Response>(jsonContent);
    }

    public async static Task<Response> PostAsBarcodeAsync(this HttpClient client, string url, object data)
    {
        var response = await PostAsJsonAsync(client, url, data);
        return response.GetJson();
    }
}
