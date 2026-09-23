using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Devkit.Server.Application.Workspace;

namespace Devkit.Server.Api.IntegrationTests;

// A deterministic HTTP boundary: production indexing and filter serialization still run,
// but tests never contact a shared Qdrant instance or an external model provider.
internal sealed class TestExternalServices : HttpMessageHandler, IHttpClientFactory
{
    private readonly Dictionary<string, Dictionary<Guid, JsonElement>> collections = new();
    private readonly Dictionary<string, JsonElement> configurations = new();
    public bool Unavailable { get; set; }
    public JsonElement LastQuery { get; private set; }
    public Func<Task>? AfterQuery { get; set; }
    public HttpClient CreateClient(string name) => new(this, disposeHandler: false);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        if (request.RequestUri?.Host != "qdrant.test") throw new InvalidOperationException("Unexpected network destination in test.");
        if (Unavailable) return new(HttpStatusCode.ServiceUnavailable);
        var path = request.RequestUri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var body = request.Content is null ? default : await request.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        if (path.Length == 1 && request.Method == HttpMethod.Get)
            return Ok(new { collections = collections.Keys.Select(name => new { name }) });
        var name = path[1];
        if (path.Length == 2 && request.Method == HttpMethod.Put)
        {
            collections.TryAdd(name, new()); configurations[name] = body;
            return Ok(true);
        }
        if (path.Length == 2 && request.Method == HttpMethod.Get)
            return Ok(new { config = new { @params = configurations[name] } });
        if (path.Length == 3 && path[2] == "index") return Ok(true);
        if (path.Length == 3 && path[2] == "points")
        {
            foreach (var point in body.GetProperty("points").EnumerateArray())
                collections[name][point.GetProperty("id").GetGuid()] = point.Clone();
            return Ok(true);
        }
        if (path.Length == 4 && path[3] == "query")
        {
            LastQuery = body.Clone();
            var must = body.GetProperty("filter").GetProperty("must").EnumerateArray().ToArray();
            var points = collections.GetValueOrDefault(name, new()).Values.Where(point =>
                must.All(condition =>
                {
                    var value = point.GetProperty("payload").GetProperty(condition.GetProperty("key").GetString()!).GetString();
                    var match = condition.GetProperty("match");
                    return match.TryGetProperty("value", out var exact) ? value == exact.GetString() : match.GetProperty("any").EnumerateArray().Any(x => x.GetString() == value);
                })).Take(body.GetProperty("limit").GetInt32()).Select(point => new { id = point.GetProperty("id").GetGuid(), score = 1d }).ToArray();
            if (AfterQuery is not null) await AfterQuery();
            return Ok(new { points });
        }
        if (path.Length == 4 && path[3] == "delete")
        {
            var source = body.GetProperty("filter").GetProperty("must")[0].GetProperty("match").GetProperty("value").GetString();
            foreach (var id in collections[name].Where(x => x.Value.GetProperty("payload").GetProperty("sourceId").GetString() == source).Select(x => x.Key).ToArray()) collections[name].Remove(id);
            return Ok(true);
        }
        throw new InvalidOperationException($"Unexpected Qdrant operation: {request.Method} {request.RequestUri.AbsolutePath}");
    }
    private static HttpResponseMessage Ok(object result) => new(HttpStatusCode.OK) { Content = JsonContent.Create(new { result, status = "ok" }) };
}

internal sealed class TestModelGateway : IModelGateway
{
    public bool ChatConfigured { get; set; }
    public bool EmbeddingConfigured { get; set; }
    public string EmbeddingProfile => "test-v1";
    public string Reply { get; set; } = "依据资料 [1]。";
    public Task<string> ChatAsync(string instruction, string content, CancellationToken ct) => Task.FromResult(Reply);
    public Task<float[][]> EmbedAsync(IReadOnlyList<string> input, CancellationToken ct)
    {
        if (!EmbeddingConfigured) throw new BusinessException(503, "model_not_configured", "Model disabled in test.");
        return Task.FromResult(input.Select(_ => new float[] { 1, 0, 0 }).ToArray());
    }
}
