using Devkit.Server.Api.Contracts;
using Devkit.Server.Application.Abstractions;
using Devkit.Server.Application.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Devkit.Server.Api.Endpoints;

public sealed class SystemEndpoints : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/system").WithTags("System");
        group.MapGet("/info", GetInfo)
            .WithName("GetSystemInfo")
            .WithSummary("Returns server identity and runtime status")
            .Produces<ApiResponse<SystemInfoResponse>>();
    }

    private static Ok<ApiResponse<SystemInfoResponse>> GetInfo(HttpContext context, ISystemInfoService systemInfoService) =>
        TypedResults.Ok(new ApiResponse<SystemInfoResponse>(systemInfoService.GetInfo(), context.TraceIdentifier));
}
