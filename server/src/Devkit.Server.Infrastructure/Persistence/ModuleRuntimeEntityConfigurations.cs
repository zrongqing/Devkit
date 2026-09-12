using Devkit.Server.Domain.Modules;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Devkit.Server.Infrastructure.Persistence;

internal sealed class ModuleInstanceConfiguration : IEntityTypeConfiguration<ModuleInstance>
{
    public void Configure(EntityTypeBuilder<ModuleInstance> builder)
    {
        builder.ToTable("ModuleInstances");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ModuleKey).HasMaxLength(100).IsRequired();
        builder.Property(x => x.InstanceId).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Version).HasMaxLength(50).IsRequired();
        builder.Property(x => x.BaseAddress).HasMaxLength(2048).IsRequired();
        builder.Property(x => x.StartedAtUtc).HasPrecision(7);
        builder.Property(x => x.LastHeartbeatAtUtc).HasPrecision(7);
        builder.Property(x => x.StoppedAtUtc).HasPrecision(7);
        builder.Property(x => x.CreatedAtUtc).HasPrecision(7);
        builder.Property(x => x.UpdatedAtUtc).HasPrecision(7);
        builder.HasIndex(x => new { x.ModuleKey, x.InstanceId }).IsUnique();
        builder.HasIndex(x => new { x.ModuleKey, x.LastHeartbeatAtUtc });
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}

internal sealed class BufferedModuleCommandConfiguration : IEntityTypeConfiguration<BufferedModuleCommand>
{
    public void Configure(EntityTypeBuilder<BufferedModuleCommand> builder)
    {
        builder.ToTable("BufferedModuleCommands");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.SourceModule).HasMaxLength(100).IsRequired();
        builder.Property(x => x.TargetModule).HasMaxLength(100).IsRequired();
        builder.Property(x => x.CommandType).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Payload).IsRequired();
        builder.Property(x => x.IdempotencyKey).HasMaxLength(200).IsRequired();
        builder.Property(x => x.CorrelationId).HasMaxLength(100);
        builder.Property(x => x.LeasedByInstanceId).HasMaxLength(100);
        builder.Property(x => x.LastError).HasMaxLength(2000);
        builder.Property(x => x.AvailableAtUtc).HasPrecision(7);
        builder.Property(x => x.LeaseExpiresAtUtc).HasPrecision(7);
        builder.Property(x => x.CompletedAtUtc).HasPrecision(7);
        builder.Property(x => x.CreatedAtUtc).HasPrecision(7);
        builder.Property(x => x.UpdatedAtUtc).HasPrecision(7).IsConcurrencyToken();
        builder.HasIndex(x => new { x.SourceModule, x.TargetModule, x.IdempotencyKey }).IsUnique();
        builder.HasIndex(x => new { x.TargetModule, x.Sequence }).IsUnique();
        builder.HasIndex(x => new { x.TargetModule, x.Status, x.Sequence });
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}

internal sealed class ModuleCommandSequenceConfiguration : IEntityTypeConfiguration<ModuleCommandSequence>
{
    public void Configure(EntityTypeBuilder<ModuleCommandSequence> builder)
    {
        builder.ToTable("ModuleCommandSequences");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TargetModule).HasMaxLength(100).IsRequired();
        builder.Property(x => x.CreatedAtUtc).HasPrecision(7);
        builder.Property(x => x.UpdatedAtUtc).HasPrecision(7);
        builder.HasIndex(x => x.TargetModule).IsUnique();
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
