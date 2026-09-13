using Devkit.Server.Domain.Common;

namespace Devkit.Server.Domain.Identity;

public sealed class UserLogin : AuditableEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public string LoginProvider { get; set; } = string.Empty;
    public string ProviderKey { get; set; } = string.Empty;
    public string? ProviderDisplayName { get; set; }
}
