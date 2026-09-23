using Devkit.Server.Api.Contracts;
using Devkit.Server.Application.Abstractions;
using Devkit.Server.Application.Contracts.Navigation;
using Devkit.Server.Application.Workspace;

namespace Devkit.Server.Api.Endpoints;

public sealed class NavigationEndpoints : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/web/navigation/menus", GetWebMenus)
            .WithName("GetWebNavigationMenus")
            .WithTags("Web Navigation")
            .WithSummary("Returns the configured Web navigation menu")
            .Produces<ApiResponse<IReadOnlyList<WebNavigationMenuItemDto>>>();

        endpoints.MapGet("/api/v1/client/navigation/menus", GetClientMenus)
            .WithName("GetClientNavigationMenus")
            .WithTags("Client Navigation")
            .WithSummary("Returns the configured Desktop client navigation menu")
            .Produces<ApiResponse<IReadOnlyList<ClientNavigationMenuItemDto>>>();
    }

    private static async Task<IResult> GetWebMenus(HttpContext context, INavigationMenuService navigationMenuService, IWorkspaceAccess access)
    {
        Actor? actor = context.User.Identity?.IsAuthenticated == true ? await access.CurrentAsync(context.RequestAborted) : null;
        var configured = navigationMenuService.GetMenus(NavigationAudience.Web);
        var allowed = configured.Where(item => item.RequiredPermission is null || actor is not null && (actor.Administrator || actor.Permissions.Contains(item.RequiredPermission))).ToList();
        bool changed;
        do
        {
            changed = allowed.RemoveAll(item => item.ParentId is not null && !allowed.Any(parent => parent.Id == item.ParentId)) > 0;
            changed |= allowed.RemoveAll(item => string.IsNullOrWhiteSpace(item.TargetKey) && !allowed.Any(child => child.ParentId == item.Id)) > 0;
        } while (changed);
        var menus = allowed
            .Select(item => new WebNavigationMenuItemDto(
                item.Id,
                item.ParentId,
                item.Title,
                item.TargetKey,
                item.IconKey,
                item.Order,
                item.IsClosable))
            .ToArray();
        return Results.Ok(new ApiResponse<IReadOnlyList<WebNavigationMenuItemDto>>(menus, context.TraceIdentifier));
    }

    private static IResult GetClientMenus(HttpContext context, INavigationMenuService navigationMenuService)
    {
        var menus = navigationMenuService.GetMenus(NavigationAudience.Client)
            .Select(item => new ClientNavigationMenuItemDto(
                item.Id,
                item.ParentId,
                item.Title,
                item.TargetKey,
                item.IconKey,
                item.Order,
                item.IsClosable))
            .ToArray();
        return Results.Ok(new ApiResponse<IReadOnlyList<ClientNavigationMenuItemDto>>(menus, context.TraceIdentifier));
    }
}
