using Devkit.Server.Domain.Common;

namespace Devkit.Server.Domain.Modules;

public sealed class BufferedModuleCommand : AuditableEntity
{
    public string SourceModule { get; set; } = string.Empty;
    public string TargetModule { get; set; } = string.Empty;
    public long Sequence { get; set; }
    public string CommandType { get; set; } = string.Empty;
    public int SchemaVersion { get; set; }
    public string Payload { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public string? CorrelationId { get; set; }
    public ModuleCommandStatus Status { get; set; }
    public int DeliveryAttempts { get; set; }
    public DateTime AvailableAtUtc { get; set; }
    public Guid? LeaseId { get; set; }
    public string? LeasedByInstanceId { get; set; }
    public DateTime? LeaseExpiresAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public string? LastError { get; set; }
}
