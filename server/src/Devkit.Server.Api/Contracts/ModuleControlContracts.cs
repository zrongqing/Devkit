using System.Text.Json;

namespace Devkit.Server.Api.Contracts;

public sealed record RegisterModuleInstanceRequest(string Version, string BaseAddress);

public sealed record SetModuleInstanceStateRequest(string State);

public sealed record EnqueueModuleCommandRequest(
    string SourceModule,
    string TargetModule,
    string CommandType,
    int SchemaVersion,
    JsonElement? Payload,
    string IdempotencyKey,
    string? CorrelationId,
    DateTime? AvailableAtUtc);

public sealed record LeaseModuleCommandRequest(int? LeaseSeconds);

public sealed record CompleteModuleCommandRequest(Guid LeaseId);

public sealed record FailModuleCommandRequest(Guid LeaseId, bool Retry, int RetryDelaySeconds, string Error);

public sealed record BufferedModuleCommandResponse(
    Guid Id,
    string SourceModule,
    string TargetModule,
    long Sequence,
    string CommandType,
    int SchemaVersion,
    JsonElement Payload,
    string IdempotencyKey,
    string? CorrelationId,
    string Status,
    int DeliveryAttempts,
    DateTime AvailableAtUtc,
    DateTime? CompletedAtUtc,
    string? LastError,
    bool IsDuplicate);

public sealed record LeasedModuleCommandResponse(
    Guid Id,
    Guid LeaseId,
    string SourceModule,
    string TargetModule,
    long Sequence,
    string CommandType,
    int SchemaVersion,
    JsonElement Payload,
    string IdempotencyKey,
    string? CorrelationId,
    int DeliveryAttempt,
    DateTime LeaseExpiresAtUtc);
