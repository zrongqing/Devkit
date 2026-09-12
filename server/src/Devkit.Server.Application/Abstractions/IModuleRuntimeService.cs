using Devkit.Server.Application.Contracts.Modules;

namespace Devkit.Server.Application.Abstractions;

public interface IModuleRuntimeService
{
    Task<ModuleOperationResult<ModuleInstanceDto>> RegisterAsync(
        RegisterModuleInstance registration,
        CancellationToken cancellationToken = default);
    Task<ModuleOperationResult<ModuleInstanceDto>> HeartbeatAsync(
        string moduleKey,
        string instanceId,
        CancellationToken cancellationToken = default);
    Task<ModuleOperationResult<ModuleInstanceDto>> SetStateAsync(
        string moduleKey,
        string instanceId,
        string state,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ModuleInstanceDto>> ListInstancesAsync(CancellationToken cancellationToken = default);
    Task<ModuleOperationResult<BufferedModuleCommandDto>> EnqueueAsync(
        EnqueueModuleCommand command,
        CancellationToken cancellationToken = default);
    Task<ModuleOperationResult<BufferedModuleCommandDto>> FindCommandAsync(
        Guid commandId,
        CancellationToken cancellationToken = default);
    Task<ModuleOperationResult<LeasedModuleCommandDto?>> LeaseNextAsync(
        string moduleKey,
        string instanceId,
        int? leaseSeconds,
        CancellationToken cancellationToken = default);
    Task<ModuleOperationResult<BufferedModuleCommandDto>> CompleteAsync(
        string moduleKey,
        string instanceId,
        Guid commandId,
        Guid leaseId,
        CancellationToken cancellationToken = default);
    Task<ModuleOperationResult<BufferedModuleCommandDto>> FailAsync(
        string moduleKey,
        string instanceId,
        Guid commandId,
        Guid leaseId,
        bool retry,
        int retryDelaySeconds,
        string error,
        CancellationToken cancellationToken = default);
}
