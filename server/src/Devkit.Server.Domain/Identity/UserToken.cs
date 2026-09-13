using Devkit.Server.Domain.Common;

namespace Devkit.Server.Domain.Identity;

public sealed class UserToken : AuditableEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public string LoginProvider { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Value { get; set; }
}
