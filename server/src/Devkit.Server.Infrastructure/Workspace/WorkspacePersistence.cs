using System.Linq.Expressions;
using Devkit.Server.Domain.Abstractions;
using Devkit.Server.Domain.Common;
using Devkit.Server.Domain.FileStorage;
using Devkit.Server.Domain.Workspace;
using Devkit.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Devkit.Server.Infrastructure.Workspace;

public sealed class WorkspaceStore(DevkitDbContext db) : IWorkspaceStore
{
    public Task<T?> FindAsync<T>(Guid id, CancellationToken ct = default) where T : AuditableEntity => db.Set<T>().SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<List<T>> ListAsync<T>(Expression<Func<T, bool>>? predicate = null, CancellationToken ct = default) where T : AuditableEntity => (predicate is null ? db.Set<T>() : db.Set<T>().Where(predicate)).ToListAsync(ct);
    public void Add<T>(T entity) where T : AuditableEntity => db.Add(entity);
    public async Task SaveAsync(CancellationToken ct = default) => await db.SaveChangesAsync(ct);
    public Task RefreshAsync<T>(T entity, CancellationToken ct = default) where T : AuditableEntity => db.Entry(entity).ReloadAsync(ct);
    public async Task<T> TransactionAsync<T>(Func<Task<T>> action, CancellationToken ct = default) => await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
    {
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var result = await action();
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return result;
    });
}

internal static class WorkspaceMapping
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

internal sealed class KnowledgeBaseMapping : IEntityTypeConfiguration<KnowledgeBase> { public void Configure(EntityTypeBuilder<KnowledgeBase> b) { WorkspaceMapping.Base(b, "StudyKnowledgeBases"); b.Property(x=>x.Name).HasMaxLength(200); } }
internal sealed class SourceMapping : IEntityTypeConfiguration<KnowledgeSource> { public void Configure(EntityTypeBuilder<KnowledgeSource> b) { WorkspaceMapping.Base(b, "StudySources"); b.Property(x=>x.Title).HasMaxLength(300); b.HasIndex(x=>x.KnowledgeBaseId); b.HasOne<KnowledgeBase>().WithMany().HasForeignKey(x=>x.KnowledgeBaseId).OnDelete(DeleteBehavior.Restrict); } }
internal sealed class VersionMapping : IEntityTypeConfiguration<SourceVersion> { public void Configure(EntityTypeBuilder<SourceVersion> b) { WorkspaceMapping.Base(b, "StudySourceVersions"); b.HasIndex(x=>new {x.SourceId,x.Revision}).IsUnique(); b.HasOne<KnowledgeSource>().WithMany().HasForeignKey(x=>x.SourceId).OnDelete(DeleteBehavior.Restrict); } }
internal sealed class ChunkMapping : IEntityTypeConfiguration<KnowledgeChunk> { public void Configure(EntityTypeBuilder<KnowledgeChunk> b) { WorkspaceMapping.Base(b, "StudyChunks"); b.HasIndex(x=>new{x.KnowledgeBaseId,x.SourceId,x.Revision}); b.HasOne<SourceVersion>().WithMany().HasForeignKey(x=>x.VersionId).OnDelete(DeleteBehavior.Restrict); } }
internal sealed class ProjectMapping : IEntityTypeConfiguration<StudyProject> { public void Configure(EntityTypeBuilder<StudyProject> b) { WorkspaceMapping.Base(b, "StudyProjects"); b.Property(x=>x.Name).HasMaxLength(200); } }
internal sealed class QuestionMapping : IEntityTypeConfiguration<Question> { public void Configure(EntityTypeBuilder<Question> b) { WorkspaceMapping.Base(b, "StudyQuestions"); b.HasIndex(x=>new{x.KnowledgeBaseId,x.Status}); b.Property(x=>x.Status).HasMaxLength(30); b.HasOne<KnowledgeBase>().WithMany().HasForeignKey(x=>x.KnowledgeBaseId).OnDelete(DeleteBehavior.Restrict); } }
internal sealed class AttemptMapping : IEntityTypeConfiguration<StudyAttempt> { public void Configure(EntityTypeBuilder<StudyAttempt> b) { WorkspaceMapping.Base(b, "StudyAttempts"); b.HasIndex(x=>new{x.Status,x.DeadlineUtc}); b.Property(x=>x.Status).HasMaxLength(30); b.Property(x=>x.Score).HasPrecision(6,2); b.HasOne<StudyProject>().WithMany().HasForeignKey(x=>x.ProjectId).OnDelete(DeleteBehavior.Restrict); } }
internal sealed class MistakeMapping : IEntityTypeConfiguration<Mistake> { public void Configure(EntityTypeBuilder<Mistake> b) { WorkspaceMapping.Base(b, "StudyMistakes"); b.HasIndex(x=>new{x.OwnerId,x.ProjectId,x.QuestionId}).IsUnique(); } }
internal sealed class HistoryMapping : IEntityTypeConfiguration<QueryHistory> { public void Configure(EntityTypeBuilder<QueryHistory> b) { WorkspaceMapping.Base(b, "StudyQueryHistory"); b.HasIndex(x=>x.ProjectId); } }
internal sealed class JobMapping : IEntityTypeConfiguration<WorkJob> { public void Configure(EntityTypeBuilder<WorkJob> b) { WorkspaceMapping.Base(b, "WorkspaceJobs"); b.Property(x=>x.Status).HasMaxLength(30); b.HasIndex(x=>new{x.Status,x.LeaseUntilUtc}); } }
internal sealed class FileMapping : IEntityTypeConfiguration<StoredFile> { public void Configure(EntityTypeBuilder<StoredFile> b) { WorkspaceMapping.Base(b, "StoredFiles"); b.Property(x=>x.ObjectKey).HasMaxLength(200); b.Property(x=>x.Name).HasMaxLength(300); b.Property(x=>x.Status).HasMaxLength(30); b.HasIndex(x=>new{x.LocationId,x.Status}); b.HasOne<StorageLocation>().WithMany().HasForeignKey(x=>x.LocationId).OnDelete(DeleteBehavior.Restrict); } }
internal sealed class ReferenceMapping : IEntityTypeConfiguration<FileReference> { public void Configure(EntityTypeBuilder<FileReference> b) { WorkspaceMapping.Base(b, "FileReferences"); b.HasIndex(x=>new{x.FileId,x.EntityId}); b.HasOne<StoredFile>().WithMany().HasForeignKey(x=>x.FileId).OnDelete(DeleteBehavior.Restrict); } }
internal sealed class LocationMapping : IEntityTypeConfiguration<StorageLocation> { public void Configure(EntityTypeBuilder<StorageLocation> b) { WorkspaceMapping.Base(b, "StorageLocations"); b.Property(x=>x.RootPath).HasMaxLength(1000); b.HasIndex(x=>x.Writable).IsUnique().HasFilter("[Writable] = 1 AND [IsDeleted] = 0"); } }
internal sealed class MigrationMapping : IEntityTypeConfiguration<FileMigrationItem> { public void Configure(EntityTypeBuilder<FileMigrationItem> b) { WorkspaceMapping.Base(b, "FileMigrationItems"); b.HasIndex(x=>new{x.JobId,x.FileId}).IsUnique(); } }
