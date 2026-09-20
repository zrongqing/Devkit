namespace Devkit.Server.Application.Contracts.Navigation;

public sealed record ClientNavigationMenuItemDto(
    string Id,
    string? ParentId,
    string Title,
    string ViewKey,
    string? IconKey,
    int Order,
    bool IsClosable);
