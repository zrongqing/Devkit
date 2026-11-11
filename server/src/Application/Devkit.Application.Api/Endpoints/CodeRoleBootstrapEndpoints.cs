using Devkit.Application.Infrastructure.Authorization;

namespace Devkit.Application.Api.Endpoints;

public sealed class CodeRoleBootstrapEndpoints : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder endpoints) =>
        endpoints.MapPut("/api/v1/internal/code-system/bootstrap/roles/{key}", async (HttpContext c, CodeRoleBootstrap roles, string key, CancellationToken ct) =>
        {
            await roles.EnsureAsync(key, c.Request.Headers["X-Code-Bootstrap-Key"].ToString(), ct);
            return TypedResults.NoContent();
        }).WithTags("编码初始化").ProducesProblem(400).ProducesProblem(401).ProducesProblem(404);
}
