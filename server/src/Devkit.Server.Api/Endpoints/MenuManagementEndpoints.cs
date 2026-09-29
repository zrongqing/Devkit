using Devkit.Server.Api.Contracts;
using Devkit.Server.Application.Navigation;
using Devkit.Server.Application.Workspace;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Devkit.Server.Api.Endpoints;

public sealed class MenuManagementEndpoints : IEndpointModule
{
    private static async Task<Ok<ApiResponse<T>>> Run<T>(HttpContext c, Func<Actor,Task<T>> action)
    {
        var actor = await c.RequestServices.GetRequiredService<IWorkspaceAccess>().CurrentAsync(c.RequestAborted);
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
        var identity=endpoints.MapGroup("/api/v1/identity").RequireAuthorization().WithTags("Identity Administration");
        identity.MapPut("/users/{id:guid}/menus",(HttpContext c,IAccountAdministration s,Guid id,MenuGrantRequest r)=>Run(c,async a=>{await s.AssignMenusAsync(a,id,r.MenuCodes,false,c.RequestAborted);return new SuccessView();}));
        identity.MapPut("/roles/{id:guid}/menus",(HttpContext c,IAccountAdministration s,Guid id,MenuGrantRequest r)=>Run(c,async a=>{await s.AssignMenusAsync(a,id,r.MenuCodes,true,c.RequestAborted);return new SuccessView();}));
    }
}
