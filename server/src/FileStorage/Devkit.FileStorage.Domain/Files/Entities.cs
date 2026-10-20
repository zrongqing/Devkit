using Devkit.DomainCommons.Entities;

namespace Devkit.FileStorage.Domain.Files;

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

public abstract class OwnedEntity : AuditableEntity
{
    public Guid OwnerId { get; set; }
    public int Revision { get; set; } = 1;
}


public sealed class StorageJob : OwnedEntity
{
    public string Kind { get; set; } = "migrate";
    public Guid TargetId { get; set; }
    public string PayloadJson { get; set; } = "{}";
    public string Status { get; set; } = "queued";
    public int Progress { get; set; }
    public int Attempts { get; set; }
    public string Error { get; set; } = "";
    public DateTime? LeaseUntilUtc { get; set; }
    public Guid? LeaseId { get; set; }
    public DateTime? RetryAfterUtc { get; set; }
}
