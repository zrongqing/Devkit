using Devkit.DomainCommons.Entities;
using Devkit.FileStorage.Domain.Abstractions;
using Devkit.FileStorage.Domain.Files;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Devkit.FileStorage.Infrastructure.Persistence;
public sealed class FileStorageDbContext(DbContextOptions<FileStorageDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder builder) => builder.ApplyConfigurationsFromAssembly(typeof(FileStorageDbContext).Assembly);
}
public sealed class FileStorageStore(FileStorageDbContext db) : IFileStorageStore
{
    public IQueryable<T> Query<T>() where T : AuditableEntity => db.Set<T>();
    public void Add<T>(T entity) where T : AuditableEntity => db.Add(entity);
    public Task<int> SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
    public Task<T> TransactionAsync<T>(Func<Task<T>> action, CancellationToken ct) => db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
    {
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var result = await action(); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return result;
    });
}
internal static class FileMappingBase
{
    public static void Base<T>(EntityTypeBuilder<T> b, string table) where T : AuditableEntity
    {
        b.HasBaseType((Type?)null);
        b.ToTable(table);
        b.HasKey(x => x.Id);
        b.HasQueryFilter(x => !x.IsDeleted);
        if (typeof(OwnedEntity).IsAssignableFrom(typeof(T)))
        {
            b.Property<int>("Revision").IsConcurrencyToken();
            b.HasIndex("OwnerId");
        }
    }
}

internal sealed class JobMapping : IEntityTypeConfiguration<StorageJob> { public void Configure(EntityTypeBuilder<StorageJob> b) { FileMappingBase.Base(b, "StorageJobs"); b.Property(x=>x.Status).HasMaxLength(30); b.HasIndex(x=>new{x.Status,x.LeaseUntilUtc}); } }
internal sealed class FileMapping : IEntityTypeConfiguration<StoredFile> { public void Configure(EntityTypeBuilder<StoredFile> b) { FileMappingBase.Base(b, "StoredFiles"); b.Property(x=>x.ObjectKey).HasMaxLength(200); b.Property(x=>x.Name).HasMaxLength(300); b.Property(x=>x.Status).HasMaxLength(30); b.HasIndex(x=>new{x.LocationId,x.Status}); b.HasOne<StorageLocation>().WithMany().HasForeignKey(x=>x.LocationId).OnDelete(DeleteBehavior.Restrict); } }
internal sealed class ReferenceMapping : IEntityTypeConfiguration<FileReference> { public void Configure(EntityTypeBuilder<FileReference> b) { FileMappingBase.Base(b, "FileReferences"); b.HasIndex(x=>new{x.FileId,x.EntityId,x.Module}).IsUnique().HasFilter("[IsDeleted] = 0"); b.HasOne<StoredFile>().WithMany().HasForeignKey(x=>x.FileId).OnDelete(DeleteBehavior.Restrict); } }
internal sealed class LocationMapping : IEntityTypeConfiguration<StorageLocation> { public void Configure(EntityTypeBuilder<StorageLocation> b) { FileMappingBase.Base(b, "StorageLocations"); b.Property(x=>x.RootPath).HasMaxLength(1000); b.HasIndex(x=>x.Writable).IsUnique().HasFilter("[Writable] = 1 AND [IsDeleted] = 0"); } }
internal sealed class MigrationMapping : IEntityTypeConfiguration<FileMigrationItem> { public void Configure(EntityTypeBuilder<FileMigrationItem> b) { FileMappingBase.Base(b, "FileMigrationItems"); b.HasIndex(x=>new{x.JobId,x.FileId}).IsUnique(); } }
