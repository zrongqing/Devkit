using Devkit.Server.Domain.Modules;

namespace Devkit.Server.Domain.Abstractions;

public interface IModuleRuntimeStore
{
    Task<ModuleInstance> UpsertInstanceAsync(ModuleInstance instance, CancellationToken cancellationToken = default);
    Task<ModuleInstance?> HeartbeatAsync(
        string moduleKey,
        string instanceId,
        DateTime heartbeatAtUtc,
        CancellationToken cancellationToken = default);
    Task<ModuleInstance?> SetInstanceStateAsync(
        string moduleKey,
        string instanceId,
        ModuleInstanceState state,
        DateTime changedAtUtc,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ModuleInstance>> ListInstancesAsync(CancellationToken cancellationToken = default);
    Task<BufferedCommandEnqueueResult> EnqueueAsync(
        BufferedModuleCommand command,
        CancellationToken cancellationToken = default);
    Task<BufferedModuleCommand?> FindCommandAsync(Guid commandId, CancellationToken cancellationToken = default);
    Task<BufferedModuleCommand?> LeaseNextAsync(
        string targetModule,
        string instanceId,
        DateTime nowUtc,
        DateTime activeAfterUtc,
        DateTime leaseExpiresAtUtc,
        CancellationToken cancellationToken = default);
    Task<BufferedModuleCommand?> CompleteAsync(
        string targetModule,
        Guid commandId,
        Guid leaseId,
        string instanceId,
        DateTime completedAtUtc,
        CancellationToken cancellationToken = default);
    Task<BufferedModuleCommand?> FailAsync(
        string targetModule,
        Guid commandId,
        Guid leaseId,
        string instanceId,
        bool retry,
        DateTime failedAtUtc,
        DateTime availableAtUtc,
        string error,
        CancellationToken cancellationToken = default);
}

public sealed record BufferedCommandEnqueueResult(
    BufferedModuleCommand Command,
    bool IsDuplicate,
    bool HasIdempotencyConflict);
