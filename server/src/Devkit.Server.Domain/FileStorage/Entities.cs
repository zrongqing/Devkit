using Devkit.Server.Domain.Workspace;
using Devkit.Server.Domain.Common;

namespace Devkit.Server.Domain.FileStorage;

public sealed class StoredFile : OwnedEntity
{
    public string Name { get; set; } = "";
    public string ContentType { get; set; } = "application/octet-stream";
    public string Purpose { get; set; } = "exam-study";
    public string ObjectKey { get; set; } = "";
    public long Length { get; set; }
    public string Sha256 { get; set; } = "";
    public Guid LocationId { get; set; }
    public string Status { get; set; } = "uploading";
}

public sealed class FileReference : AuditableEntity
{
    public Guid OwnerId { get; set; }
    public Guid FileId { get; set; }
    public string Module { get; set; } = "exam-study";
    public Guid EntityId { get; set; }
}

public sealed class StorageLocation : AuditableEntity
{
    public string Name { get; set; } = "";
    public string RootPath { get; set; } = "";
    public bool Writable { get; set; }
}

public sealed class FileMigrationItem : AuditableEntity
{
    public Guid JobId { get; set; }
    public Guid FileId { get; set; }
    public Guid SourceLocationId { get; set; }
    public Guid TargetLocationId { get; set; }
    public string Status { get; set; } = "pending";
    public string Error { get; set; } = "";
}
