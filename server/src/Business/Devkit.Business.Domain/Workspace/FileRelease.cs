using Devkit.DomainCommons.Entities;
namespace Devkit.Business.Domain.Workspace;

// A durable compensation is written before creating a remote reference.
// Detach operations for deleted sources are committed with the source transaction.
public sealed class FileRelease : AuditableEntity
{
    public Guid EntityId { get; set; }
    public bool KeepIfVersionExists { get; set; }
    public DateTime NotBeforeUtc { get; set; }
}
