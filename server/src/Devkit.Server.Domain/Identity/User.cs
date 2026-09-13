using Devkit.Server.Domain.Common;

namespace Devkit.Server.Domain.Identity;

public sealed class User : AuditableEntity
{
    public string UserName { get; set; } = string.Empty;
    public string NormalizedUserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string NormalizedEmail { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public int FailedAccessCount { get; set; }
    public DateTime? LockoutEndUtc { get; set; }
    public int AuthenticationVersion { get; set; }
    public ICollection<UserRole> UserRoles { get; set; } = [];
    public ICollection<UserClaim> UserClaims { get; set; } = [];
    public ICollection<UserLogin> UserLogins { get; set; } = [];
    public ICollection<UserToken> UserTokens { get; set; } = [];
    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
}
