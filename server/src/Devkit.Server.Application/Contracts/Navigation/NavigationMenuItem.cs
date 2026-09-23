namespace Devkit.Server.Application.Contracts.Navigation;

public sealed record NavigationMenuItem(
    string Id,
    string? ParentId,
    string Title,
    string TargetKey,
    string? IconKey,
    int Order,
    bool IsClosable,
    string? RequiredPermission = null);
