using Devkit.Identity.Domain.Common;

namespace Devkit.Identity.Domain.Identity;

public sealed class UserToken : AuditableEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public string LoginProvider { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Value { get; set; }
}
