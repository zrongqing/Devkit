namespace Devkit.Services.Interfaces.Authentication;

public sealed record TokenPairDto(
    string AccessToken,
    string TokenType,
    DateTime AccessTokenExpiresAtUtc,
    string RefreshToken,
    DateTime RefreshTokenExpiresAtUtc,
    UserProfileDto User);
