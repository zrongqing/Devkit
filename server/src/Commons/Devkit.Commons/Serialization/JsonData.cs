using System.Text.Json;

namespace Devkit.Commons.Serialization;

public static class JsonData
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
    public static T Read<T>(string value) => JsonSerializer.Deserialize<T>(value, Options)!;
    public static string Write<T>(T value) => JsonSerializer.Serialize(value, Options);
}
