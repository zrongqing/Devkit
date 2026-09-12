using System.Data;
using Devkit.Server.Domain.Abstractions;
using Devkit.Server.Domain.Modules;
using Devkit.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Devkit.Server.Infrastructure.Modules;

internal sealed class SqlModuleRuntimeStore(DevkitDbContext dbContext) : IModuleRuntimeStore
{
    public async Task<ModuleInstance> UpsertInstanceAsync(
        ModuleInstance instance,
        CancellationToken cancellationToken = default)
    {
        var existing = await dbContext.ModuleInstances.SingleOrDefaultAsync(
            item => item.ModuleKey == instance.ModuleKey && item.InstanceId == instance.InstanceId,
            cancellationToken);
        if (existing is null)
        {
            dbContext.ModuleInstances.Add(instance);
            existing = instance;
        }
        else
        {
            existing.Version = instance.Version;
            existing.BaseAddress = instance.BaseAddress;
            existing.State = ModuleInstanceState.Running;
            existing.StartedAtUtc = instance.StartedAtUtc;
            existing.LastHeartbeatAtUtc = instance.LastHeartbeatAtUtc;
            existing.StoppedAtUtc = null;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return existing;
    }

    public async Task<ModuleInstance?> HeartbeatAsync(
        string moduleKey,
        string instanceId,
        DateTime heartbeatAtUtc,
        CancellationToken cancellationToken = default)
    {
        var instance = await dbContext.ModuleInstances.SingleOrDefaultAsync(
            item => item.ModuleKey == moduleKey && item.InstanceId == instanceId,
            cancellationToken);
        if (instance is null || instance.State == ModuleInstanceState.Stopped)
        {
            return null;
        }

        instance.LastHeartbeatAtUtc = heartbeatAtUtc;
        await dbContext.SaveChangesAsync(cancellationToken);
        return instance;
    }

    public async Task<ModuleInstance?> SetInstanceStateAsync(
        string moduleKey,
        string instanceId,
        ModuleInstanceState state,
        DateTime changedAtUtc,
        CancellationToken cancellationToken = default)
    {
        var instance = await dbContext.ModuleInstances.SingleOrDefaultAsync(
            item => item.ModuleKey == moduleKey && item.InstanceId == instanceId,
            cancellationToken);
        if (instance is null)
        {
            return null;
        }

        instance.State = state;
        instance.LastHeartbeatAtUtc = changedAtUtc;
        instance.StoppedAtUtc = state == ModuleInstanceState.Stopped ? changedAtUtc : null;
        await dbContext.SaveChangesAsync(cancellationToken);
        return instance;
    }

    public async Task<IReadOnlyList<ModuleInstance>> ListInstancesAsync(CancellationToken cancellationToken = default) =>
        await dbContext.ModuleInstances
            .AsNoTracking()
            .OrderBy(item => item.ModuleKey)
            .ThenBy(item => item.InstanceId)
            .ToArrayAsync(cancellationToken);

    public async Task<BufferedCommandEnqueueResult> EnqueueAsync(
        BufferedModuleCommand command,
        CancellationToken cancellationToken = default)
    {
        var strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            var duplicate = await dbContext.BufferedModuleCommands.SingleOrDefaultAsync(
                item => item.SourceModule == command.SourceModule
                    && item.TargetModule == command.TargetModule
                    && item.IdempotencyKey == command.IdempotencyKey,
                cancellationToken);
            if (duplicate is not null)
            {
                var conflicts = duplicate.CommandType != command.CommandType
                    || duplicate.SchemaVersion != command.SchemaVersion
                    || duplicate.Payload != command.Payload;
                await transaction.CommitAsync(cancellationToken);
                return new BufferedCommandEnqueueResult(duplicate, IsDuplicate: !conflicts, HasIdempotencyConflict: conflicts);
            }

            var sequence = await dbContext.ModuleCommandSequences.SingleOrDefaultAsync(
                item => item.TargetModule == command.TargetModule,
                cancellationToken);
            if (sequence is null)
            {
                command.Sequence = 1;
                sequence = new ModuleCommandSequence { TargetModule = command.TargetModule, NextValue = 2 };
                dbContext.ModuleCommandSequences.Add(sequence);
            }
            else
            {
                command.Sequence = sequence.NextValue;
                sequence.NextValue++;
            }

            dbContext.BufferedModuleCommands.Add(command);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new BufferedCommandEnqueueResult(command, IsDuplicate: false, HasIdempotencyConflict: false);
        });
    }

    public Task<BufferedModuleCommand?> FindCommandAsync(Guid commandId, CancellationToken cancellationToken = default) =>
        dbContext.BufferedModuleCommands.AsNoTracking().SingleOrDefaultAsync(item => item.Id == commandId, cancellationToken);

    public async Task<BufferedModuleCommand?> LeaseNextAsync(
        string targetModule,
        string instanceId,
        DateTime nowUtc,
        DateTime activeAfterUtc,
        DateTime leaseExpiresAtUtc,
        CancellationToken cancellationToken = default)
    {
        var strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            var activeInstance = await dbContext.ModuleInstances.AnyAsync(
                item => item.ModuleKey == targetModule
                    && item.InstanceId == instanceId
                    && item.State == ModuleInstanceState.Running
                    && item.LastHeartbeatAtUtc >= activeAfterUtc,
                cancellationToken);
            if (!activeInstance)
            {
                await transaction.CommitAsync(cancellationToken);
                return null;
            }

            var next = await dbContext.BufferedModuleCommands
                .Where(item => item.TargetModule == targetModule
                    && (item.Status == ModuleCommandStatus.Pending || item.Status == ModuleCommandStatus.Leased))
                .OrderBy(item => item.Sequence)
                .FirstOrDefaultAsync(cancellationToken);
            if (next is null
                || (next.Status == ModuleCommandStatus.Pending && next.AvailableAtUtc > nowUtc)
                || (next.Status == ModuleCommandStatus.Leased && next.LeaseExpiresAtUtc > nowUtc))
            {
                await transaction.CommitAsync(cancellationToken);
                return null;
            }

            next.Status = ModuleCommandStatus.Leased;
            next.LeaseId = Guid.CreateVersion7();
            next.LeasedByInstanceId = instanceId;
            next.LeaseExpiresAtUtc = leaseExpiresAtUtc;
            next.DeliveryAttempts++;
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return next;
        });
    }

    public async Task<BufferedModuleCommand?> CompleteAsync(
        string targetModule,
        Guid commandId,
        Guid leaseId,
        string instanceId,
        DateTime completedAtUtc,
        CancellationToken cancellationToken = default)
    {
        var command = await FindLeasedCommandAsync(targetModule, commandId, leaseId, instanceId, completedAtUtc, cancellationToken);
        if (command is null)
        {
            return null;
        }

        command.Status = ModuleCommandStatus.Succeeded;
        command.CompletedAtUtc = completedAtUtc;
        command.LeaseId = null;
        command.LeasedByInstanceId = null;
        command.LeaseExpiresAtUtc = null;
        command.LastError = null;
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return command;
        }
        catch (DbUpdateConcurrencyException)
        {
            dbContext.ChangeTracker.Clear();
            return null;
        }
    }

    public async Task<BufferedModuleCommand?> FailAsync(
        string targetModule,
        Guid commandId,
        Guid leaseId,
        string instanceId,
        bool retry,
        DateTime failedAtUtc,
        DateTime availableAtUtc,
        string error,
        CancellationToken cancellationToken = default)
    {
        var command = await FindLeasedCommandAsync(targetModule, commandId, leaseId, instanceId, failedAtUtc, cancellationToken);
        if (command is null)
        {
            return null;
        }

        command.Status = retry ? ModuleCommandStatus.Pending : ModuleCommandStatus.DeadLetter;
        command.AvailableAtUtc = availableAtUtc;
        command.LeaseId = null;
        command.LeasedByInstanceId = null;
        command.LeaseExpiresAtUtc = null;
        command.LastError = error;
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return command;
        }
        catch (DbUpdateConcurrencyException)
        {
            dbContext.ChangeTracker.Clear();
            return null;
        }
    }

    private Task<BufferedModuleCommand?> FindLeasedCommandAsync(
        string targetModule,
        Guid commandId,
        Guid leaseId,
        string instanceId,
        DateTime nowUtc,
        CancellationToken cancellationToken) =>
        dbContext.BufferedModuleCommands.SingleOrDefaultAsync(
            item => item.Id == commandId
                && item.TargetModule == targetModule
                && item.Status == ModuleCommandStatus.Leased
                && item.LeaseId == leaseId
                && item.LeasedByInstanceId == instanceId
                && item.LeaseExpiresAtUtc >= nowUtc,
            cancellationToken);
}
