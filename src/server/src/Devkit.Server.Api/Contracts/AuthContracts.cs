namespace Devkit.Server.Api.Contracts;

public sealed record RegisterRequest(string UserName, string Email, string Password);
public sealed record LoginRequest(string Account, string Password);
public sealed record RefreshRequest(string RefreshToken);
public sealed record LogoutRequest(string RefreshToken);
