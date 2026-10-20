using Devkit.DomainCommons.Entities;

namespace Devkit.Business.Domain.Modules;

public sealed class ModuleCommandSequence : AuditableEntity
{
    public string TargetModule { get; set; } = string.Empty;
    public long NextValue { get; set; } = 1;
}
