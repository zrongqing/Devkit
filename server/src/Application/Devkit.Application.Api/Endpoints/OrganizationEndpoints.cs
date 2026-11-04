using Devkit.Application.Api.Contracts;
using Devkit.Application.Application.Administration;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Devkit.Application.Api.Endpoints;

public sealed class OrganizationEndpoints : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/identity/organization").RequireAuthorization().WithTags("Organization");
        group.MapGet("/", (HttpContext c, OrganizationAdministration service) => Result(c, service.ReadAsync(c.RequestAborted)));
        group.MapPost("/departments", (HttpContext c, OrganizationAdministration service, DepartmentRequest input) =>
            Result(c, service.DepartmentAsync(null, input.Name, input.ParentId, c.RequestAborted)));
        group.MapPut("/departments/{id:guid}", (HttpContext c, OrganizationAdministration service, Guid id, DepartmentRequest input) =>
            Result(c, service.DepartmentAsync(id, input.Name, input.ParentId, c.RequestAborted)));
        group.MapDelete("/departments/{id:guid}", (CancellationToken ct, OrganizationAdministration service, Guid id) =>
            Done(service.DeleteDepartmentAsync(id, ct)));
        group.MapPost("/positions", (HttpContext c, OrganizationAdministration service, PositionRequest input) =>
            Result(c, service.PositionAsync(null, input.Name, input.DepartmentId, c.RequestAborted)));
        group.MapPut("/positions/{id:guid}", (HttpContext c, OrganizationAdministration service, Guid id, PositionRequest input) =>
            Result(c, service.PositionAsync(id, input.Name, input.DepartmentId, c.RequestAborted)));
        group.MapDelete("/positions/{id:guid}", (CancellationToken ct, OrganizationAdministration service, Guid id) =>
            Done(service.DeletePositionAsync(id, ct)));
        group.MapPut("/positions/{id:guid}/members/{userId:guid}", (CancellationToken ct, OrganizationAdministration service, Guid id, Guid userId) =>
            Done(service.SetMemberAsync(id, userId, true, ct)));
        group.MapDelete("/positions/{id:guid}/members/{userId:guid}", (CancellationToken ct, OrganizationAdministration service, Guid id, Guid userId) =>
            Done(service.SetMemberAsync(id, userId, false, ct)));
        endpoints.MapGet("/api/v1/identity/audit", (HttpContext c, OrganizationAdministration service, long? before) =>
            Result(c, service.AuditAsync(before, c.RequestAborted))).RequireAuthorization().WithTags("Administration Audit");
    }

    private static async Task<Ok<ApiResponse<T>>> Result<T>(HttpContext context, Task<T> operation) =>
        TypedResults.Ok(new ApiResponse<T>(await operation, context.TraceIdentifier));

    private static async Task<NoContent> Done(Task operation)
    {
        await operation;
        return TypedResults.NoContent();
    }
}
