using Devkit.Application.Application.Authorization;
using Devkit.Application.Application.Navigation;
using Devkit.Application.Domain.Organization;
using Devkit.DomainCommons.Querying;

namespace Devkit.Application.Application.Administration;

public sealed class AdministrationPageService(IAccountAdministration accounts, OrganizationAdministration organization)
{
    private static string PermissionNames(IEnumerable<string> values) => string.Join("、", PermissionCatalog.Expand(values).Select(v => v + " " + (PermissionCatalog.Modules.FirstOrDefault(p => p.Key == v)?.Name ?? v)));
    public async Task<PageResult<AccountView>> UsersAsync(AuthorizationSubject actor, PageQuery query, string? module, CancellationToken ct)
    {
        var rows = await accounts.UsersAsync(actor, ct);
        return PageQueries.Snapshot(rows, query, new Dictionary<string, Func<AccountView, object?>>
        {
            ["permissions"] = x => PermissionNames(x.Permissions), ["effectivePermissions"] = x => PermissionNames(x.EffectivePermissions),
            ["directModuleGrant"] = x => PermissionCatalog.Expand(x.Permissions).Contains(module) ? "true 已授权" : "false 未授权",
            ["effectiveModuleGrant"] = x => x.EffectivePermissions.Contains(module) ? "true 可访问" : "false 未授权",
            ["userMenuCounts"] = x => $"{x.MenuCodes.Length} / {x.EffectiveMenuCodes.Length}",
        });
    }
    public async Task<PageResult<RoleView>> RolesAsync(AuthorizationSubject actor, PageQuery query, string? module, CancellationToken ct)
    {
        var rows = await accounts.RolesAsync(actor, ct);
        return PageQueries.Snapshot(rows, query, new Dictionary<string, Func<RoleView, object?>>
        {
            ["permissions"] = x => x.Name == "Administrator" ? "全部权限（含未来新增模块） " + PermissionNames(x.Permissions) : PermissionNames(x.Permissions),
            ["roleModuleGrant"] = x => x.Name == "Administrator" || PermissionCatalog.Expand(x.Permissions).Contains(module) ? "true 已授权" : "false 未授权",
            ["roleMenuCount"] = x => x.MenuCodes.Length,
        });
    }
    public async Task<PageResult<DisabledAccountView>> DisabledAsync(AuthorizationSubject actor, PageQuery query, CancellationToken ct) => PageQueries.Snapshot(await accounts.DisabledAsync(actor, ct), query);
    public async Task<PageResult<Position>> PositionsAsync(Guid id, PageQuery query, CancellationToken ct)
    {
        var directory = await organization.ReadAsync(ct);
        if (!directory.Departments.Any(d => d.Id == id)) throw new IdentityException(404, "not_found", "部门不存在。");
        return PageQueries.Snapshot(directory.Positions.Where(p => p.DepartmentId == id), query, new Dictionary<string, Func<Position, object?>>
        { ["members"] = p => directory.Members.Where(m => m.PositionId == p.Id).Select(m => directory.Users.FirstOrDefault(u => u.Id == m.UserId)?.Name).ToArray() });
    }
}
