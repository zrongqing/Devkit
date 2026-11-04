using Devkit.Application.Application.Authorization;
using Devkit.Application.Domain.Organization;

namespace Devkit.Application.Application.Administration;

public sealed class OrganizationAdministration(IOrganizationStore store, IAdministrationLog log, ICurrentIdentity identity)
{
    private async Task Authorize(CancellationToken ct) => (await identity.CurrentAsync(ct)).Require("system.organization.manage");
    public async Task<Devkit.DomainCommons.Querying.PageResult<AdministrationEvent>> QueryAuditAsync(Devkit.DomainCommons.Querying.PageQuery query, CancellationToken ct)
    {
        (await identity.CurrentAsync(ct)).Require("system.audit.view");
        return await log.PageAsync(query, ct);
    }

    private static string Name(string value) => value?.Trim() is { Length: > 0 and <= 100 } name ? name
        : throw new IdentityException(400, "invalid_name", "名称须为 1 到 100 个字符。");

    public async Task<DirectorySnapshot> ReadAsync(CancellationToken ct)
    {
        await Authorize(ct);
        return await store.ReadAsync(ct);
    }

    public async Task<Guid> DepartmentAsync(Guid? id, string name, Guid? parentId, CancellationToken ct)
    {
        await Authorize(ct);
        return await store.SaveDepartmentAsync(id, Name(name), parentId, ct);
    }

    public async Task<Guid> PositionAsync(Guid? id, string name, Guid departmentId, CancellationToken ct)
    {
        await Authorize(ct);
        return await store.SavePositionAsync(id, Name(name), departmentId, ct);
    }

    public async Task DeleteDepartmentAsync(Guid id, CancellationToken ct)
    {
        await Authorize(ct);
        await store.DeleteDepartmentAsync(id, ct);
    }

    public async Task DeletePositionAsync(Guid id, CancellationToken ct)
    {
        await Authorize(ct);
        await store.DeletePositionAsync(id, ct);
    }

    public async Task SetMemberAsync(Guid positionId, Guid userId, bool assigned, CancellationToken ct)
    {
        await Authorize(ct);
        await store.SetMemberAsync(positionId, userId, assigned, ct);
    }

    public async Task<AdministrationEvent[]> AuditAsync(long? before, CancellationToken ct)
    {
        (await identity.CurrentAsync(ct)).Require("system.audit.view");
        if (before <= 0) throw new IdentityException(400, "invalid_cursor", "分页游标必须为正数。");
        return await log.ReadAsync(before, ct);
    }
}
