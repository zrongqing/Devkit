using Devkit.Server.Application.Contracts.Auth;

namespace Devkit.Server.Application.Abstractions;

public interface IAuthService
{
    Task<AuthResult<UserProfileDto>> RegisterAsync(string userName, string email, string password, CancellationToken cancellationToken);
    Task<AuthResult<TokenPairDto>> LoginAsync(string account, string password, CancellationToken cancellationToken);
    Task<AuthResult<TokenPairDto>> RefreshAsync(string refreshToken, CancellationToken cancellationToken);
    Task LogoutAsync(string refreshToken, CancellationToken cancellationToken);
    Task LogoutAllAsync(Guid userId, CancellationToken cancellationToken);
    Task<AuthResult<UserProfileDto>> GetProfileAsync(Guid userId, CancellationToken cancellationToken);
}
