using Devkit.Identity.Application.Authorization;

namespace Devkit.Identity.Application.Administration;

public sealed record AccountView(Guid Id, string UserName, string Email, string[] Roles, string[] Permissions, string[] EffectivePermissions, string[] MenuCodes, string[] EffectiveMenuCodes);
public sealed record RoleView(Guid Id, string Name, string[] Permissions, string[] MenuCodes);
public sealed record AccountRequest(string UserName, string Email, string Password, Guid[] RoleIds);
public sealed record RoleRequest(string Name, string[] Permissions);
public sealed record AssignRolesRequest(Guid[] RoleIds);
public sealed record AccountUpdateRequest(string UserName, string Email);
public sealed record AssignPermissionsRequest(string[] Permissions);
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
public interface IAccountAdministration
{
    Task<IReadOnlyList<AccountView>> UsersAsync(AuthorizationSubject actor, CancellationToken ct);
    Task<IReadOnlyList<RoleView>> RolesAsync(AuthorizationSubject actor, CancellationToken ct);
    Task AssignMenusAsync(AuthorizationSubject actor, Guid id, string[] menuCodes, bool role, CancellationToken ct);
    Task<Guid> CreateAsync(AuthorizationSubject actor, AccountRequest request, CancellationToken ct);
    Task UpdateAsync(AuthorizationSubject actor, Guid userId, AccountUpdateRequest request, CancellationToken ct);
    Task AssignPermissionsAsync(AuthorizationSubject actor, Guid userId, string[] permissions, CancellationToken ct);
    Task<Guid> SaveRoleAsync(AuthorizationSubject actor, Guid? id, RoleRequest request, CancellationToken ct);
    Task AssignAsync(AuthorizationSubject actor, Guid userId, Guid[] roleIds, CancellationToken ct);
    Task DisableAsync(AuthorizationSubject actor, Guid userId, CancellationToken ct);
    Task ChangePasswordAsync(AuthorizationSubject actor, ChangePasswordRequest request, CancellationToken ct);
}
