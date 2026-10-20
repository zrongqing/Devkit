using Devkit.Identity.Api.Contracts;
using Devkit.Identity.Application.Navigation;
using Devkit.Identity.Application.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Devkit.Identity.Api.Endpoints;

public sealed class MenuManagementEndpoints : IEndpointModule
{
    private static async Task<Ok<ApiResponse<T>>> Run<T>(HttpContext c, Func<AuthorizationSubject,Task<T>> action)
    {
        var actor = await c.RequestServices.GetRequiredService<ICurrentIdentity>().CurrentAsync(c.RequestAborted);
        return TypedResults.Ok(new ApiResponse<T>(await action(actor),c.TraceIdentifier));
    }
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group=endpoints.MapGroup("/api/v1/web/menu-management").RequireAuthorization().WithTags("Web Menu Management");
        group.MapGet("/menus",(HttpContext c,WebMenuService s)=>Run(c,a=>s.ListAsync(a,c.RequestAborted)));
        group.MapPost("/menus",(HttpContext c,WebMenuService s,DirectoryCreateRequest r)=>Run(c,a=>s.CreateDirectoryAsync(a,r,c.RequestAborted)));
        group.MapPut("/menus/{code}",(HttpContext c,WebMenuService s,string code,MenuEditRequest r)=>Run(c,a=>s.EditAsync(a,code,r,c.RequestAborted)));
        group.MapDelete("/menus/{code}",(HttpContext c,WebMenuService s,string code,int version)=>Run(c,a=>s.DeleteDirectoryAsync(a,code,version,c.RequestAborted)));
        group.MapPost("/compare",(HttpContext c,WebMenuService s,MenuManifestRequest r)=>Run(c,a=>s.CompareAsync(a,r,c.RequestAborted)));
        group.MapPost("/sync",(HttpContext c,WebMenuService s,MenuSyncRequest r)=>Run(c,a=>s.SyncAsync(a,r,c.RequestAborted)));
    }
}
