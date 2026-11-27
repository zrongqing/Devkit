using Devkit.Application.Api.Contracts;
using Devkit.Application.Application.Authorization;
using Devkit.Application.Application.Settings;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Devkit.Application.Api.Endpoints;

public sealed class SystemSettingsEndpoints : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/identity/system-settings").WithTags("System Settings");
        group.MapGet("", async (HttpContext context, SystemSettingsService service, CancellationToken ct) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            return Result(context, await service.GetTimeZoneAsync(ct));
        }).WithSummary("Returns the Web display time zone; null follows the browser");

        group.MapPut("", UpdateAsync)
            .RequireAuthorization()
            .Accepts<SystemSettingsContract>("application/json")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .WithSummary("Sets the Web display time zone; requires system.settings.manage");
    }

    private static async Task<Ok<ApiResponse<SystemSettingsContract>>> UpdateAsync(HttpContext context,
        ICurrentIdentity identity, SystemSettingsService service, CancellationToken ct)
    {
        (await identity.CurrentAsync(ct)).Require(SystemSettingsService.ManagePermission);
        if (!context.Request.HasJsonContentType())
            throw new IdentityException(400, "invalid_json", "请提交包含 timeZone 字段的 JSON 对象。");

        // Read explicitly so malformed JSON uses the shared ProblemDetails handler.
        var request = await context.Request.ReadFromJsonAsync<SystemSettingsContract>(ct)
            ?? throw new IdentityException(400, "invalid_json", "请提交包含 timeZone 字段的 JSON 对象。");
        return Result(context, await service.UpdateTimeZoneAsync(request.TimeZone, ct));
    }

    private static Ok<ApiResponse<SystemSettingsContract>> Result(HttpContext context, string? timeZone) =>
        TypedResults.Ok(new ApiResponse<SystemSettingsContract>(new(timeZone), context.TraceIdentifier));
}
