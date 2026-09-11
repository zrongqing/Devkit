namespace Devkit.Server.Infrastructure.Identity;

internal static class PasswordPolicy
{
    public static string? Validate(string password)
    {
        if (password.Length < 12)
        {
            return "Password must contain at least 12 characters.";
        }

        if (!password.Any(char.IsUpper) || !password.Any(char.IsLower) || !password.Any(char.IsDigit))
        {
            return "Password must contain uppercase, lowercase, and numeric characters.";
        }

        return null;
    }
}
