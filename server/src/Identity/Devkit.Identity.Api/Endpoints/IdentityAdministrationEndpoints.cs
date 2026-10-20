using Devkit.Identity.Application.Administration;
using Devkit.Identity.Api.Contracts;
using Devkit.Identity.Application.Navigation;
using Devkit.Identity.Application.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Devkit.Identity.Api.Endpoints;

public sealed class IdentityAdministrationEndpoints : IEndpointModule
{
    private static async Task<Ok<ApiResponse<T>>> Run<T>(HttpContext context, Func<AuthorizationSubject, Task<T>> operation)
    {
        var actor = await context.RequestServices.GetRequiredService<ICurrentIdentity>().CurrentAsync(context.RequestAborted);
        return TypedResults.Ok(new ApiResponse<T>(await operation(actor), context.TraceIdentifier));
    }

    private static Task<Ok<ApiResponse<SuccessView>>> Run(HttpContext context, Func<AuthorizationSubject, Task> operation) =>
        Run(context, async actor => { await operation(actor); return new SuccessView(); });

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var identity=endpoints.MapGroup("/api/v1/identity").RequireAuthorization().WithTags("Identity Administration");
        identity.MapGet("/users",(HttpContext c,IAccountAdministration s)=>Run(c,a=>s.UsersAsync(a,c.RequestAborted)));
        identity.MapGet("/permissions", (HttpContext c, ICurrentIdentity access) => Run(c, a => { a.RequireAny("system.users.manage", "system.roles.manage", "system.permissions.manage"); return Task.FromResult(PermissionCatalog.Modules); }));
        identity.MapPut("/users/{id:guid}", (HttpContext c, IAccountAdministration s, Guid id, AccountUpdateRequest r) => Run(c, a => s.UpdateAsync(a, id, r, c.RequestAborted)));
        identity.MapPut("/users/{id:guid}/permissions", (HttpContext c, IAccountAdministration s, Guid id, AssignPermissionsRequest r) => Run(c, a => s.AssignPermissionsAsync(a, id, r.Permissions, c.RequestAborted)));
        identity.MapPost("/users",(HttpContext c,IAccountAdministration s,AccountRequest r)=>Run(c,a=>s.CreateAsync(a,r,c.RequestAborted)));
        identity.MapDelete("/users/{id:guid}",(HttpContext c,IAccountAdministration s,Guid id)=>Run(c,a=>s.DisableAsync(a,id,c.RequestAborted)));
        identity.MapGet("/roles",(HttpContext c,IAccountAdministration s)=>Run(c,a=>s.RolesAsync(a,c.RequestAborted)));
        identity.MapPost("/roles",(HttpContext c,IAccountAdministration s,RoleRequest r)=>Run(c,a=>s.SaveRoleAsync(a,null,r,c.RequestAborted)));
        identity.MapPut("/roles/{id:guid}",(HttpContext c,IAccountAdministration s,Guid id,RoleRequest r)=>Run(c,a=>s.SaveRoleAsync(a,id,r,c.RequestAborted)));
        identity.MapPut("/users/{id:guid}/roles",(HttpContext c,IAccountAdministration s,Guid id,AssignRolesRequest r)=>Run(c,a=>s.AssignAsync(a,id,r.RoleIds,c.RequestAborted)));
        endpoints.MapGet("/api/v1/auth/permissions",(HttpContext c,ICurrentIdentity access)=>Run(c,a=>Task.FromResult(a))).RequireAuthorization().WithTags("Authentication");
        endpoints.MapPost("/api/v1/auth/password",(HttpContext c,IAccountAdministration s,ChangePasswordRequest r)=>Run(c,a=>s.ChangePasswordAsync(a,r,c.RequestAborted))).RequireAuthorization().WithTags("Authentication");
        identity.MapPut("/users/{id:guid}/menus", (HttpContext c, IAccountAdministration s, Guid id, MenuGrantRequest r) => Run(c, a => s.AssignMenusAsync(a, id, r.MenuCodes, false, c.RequestAborted)));
        identity.MapPut("/roles/{id:guid}/menus", (HttpContext c, IAccountAdministration s, Guid id, MenuGrantRequest r) => Run(c, a => s.AssignMenusAsync(a, id, r.MenuCodes, true, c.RequestAborted)));
    }
}
