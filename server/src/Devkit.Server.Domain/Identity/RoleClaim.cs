using Devkit.Server.Domain.Common;

namespace Devkit.Server.Domain.Identity;

public sealed class RoleClaim : AuditableEntity
{
    public Guid RoleId { get; set; }
    public Role Role { get; set; } = null!;
    public string ClaimType { get; set; } = string.Empty;
    public string ClaimValue { get; set; } = string.Empty;
}
