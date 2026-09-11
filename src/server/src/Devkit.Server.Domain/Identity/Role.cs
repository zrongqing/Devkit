using Devkit.Server.Domain.Common;

namespace Devkit.Server.Domain.Identity;

public sealed class Role : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string NormalizedName { get; set; } = string.Empty;
    public ICollection<UserRole> UserRoles { get; set; } = [];
}
