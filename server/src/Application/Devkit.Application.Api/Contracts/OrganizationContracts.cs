namespace Devkit.Application.Api.Contracts;

public sealed record DepartmentRequest(string Name, Guid? ParentId);
public sealed record PositionRequest(string Name, Guid DepartmentId);
