namespace Devkit.Server.Application.Contracts.Auth;

public sealed record UserProfileDto(
    Guid Id,
    string UserName,
    string Email,
    IReadOnlyList<string> Roles,
    DateTime CreatedAtUtc);
