namespace Devkit.Contracts;

public sealed record ClientNavigationMenuItemDto(
    string Id,
    string? ParentId,
    string Title,
    string ViewKey,
    string? IconKey,
    int Order,
    bool IsClosable);
