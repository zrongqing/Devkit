using Devkit.Server.Domain.Common;

namespace Devkit.Server.Domain.Identity;

public sealed class UserRole : AuditableEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public Guid RoleId { get; set; }
    public Role Role { get; set; } = null!;
}
