namespace Devkit.Server.Application.Contracts.Navigation;

public sealed record WebNavigationMenuItemDto(
    string Id,
    string? ParentId,
    string Title,
    string RouteKey,
    string? IconKey,
    int Order,
    bool IsClosable,
    string MenuCode,
    string Type);
