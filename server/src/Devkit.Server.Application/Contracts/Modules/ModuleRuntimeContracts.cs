namespace Devkit.Server.Application.Contracts.Modules;

public sealed record RegisterModuleInstance(
    string ModuleKey,
    string InstanceId,
    string Version,
    string BaseAddress);

public sealed record EnqueueModuleCommand(
    string SourceModule,
    string TargetModule,
    string CommandType,
    int SchemaVersion,
    string Payload,
    string IdempotencyKey,
    string? CorrelationId,
    DateTime? AvailableAtUtc);

public sealed record ModuleInstanceDto(
    Guid Id,
    string ModuleKey,
    string InstanceId,
    string Version,
    string BaseAddress,
    string Status,
    DateTime StartedAtUtc,
    DateTime LastHeartbeatAtUtc,
    DateTime? StoppedAtUtc);

public sealed record BufferedModuleCommandDto(
    Guid Id,
    string SourceModule,
    string TargetModule,
    long Sequence,
    string CommandType,
    int SchemaVersion,
    string Payload,
    string IdempotencyKey,
    string? CorrelationId,
    string Status,
    int DeliveryAttempts,
    DateTime AvailableAtUtc,
    DateTime? CompletedAtUtc,
    string? LastError,
    bool IsDuplicate = false);

public sealed record LeasedModuleCommandDto(
    Guid Id,
    Guid LeaseId,
    string SourceModule,
    string TargetModule,
    long Sequence,
    string CommandType,
    int SchemaVersion,
    string Payload,
    string IdempotencyKey,
    string? CorrelationId,
    int DeliveryAttempt,
    DateTime LeaseExpiresAtUtc);

public sealed record ModuleOperationResult<T>(
    bool Succeeded,
    T? Value,
    string? ErrorCode,
    string? ErrorMessage)
{
    public static ModuleOperationResult<T> Success(T value) => new(true, value, null, null);

    public static ModuleOperationResult<T> Failure(string code, string message) => new(false, default, code, message);
}

public sealed record ModuleRuntimePolicy(
    TimeSpan OfflineAfter,
    TimeSpan DefaultLease,
    TimeSpan MaximumLease,
    int MaximumPayloadBytes);
