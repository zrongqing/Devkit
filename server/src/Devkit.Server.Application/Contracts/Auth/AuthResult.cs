namespace Devkit.Server.Application.Contracts.Auth;

public sealed record AuthResult<T>(T? Value, string? ErrorCode, string? ErrorMessage)
{
    public bool Succeeded => ErrorCode is null;

    public static AuthResult<T> Success(T value) => new(value, null, null);

    public static AuthResult<T> Failure(string code, string message) => new(default, code, message);
}
