using Devkit.Commons.Exceptions;
using Devkit.FileStorage.Domain.Files;

namespace Devkit.FileStorage.Application.Files;

public sealed class FileStorageException(int status, string code, string message) : ServiceException(status, code, message) { }
public sealed record Actor(Guid Id, bool Administrator, IReadOnlyList<string> Permissions, IReadOnlyList<string>? MenuCodes = null)
{
    public void Require(string permission) => RequireAny([permission]);
    public void RequireAny(params string[] permissions)
    {
        if (!Administrator && !permissions.Any(Permissions.Contains))
            throw new FileStorageException(403, "forbidden", "没有此功能的权限。");
    }
    public void Own(Guid owner)
    {
        if (!Administrator && Id != owner) throw new FileStorageException(404, "not_found", "内容不存在或无权访问。");
    }
}
public sealed record FileView(Guid Id, string Name, string ContentType, long Length, string Sha256, string Purpose, Guid OwnerId, string Status, int ReferenceCount, DateTime CreatedAtUtc);
public sealed record OpenedFile(Stream Stream, string Name, string ContentType);
public sealed record StorageView(Guid Id, string Name, string RootPath, bool Writable, long? AvailableBytes, long FileCount, long TotalBytes);
public sealed record LocationRequest(string Name, string RootPath);
public sealed record MigrationRequest(Guid SourceLocationId, Guid TargetLocationId);
public sealed record SuccessView(bool Ok = true);
public sealed record JobCreatedView(Guid Id);
public interface IFileAccess { Task<Actor> CurrentAsync(CancellationToken ct = default); }
public interface IFileService
{
    Task<FileView> UploadAsync(Actor actor, string name, string purpose, Stream stream, CancellationToken ct);
    Task<IReadOnlyList<FileView>> ListAsync(Actor actor, CancellationToken ct);
    Task<OpenedFile> OpenAsync(Actor actor, Guid id, CancellationToken ct);
    Task<IReadOnlyList<FileReference>> ReferencesAsync(Actor actor, Guid id, CancellationToken ct);
    Task PurgeAsync(Actor actor, Guid id, CancellationToken ct);
    Task<IReadOnlyList<StorageView>> LocationsAsync(Actor actor, CancellationToken ct);
    Task<StorageLocation> AddLocationAsync(Actor actor, LocationRequest request, CancellationToken ct);
    Task<Guid> MigrateAsync(Actor actor, MigrationRequest request, CancellationToken ct);
    Task CleanupAsync(Actor actor, Guid jobId, CancellationToken ct);
    Task<IReadOnlyList<StorageJob>> JobsAsync(Actor actor, CancellationToken ct);
    Task RetryAsync(Actor actor, Guid id, CancellationToken ct);
}
public interface IFileReferenceService
{
    Task LinkAsync(Actor actor, Guid id, Guid entityId, Guid referenceOwnerId, string module, CancellationToken ct);
    Task UnlinkAsync(Guid entityId, string module, CancellationToken ct);
    Task<OpenedFile> OpenReferencedAsync(Guid id, Guid entityId, string module, CancellationToken ct);
}
