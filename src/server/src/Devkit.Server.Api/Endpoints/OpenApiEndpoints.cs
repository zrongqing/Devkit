namespace Devkit.Server.Api.Endpoints;

/// <summary>Minimal OpenAPI document kept dependency-free until a documentation UI is selected.</summary>
public static class OpenApiEndpoints
{
    public static IEndpointRouteBuilder MapDevkitOpenApi(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/openapi/v1.json", () => Results.Json(new
        {
            openapi = "3.0.1",
            info = new { title = "Devkit Server API", version = "v1" },
            paths = new Dictionary<string, object>
            {
                ["/health"] = new { get = new { summary = "Service liveness check" } },
                ["/api/v1/system/info"] = new { get = new { summary = "Returns system runtime information" } }
            }
        })).WithTags("OpenAPI");
        return endpoints;
    }
}
