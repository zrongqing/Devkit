namespace Devkit.Server.Application.Workspace;

public sealed record AccountView(Guid Id, string UserName, string Email, string[] Roles);
public sealed record RoleView(Guid Id, string Name, string[] Permissions);
public sealed record AccountRequest(string UserName, string Email, string Password, Guid[] RoleIds);
public sealed record RoleRequest(string Name, string[] Permissions);
public sealed record AssignRolesRequest(Guid[] RoleIds);
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
public interface IAccountAdministration
{
    Task<IReadOnlyList<AccountView>> UsersAsync(Actor actor, CancellationToken ct);
    Task<IReadOnlyList<RoleView>> RolesAsync(Actor actor, CancellationToken ct);
    Task<Guid> CreateAsync(Actor actor, AccountRequest request, CancellationToken ct);
    Task<Guid> SaveRoleAsync(Actor actor, Guid? id, RoleRequest request, CancellationToken ct);
    Task AssignAsync(Actor actor, Guid userId, Guid[] roleIds, CancellationToken ct);
    Task DisableAsync(Actor actor, Guid userId, CancellationToken ct);
    Task ChangePasswordAsync(Actor actor, ChangePasswordRequest request, CancellationToken ct);
}
