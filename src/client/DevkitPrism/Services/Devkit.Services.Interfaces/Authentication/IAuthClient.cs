namespace Devkit.Services.Interfaces.Authentication;

public interface IAuthClient
{
    Task<UserProfileDto> RegisterAsync(string userName, string email, string password, CancellationToken cancellationToken = default);
    Task<TokenPairDto> LoginAsync(string account, string password, CancellationToken cancellationToken = default);
    Task<TokenPairDto> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default);
    Task LogoutAsync(string refreshToken, CancellationToken cancellationToken = default);
    Task LogoutAllAsync(string accessToken, CancellationToken cancellationToken = default);
    Task<UserProfileDto> GetCurrentUserAsync(string accessToken, CancellationToken cancellationToken = default);
}
