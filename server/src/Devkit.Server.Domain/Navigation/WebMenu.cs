using Devkit.Server.Domain.Common;

namespace Devkit.Server.Domain.Navigation;

public sealed class WebMenu : AuditableEntity
{
    public string MenuCode { get; set; } = "";
    public string Type { get; set; } = "module";
    public string? ParentCode { get; set; }
    public string Title { get; set; } = "";
    public string? RouteKey { get; set; }
    public string? IconKey { get; set; }
    public int Order { get; set; }
    public bool Enabled { get; set; } = true;
    public bool IsPublic { get; set; }
    public bool IsClosable { get; set; } = true;
    public string Source { get; set; } = "frontend";
    public string RequiredPermissionsJson { get; set; } = "[]";
    public string? DeclarationJson { get; set; }
    public int Revision { get; set; } = 1;
}

public sealed class WebMenuState
{
    public int Id { get; set; } = 1;
    public int Version { get; set; }
}

public sealed record WebMenuSnapshot(int Version, IReadOnlyList<WebMenu> Menus);

public interface IWebMenuStore
{
    Task<WebMenuSnapshot> ReadAsync(CancellationToken ct);
    Task CommitAsync(int expectedVersion, IReadOnlyList<WebMenu> menus,
        IReadOnlyDictionary<string, string[]>? initialGrants, CancellationToken ct);
}

public interface IWebMenuDefaults
{
    IReadOnlyList<WebMenu> Create();
}
