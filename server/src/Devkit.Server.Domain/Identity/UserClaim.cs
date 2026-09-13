using Devkit.Server.Domain.Common;

namespace Devkit.Server.Domain.Identity;

public sealed class UserClaim : AuditableEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public string ClaimType { get; set; } = string.Empty;
    public string ClaimValue { get; set; } = string.Empty;
}
