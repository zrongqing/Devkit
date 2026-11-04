using System.Data;
using Devkit.Application.Application.Authorization;
using Devkit.Application.Domain.Organization;
using Microsoft.EntityFrameworkCore;

namespace Devkit.Application.Infrastructure.Persistence;

internal sealed class OrganizationStore(ApplicationDbContext db) : IOrganizationStore, IAdministrationLog
{
    private DbSet<Department> Departments => db.Set<Department>();
    private DbSet<Position> Positions => db.Set<Position>();
    private DbSet<Occupancy> Members => db.Set<Occupancy>();
    private static IdentityException Missing(string item) => new(404, "not_found", item + "不存在。");
    private static IdentityException Conflict(string message) => new(409, "organization_conflict", message);

    public async Task<DirectorySnapshot> ReadAsync(CancellationToken ct) => new(
        await Departments.AsNoTracking().OrderBy(x => x.Name).ToArrayAsync(ct),
        await Positions.AsNoTracking().OrderBy(x => x.Name).ToArrayAsync(ct),
        await Members.AsNoTracking().OrderBy(x => x.PositionId).ThenBy(x => x.UserId).ToArrayAsync(ct),
        await db.Users.IgnoreQueryFilters().Where(x => !x.IsDeleted || Members.Any(m => m.UserId == x.Id))
            .OrderBy(x => x.UserName).Select(x => new DirectoryUser(x.Id, x.UserName, x.IsDeleted)).ToArrayAsync(ct));

    public Task<AdministrationEvent[]> ReadAsync(long? before, CancellationToken ct) =>
        db.Set<AdministrationEvent>().AsNoTracking().Where(x => before == null || x.Id < before)
            .OrderByDescending(x => x.Id).Take(50).ToArrayAsync(ct);

    // Serialize hierarchy validation with the write so concurrent moves cannot form a cycle.
    private Task<T> WriteAsync<T>(Func<Task<T>> change, CancellationToken ct) =>
        db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var result = await change();
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return result;
        });

    public Task<Guid> SaveDepartmentAsync(Guid? id, string name, Guid? parentId, CancellationToken ct) => WriteAsync(async () =>
    {
        var departments = await Departments.ToDictionaryAsync(x => x.Id, ct);
        var department = id.HasValue ? departments.GetValueOrDefault(id.Value) ?? throw Missing("部门") : new Department();
        for (var ancestor = parentId; ancestor.HasValue;)
        {
            if (ancestor == department.Id) throw Conflict("上级部门不能是本部门或其下级。");
            if (!departments.TryGetValue(ancestor.Value, out var parent)) throw Missing("上级部门");
            ancestor = parent.ParentId;
        }
        if (await Departments.AnyAsync(x => x.ParentId == parentId && x.Name == name && x.Id != department.Id, ct))
            throw Conflict("同级部门名称重复。");
        if (!id.HasValue) Departments.Add(department);
        department.ParentId = parentId;
        department.Name = name;
        return department.Id;
    }, ct);

    public Task<Guid> SavePositionAsync(Guid? id, string name, Guid departmentId, CancellationToken ct) => WriteAsync(async () =>
    {
        var position = id.HasValue ? await Positions.FindAsync([id.Value], ct) ?? throw Missing("职位") : new Position();
        if (!await Departments.AnyAsync(x => x.Id == departmentId, ct)) throw Missing("部门");
        if (id.HasValue && position.DepartmentId != departmentId && await Members.AnyAsync(x => x.PositionId == id, ct))
            throw Conflict("请先移除该职位人员，再调整所属部门。");
        if (await Positions.AnyAsync(x => x.DepartmentId == departmentId && x.Name == name && x.Id != position.Id, ct))
            throw Conflict("部门内职位名称重复。");
        if (!id.HasValue) Positions.Add(position);
        position.DepartmentId = departmentId;
        position.Name = name;
        return position.Id;
    }, ct);

    public Task DeleteDepartmentAsync(Guid id, CancellationToken ct) => WriteAsync(async () =>
    {
        var department = await Departments.FindAsync([id], ct) ?? throw Missing("部门");
        if (await Departments.AnyAsync(x => x.ParentId == id, ct) || await Positions.AnyAsync(x => x.DepartmentId == id, ct))
            throw Conflict("请先移走下级部门和职位。");
        Departments.Remove(department);
        return id;
    }, ct);

    public Task DeletePositionAsync(Guid id, CancellationToken ct) => WriteAsync(async () =>
    {
        var position = await Positions.FindAsync([id], ct) ?? throw Missing("职位");
        if (await Members.AnyAsync(x => x.PositionId == id, ct)) throw Conflict("请先移除该职位人员。");
        Positions.Remove(position);
        return id;
    }, ct);

    public Task SetMemberAsync(Guid positionId, Guid userId, bool assigned, CancellationToken ct) => WriteAsync(async () =>
    {
        if (!await Positions.AnyAsync(x => x.Id == positionId, ct)) throw Missing("职位");
        var member = await Members.FindAsync([positionId, userId], ct);
        if (assigned)
        {
            if (!await db.Users.AnyAsync(x => x.Id == userId, ct)) throw Missing("有效用户");
            if (member is null) Members.Add(new Occupancy(userId, positionId));
        }
        else if (member is not null) Members.Remove(member);
        return userId;
    }, ct);
}
