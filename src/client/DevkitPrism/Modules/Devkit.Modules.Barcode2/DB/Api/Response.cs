using Newtonsoft.Json;

namespace Barcode2.DB.Api;

public class Response
{
    public const string OK = "ok";
    public const string Success = "success";
    public const string Error = "error";
    public const string Information = "information";
    [JsonProperty("code")]
    public string? Code { get; set; }
    [JsonProperty("text")]
    public string? Text { get; set; }
    [JsonProperty("data")]
    public dynamic? Data { get; set; }
}
