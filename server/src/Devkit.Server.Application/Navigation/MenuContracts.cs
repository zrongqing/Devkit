using System.ComponentModel.DataAnnotations;
using Devkit.Server.Domain.Navigation;

namespace Devkit.Server.Application.Navigation;

public sealed record MenuDeclaration([property: RegularExpression("^[a-z][a-z0-9]*(\\.[a-z][a-z0-9]*)*$"), MaxLength(120)] string MenuCode, string Type, string? ParentCode, string Title,
    string? RouteKey, string? IconKey, int Order, string[] RequiredPermissions, bool IsClosable = true);
public sealed record MenuManifestRequest(MenuDeclaration[] Menus);
public sealed record MenuSyncItem(string MenuCode, string[] ApplyFields);
public sealed record MenuSyncRequest(int Version, MenuDeclaration[] Menus, MenuSyncItem[] Items);
public sealed record MenuEditRequest(int Version, string Title, string? ParentCode, string? IconKey, int Order, bool Enabled);
public sealed record DirectoryCreateRequest(int Version, [property: RegularExpression("^[a-z][a-z0-9]*(\\.[a-z][a-z0-9]*)*$"), MaxLength(120)] string MenuCode, string Title, string? ParentCode, string? IconKey, int Order);
public sealed record MenuGrantRequest(string[] MenuCodes);
public sealed record MenuView(string MenuCode, string Type, string? ParentCode, string Title,
    string? RouteKey, string? IconKey, int Order, bool Enabled, bool IsPublic, bool IsClosable,
    string Source, string[] RequiredPermissions, int Revision, MenuDeclaration? LastDeclaration);
public sealed record MenuTreeView(int Version, IReadOnlyList<MenuView> Menus);
public sealed record MenuDifference(string MenuCode, string Status, MenuDeclaration? Local, MenuView? Current);
public sealed record MenuComparison(int Version, IReadOnlyList<MenuDifference> Items);
