using Newtonsoft.Json;

namespace Devkit.Modules.Ssamc.Data.Api;

public class Response
{
    [JsonProperty("code")]
    public string? Code { get; set; }
    [JsonProperty("text")]
    public string? Text { get; set; }
    [JsonProperty("data")]
    public dynamic? Data { get; set; }

    public const string OK = "ok";
    public const string Success = "success";
    public const string Error = "error";
    public const string Information = "information";
}
