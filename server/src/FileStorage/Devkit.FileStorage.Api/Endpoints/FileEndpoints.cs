using Devkit.FileStorage.Application.Files;
using Devkit.FileStorage.Domain.Files;
using Microsoft.AspNetCore.Http.HttpResults;
namespace Devkit.FileStorage.Api.Endpoints;
public sealed class FileEndpoints : IEndpointModule
{
    private static async Task<Ok<ApiResponse<T>>> Run<T>(HttpContext c,Func<Actor,Task<T>> operation)
    {var actor=await c.RequestServices.GetRequiredService<IFileAccess>().CurrentAsync(c.RequestAborted);return TypedResults.Ok(new ApiResponse<T>(await operation(actor),c.TraceIdentifier));}
    private static Task<Ok<ApiResponse<SuccessView>>> Run(HttpContext c,Func<Actor,Task> operation)=>Run(c,async actor=>{await operation(actor);return new SuccessView();});
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var file=endpoints.MapGroup("/api/v1/files").RequireAuthorization().WithTags("Files");
        file.MapGet("/",(HttpContext c,IFileService s)=>Run(c,a=>s.ListAsync(a,c.RequestAborted)));
        file.MapPost("/",async(HttpContext c,IFileService s,IFormFile file,string purpose="exam-study")=>
        {await using var stream=file.OpenReadStream();return await Run(c,a=>s.UploadAsync(a,file.FileName,purpose,stream,c.RequestAborted));}).DisableAntiforgery().Produces<ApiResponse<FileView>>();
        file.MapGet("/{id:guid}/content",async(HttpContext c,IFileService s,IFileAccess access,Guid id)=>
        {var f=await s.OpenAsync(await access.CurrentAsync(c.RequestAborted),id,c.RequestAborted);c.Response.Headers.XContentTypeOptions="nosniff";return Results.Stream(f.Stream,f.ContentType,f.Name,enableRangeProcessing:true);});
        file.MapDelete("/{id:guid}",(HttpContext c,IFileService s,Guid id)=>Run(c,a=>s.PurgeAsync(a,id,c.RequestAborted)));
        file.MapGet("/{id:guid}/references",(HttpContext c,IFileService s,Guid id)=>Run(c,a=>s.ReferencesAsync(a,id,c.RequestAborted)));

        var storage=endpoints.MapGroup("/api/v1/storage").RequireAuthorization().WithTags("Storage");
        storage.MapGet("/locations",(HttpContext c,IFileService s)=>Run(c,a=>s.LocationsAsync(a,c.RequestAborted)));
        storage.MapPost("/locations",(HttpContext c,IFileService s,LocationRequest r)=>Run(c,a=>s.AddLocationAsync(a,r,c.RequestAborted)));
        storage.MapPost("/migrations",async(HttpContext c,IFileService s,IFileAccess access,MigrationRequest r)=>
        {var id=await s.MigrateAsync(await access.CurrentAsync(c.RequestAborted),r,c.RequestAborted);return TypedResults.Accepted("/api/v1/storage/jobs",new ApiResponse<JobCreatedView>(new(id),c.TraceIdentifier));});
        storage.MapPost("/migrations/{id:guid}/cleanup",(HttpContext c,IFileService s,Guid id)=>Run(c,a=>s.CleanupAsync(a,id,c.RequestAborted)));
        storage.MapGet("/jobs", (HttpContext c,IFileService files)=>Run(c,a=>files.JobsAsync(a,c.RequestAborted)));
        storage.MapPost("/jobs/{id:guid}/retry", (HttpContext c,IFileService files,Guid id)=>Run(c,a=>files.RetryAsync(a,id,c.RequestAborted)));
    }
}
