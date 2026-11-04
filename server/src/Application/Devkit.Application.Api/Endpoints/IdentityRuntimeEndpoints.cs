using Devkit.Commons.Runtime;
using Devkit.Application.Application.Authorization;

namespace Devkit.Application.Api.Endpoints;

public sealed class IdentityRuntimeEndpoints : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/identity/runtime", async (HttpContext context, ICurrentIdentity identity,
            IHostEnvironment environment, TimeProvider clock) =>
        {
            (await identity.CurrentAsync(context.RequestAborted)).Require("system.monitor.view");
            var info = new SystemInfoResponse("Devkit Application", typeof(Program).Assembly.GetName().Version!.ToString(),
                environment.EnvironmentName, clock.GetUtcNow());
            return TypedResults.Ok(new ApiResponse<SystemRuntimeResponse>(SystemRuntimeResponse.Sample(info), context.TraceIdentifier));
        }).RequireAuthorization().WithTags("System Maintenance");
    }
}
