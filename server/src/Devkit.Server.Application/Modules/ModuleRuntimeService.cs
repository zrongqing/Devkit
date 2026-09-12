using System.Text;
using System.Text.Json;
using Devkit.Server.Application.Abstractions;
using Devkit.Server.Application.Contracts.Modules;
using Devkit.Server.Domain.Abstractions;
using Devkit.Server.Domain.Modules;

namespace Devkit.Server.Application.Modules;

public sealed class ModuleRuntimeService(
    IModuleRuntimeStore store,
    ModuleRuntimePolicy policy,
    TimeProvider timeProvider) : IModuleRuntimeService
{
    public async Task<ModuleOperationResult<ModuleInstanceDto>> RegisterAsync(
        RegisterModuleInstance registration,
        CancellationToken cancellationToken = default)
    {
        var moduleKey = NormalizeIdentifier(registration.ModuleKey);
        var instanceId = NormalizeIdentifier(registration.InstanceId, allowColonAndUnderscore: true);
        if (moduleKey is null || instanceId is null)
        {
            return ModuleOperationResult<ModuleInstanceDto>.Failure(
                "invalid_module_identity",
                "Module key or instance ID is invalid.");
        }

        if (string.IsNullOrWhiteSpace(registration.Version) || registration.Version.Length > 50)
        {
            return ModuleOperationResult<ModuleInstanceDto>.Failure("invalid_module_version", "Module version is required and cannot exceed 50 characters.");
        }

        if (!Uri.TryCreate(registration.BaseAddress, UriKind.Absolute, out var baseAddress)
            || baseAddress.Scheme is not ("http" or "https")
            || registration.BaseAddress.Length > 2048)
        {
            return ModuleOperationResult<ModuleInstanceDto>.Failure("invalid_base_address", "Module base address must be an absolute HTTP or HTTPS URI.");
        }

        var now = UtcNow();
        var instance = new ModuleInstance
        {
            ModuleKey = moduleKey,
            InstanceId = instanceId,
            Version = registration.Version.Trim(),
            BaseAddress = baseAddress.ToString(),
            State = ModuleInstanceState.Running,
            StartedAtUtc = now,
            LastHeartbeatAtUtc = now
        };
        var saved = await store.UpsertInstanceAsync(instance, cancellationToken);
        return ModuleOperationResult<ModuleInstanceDto>.Success(ToDto(saved, now));
    }

    public async Task<ModuleOperationResult<ModuleInstanceDto>> HeartbeatAsync(
        string moduleKey,
        string instanceId,
        CancellationToken cancellationToken = default)
    {
        var identity = NormalizeIdentity(moduleKey, instanceId);
        if (identity is null)
        {
            return ModuleOperationResult<ModuleInstanceDto>.Failure("invalid_module_identity", "Module key or instance ID is invalid.");
        }

        var now = UtcNow();
        var instance = await store.HeartbeatAsync(identity.Value.ModuleKey, identity.Value.InstanceId, now, cancellationToken);
        return instance is null
            ? ModuleOperationResult<ModuleInstanceDto>.Failure("module_instance_not_found", "Register the module instance before sending heartbeats.")
            : ModuleOperationResult<ModuleInstanceDto>.Success(ToDto(instance, now));
    }

    public async Task<ModuleOperationResult<ModuleInstanceDto>> SetStateAsync(
        string moduleKey,
        string instanceId,
        string state,
        CancellationToken cancellationToken = default)
    {
        var identity = NormalizeIdentity(moduleKey, instanceId);
        if (identity is null)
        {
            return ModuleOperationResult<ModuleInstanceDto>.Failure("invalid_module_identity", "Module key or instance ID is invalid.");
        }

        if (!Enum.TryParse<ModuleInstanceState>(state, true, out var parsedState))
        {
            return ModuleOperationResult<ModuleInstanceDto>.Failure("invalid_module_state", "State must be running, draining, or stopped.");
        }

        var now = UtcNow();
        var instance = await store.SetInstanceStateAsync(
            identity.Value.ModuleKey,
            identity.Value.InstanceId,
            parsedState,
            now,
            cancellationToken);
        return instance is null
            ? ModuleOperationResult<ModuleInstanceDto>.Failure("module_instance_not_found", "The module instance was not found.")
            : ModuleOperationResult<ModuleInstanceDto>.Success(ToDto(instance, now));
    }

    public async Task<IReadOnlyList<ModuleInstanceDto>> ListInstancesAsync(CancellationToken cancellationToken = default)
    {
        var now = UtcNow();
        var instances = await store.ListInstancesAsync(cancellationToken);
        return instances.Select(instance => ToDto(instance, now)).ToArray();
    }

    public async Task<ModuleOperationResult<BufferedModuleCommandDto>> EnqueueAsync(
        EnqueueModuleCommand command,
        CancellationToken cancellationToken = default)
    {
        var sourceModule = NormalizeIdentifier(command.SourceModule);
        var targetModule = NormalizeIdentifier(command.TargetModule);
        if (sourceModule is null || targetModule is null)
        {
            return ModuleOperationResult<BufferedModuleCommandDto>.Failure("invalid_module_identity", "Source and target module keys are required.");
        }

        if (string.IsNullOrWhiteSpace(command.CommandType) || command.CommandType.Length > 200 || command.SchemaVersion < 1)
        {
            return ModuleOperationResult<BufferedModuleCommandDto>.Failure("invalid_command_contract", "Command type and a positive schema version are required.");
        }

        if (string.IsNullOrWhiteSpace(command.IdempotencyKey) || command.IdempotencyKey.Length > 200)
        {
            return ModuleOperationResult<BufferedModuleCommandDto>.Failure("invalid_idempotency_key", "Idempotency key is required and cannot exceed 200 characters.");
        }

        if (command.CorrelationId?.Length > 100)
        {
            return ModuleOperationResult<BufferedModuleCommandDto>.Failure("invalid_correlation_id", "Correlation ID cannot exceed 100 characters.");
        }

        if (Encoding.UTF8.GetByteCount(command.Payload) > policy.MaximumPayloadBytes || !IsJson(command.Payload))
        {
            return ModuleOperationResult<BufferedModuleCommandDto>.Failure(
                "invalid_command_payload",
                $"Payload must be valid JSON and cannot exceed {policy.MaximumPayloadBytes} UTF-8 bytes.");
        }

        var now = UtcNow();
        var entity = new BufferedModuleCommand
        {
            SourceModule = sourceModule,
            TargetModule = targetModule,
            CommandType = command.CommandType.Trim(),
            SchemaVersion = command.SchemaVersion,
            Payload = command.Payload,
            IdempotencyKey = command.IdempotencyKey.Trim(),
            CorrelationId = string.IsNullOrWhiteSpace(command.CorrelationId) ? null : command.CorrelationId.Trim(),
            Status = ModuleCommandStatus.Pending,
            AvailableAtUtc = command.AvailableAtUtc is null || command.AvailableAtUtc < now ? now : command.AvailableAtUtc.Value.ToUniversalTime()
        };
        var enqueueResult = await store.EnqueueAsync(entity, cancellationToken);
        if (enqueueResult.HasIdempotencyConflict)
        {
            return ModuleOperationResult<BufferedModuleCommandDto>.Failure(
                "idempotency_conflict",
                "The idempotency key is already associated with a different command.");
        }

        return ModuleOperationResult<BufferedModuleCommandDto>.Success(ToDto(enqueueResult.Command, enqueueResult.IsDuplicate));
    }

    public async Task<ModuleOperationResult<BufferedModuleCommandDto>> FindCommandAsync(
        Guid commandId,
        CancellationToken cancellationToken = default)
    {
        var command = await store.FindCommandAsync(commandId, cancellationToken);
        return command is null
            ? ModuleOperationResult<BufferedModuleCommandDto>.Failure("module_command_not_found", "The buffered module command was not found.")
            : ModuleOperationResult<BufferedModuleCommandDto>.Success(ToDto(command));
    }

    public async Task<ModuleOperationResult<LeasedModuleCommandDto?>> LeaseNextAsync(
        string moduleKey,
        string instanceId,
        int? leaseSeconds,
        CancellationToken cancellationToken = default)
    {
        var identity = NormalizeIdentity(moduleKey, instanceId);
        if (identity is null)
        {
            return ModuleOperationResult<LeasedModuleCommandDto?>.Failure("invalid_module_identity", "Module key or instance ID is invalid.");
        }

        var requestedLease = leaseSeconds is null
            ? policy.DefaultLease
            : TimeSpan.FromSeconds(leaseSeconds.Value);
        if (requestedLease <= TimeSpan.Zero || requestedLease > policy.MaximumLease)
        {
            return ModuleOperationResult<LeasedModuleCommandDto?>.Failure(
                "invalid_lease_duration",
                $"Lease duration must be between 1 and {(int)policy.MaximumLease.TotalSeconds} seconds.");
        }

        var now = UtcNow();
        var command = await store.LeaseNextAsync(
            identity.Value.ModuleKey,
            identity.Value.InstanceId,
            now,
            now.Subtract(policy.OfflineAfter),
            now.Add(requestedLease),
            cancellationToken);
        return ModuleOperationResult<LeasedModuleCommandDto?>.Success(command is null ? null : ToLeasedDto(command));
    }

    public async Task<ModuleOperationResult<BufferedModuleCommandDto>> CompleteAsync(
        string moduleKey,
        string instanceId,
        Guid commandId,
        Guid leaseId,
        CancellationToken cancellationToken = default)
    {
        var identity = NormalizeIdentity(moduleKey, instanceId);
        if (identity is null)
        {
            return ModuleOperationResult<BufferedModuleCommandDto>.Failure("invalid_module_identity", "Module key or instance ID is invalid.");
        }

        var command = await store.CompleteAsync(identity.Value.ModuleKey, commandId, leaseId, identity.Value.InstanceId, UtcNow(), cancellationToken);
        if (command is null)
        {
            return ModuleOperationResult<BufferedModuleCommandDto>.Failure("module_command_lease_conflict", "The command lease is missing, expired, or owned by another instance.");
        }

        return ModuleOperationResult<BufferedModuleCommandDto>.Success(ToDto(command));
    }

    public async Task<ModuleOperationResult<BufferedModuleCommandDto>> FailAsync(
        string moduleKey,
        string instanceId,
        Guid commandId,
        Guid leaseId,
        bool retry,
        int retryDelaySeconds,
        string error,
        CancellationToken cancellationToken = default)
    {
        var identity = NormalizeIdentity(moduleKey, instanceId);
        if (identity is null)
        {
            return ModuleOperationResult<BufferedModuleCommandDto>.Failure("invalid_module_identity", "Module key or instance ID is invalid.");
        }

        if (retryDelaySeconds is < 0 or > 86400 || string.IsNullOrWhiteSpace(error) || error.Length > 2000)
        {
            return ModuleOperationResult<BufferedModuleCommandDto>.Failure("invalid_failure_details", "Failure requires an error up to 2000 characters and a retry delay from 0 to 86400 seconds.");
        }

        var now = UtcNow();
        var command = await store.FailAsync(
            identity.Value.ModuleKey,
            commandId,
            leaseId,
            identity.Value.InstanceId,
            retry,
            now,
            now.AddSeconds(retryDelaySeconds),
            error.Trim(),
            cancellationToken);
        if (command is null)
        {
            return ModuleOperationResult<BufferedModuleCommandDto>.Failure("module_command_lease_conflict", "The command lease is missing, expired, or owned by another instance.");
        }

        return ModuleOperationResult<BufferedModuleCommandDto>.Success(ToDto(command));
    }

    private ModuleInstanceDto ToDto(ModuleInstance instance, DateTime now)
    {
        var status = instance.State switch
        {
            ModuleInstanceState.Stopped => "stopped",
            ModuleInstanceState.Draining => "draining",
            _ when instance.LastHeartbeatAtUtc < now.Subtract(policy.OfflineAfter) => "offline",
            _ => "online"
        };
        return new ModuleInstanceDto(
            instance.Id,
            instance.ModuleKey,
            instance.InstanceId,
            instance.Version,
            instance.BaseAddress,
            status,
            instance.StartedAtUtc,
            instance.LastHeartbeatAtUtc,
            instance.StoppedAtUtc);
    }

    private static BufferedModuleCommandDto ToDto(BufferedModuleCommand command, bool isDuplicate = false) => new(
        command.Id,
        command.SourceModule,
        command.TargetModule,
        command.Sequence,
        command.CommandType,
        command.SchemaVersion,
        command.Payload,
        command.IdempotencyKey,
        command.CorrelationId,
        command.Status.ToString().ToLowerInvariant(),
        command.DeliveryAttempts,
        command.AvailableAtUtc,
        command.CompletedAtUtc,
        command.LastError,
        isDuplicate);

    private static LeasedModuleCommandDto ToLeasedDto(BufferedModuleCommand command) => new(
        command.Id,
        command.LeaseId!.Value,
        command.SourceModule,
        command.TargetModule,
        command.Sequence,
        command.CommandType,
        command.SchemaVersion,
        command.Payload,
        command.IdempotencyKey,
        command.CorrelationId,
        command.DeliveryAttempts,
        command.LeaseExpiresAtUtc!.Value);

    private static bool IsJson(string payload)
    {
        try
        {
            using var _ = JsonDocument.Parse(payload);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static (string ModuleKey, string InstanceId)? NormalizeIdentity(string moduleKey, string instanceId)
    {
        var normalizedModuleKey = NormalizeIdentifier(moduleKey);
        var normalizedInstanceId = NormalizeIdentifier(instanceId, allowColonAndUnderscore: true);
        return normalizedModuleKey is null || normalizedInstanceId is null
            ? null
            : (normalizedModuleKey, normalizedInstanceId);
    }

    private static string? NormalizeIdentifier(string value, bool allowColonAndUnderscore = false)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim().ToLowerInvariant();
        if (normalized.Length is < 2 or > 100)
        {
            return null;
        }

        return normalized.All(character => char.IsAsciiLetterOrDigit(character)
            || character is '.' or '-'
            || (allowColonAndUnderscore && character is ':' or '_'))
            ? normalized
            : null;
    }

    private DateTime UtcNow() => timeProvider.GetUtcNow().UtcDateTime;
}
