namespace Devkit.Business.Api.Endpoints;

public static class OpenApiEndpoints
{
    public static IEndpointRouteBuilder MapDevkitOpenApi(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapOpenApi("/openapi/{documentName}.json");
        return endpoints;
    }
}
