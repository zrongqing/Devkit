namespace Devkit.Services.Interfaces.Authentication;

public sealed record UserProfileDto(
    Guid Id,
    string UserName,
    string Email,
    IReadOnlyList<string> Roles,
    DateTime CreatedAtUtc);
