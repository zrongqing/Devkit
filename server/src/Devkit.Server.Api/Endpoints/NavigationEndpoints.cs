using Devkit.Server.Api.Contracts;
using Devkit.Server.Application.Abstractions;
using Devkit.Server.Application.Contracts.Navigation;
using Devkit.Server.Application.Workspace;
using Devkit.Server.Application.Navigation;

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

    private static async Task<IResult> GetWebMenus(HttpContext context, WebMenuService menus, IWorkspaceAccess access)
    {
        Actor? actor = context.User.Identity?.IsAuthenticated == true ? await access.CurrentAsync(context.RequestAborted) : null;
        var items = (await menus.NavigationAsync(actor,context.RequestAborted)).Select(x=>new WebNavigationMenuItemDto(
            x.MenuCode,x.ParentCode,x.Title,x.RouteKey ?? "",x.IconKey,x.Order,x.IsClosable,x.MenuCode,x.Type)).ToArray();
        return Results.Ok(new ApiResponse<IReadOnlyList<WebNavigationMenuItemDto>>(items,context.TraceIdentifier));
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
