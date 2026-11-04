using Devkit.Application.Domain.Identity;
using Devkit.Application.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Devkit.Application.Infrastructure.Persistence;

internal sealed class DepartmentMapping : IEntityTypeConfiguration<Department>
{
    public void Configure(EntityTypeBuilder<Department> entity)
    {
        entity.ToTable("sys_department");
        entity.Property(x => x.Name).HasMaxLength(100);
        entity.HasOne<Department>().WithMany().HasForeignKey(x => x.ParentId).OnDelete(DeleteBehavior.Restrict);
        entity.HasIndex(x => new { x.ParentId, x.Name }).IsUnique().HasFilter(null);
    }
}

internal sealed class PositionMapping : IEntityTypeConfiguration<Position>
{
    public void Configure(EntityTypeBuilder<Position> entity)
    {
        entity.ToTable("sys_position");
        entity.Property(x => x.Name).HasMaxLength(100);
        entity.HasOne<Department>().WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        entity.HasIndex(x => new { x.DepartmentId, x.Name }).IsUnique();
    }
}

internal sealed class OccupancyMapping : IEntityTypeConfiguration<Occupancy>
{
    public void Configure(EntityTypeBuilder<Occupancy> entity)
    {
        entity.ToTable("sys_occupancy");
        entity.HasKey(x => new { x.PositionId, x.UserId });
        entity.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne<Position>().WithMany().HasForeignKey(x => x.PositionId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class AdministrationEventMapping : IEntityTypeConfiguration<AdministrationEvent>
{
    public void Configure(EntityTypeBuilder<AdministrationEvent> entity)
    {
        entity.ToTable("sys_administration_event");
        entity.Property(x => x.OccurredUtc).HasPrecision(7);
        entity.Property(x => x.Operation).HasMaxLength(512);
        entity.Property(x => x.TraceId).HasMaxLength(128);
    }
}
