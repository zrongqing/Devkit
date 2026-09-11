namespace Devkit.Server.Domain.Common;

public abstract class AuditableEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public bool IsDeleted { get; set; }
}
