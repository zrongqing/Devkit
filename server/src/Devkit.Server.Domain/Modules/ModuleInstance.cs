using Devkit.Server.Domain.Common;

namespace Devkit.Server.Domain.Modules;

public sealed class ModuleInstance : AuditableEntity
{
    public string ModuleKey { get; set; } = string.Empty;
    public string InstanceId { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string BaseAddress { get; set; } = string.Empty;
    public ModuleInstanceState State { get; set; }
    public DateTime StartedAtUtc { get; set; }
    public DateTime LastHeartbeatAtUtc { get; set; }
    public DateTime? StoppedAtUtc { get; set; }
}
