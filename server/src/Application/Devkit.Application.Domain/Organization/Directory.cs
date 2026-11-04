namespace Devkit.Application.Domain.Organization;

public sealed class Department
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public Guid? ParentId { get; set; }
}

public sealed class Position
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DepartmentId { get; set; }
    public string Name { get; set; } = "";
}

// A person can hold positions in several departments.
public sealed record Occupancy(Guid UserId, Guid PositionId);
public sealed record DirectoryUser(Guid Id, string Name, bool Disabled);
public sealed record DirectorySnapshot(Department[] Departments, Position[] Positions, Occupancy[] Members, DirectoryUser[] Users);

public interface IOrganizationStore
{
    Task<DirectorySnapshot> ReadAsync(CancellationToken ct);
    Task<Guid> SaveDepartmentAsync(Guid? id, string name, Guid? parentId, CancellationToken ct);
    Task<Guid> SavePositionAsync(Guid? id, string name, Guid departmentId, CancellationToken ct);
    Task DeleteDepartmentAsync(Guid id, CancellationToken ct);
    Task DeletePositionAsync(Guid id, CancellationToken ct);
    Task SetMemberAsync(Guid positionId, Guid userId, bool assigned, CancellationToken ct);
}

public sealed class AdministrationEvent
{
    public long Id { get; set; }
    public DateTime OccurredUtc { get; set; }
    public Guid ActorId { get; set; }
    public string Operation { get; set; } = "";
    public string TraceId { get; set; } = "";
}
public interface IAdministrationLog
{
    Task<Devkit.DomainCommons.Querying.PageResult<AdministrationEvent>> PageAsync(Devkit.DomainCommons.Querying.PageQuery query, CancellationToken ct);
    Task<AdministrationEvent[]> ReadAsync(long? before, CancellationToken ct);
}
