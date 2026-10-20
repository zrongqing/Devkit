namespace Devkit.Business.Domain.Modules;

public enum ModuleCommandStatus
{
    Pending = 0,
    Leased = 1,
    Succeeded = 2,
    DeadLetter = 3
}
