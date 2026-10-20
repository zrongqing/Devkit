using Devkit.FileStorage.Api.Contracts;
using Devkit.FileStorage.Application.Files;

namespace Devkit.FileStorage.Api.Endpoints;
public sealed class InternalFileEndpoints : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/internal/files").WithTags("Service File References")
            .AddEndpointFilter<FileClientFilter>();
        group.MapPut("/{id:guid}/references/{entityId:guid}", async (HttpContext c, IFileReferenceService files,
            IFileAccess access, Guid id, Guid entityId, FileReferenceRequest request) =>
        {
            if (entityId == Guid.Empty || request.OwnerId == Guid.Empty)
                throw new FileStorageException(400, "invalid_reference", "引用和所有者不能为空。");
            await files.LinkAsync(await access.CurrentAsync(c.RequestAborted), id, entityId, request.OwnerId,
                FileClientFilter.Module(c), c.RequestAborted);
            return Results.Ok(new ApiResponse<SuccessView>(new(), c.TraceIdentifier));
        }).RequireAuthorization().Produces<ApiResponse<SuccessView>>().ProducesProblem(401).ProducesProblem(403).ProducesProblem(404).ProducesProblem(409);
        group.MapDelete("/references/{entityId:guid}", async (HttpContext c, IFileReferenceService files, Guid entityId) =>
        {
            await files.UnlinkAsync(entityId, FileClientFilter.Module(c), c.RequestAborted);
            return Results.Ok(new ApiResponse<SuccessView>(new(), c.TraceIdentifier));
        }).Produces<ApiResponse<SuccessView>>().ProducesProblem(401);
        group.MapGet("/{id:guid}/content", async (HttpContext c, IFileReferenceService files, Guid id, Guid entityId) =>
        {
            var file = await files.OpenReferencedAsync(id, entityId, FileClientFilter.Module(c), c.RequestAborted);
            c.Response.Headers.XContentTypeOptions = "nosniff";
            return Results.Stream(file.Stream, file.ContentType, file.Name);
        }).Produces(200, contentType: "application/octet-stream").ProducesProblem(401).ProducesProblem(404).ProducesProblem(409);
    }
}
